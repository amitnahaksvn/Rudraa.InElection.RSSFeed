# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Build everything
dotnet build Rudraa.InElection.RSSFeed.slnx      # note: .slnx, not .sln

# Run all tests
dotnet test tests/PoliticalNews.Tests/PoliticalNews.Tests.csproj

# Run a single test
dotnet test tests/PoliticalNews.Tests/PoliticalNews.Tests.csproj --filter "FullyQualifiedName~NewsCrawlerOrchestratorTests.RunCrawlAsync_FeedFailure_RecordsCompletedWithErrorsAndFailedFeedName"

# Run the crawler (scheduled background service, no HTTP surface)
dotnet run --project src/Worker

# Run the read/query API standalone (Minimal API endpoints dispatch through Mediator)
dotnet run --project src/Web

# Run everything together via the Aspire AppHost (local Mongo container + Web + Worker,
# with the Aspire dashboard for logs/traces/metrics) - requires Docker running
dotnet run --project src/AppHost

# Or without Aspire/Docker at all
docker compose up --build
```

Real credentials (e.g. an Atlas connection string) belong in user-secrets, never in
`appsettings.json`:
```bash
dotnet user-secrets set "MongoDb:ConnectionString" "mongodb+srv://..." --project src/Worker
dotnet user-secrets set "MongoDb:DatabaseName" "SomeDbName" --project src/Worker
# repeat --project src/Web if running Web standalone (outside the AppHost)
# for the AppHost itself: dotnet user-secrets set "ConnectionStrings:mongodb" "mongodb+srv://..." --project src/AppHost
#                          dotnet user-secrets set "UseLocalMongo" "false" --project src/AppHost
```
MongoDB database names cannot contain `.` (or `/ \ " $ * < > : | ?` / spaces) - this rejects a
name copy-pasted straight from a domain-style string like `Foo.Bar`.

## Architecture

Clean Architecture, dependency direction is strictly inward:
`Domain <- Application <- Infrastructure <- (Web | Worker)`. `Domain` has zero dependencies
(not even on MongoDB.Driver - entities are plain POCOs); Mongo BSON class maps are registered
centrally in `Infrastructure/Mongo/MongoClassMapConfigurator.cs`, not via attributes on entities.

**Two composition roots, one crawl engine.** `Worker` (a plain `BackgroundService`
console host, no HTTP) runs the cron schedule; `Web` exposes read endpoints plus a
manual trigger. Both ultimately call the same `INewsCrawlerService.RunCrawlAsync` implementation
(`NewsCrawlerOrchestrator` in `Application/Services`), which internally acquires a Mongo-backed
distributed lock (`ICrawlLockRepository`) **per provider** (`"{LockName}:{Provider}"`, e.g.
`news-crawler:AajTak`) before crawling that provider. The invariant is per-provider mutual
exclusion (a scheduled tick, a manual API trigger, or another instance can never crawl the *same*
provider concurrently) - different providers deliberately crawl in parallel, because every
provider's Hangfire recurring job fires on the same cron tick and a single global lock would let
exactly one provider win per tick and starve the rest (a real bug this design replaced). A run
skips just the providers whose locks are held; only if *every* requested provider is lock-skipped
does it return a non-persisted `Skipped` history.

**Application layer is CQRS via `Mediator` (source-generator based, not MediatR - swapped
deliberately to avoid MediatR's commercial licensing) + FluentValidation.** Each query/command
lives in its own file under `Application/News/Queries/*`, `Application/Crawl/Queries/*`, or
`Application/Crawl/Commands/*`, containing both the request record and its `IRequestHandler` in
that same file, plus a sibling `*Validator.cs`. Pipeline behaviours (`Application/Common/Behaviours`)
run in this order: Logging -> UnhandledException -> Validation -> Performance. Handlers return
`ValueTask<TResponse>` (not `Task`) - that's the `Mediator` package's API, distinct from MediatR.

**`Web` is Minimal API, not MVC controllers - deliberately, so nothing should be named
`*Controller`.** `Web/Endpoints/{Feature}.cs` (`News.cs`, `Crawl.cs`) are static classes
implementing the marker interface `Web/Infrastructure/IEndpointGroup`, each with a
`public static void Map(RouteGroupBuilder groupBuilder)` and `public static` handler methods
(`ISender sender, ...` as parameters, returning `Task<Ok<T>>`/`Task<Results<...>>` via
`TypedResults`). `Web/Infrastructure/WebApplicationExtensions.MapEndpoints(Assembly)` reflection-scans
for `IEndpointGroup` implementations and invokes their static `Map` method - adding a new feature's
endpoints means adding one class, nothing to register by hand in `Program.cs`.
`Web/Infrastructure/EndpointRouteBuilderExtensions` adds `MapGet`/`MapPost`/etc. overloads typed to
`RouteGroupBuilder` specifically (so they're picked over the ASP.NET Core built-ins by C#'s
more-specific-receiver rule) that auto-derive `WithName(handler.Method.Name)`; `Program.cs`'s
`CustomOperationIds` then surfaces that same name as the OpenAPI `operationId`. DTOs returned by
handlers (`Application/News/Dtos`, `Application/Crawl/Dtos`) are also the HTTP response shape -
there is no separate Web-layer contract/mapping type. Errors (including FluentValidation failures
*and* Minimal API's own parameter-binding failures, i.e. `BadHttpRequestException`) are centralized
in `Web/Infrastructure/ProblemDetailsExceptionHandler` -> RFC7807 ProblemDetails; endpoints never
try/catch.

**RSS provider abstraction is designed for multi-provider expansion.** `IRssProvider`
(`Application/Abstractions`) is implemented by `BaseRssProvider` (`Infrastructure/RssProviders`),
which owns the entire generic pipeline: HTTP fetch, RSS 2.0 XML parsing, image extraction
(`media:content` -> `media:thumbnail` -> `enclosure` -> `og:image` HTML fallback), and
normalization into `NormalizedArticle`. A concrete provider (`AajTakRssProvider`, `AbpNewsRssProvider`)
supplies just `Name` and an `IHttpClientFactory` client name - no MongoDB or persistence code.
Adding a provider for a later phase (ANI/NDTV/PIB/...) means: one new `BaseRssProvider` subclass,
one `services.AddHttpClient(...)` + `AddSingleton<IRssProvider, ...>()` in
`Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs`, and one new
block under `NewsCrawler:Providers` in `appsettings.json`. Feed URLs are never hardcoded.

**Each provider owns its own cron schedule**, not a single global one: `RssProviderOptions.Cron`
(a standard 5-field expression) lives per provider block. Scheduling is Hangfire, not a hand-rolled
timer loop: `Worker/Program.cs` registers one native Hangfire recurring job per enabled provider at
startup, job id `news-crawl-{provider}` (`Infrastructure/Scheduling/HangfireJobIds.NewsCrawl`) -
`AddOrUpdate` is idempotent, so every restart just re-syncs each job's cron expression against
config rather than creating duplicates. `Hangfire.Mongo` backs job storage (collections prefixed
`hangfire`, separate from the app's own collections); only `Worker` calls `AddHangfireServer()` and
actually executes jobs - `Web` registers the same Mongo storage read-only (no server) purely so it
can host the dashboard (`app.UseHangfireDashboard()` at `/hangfire`, gated behind
`Api:EnableHangfireDashboard` - off by default because Hangfire's dashboard has no built-in auth; on
in `appsettings.Development.json`). `MongoStorageOptions.CheckConnection = false` on both - Hangfire.Mongo's
default synchronous startup ping crashes the whole host if Atlas is briefly slow to answer, which
Mongo connectivity is already verified elsewhere anyway. A manually triggered crawl
(`POST /api/crawl/trigger`) still runs every enabled provider regardless of its individual schedule
(`RunCrawlAsync(CancellationToken)`, no provider filter) - independent of Hangfire, straight through
`NewsCrawlerOrchestrator`.

**Every Hangfire job invokes `Infrastructure/Scheduling/HangfireCrawlJobExecutor.RunAsync`, never
`INewsCrawlerService` directly** - both from `Worker/Program.cs`'s startup registration and from
`HangfireCrawlJobTrigger.CreateOrUpdate` (below). This one indirection buys two things: a
`[JobDisplayName("Crawl {0}")]` so the Hangfire dashboard shows "Crawl AajTak" instead of a raw
method signature, and a `ILogger.BeginScope` tagging every log line the run produces with that
specific Hangfire job execution's own id (via the auto-injected `PerformContext`, never
serialized/caller-supplied) - so a run seen in the dashboard can be traced back to its exact logs.
It's also the one place enforcing that a Hangfire job can only ever do this one fixed, safe thing
(crawl a specific already-configured provider) - never arbitrary caller-supplied code.

**`HangfireCrawlJobExecutor` is tagged `[Queue("rss")]` and Worker's `AddHangfireServer()` reads
which queues/how many concurrent workers from a new `Hangfire` config section
(`Application/Options/HangfireOptions`, default `Queues: ["rss", "default"]`,
`WorkerCount: null` meaning Hangfire's own `Environment.ProcessorCount * 5` default) - this exists
specifically so a production deployment can run separate replica groups of the exact same Worker
image, each independently scaled via ordinary replica count, without needing a second service:
e.g. one replica group started with `Hangfire__Queues__0=rss`, another with
`Hangfire__Queues__0=api` once a news-API-fetching executor (tagged `[Queue("api")]`) exists.
`"default"` stays in the default queue list so untagged jobs (currently just the raw-response
cleanup job) keep running on a single-queue deployment without extra config. All jobs still share
one Hangfire Mongo storage/dashboard and one dedup pipeline regardless of how many queues or
replica groups are running - only the *processing* is split, not the data model or job storage.

**Recurring-job management beyond static config, all through `ICrawlJobTrigger`/`ICrawlJobStatusReader`
(`Application/Abstractions`, implemented in `Infrastructure/Scheduling`) so Application never
references Hangfire types directly:**
- `POST /api/crawl/trigger/{provider}` - enqueues that provider's job to run now, ahead of its cron,
  without changing the schedule; returns immediately (the crawl runs asynchronously wherever
  `Worker`'s Hangfire server is), unlike `POST /api/crawl/trigger`.
- `POST /api/crawl/jobs` (body: `jobName`, `cron`, `timeZone` - defaults to `"UTC"`) - creates or
  updates one provider's recurring job live. `jobName` must already be an enabled provider with a
  `Cron` in `NewsCrawler.appsettings.json` (validated via `CreateOrUpdateRecurringJobCommandValidator`,
  which also syntax-checks `cron` via `Cronos.CronExpression.Parse` and `timeZone` via
  `TimeZoneInfo.FindSystemTimeZoneById` - both 400 on failure, not a 500). This is a **live
  override only** - it does not persist to `NewsCrawler.appsettings.json`, so it lasts until
  `Worker` next restarts and re-registers every provider's job from that file.
- `GET /api/crawl/jobs/{provider}` - current schedule plus the last run's Hangfire job id/state
  (`Succeeded`/`Failed`/`Processing`/...) and, if it failed, the exception type/message - 404 if no
  recurring job is registered for that provider.
- `GET /api/crawl/history/{id}` - single `CrawlHistory` record by its Mongo id, 404 if not found.

**`NewsCrawler:*` config lives in one shared file, not duplicated per project:**
`src/NewsCrawler.appsettings.json` (providers/feeds/schedules) is linked into both
`Worker.csproj` and `Web.csproj` via a `Content Include="..\NewsCrawler.appsettings.json"`
item and loaded in both `Program.cs` files via `AddJsonFile(Path.Combine(AppContext.BaseDirectory, ...))`
- `AppContext.BaseDirectory`, not `ContentRootPath`, because `dotnet run` sets the latter to the
project's source directory while a published/Docker deployment's is its own output folder, and
`AppContext.BaseDirectory` is the one location consistent between the two. `Web` needs this too
because its manual trigger runs a real crawl in-process, not just reads. The source is inserted
*before* the environment-variables source in the config chain (not appended, which is
`CreateBuilder`'s default for anything added after it) specifically so `NewsCrawler__*` env vars -
e.g. `NewsCrawler__Providers__0__Cron`, `NewsCrawler__SaveRawResponses` - can still override the
shared file, matching the same env-var-wins convention already used for `MongoDb:ConnectionString`.

**Duplicate detection order** (`Infrastructure/Persistence/NewsArticleRepository.UpsertAsync`):
`Url` -> `OriginalGuid` -> `Hash` (SHA-256 of normalized title + `PublishedAt`, computed by
`Application/Services/ArticleHasher`). A match on `Url`/`OriginalGuid` with changed content
(title/summary/content/image) updates the existing document; a match with no change, or any
`Hash` match, is a no-op duplicate skip. Articles are never duplicated.

**Configuration resolution for Mongo is dual-path by design:** `Infrastructure`'s
`AddInfrastructure()` binds `MongoDb:ConnectionString` from `appsettings.json`/user-secrets, but
`PostConfigure` overrides it with `ConnectionStrings:mongodb` when present. That second key is
what the Aspire AppHost (`AppHost/AppHost.cs`) injects automatically via
`WithReference(...)`, so the exact same `Infrastructure`/`Web`/`Worker` code runs unchanged
whether launched through the AppHost, plain `dotnet run`, or docker-compose. `AddInfrastructure()`
also registers `MongoIndexInitializerHostedService` (in `Infrastructure/Mongo`, shared by both
hosts) - it runs on startup before either host starts serving, creating every collection/index
automatically the first time either process connects to a brand-new database.

**`ServiceDefaults`** (Aspire's shared-project convention) is referenced by both
`Web` and `Worker` and adds OpenTelemetry tracing/metrics/logging, default health checks, service
discovery, and HTTP resilience via one `builder.AddServiceDefaults()` call. `Web` additionally
calls `app.MapDefaultEndpoints()` to expose `/health` and `/alive` (Worker has no HTTP listener,
so it only gets the telemetry/resilience side). `AppHost/AppHost.cs`'s `UseLocalMongo` toggle
picks between an Aspire-managed local Mongo container (`Aspire.Hosting.MongoDB`, default, zero
credentials needed) and an external connection string resource (`AddConnectionString("mongodb")`,
e.g. a real Atlas cluster) - no code outside `AppHost.cs` needs to know which one is active.

## Known data caveats

Only one Aaj Tak (`aajtak.in`) RSS feed could be publicly verified (`?id=home` - no other
category-slug or numeric-id pattern resolved). The rest of the `NewsCrawler:Providers[Name=AajTak]`
feed list in `appsettings.json` are `tak.live` feeds (India Today Group's sister "Tak" video
network - `news-tak`, `crime-tak`, `bharat-tak`, etc.), grouped under the same `AajTak` provider
block per explicit instruction. Three tak.live slugs that were tried and don't resolve
(`sports-tak`, `mumbai-tak`, `short-videos`) are intentionally excluded, not omissions.

ABP Live (`abplive.com`) exposes a plain, consistent `{path}/feed` pattern (no `?id=` numeric
scheme like Aaj Tak) - every one of the 54 feeds under `NewsCrawler:Providers[Name=ABPNews]` was
individually curl-verified (HTTP 200, well-formed `<rss>` body) before being added, covering
national/world/state news, elections, fact-check, business, entertainment, lifestyle, astrology,
and a few utility categories (education, agriculture, GK, web-stories). `abplive.com` is the
Hindi-language edition (`Language: "hi"`, matching AajTak); ABP's other language editions
(`bengali.`, `marathi.`, `tamil.`, `telugu.`, `gujarati.`, `punjabi.`, `news.` for English) live on
separate subdomains and are not wired in.

Five more English-language providers, each feed individually curl-verified before adding:
**India TV** (`indiatvnews.com/rssnews/topstory{-slug}.xml` - the politics feed serves mostly
stale 2023 items with only an occasional fresh one; kept, but don't expect volume from it).
**News18** (`news18.com/rss/{slug}.xml`, 200-item feeds - its Akamai CDN returns 403 to
crawler-style User-Agents while serving the same public feeds to browser UAs, so News18's named
HttpClient is registered with `BrowserUserAgent` in `InfrastructureServiceCollectionExtensions`,
the only provider that differs there). **NDTV** (feeds live on FeedBurner -
`feeds.feedburner.com/ndtvnews-*` / `ndtvprofit-latest` / `gadgets360-latest` - not ndtv.com).
**IndianExpress** (standard WordPress `/section/{name}/feed/`, 200-item feeds, `dc:creator`
authors, sometimes empty descriptions). **TheHindu** (`{section}/feeder/default.rss`, 60-item
feeds). All five use spec-cased `pubDate` and `media:content`/`media:thumbnail` image tags, so
none of them needed `BaseRssProvider` changes.

Zee News (`zeenews.india.com`, English - `Language: "en"`) uses `rss/{slug}.xml` with
*inconsistent* slugs (`business.xml` but `sports-news.xml`; `cricket-news.xml` 302s into
`sports-news.xml`) and has no public RSS index page - each of the 11 feeds under
`NewsCrawler:Providers[Name=ZeeNews]` was individually curl-verified; invalid slugs 301 to the
homepage rather than 404, so a "successful" fetch of HTML is the failure signature to watch for.
No state-level feeds exist (every state slug tried redirects home). Three Zee-specific quirks are
handled in `BaseRssProvider`, deliberately in the shared base since they're spec-tolerance, not
Zee-only behavior: (1) items use lowercase `<pubdate>`, so the pubDate lookup is case-insensitive;
(2) dates come as `"Thursday, July 02, 2026, 14:08 GMT +5:30"`, so `ParsePublishDate` falls back to
stripping the literal `GMT` and zero-padding single-digit offsets before re-parsing; (3) items
carry **no image tags at all**, so every article image comes from the `og:image` HTML fallback -
one extra HTTP request per new article, which is why the 191-item `latest-news.xml` aggregate feed
is configured but `Enabled: false`.

11 more providers (8 English, 3 Hindi - `Language: "hi"` for `NavbharatTimes`/`AmarUjala`/
`DainikBhaskar`/`LiveHindustan`), same verify-every-URL-before-adding discipline, all using
spec-cased `pubDate` so none needed `BaseRssProvider` changes: **TimesOfIndia**
(`rssfeeds/{numericId}.cms` - a *different* numeric-id scheme from its sister paper
`NavbharatTimes`, which uses `langapi/sitemap/gstandrssfeed/{id}.xml` and only had one publicly
findable id). **HindustanTimes** (`feeds/rss/{section}/rssfeed.xml`; `topnews` returns 0 items and
is excluded). **ThePrint** (WordPress `/category/{name}/feed/`; the bare `/feed/` itself returns 0
items). **ScrollIn** (published entirely through FeedBurner - `feeds.feedburner.com/ScrollinArticles.rss`
- discoverable only via the `<link rel="alternate">` tag on scroll.in's homepage, no working
scroll.in URL of its own). **Mint** (`livemint.com/rss/{section}`, 35-item feeds). **DeccanHerald**
and **NewIndianExpress** (each has exactly one working feed, the bare WordPress `/feed` - every
section-specific guess 404s for both). **AmarUjala** (`rss/{slug}.xml`, hundreds of district-level
feeds alongside the ~8 topical ones actually used). **DainikBhaskar**
(`rss-v1--category-{numericId}.xml` - the id-to-category mapping isn't in the URL at all, so each
candidate id's own `<title>` had to be fetched during discovery to identify it). **LiveHindustan**
(served from a separate `api.livehindustan.com` subdomain, discoverable via livehindustan.com's own
`/rss/` index page).

**The Week** (`theweek.in`, English) uses a `{path}.rss` pattern under `/news/` (e.g.
`news/india.rss`) discovered on a retry after it was initially misjudged as broken - the earlier
`news.rss` guess was the sparse dead end, not the section-scoped feeds. 6 topical feeds are wired
up (India, World, BizTech, SciTech, Sports, Entertainment) plus a 7th aggregate `theweek.rss`
configured but `Enabled: false` (same "keep the narrow feeds, skip the noisy aggregate" pattern as
ZeeNews's `latest-news.xml`). The Week needed a real `BaseRssProvider` parsing change, not just a
new provider class: its `pubDate` is a Java `Date.toString()` string
(`"Thu Jan 5 21:09:41 IST 2026"` - space-padded single-digit days, and `IST` instead of an
offset) that neither of `ParsePublishDate`'s prior two tiers (plain parse, Zee's `GMT`-stripping
path) could handle, so a third tier was added: strip a literal `IST` token to ` +05:30 ` and, if
that alone doesn't parse, reorder the Java token sequence (`ddd MMM d HH:mm:ss zzz yyyy` ->
`d MMM yyyy HH:mm:ss zzz`) via `JavaDateOrderRegex` before retrying - deliberately in the shared
base rather than a Week-only override, since it's spec-tolerance for a known JVM string format,
not something unique to this one publisher.

**Six providers the user asked for could not be added - no working public RSS found, each
verified blocked/broken rather than just guessed-and-gave-up:** **Firstpost** (its
`commonfeeds/v1/eng/rss/*` API returns a JSON 400 "Invalid Property name" error, not XML; no
alternative endpoint found). **Business Standard** (Akamai WAF returns "Access Denied" to every
request pattern tried, including full browser headers - blocked at the network layer, not a
UA/format issue). **The Wire** (every path, including the homepage itself, serves an identical
9KB SPA-shell stub with `<meta http-equiv="refresh" content="999999">` - a bot-detection
interstitial, not real content, from this environment). **Outlook India** (no RSS `<link>` tag or
guessable URL pattern found). **Dainik Jagran** (robots.txt lists only XML sitemaps, no RSS; no
`<link>` tag on the homepage). **Jansatta** (same - no discoverable feed). None of these are wired
into `NewsCrawler.appsettings.json`; if a working feed URL turns up later, adding any of them is
the same purely-additive pattern as every provider above.

Five more English-language providers, each individually curl-verified (HTTP 200, well-formed
`<rss>` body, correct `Content-Type`) before adding, none needing `BaseRssProvider` changes since
all use spec-cased `pubDate`: **IndiaToday** (`indiatoday.in/rss/{numericId}` - a numeric-id scheme
discovered via the site's own `/rss` index page, same shape as AajTak's; the id-to-category mapping
came from that index page's link text, e.g. `1206578`→India, `1206514`→Nation; `TopStories`
(`1206584`) returns 0 items and is excluded; the 131-item `Home` aggregate (`/rss/home`) is
configured but `Enabled: false`, same "narrow feeds over noisy aggregate" pattern as ZeeNews/
TheWeek; no image tags, relies on the `og:image` HTML fallback). **DeccanChronicle** (only the
bare `/feed` works - every section-specific guess 404s, same situation as DeccanHerald/
NewIndianExpress; 506 items on that one feed; images come from a non-standard bare `<image>` tag
that `BaseRssProvider.ExtractImage` doesn't recognize, so these also fall through to the
`og:image` fetch). **OneIndia** (`oneindia.com/rss/feeds/{slug}-fb.xml`, discovered via the site's
own `/rss/` index page; its CDN returns 403 to crawler-style User-Agents while serving the same
public feeds to browser UAs - the second provider after News18 registered with `BrowserUserAgent`
in `InfrastructureServiceCollectionExtensions`; the 50-item `oneindia-fb` aggregate is configured
but `Enabled: false`). **NewsX** (standard WordPress `/feed` and `/category/{name}/feed`;
`politics` and `national-affairs` category slugs return 410 Gone and are excluded; no image tags,
relies on `og:image` fallback). **DnaIndia** (`dnaindia.com/feeds/{slug}.xml`; `cricket`,
`politics`, and `uttar-pradesh` slugs 404 and are excluded; uses standard `<enclosure>` for images).

**Four more providers the user asked for could not be added, each verified blocked/broken:**
**The Statesman** (Cloudflare bot-detection returns a "Just a moment..." interstitial with HTTP 403
on every path, including with full browser headers - blocked at the network layer; the bare
`/feed` and `/rss` paths, when reached, return WordPress's own `wp_die` "No feed available" error
rather than real content, so even without the block there's nothing there). **The Pioneer**
(`dailypioneer.com` is a Next.js SPA - every guessed RSS path either 404s or serves the SPA shell
as `text/html`; no `<link rel="alternate" type="application/rss+xml">` tag on the homepage to
discover a real endpoint from). **Patrika** (`patrika.com`'s `robots.txt` explicitly lists
`Disallow: /patrika-rss/` - the RSS path is real and discoverable, but respecting `robots.txt`
`Disallow` directives is a firm constraint in this codebase, the same reasoning Firstpost was
excluded under earlier, so it was not fetched or wired in). **Punjab Kesari** (`punjabkesari.in`
has no RSS `<link>` tag on its homepage and every guessed URL pattern 404s; `robots.txt` lists only
sitemaps, no RSS hints). None of these four are wired into `NewsCrawler.appsettings.json`.

**Government/legislative sources, plus Google News and YouTube - a much larger, mostly-negative
verification pass.** The user asked for ~15 named government/legislative bodies and ~90 more
(all central ministries, several statutory bodies, Google News, Google Alerts, and ~18 YouTube
channels). Individual Indian ministry websites turned out to be almost universally RSS-less -
**PIB** (`pib.gov.in`), **NDMA** (`ndma.gov.in`), and the **Ministry of Ports, Shipping and
Waterways** (`shipmin.gov.in`) are the only three that actually work, plus the citizen-engagement
platform **MyGov India** (`mygov.in`); everything else in that list (PMO India, ECI, Lok Sabha,
Rajya Sabha, PRS Legislative Research, India.gov.in, data.gov.in, RBI, SEBI, UIDAI, NIC, IMD, NITI
Aayog's own website, and 49 of the 50 named ministries) has no working RSS and is not wired in.

**PIB** (`pib.gov.in`) is an ASP.NET WebForms site with an undocumented but discoverable
`RssMain.aspx?ModId={category}&lang={language}` scheme - `ModId=6` is press releases, `ModId=8`
photo features, `ModId=9` media invitations; `lang=1` English, `lang=2` Hindi, `lang=3` Urdu (every
`lang` value above 3 silently falls back to Hindi content, so there is no fourth "regional"
language reachable this way - the separately-branded `pibregional.nic.in` multilingual site
mentioned in one of PIB's own old press releases is unreachable/dead, so "PIB Regional" specifically
was not added). Five feeds are wired up under one `PIB` provider block (English, Hindi, Urdu,
PhotoFeature, MediaInvitation).

**NDMA** and **MinistryOfPortsShipping** are each a single bare `/rss.xml` (10 items apiece);
Ports/Shipping's feed content skews toward audit/annual-report announcements rather than
press-release news, and includes one permanent placeholder "Test" item from the publisher's own
CMS - both accepted as-is, same as any other publisher's own editorial choices. **MyGovIndia**
(`mygov.in/rss.xml`, 10 items) is a mix of campaign announcements, ministry group-page listings,
and blog posts, reflecting what the platform itself actually is, not a pure news feed - also
accepted as-is.

**PMO India** (`pmindia.gov.in/en/feed/`) technically returns 200 with a well-formed RSS body, but
the feed contains exactly one item: a stale "test post" dated June 2024 from a developer's own
email address. The site's real speech/press-release content isn't exposed through that feed or any
guessed category-specific variant, so PMO India was not wired in (its YouTube channel was, see
below). **Rajya Sabha**'s `sansad.in/rs` section has `robots.txt: Disallow: /` - a blanket
disallow respected the same way Firstpost's and Patrika's were, so it was excluded on policy
grounds without ever being fetched, independent of whether a feed exists there. **ECI**, **Lok
Sabha**, **PRS Legislative Research**, **India.gov.in**, **data.gov.in**, **RBI**, **SEBI**,
**UIDAI**, **NIC**, and **NITI Aayog**'s own website all have no discoverable RSS `<link>` tag and
no working guessed URL pattern. **IMD** (`mausam.imd.gov.in` and `www.imd.gov.in`) is unreachable
outright - the TCP connection itself times out from this environment, not an HTTP-level block.

**Google News** (`news.google.com/rss/search`) is architecturally different from every other
provider: `GoogleNewsRssProvider` overrides a new `BaseRssProvider.ResolveFeedUrl(RssFeedOptions)`
virtual hook (default implementation is the identity, `feed.Url` unchanged, so all 30+ other
providers are unaffected) to treat a configured feed's `Url` as a plain-text search *topic*
(e.g. `"India politics"`) rather than a literal feed URL, building the actual
`news.google.com/rss/search?q={topic}&hl=en-IN&gl=IN&ceid=IN:en` URL from it at fetch time. This
means adding a new topic later is purely a configuration change - one new `Feeds` entry with the
topic text as `Url` - never a code change. Two things about Google News were deliberately accepted
as known trade-offs rather than engineered around: (1) the feed's own `<copyright>` element states
it is "made available solely for the purpose of rendering Google News results within a personal
feed reader for personal, non-commercial use" and that "any other use...is expressly prohibited" -
persisting it into this crawler's database is arguably outside that scope; flagged explicitly to
the user, who chose to proceed anyway. (2) Each article's `<link>` is an opaque
`news.google.com/rss/articles/{token}` redirect that does *not* resolve to the real publisher URL
via a normal HTTP redirect (Google resolves it client-side via JS, confirmed by following the
redirect and finding it just loops back to the same token URL with extra query params) - reliably
decoding it would need either a headless browser or an unofficial reverse-engineered scheme, both
judged too fragile to depend on, so Google News articles are stored under their Google-hosted link
as-is. Practical effect: the same story already ingested from a direct publisher feed will **not**
dedupe against its Google News copy - they end up as two separate documents by design, not a bug.

**YouTube** channel feeds are Atom 1.0 (`<entry>`/`<published>`/`<id>`/`<link href="...">`), not
RSS 2.0 - a different element vocabulary throughout, not a `BaseRssProvider`-style parsing quirk -
so `YouTubeRssProvider` implements `IRssProvider` directly rather than extending
`BaseRssProvider`, keeping the shared RSS 2.0 pipeline (used by every other provider) completely
untouched. A configured feed's `Url` is the full
`https://www.youtube.com/feeds/videos.xml?channel_id={id}` URL - channel ids are opaque (`UC...`)
and have to be resolved once per channel (from the channel page's own `"externalId"` JSON field,
or a YouTube search result's `"channelId"`) before adding a config entry; from then on, adding
another channel is purely a configuration change - one new `Feeds` list entry - same as every
other provider. Each video is normalized into the same `NormalizedArticle` shape everything else
uses (`Content`/`Summary` from the video's `media:description`, `ImageUrl` from `media:thumbnail`,
`Url` the watch page, tagged `"video"`) so it flows through the existing dedup/persistence/API
pipeline unchanged - there is no separate "video" concept in the domain model. 18 channels are
wired up: Narendra Modi, Sansad TV, DD News, DD India, PMO India, PIB India, Election Commission
of India, MyGov India, and the Ministries of External Affairs, Defence, Home Affairs, Finance,
Railways, Road Transport & Highways, Electronics & IT, Health & Family Welfare, Education, and
NITI Aayog - each individually verified by checking the resolved channel id's actual feed
`<title>` matches the expected entity (several initial handle guesses resolved to entirely
unrelated channels - e.g. `@PIB` and `@mha` are squatted by unrelated personal accounts - and were
discarded rather than trusted on the handle name alone).

**Raw-response retention is enforced by an active daily cleanup job, not just the passive Mongo
TTL index.** `NewsCrawler:RawResponseRetention` (default `7.00:00:00`, 7 days) drives two
independent mechanisms on the `RssRawResponses` collection: the TTL index
(`ttl_rawresponse_createdat` on `CreatedAt`, set up in
`RssRawResponseRepository.EnsureIndexesAsync`) and a Hangfire recurring job,
`NewsCrawler:RawResponseCleanupCron` (default `"0 5 * * *"`, 5 AM `Asia/Kolkata` - hardcoded to
IST since every provider/user of this app is India-focused, with a fallback to UTC only if the
host's tzdata lacks that zone id rather than crashing Worker startup over it), registered as
`cleanup-raw-responses` and executed through `HangfireRawResponseCleanupExecutor` ->
`IRawResponseCleanupService.CleanupAsync` -> `IRssRawResponseRepository.DeleteOlderThanAsync`
(same `PerformContext`-scoped-logging wrapper pattern as `HangfireCrawlJobExecutor`). The two
mechanisms are redundant by design: Mongo's TTL background thread only guarantees eventual
deletion "within 60 seconds" of expiry on its own schedule, not a specific wall-clock time, so the
explicit 5 AM job gives a deterministic daily deletion point without removing the TTL index as a
second, maintenance-free backstop. Because `RssRawResponseRepository.EnsureIndexesAsync` runs on
every host startup and a TTL index's `expireAfterSeconds` cannot be changed via plain
`createIndexes` once it already exists under that name (MongoDB rejects it outright as "an
equivalent index already exists with the same name but different options" instead of updating it -
unlike every other index option), that method detects a mismatch against the currently configured
retention and drops + recreates just that one index rather than crashing
`MongoIndexInitializerHostedService` (and therefore the whole host) the way it did when
`RawResponseRetention` was first changed from its original 30-day default down to 7 days against
an Atlas database that already had the old TTL value baked in.

Three more providers, added specifically to close a gap against Inshorts' publicly-known source
list (Reuters, Indian Express, PTI, The Guardian, NYT, Outlook, TechCrunch, Sportskeeda), each
individually curl-verified before adding: **NYTimes** (`rss.nytimes.com/services/xml/rss/nyt/
{Section}.xml` - a legacy but still-working feed path; `Sports.xml` returns 0 items and is
excluded; `World`/`Business`/`Technology`/`Politics`/`Science` all verified with real items,
`media:content` images, and spec-cased `pubDate`, so no `BaseRssProvider` changes needed).
**TechCrunch** (standard WordPress `/feed/`, no image tags, relies on the `og:image` HTML
fallback). **Sportskeeda** (a single all-sports aggregate mixing cricket/football/NFL/WWE/etc.;
no section-specific feed or `rel="alternate"` RSS link exists on any category page, so unlike
ZeeNews/TheWeek/IndiaToday's "narrow feeds over noisy aggregate" pattern, this one aggregate is
the only option and is `Enabled: true`; standard `media:thumbnail` and `pubDate`). Configured
directly against `api.sportskeeda.com/v3/feeds_v2/1414?limit=1000&response_type=w3c`, not the
human-friendly `sportskeeda.com/feed` alias - that alias 301s via a **CloudFront Function** (edge
compute, not a static redirect rule - visible as `x-cache: FunctionGeneratedResponse from
cloudfront` on the 301 response itself), which returned a 405 in production while still 301-ing
fine from this environment - almost certainly PoP/request-dependent function behavior, not a
fixed rule, so it's a different mechanism from the Akamai-WAF-blocks-Render's-IP pattern seen with
News18/IndianExpress even though the symptom ("works here, not in production") looks the same.
Hitting the resolved API URL directly serves identical content with zero redirect involved,
sidestepping the function entirely.

**Two more providers from that same Inshorts gap-check could not be added.** **Reuters**: no
working public RSS survives today - the old `feeds.reuters.com/reuters/topNews` path no longer
resolves, `reutersagency.com`'s feed (that's their PR/licensing site, not their news site) 404s,
and `reuters.com/rssFeed/topNews` / `reuters.com/world/rss` both return 401 behind a
"please enable JS and disable any ad blocker" challenge page - Reuters discontinued public RSS
some years back and now actively blocks non-browser requests at the edge. Two legacy FeedBurner
slugs (`feeds.feedburner.com/reuters/topNews`, `.../Reuters/worldNews`) still return HTTP 200 but
have been squatted by an unrelated third-party aggregator ("Flipso") - a live example of the
"successful fetch of HTML/wrong-content is the failure signature to watch for" caveat already
noted for Zee News. **PTI** (`ptinews.com`): `/feed` and `/rss` both return HTTP 200, but the body
is an Angular SPA HTML shell (`<!DOCTYPE html>`), not real RSS - the same disguised-non-content
failure signature, not a working feed. Neither is wired into `NewsCrawler.appsettings.json`.

**12 more providers, cross-checked against Feedspot's "Indian News RSS Feeds" list
(rss.feedspot.com/indian_news_rss_feeds/), each individually curl-verified before adding.** The
Feedspot list runs to 126 entries; the overwhelming majority are hyper-regional/small-town outlets
(Kashmir/Assam/Northeast/Chandigarh-local papers) or PR/startup content mills (KNN India,
BioVoice News, TechGenYZ, ...) that don't match this codebase's existing scope of major
national/institutional sources - those were deliberately not attempted. Nine are standard RSS 2.0,
no `BaseRssProvider` changes needed (all spec-cased `pubDate`, verified parseable): **Frontline**
(The Hindu Group's magazine, same `{section}/feeder/default.rss` pattern as TheHindu itself).
**HinduBusinessLine** (plain `?service=rss` on the site root). **TimesNow**
(`timesnownews.com/feeds/gns-en-latest.xml`, a single 100-item aggregate, no section-specific
variant found; no image tags, relies on `og:image`). **AltNews** (a fact-checking outlet, standard
WordPress `/feed/`; no image tags, relies on `og:image`). **TheBetterIndia** (`/feed/`, redirects
once - HttpClient follows it transparently). **TheProbe** (investigative journalism; the working
URL is the bare `theprobe.in/rss`, discovered via the homepage's own
`rel="alternate" type="application/rss+xml"` link tag - `/feed` and `/rss.xml` both 404).
**IndiaDotCom** (`india.com/feed/`; no image tags, relies on `og:image`). **Onmanorama** (Kerala's
Manorama Group English edition, `onmanorama.com/news.feeds.rss.xml`). **TelanganaToday**: only the
bare `/feed` actually works - every guessed category path (`telangana/feed`,
`category/andhra-pradesh/feed`, `opinion/feed`) silently returns the identical 500-item
everything-feed rather than filtering, and `national-international/feed` returns 0 items, so
(same situation as DeccanChronicle/DeccanHerald/NewIndianExpress) this one large aggregate is the
only real option and is wired up as-is.

Three more turned out to be **Atom 1.0, not RSS 2.0** - all three are hosted on the same QuintType
CMS platform's CDN (`prod-qt-images.s3.amazonaws.com/production/{site}/feed.xml`) and serve
identical `<entry>`/`<published>`/`<id>`/`<link href="..." rel="alternate">` markup, a different
element vocabulary throughout rather than a `BaseRssProvider`-style spec-tolerance quirk - the
same reasoning `YouTubeRssProvider` already implements `IRssProvider` directly instead of
extending `BaseRssProvider`. A new shared `BaseAtomRssProvider` (`Infrastructure/RssProviders/`)
factors out that parsing once; unlike YouTube's entries (which carry their own
`media:thumbnail`), these have no image tag at all, so every article's image comes from the
`og:image` HTML fallback - reusing `BaseRssProvider.TryExtractOgImageAsync` rather than
duplicating it, since that method was already provider-agnostic. The three providers: **TheQuint**,
**FreePressJournal**, and **NationalHerald** - each just a thin `BaseAtomRssProvider` subclass
supplying `Name`/`HttpClientName`, same shape as every RSS 2.0 provider subclassing
`BaseRssProvider`.

**Six more from that same Feedspot cross-check could not be added, each verified rather than
guessed-and-gave-up.** **Financial Express**: explicit `wp_die` `"Feeds have been disabled."` error
with HTTP 410 on every path tried - a deliberate, stated policy, not a technical block (same
"provider turned RSS off on purpose" category as Business Standard/Firstpost). **LiveLaw** (legal
news): every guessed path (`/feed`, `/rss.xml`) 404s, no `rel="alternate"` RSS link on the
homepage either. **The News Minute**: same - `/rss.xml`, `/feed` both return either 404 or an
empty 0-item body, no discoverable feed link on the homepage. **Rediff**: every guessed path
404s. **The Asian Age**: every attempt (including with a browser User-Agent) returns a bare HTTP
500 server error - the site itself appears to be in a broken/deprecated state, not actively
blocking. **Tribune India**: `/rss/feed` returns HTTP 403 even with a browser User-Agent - the
same WAF-block signature already seen with News18/IndianExpress/PIB, just not confirmed to have a
working feed underneath it worth working around. None of these six are wired into
`NewsCrawler.appsettings.json`.

**Three explicitly partisan/opinion outlets from that same Feedspot list - OpIndia, TFIPost,
Organiser (right-leaning) - were added deliberately, not by default. The Feedspot list included
these alongside National Herald (left-leaning, Congress-affiliated) added just above; since this
is specifically an election-news app, whether to include openly opinion-leaning sources at all -
and from which side(s) - was treated as the user's call rather than a coverage/technical one,
raised explicitly rather than silently deciding either way.** The user chose to include all four
"for balance" rather than exclude opinion-leaning sources entirely. All three are standard
WordPress `/feed/` (spec-cased `pubDate`, no image tags - relies on `og:image`), no
`BaseRssProvider` changes needed.

**International expansion (24 providers, 16 countries) from a user-supplied Country/Publisher/Feed
table spanning 21 countries** - the first non-India-focused feeds this app carries. Every URL was
individually curl-verified (not just the ones that "looked" like they'd work) before being wired
in, and this pass introduced a genuinely new cross-cutting concern:

**`NewsCrawler:Providers` became `NewsCrawler:Countries` - a new grouping level above providers,
not a field bolted onto individual feeds.** The first cut of this (Country as a property on
`RssFeedOptions`) was wrong: it gave visibility but no actual lever - there was still no single
switch to turn off "every India feed" or "every UK feed" at once, only per-provider/per-feed
`Enabled`. The corrected shape is `NewsCrawlerOptions.Countries: List<CountryOptions>`, each with
its own `Name` + `Enabled` + `Providers: List<RssProviderOptions>` (which still holds its own
`Enabled` and each of its `Feeds` still holds its own `Enabled`, unchanged) - three independent
on/off switches: country, provider, feed. `RssFeedOptions`/`FeedFetchResult` themselves carry no
`Country` at all; `NewsCrawlerOrchestrator` knows the country from the `Countries` loop it's
already iterating and stamps it directly onto the results - `NormalizedArticle.Country` via a
`with` expression on the articles returned from a successful fetch (record, so non-destructive),
and `ErrorNotification.Country` directly from the loop variable on a failed one. From there it
still flows `NormalizedArticle.Country` -> `NewsArticle.Country` (via `ArticlePersister`) and
`ErrorNotification.Country` -> `ErrorLog.Country` (via `ErrorLogRecorder`) -> shown as its own
column in the error-notification batch email's summary table and context panel, same end result
as before (a batch of crawl failures can be scanned by country at a glance) but the config
structure now actually supports "turn off Israel for now" as a one-line edit.
`HangfireRecurringJobRegistrar.RegisterNewsCrawlerRecurringJobs` and the two FluentValidation
validators that check "is this an enabled provider" (`TriggerProviderJobCommandValidator`,
`CreateOrUpdateRecurringJobCommandValidator`) all flatten `Countries.Where(Enabled).SelectMany(
Providers).Where(Enabled)` the same way - a provider under a disabled country gets no recurring
job registered at all, not just a skipped run.

**`BaseRssProvider.ParsePublishDate` gained a fourth tolerance tier for North American timezone
abbreviations** (EDT/EST/CDT/CST/MDT/MST/PDT/PST -> their fixed numeric UTC offset, e.g.
`EDT -> -04:00`) - same category of fix as the existing GMT-strip (Zee News) and IST-replace (The
Week) tiers, needed because CBC News emits `"Wed, 24 Jun 2026 21:33:43 EDT"` and .NET's
`DateTimeOffset.Parse` doesn't recognize named zone abbreviations beyond GMT/UTC. Deliberately a
fixed offset per abbreviation (not DST-aware calculation) since CBC's own abbreviation already
tells you which of standard/daylight time it is - EDT is always `-04:00`, EST is always `-05:00`,
by definition.

**24 providers added, standard RSS 2.0 (no other `BaseRssProvider` changes needed), one per
country except where noted:**
**United States**: NPR (`feeds.npr.org/1001/rss.xml`).
**United Kingdom**: BBCNews (3 feeds: TopStories/World/Politics, classic `feeds.bbci.co.uk`
endpoints), SkyNews (`feeds.skynews.com/feeds/rss/home.xml`), TheGuardian
(`theguardian.com/world/rss` - a plain RSS feed, distinct from the already-existing JSON
News-API `Guardian` provider which needs an API key and whose pipeline is disabled).
**Canada**: CBCNews (see the EDT tolerance tier above), GlobalNews (standard WordPress `/feed/`).
**Australia**: ABCNewsAustralia (`abc.net.au` - the Australian Broadcasting Corporation, distinct
from the American ABC network), SBSNews, SydneyMorningHerald.
**Germany**: DW/Deutsche Welle (`rss.dw.com/xml/rss-en-all`, no image tags - relies on `og:image`),
DerSpiegel (the English "International" edition).
**France**: France24 (the English edition at `france24.com/en/rss`).
**Japan**: NHKWorld (no image tags - relies on `og:image`), JapanTimes (standard WordPress
`/feed/`).
**South Korea**: Yonhap (the English edition, `en.yna.co.kr/RSS/news.xml`).
**Singapore**: CNA/Channel News Asia (`channelnewsasia.com/rssfeeds/8395986`).
**Indonesia**: Antara (the English edition, no image tags - relies on `og:image`).
**Thailand**: BangkokPost (no image tags - relies on `og:image`).
**Qatar**: AlJazeera (`aljazeera.com/xml/rss/all.xml`, no image tags - relies on `og:image`).
**Israel**: JerusalemPost, TimesOfIsrael (standard WordPress `/feed/`).
**Mexico**: MexicoNewsDaily (standard WordPress `/feed/`).
**Turkey**: AnadoluAgency (the English World feed, no image tags - relies on `og:image`).
**Russia**: TASS (`tass.com/rss/v2.xml`, no image tags - relies on `og:image`).

**Three technically-"working" feeds were deliberately excluded despite returning HTTP 200 with
well-formed XML and a real item count - a new failure signature this codebase hadn't documented
before: a live-looking feed that's actually frozen/abandoned.** **CNN**
(`rss.cnn.com/rss/cnn_topstories.rss` and `cnn_world.rss`): every item's `pubDate` is from April
2023 - CNN's classic RSS feeds are no longer updated, over three years stale. **Xinhua**
(`english.news.cn/rss/worldrss.xml`) and **China Daily** (`chinadaily.com.cn/rss/world_rss.xml`):
items date from 2017-2018 (China Daily's items have no `pubDate` at all, and its linked article
URLs are literally path-stamped `/a/201712/...`) - both frozen for the better part of a decade.
None of the three would ever deliver a single current article, so despite passing every technical
check, they're not wired into `NewsCrawler.appsettings.json`. Worth remembering as its own
category distinct from "blocked" or "wrong content type": a feed can return 200 and valid RSS and
still be dead.

**A handful of India rows from the same source table were already covered and skipped as
duplicates**: The Hindu's National/International/Business/Opinion feeds, NDTV's Top Stories, and
Times of India's India feed (same numeric id, `-2128936835.cms`, just labeled differently) all
already existed. Two India rows were checked and don't work: India Today's bare `/rss` (200, but
an HTML discovery/index page, not a feed - the already-existing numbered-id feeds under
`IndiaToday` are the real thing) and PIB's `rss.aspx` (404 - the already-existing `RssMain.aspx`
scheme is the real one).

**Ten more from the same table were verified and don't work, each checked rather than
guessed-and-gave-up**: **Reuters** (`reutersagency.com/feed/?best-topics=world` - 404; that's
their PR/licensing site, not their news site, consistent with Reuters having no working public
RSS at all, per the earlier Inshorts-gap-check entry above). **AP News**
(`apnews.com/hub/ap-top-news?output=rss` returns the plain homepage HTML, not RSS; `/apf-topnews`
redirects to the homepage too; `/rss` 404s - AP has no accessible classic RSS from this
environment). **CTV News** and **Le Monde**'s English edition: given URLs 404, no working
alternate found. **Bernama** (Malaysia): 404. **Gulf News** and **The National** (UAE): 404 on the
given URL and on guessed alternates. **Arab News** (Saudi Arabia): HTTP 403 even with a browser
User-Agent - blocked. **News24** (South Africa): the given `feeds.news24.com` subdomain doesn't
resolve at all (DNS failure); `www.news24.com/news24/rss` redirects to a 404. **Agência Brasil**
(Brazil): the given URL 404s; `/feed` redirects to a 500 server error. **Ukrinform** (Ukraine):
the given URL 404s; `/rss.xml` redirects to a 404 page. None of these ten are wired into
`NewsCrawler.appsettings.json`.

**19 more United States providers**, verified against a second user-supplied publisher table
(all individually curl-tested, several with more than one candidate URL before landing on the
real one), added to the existing `United States` country block alongside NYTimes/TechCrunch/NPR:
**ABCNews** (3 feeds: TopStories/US/Politics, classic `feeds.abcnews.com` endpoints).
**CBSNews** (3 feeds: Main/US/Politics, `cbsnews.com/latest/rss/{section}`). **NBCNews** (2 feeds:
News/Politics, classic `feeds.nbcnews.com`). **FoxNews** (3 feeds: Latest/Politics/National,
classic `feeds.foxnews.com`). **WashingtonPost** (3 feeds: Politics/National/World, classic
`feeds.washingtonpost.com/rss/{section}` - Politics is a genuinely low-volume feed, 1 item at
verification time, not broken). **WSJ** (2 feeds: WorldNews/Markets from `feeds.a.dj.com/rss/`;
a third, `RSSUSnews.xml`, returns a bare S3/CloudFront `<Error><Code>AccessDenied</Code>` XML body
- not real content - and is excluded). **Bloomberg** (3 feeds: Markets/Politics/Technology,
`feeds.bloomberg.com/{section}/news.rss`). **TheHill** (standard WordPress `/feed`, 100-item
aggregate). **Newsweek** (`/rss`). **Time** (standard WordPress `/feed`). **Forbes** - only
`/business/feed/` is wired up; `/most-popular/feed/` returns real, well-formed RSS but every item
is dated Jan-Oct 2024 (stale by well over a year) - an evergreen "most read" list, not recent
news, the same "technically 200 but dead" trap as CNN/Xinhua/China Daily above, so deliberately
excluded. **Fortune** (standard WordPress `/feed`). **BusinessInsider** (`/rss`, ISO-8601
`pubDate`, parses fine via the default tier). **MarketWatch** (`feeds.marketwatch.com/marketwatch/
topstories`). **CNBC** (2 feeds keyed by CNBC's own legacy numeric content ids -
`cnbc.com/id/{id}/device/rss/rss.html` - not a human-readable slug scheme). **LATimes**
(`world-nation/rss2.0.xml`). **ProPublica** (`/feeds/propublica/main`). **PBSNews** - the working
URL is `pbs.org/newshour/feeds/rss/headlines`; the bare `.../feeds/rss` returns HTTP 202 with an
empty body, not real content. **Vox** (`/rss/index.xml`, ISO-8601 `pubDate`).

**Ten more from that same US table verified and don't work.** **Reuters** (`reuters.com/tools/
rss` - HTTP 401, consistent with Reuters having no accessible public RSS from this environment at
all, per the two earlier Reuters entries above). **AP News** (`apnews.com/rss` 404s;
`?outputType=rss` on the top-news hub URL returns the plain homepage HTML, not RSS - no working
classic feed found). **USA TODAY** (`rssfeeds.usatoday.com/usatoday-NewsTopStories` returns 200
but HTML, not RSS; `usatoday.com/rss` returns HTTP 406 Not Acceptable). **Politico**
(`politico.com/rss/politicopicks.xml` serves a Cloudflare "Just a moment..." bot-challenge page -
blocked, not a working feed, despite returning HTTP 200). **Axios** (`/feed` returns HTTP 403 even
with a browser User-Agent). **Chicago Tribune** (`/arcio/rss` returns HTTP 403 even with a browser
User-Agent). **HuffPost** (`/section/front-page/feed` returns genuinely well-formed RSS - real
`<channel>` metadata, correct namespaces - but zero `<item>` elements; an empty feed, not a broken
one). **U.S. News & World Report** (`usnews.com/rss` - connection times out outright, not an
HTTP-level block). **WSJ's `RSSUSnews.xml`** and **Forbes's `/most-popular/feed/`** - covered
above (blocked-disguised-as-XML and stale-content respectively). None of these ten are wired
into `NewsCrawler.appsettings.json`.

(In the course of correcting the `Countries` config restructure above, 26 stale per-feed
`Country` properties - leftover from before that fix, when `RssFeedOptions` briefly carried its
own `Country` field - were found and removed from the JSON; harmless since the C# model no longer
declares that property so they were silently ignored, but removed for clarity.)

**United Kingdom deepened substantially** from a third user-supplied publisher table: BBCNews
grew from 3 feeds to 9 (added UK/Business/Technology/Science/Health/Entertainment), SkyNews from
1 to 6 (added UK/World/Politics/Business/Technology), TheGuardian from 1 to 11 (added UK/Politics/
Business/Technology/Environment/Science/Sport/Football/Culture/Opinion) - all just config
additions to already-existing providers, no new provider classes needed for those three. Nine
genuinely new UK providers, all individually curl-verified: **BBCSport** (a separate BBC feed/
section from BBCNews, not one of its categories). **FinancialTimes** (`ft.com/rss/home` plus
per-section `?format=rss` query-parameter feeds - World/UK/Companies/Markets/Technology).
**TheIndependent** (Home/UK/World, standard `/news/{section}/rss`). **TheTelegraph**
(Home/News/Politics/Business/Sport, `{section}/rss.xml`) - Home/Business/Sport update multiple
times a day, but News and Politics specifically update far less often (newest item ~1-2 weeks old
at verification time) - not frozen/dead like CNN/Xinhua/Forbes-most-popular (dates progress
steadily backward from that newest item, it's just a slow trickle, likely because most Telegraph
politics coverage sits behind their paywall and only occasional pieces reach the free RSS feed) -
kept as configured, same "keep it, note the low volume" precedent as IndiaTV's politics feed
elsewhere in this file. **Metro**, **EveningStandard**, **DailyExpress**, **Mirror** (all standard
WordPress-style `/feed`-shaped URLs). **PoliticsCoUk** (standard WordPress `/feed/`) - same
low-frequency-but-genuinely-live situation as Telegraph News/Politics (newest item ~2 weeks old),
kept for the same reason.

**Two from that same UK table don't work.** **Reuters** (`reutersagency.com/feed/?best-topics=
world` - 404 yet again, the fourth independent confirmation in this file that Reuters has no
accessible public RSS from this environment). **Daily Mail**
(`dailymail.co.uk/news/index.rss` - the connection itself times out outright with both a crawler
and a browser User-Agent, a network-level block/timeout rather than an HTTP-level one). Neither
is wired into `NewsCrawler.appsettings.json`.

**Canada/Australia/Japan deepened from a fourth user-supplied publisher table.** Existing
providers gained more feeds (config-only): CBCNews 1 -> 6 (added Canada/Politics/Business/World/
Technology, all `cbc.ca/webfeed/rss/rss-{section}`), GlobalNews 1 -> 4 (added Politics/World/
Business), NHKWorld 1 -> 5 (added World/Business/Science/Politics via NHK's own `cat3`/`cat5`/
`cat7`/`cat4` numeric-category scheme). Eight new providers, all curl-verified: **NationalPost**
(Canada, standard WordPress `/feed`). **SevenNews**/7NEWS (Australia, `/rss`). **TheAge**
(Australia, `smh.com.au`'s sister masthead, same `/rss/feed.xml` pattern). **GuardianAustralia**
(a separate provider from the UK `TheGuardian` one - a distinct editorial edition, not just
another category - `theguardian.com/australia-news/rss`; the requested Politics-edition feed
404s, and the requested World feed is the *exact same URL* already configured under `TheGuardian`
in the UK block, so both were excluded to avoid double-fetching one URL under two provider names
for no benefit). **NikkeiAsia** (Japan, an RSS 1.0/RDF feed whose items carry no date element at
all - no `pubDate`, no `dc:date` - so `PublishedAt` is always null for this provider specifically;
a genuine feed limitation, not a parsing bug). **MainichiJapan** (The Mainichi's English edition,
standard RSS 2.0). **AsahiShimbun** (Japanese-language, `Language: "ja"` - also RSS 1.0/RDF, and
this one *does* carry dates, just via Dublin Core's `<dc:date>` instead of `<pubDate>` - see the
`BaseRssProvider` fix below). **JapanToday** (English-language, standard WordPress `/feed`).

**`BaseRssProvider.ParseItemAsync`'s date lookup gained a Dublin Core fallback**: RSS 1.0/RDF
feeds (The Asahi Shimbun) have no `<pubDate>` element at all and use `<dc:date>` instead (the
`DublinCore` XML namespace was already declared and in use for the `dc:creator` author fallback,
so this reuses it rather than adding a new one) - falls back to `dc:date` only when no
`pubDate`-named element is found, so it can never override a real `pubDate` on a feed that has
both. Same category of fix as the existing GMT-strip/IST-replace/EDT-offset tiers - a new,
verified spec-tolerance gap, not a hypothetical one, covered by its own regression test
(`AsahiShimbunRssProviderTests`) the same way CBC's EDT fix was.

**Five more from that same Canada/Australia/Japan table don't work.** **CTV News** (all 5
requested feed URLs - Top Stories/Canada/Politics/World/Business - 404, including the exact URL
this codebase had already tried once before for CTV and found dead). **Toronto Star** (403
Forbidden, including on guessed `/feed`/`/rss` alternates). **The Globe and Mail** (404 on the
given URL and on guessed alternates). **The Australian** (403 Forbidden). **9News** (404, no
working alternate found). **News.com.au** (`/content-feeds/latest-news/` returns the plain
article-listing HTML page, not RSS, despite the URL's name suggesting a feed). **Kyodo News**
(404, no working alternate found). **Yomiuri Shimbun** (403 Forbidden on the given URL; a guessed
alternate domain is unreachable outright). ABC News Australia's four additional requested
category feeds (Politics/Business/World/Science) all 404 - only the existing numbered `TopStories`
feed (`/feed/51120/rss.xml`) works; no discoverable category-feed URL pattern was found via the
site's own homepage either. None of these are wired into `NewsCrawler.appsettings.json`.

**Germany/France deepened from a fifth user-supplied publisher table.** Existing providers gained
more feeds (config-only): DW 1 -> 5 (added World/Business/Germany/Science) - two of the
requested slugs (`rss-en-business`, `rss-en-sci`) return a literal `"Error: no feed by that
name."` body despite HTTP 200, so DW's *real* slugs were found by trial (`rss-en-bus` for
Business, `rss-en-science` for Science - both verified with real, fresh content; several other
guessed variants, `rss-en-eco`/`rss-en-scitech`/`rss-en-business-and-innovation`/`rss-en-economy`,
all returned that same fake-200 error body and were discarded). DerSpiegel 1 -> 5 (added
Germany/Business/Technology/Science, all `Language: "de"` - the German-language sections, unlike
the existing English "International" edition; deliberately mixed-language within one provider
since that's genuinely what Spiegel publishes per section). France24 1 -> 4 (added World/
Business/Europe; the requested Science/Technology feed - `france24.com/en/science/rss` - 404s
and is excluded). Eight new providers, all curl-verified: **FAZ**/Frankfurter Allgemeine Zeitung
(Germany, German-language, 3 feeds: News/Politics/Business). **Tagesschau** (Germany,
German-language, ARD's flagship news program). **ZDF** (Germany, German-language).
**LeMonde** (France, French-language section feeds only - the requested English-edition feed,
`lemonde.fr/en/rss/full.xml`, 404s; International and Economy sections work). **LeFigaro**
(France, French-language, 3 feeds: News/Politics/Economy). **RFI**/Radio France Internationale
(France, English-language edition). **FranceInfo** (France, French-language). **Liberation**
(France, French-language, Arc XP CMS pattern).

**Four more from that same table don't work.** **Handelsblatt** (Germany, 404 on the requested
`contentexport/feed` URL). **Reuters Germany** (`reutersagency.com/feed/?best-topics=uk` - 404,
the fifth independent confirmation in this file that Reuters has no accessible public RSS from
this environment). **AFP** (France, 404 on the requested `/en/rss` URL). **Les Echos** (France,
HTTP 403 Forbidden). None of these four are wired into `NewsCrawler.appsettings.json`.

**Singapore/South Korea/China/Qatar/Israel/South Africa/Brazil from a sixth user-supplied
publisher table - the highest dead-end rate of any batch so far, including one country (UAE)
with zero working feeds at all.** Existing providers gained one feed each (config-only): CNA
(added the Singapore-specific `rssfeeds/8395954`; the requested Asia/World/Business feed ids all
404 - only TopStories and Singapore have real ids), Yonhap (added National; Politics/Economy/
World all 404). Six new providers, all curl-verified: **GlobalTimes** (China, English-language,
state-affiliated - genuinely live and current, unlike Xinhua/China Daily covered below).
**YnetNews** (Israel, English-language). **GulfTimes** (Qatar) - the bare `/rss` path turned out
to be an HTML index page listing numbered feed links (`/rssFeed/{id}`, the same "index page, not
a feed" pattern as IndianExpress/TimesOfIndia elsewhere in this file), not a feed itself;
`/rssFeed/9` (the first one listed) is real and current. **SABCNews** (South Africa,
English-language). **Folha** (Brazil, Portuguese - an RSS 0.91 feed with no `pubDate` at all,
so `PublishedAt` is always null here, same situation as Nikkei Asia; also declares
`encoding="ISO-8859-1"` in its own XML prolog while the HTTP `Content-Type` header omits a
charset, so accented Portuguese characters in title/description could come through mangled under
.NET's default UTF-8 decode - a known, unfixed cosmetic caveat, not a functional one; links,
dedup, and persistence are all ASCII-safe regardless). **BrazilReports** (Brazil,
English-language) - a genuinely low-frequency-publishing site (newest item ~5-6 weeks old at
verification time) rather than a broken/frozen feed, kept per the same "keep it, note the low
volume" precedent as IndiaTV/Telegraph-News-Politics/PoliticsCoUk.

**Two feeds that returned HTTP 200 with real-looking RSS turned out to be genuinely empty (real
channel metadata, zero `<item>` elements) rather than blocked or broken** - the same category as
HuffPost covered above: **O Globo** (Brazil) and **The Korea Times**'s real feed (`koreatimes.co.kr/
www/rss/rss.xml` redirects to `feed.koreatimes.co.kr/k/allnews.xml`, which is real and reachable
but empty). Neither is wired in.

**Everything else from this table - the large majority - is dead, each verified rather than
guessed-and-gave-up:**
**Xinhua**'s two new requested feeds (China/Business) are stale 2017-dated content, consistent
with Xinhua's existing exclusion elsewhere in this file. **China Daily**'s new requested feed is
the same 2017-era stale content (Business variant 404s outright). **CGTN** and **People's Daily**
(China): 404. **The Straits Times** and **The Business Times** (Singapore): every feed URL
returns a literal S3/CloudFront `<Error><Code>AccessDenied</Code>` XML body - the same
disguised-block signature already seen elsewhere in this file (Reuters's old FeedBurner slugs),
not real content despite HTTP 200. **TODAY** (Singapore): 403. **The Korea Herald**: an entirely
empty HTTP response body. **JoongAng Daily**: 404. **KBS World**: a zero-byte response. **Gulf
News**, **The National**, and **Khaleej Times** (UAE): every requested feed URL 404s for all
three. **WAM**/Emirates News Agency (UAE): redirects to an F5 bot-protection JavaScript
challenge page, not real content. **The Jerusalem Post**'s four new requested category feeds
(Israel/Middle East/International/Business) and **Times of Israel**'s two new requested feeds
(Israel & Region 404s, Politics 403s) - only each provider's existing single feed works.
**Haaretz**: 403. **Al Jazeera**'s four "new" requested rows (News/World/Business/Politics) are
all the exact same URL as the already-configured AllNews feed - skipped as duplicates, not
re-added. **The Peninsula Qatar**: returns a generic homepage-shaped HTML page, not RSS.
**News24** (South Africa): the given `feeds.news24.com` subdomain still doesn't resolve at all
(DNS failure) - the same finding as an earlier table's News24 entry. **TimesLIVE**, **Mail &
Guardian**, and **IOL** (South Africa): 404. **Agência Brasil**'s three requested feeds: 404,
consistent with its existing exclusion elsewhere in this file. **G1** (Brazil): 403. **Estadão**
(Brazil): 404. None of these are wired into `NewsCrawler.appsettings.json`.

**Mexico/Turkey/Russia deepened, plus a new Ukraine country block, from a seventh user-supplied
publisher table - another high dead-end rate.** AnadoluAgency gained Politics/Economy (config
only, 1 -> 3 feeds). Six new providers, all curl-verified: **Reforma** (Mexico, Spanish-language).
**DailySabah** (Turkey, English-language) - the bare `/rss` path is only an HTML index page
listing category feed links (`/rss/{category}`), the same "index page, not a feed" pattern as
GulfTimes/IndianExpress/TimesOfIndia elsewhere in this file; Business/Politics/World are the real
working feeds. **KyivPost** (Ukraine, English-language - the only working Ukrainian feed found in
this whole batch). **RT** (Russia, English-language, state-affiliated - 2 feeds, verified as
genuinely distinct content, not duplicates of each other despite both being labeled generically).
**InterfaxRussia** (Russia, Russian-language - a distinct provider from the already-excluded
"Interfax-Ukraine", named `InterfaxRussia` in this codebase specifically to avoid future
ambiguity between the two). **RBC** (Russia, Russian-language business edition). TASS's three
requested section-specific feeds (`tass.com/world|economy|politics/rss/v2.xml`) all 404 - TASS
still has only the one working feed already configured.

**Everything else from this table is dead** - Mexico: **El Universal** (all 3 requested feeds
404), **Milenio** (404), **La Jornada** (403), **Excélsior** (404). Turkey: **TRT World** (404),
**Hürriyet** (403 on both requested feeds). Ukraine: **Ukrinform** (all 3 requested feeds 404,
consistent with its exclusion in an earlier table), **The Kyiv Independent** (404), **Interfax-
Ukraine** (404), **UNIAN** (403). None of these are wired into `NewsCrawler.appsettings.json`.

**Four new countries added - Italy, Spain, Netherlands, Sweden - from an eighth user-supplied
publisher table.** 16 new providers, each curl-verified, none needing `BaseRssProvider` changes.
**ANSA** (Italy, English-language) - the requested per-section feeds
(`ansa.it/english/{news,politics,economy,world}/rss.xml`) all 404; the real (and only) working
feed is the site-wide `ansa.it/english/english_rss.xml`, discovered by testing ANSA's other known
`_rss.xml`-suffix naming convention. **Corriere della Sera** (Italy, Italian-language) -
`xml2.corriereobjects.it/rss/homepage.xml`. **Il Sole 24 Ore** (Italy, Italian-language business
daily) - the requested `/rss/home.xml` 404s, but the site's own `/rss` index page lists real
per-section feeds using a `--` subsection separator (e.g. `italia--politica.xml`); 4 feeds wired
up (Latest/Politics/Business/World). **El País** (Spain, Spanish-language) - only the homepage
feed (`.../elpais.com/portada`) resolves; the requested International/Economy feeds and every
other guessed section slug under the same `mrss-s` path pattern 404, so this provider ships with
one feed only. **El Mundo**, **ABC** (Spain; named `AbcEspana` in this codebase to disambiguate
from the existing US ABC News / Australia ABC News providers), **Europa Press** (all Spain,
Spanish-language) - all worked as given. **NL Times** (Netherlands, English-language), **DutchNews**
(Netherlands, English-language, standard WordPress `/feed`), **NOS** (Netherlands, Dutch-language
public broadcaster) - the requested second "Politics" feed (`feeds.nos.nl/nospolitiek`) 404s and
no alternative is discoverable on nos.nl's own homepage, so only the general-news feed is wired
up. **NU.nl**, **De Telegraaf** (both Netherlands, Dutch-language) - worked as given. **SVT News**,
**Dagens Nyheter**, **Sydsvenskan**, **Aftonbladet** (all Sweden, Swedish-language) - worked as
given.

**Three Netherlands/Italy/Spain requests could not be added, plus RAI News, each verified
blocked/broken:** **RAI News** (Italy) - no RSS `<link>` on the homepage; its known
`atomatic/rainews-rss/...` legacy path returns HTTP 401 (not 404) on every guessed section,
suggesting the feed still technically exists but is no longer publicly reachable. **La Repubblica**
(Italy) - 403 on the requested feed even with a browser UA and Referer header. **RTVE** (Spain) -
no RSS `<link>` or guessable URL pattern found; the requested `/rss/noticias.xml` 404s. **Sveriges
Radio** (Sweden) - 403 at the network layer on both the requested feed and its own `/rss` index
page. None of these four are wired into `NewsCrawler.appsettings.json`. (**The Local Sweden**, also
in this table, was initially marked dead here too - see the correction two batches below, once
thelocal.com's shared feed-builder platform was discovered.)

**Eight new countries - Norway, Finland, Belgium, Switzerland, Austria, Ireland, Denmark, New
Zealand - from a ninth user-supplied publisher table, most requiring a fallback URL discovered
via the publisher's own homepage rather than the URL as given.** 21 new providers, all
curl-verified. **Norway**: NRK, Aftenposten, E24 all worked as given; **The Local Norway**'s
requested `thelocal.no/feed` 404s, but its homepage declares a `rel="alternate"` link to
`feeds.thelocal.com/rss/builder/no` - thelocal.com's own shared feed-builder platform, keyed by
country code (the same platform behind The Local Denmark below, and - once this pattern was
spotted - retroactively behind **The Local Sweden** too: its `thelocal.se/feed` guess had been
marked dead in an earlier batch, but `feeds.thelocal.com/rss/builder/se` works the same way and
was added to the existing Sweden country block as a fifth provider once this pattern surfaced). **Finland**: YLE News,
Helsingin Sanomat, Ilta-Sanomat all worked as given; **Kauppalehti**'s requested `/rss` 404s and no
alternative is discoverable anywhere on its homepage. **Belgium**: only **RTBF** could be added -
the requested `rtbf.be/rss` 404s and `rss.rtbf.be`'s own root 403s, but RTBF's own
"how RSS works" help article (`rtbf.be/article/le-flux-rss-mode-d-emploi-3266`) lists real
per-section feed URLs on that same host; `rss.rtbf.be/article/rss/highlight_rtbf_info.xml` is the
general-news one. **Switzerland**: only **NZZ** could be added - the requested `nzz.ch/rss` returns
the plain homepage HTML, not a feed, but `nzz.ch/recent.rss` (found via NZZ's other known
`*.rss`-suffix convention) is real. **Austria**: all four worked, two via a fallback URL - **ORF
News**'s requested `orf.at/stories/rss.xml` 404s, but the homepage's own `rel="alternate"` tag
points to `rss.orf.at/news.xml` (an RSS 1.0/RDF feed using `dc:date`, not `pubDate` - already
covered by `BaseRssProvider`'s existing Dublin Core fallback, no code change needed); **Kurier**'s
requested `/rss` 404s, but `kurier.at/xml/rss` is real; Der Standard and Die Presse worked as
given. **Ireland**: all four worked, one via a fallback URL - **The Irish Times**'s requested
`/feeds/rss/` 404s, but following a working legacy `/cmlink/` redirect resolves to its real Arc XP
CMS outbound-feeds URL, `irishtimes.com/arc/outboundfeeds/feed-irish-news/?from=0&size=20`; RTÉ
News, Irish Independent, The Journal worked as given. **Denmark**: only **DR News** worked as
given; **The Local Denmark**'s requested `/feed` 404s, resolved the same way as The Local Norway
above, via `feeds.thelocal.com/rss/builder/dk`. **New Zealand**: **RNZ** worked as given; **Stuff**
is Atom 1.0 (`<feed>`/`<entry>`/`<published>`), not RSS 2.0, so `StuffRssProvider` extends
`BaseAtomRssProvider` rather than `BaseRssProvider` - same reasoning as The Quint/Free Press
Journal/National Herald; unlike those three, Stuff's entries do carry their own `media:content`
image tags, but `BaseAtomRssProvider` doesn't parse that element (none of its existing users have
one), so images still resolve correctly via the `og:image` HTML fallback, just with one avoidable
extra request per article - accepted as-is rather than special-cased for a single provider.

**Fourteen more from that same ninth table are dead, each verified rather than
guessed-and-gave-up.** Belgium: **VRT NWS** (every guessed path 404s or redirects back to its own
homepage, no discoverable feed), **The Brussels Times** (`/feed` and every guessed alternate
return plain homepage HTML - an SPA route, not real content), **Belga News Agency** (`/rss` 404s;
`/feed` redirects straight to a 404). Switzerland: **Swissinfo** (`/eng/rss` returns HTTP 410
Gone - a deliberate "feed retired" signal, not a technical failure - and no alternative is
discoverable on its homepage, which only declares `hreflang` alternates, not RSS), **SRF News**
(every guessed path 404s, no RSS `<link>` on its homepage), **RTS** (`/info/rss` 404s;
`/rss/info.xml` returns HTTP 406 whose body, once decompressed, is itself just a French
"Page Introuvable" 404 page - the same disguised-non-content signature already documented
elsewhere in this file for other publishers, not a working feed). Austria: none - all four
requested rows worked (see above). Ireland: none - all four worked. Denmark: **TV2 News**
(`/rss` redirects to a plain 404), **Berlingske** (`/rss` 404s; no RSS `<link>` anywhere on its
homepage despite a 670KB page body). New Zealand: **NZ Herald** (`/rss/` returns plain homepage
HTML; guessed Arc-XP-style and legacy paths 404; `/feed` redirects straight to a 404), **1News**
(`/rss.xml` 404s; `/feed` redirects to a 404). Finland: **Kauppalehti** (see above). None of these
fourteen are wired into `NewsCrawler.appsettings.json`.

**Seventeen new countries - Poland, Czech Republic, Romania, Hungary, Greece, Portugal, Malaysia,
Vietnam, Philippines, Pakistan, Bangladesh, Nepal, Sri Lanka, Nigeria, Kenya, Egypt, Taiwan -
plus Indonesia deepened, from a tenth and eleventh user-supplied publisher table (two tables
pasted together, the largest batch so far). 38 new providers, most needing a fallback URL found
via the publisher's own homepage rather than the URL as given** - the same "index page/
rel=alternate discovery" pattern used throughout this file, applied at higher volume than any
prior batch. **Poland**: **Rzeczpospolita**'s requested `/rss` 404s, but its homepage declares
`rel="alternate"` to `/rss_main`; **Onet**'s requested `/rss` 404s too, but its news subdomain
serves a real feed at `wiadomosci.onet.pl/.feed` (Onet's own dot-feed convention); **TVP World**
and **PAP** have no discoverable feed at all. **Czech Republic**: all four requested rows
resolved, three via a fallback - **CTK**'s real feed is
`ceskenoviny.cz/sluzby/rss/zpravy.php`, **Radio Prague International**'s is
`english.radio.cz/rcz-rss/en`, both declared via `rel="alternate"` tags the requested URLs
lacked; iDNES and Seznam Zpravy worked as given. **Romania**: **Agerpres**'s requested feed
redirects through a broken third-party feed-extraction proxy (`api.allorigins.win/.../
bazqux.com/...`, itself erroring with HTTP 520) rather than serving real content - confirmed
broken, not just guessed-and-gave-up; **Romania Insider** resolved via `/feed` once `/rss` 404d;
Digi24 and HotNews worked as given. **Hungary**: all four requested rows resolved - **HVG**,
**Index.hu**, **Hungary Today** worked as given; the row labeled "MTI (Hungarian News Agency)"
resolves to a real feed at `magyarnemzet.hu/feed`, but that feed's own `<title>` and content are
unambiguously **Magyar Nemzet** (a Hungarian conservative daily), not MTI (a different,
unrelated state news agency with no discoverable public feed of its own) - named
`MagyarNemzet` in this codebase for what the feed actually is, not the requested label, flagged
explicitly rather than silently mislabeled. **Greece**: only **Proto Thema** worked; Ekathimerini
(403), ANA-MPA (Incapsula bot-block, confirmed even with extra headers), and Kathimerini Greece
(HTTP 410 Gone, no alternative discoverable) are all dead. **Portugal**: only **RTP Noticias**
worked; **Publico** returns HTTP 202 with an empty body on both `/rss` and its own homepage - a
bot-challenge/queueing response, not a working feed; SIC Noticias and The Portugal News are
blocked outright (403 on the homepage itself for the latter). **Indonesia**: **Tempo** added as a
new provider alongside the already-existing Antara; Jakarta Post and Kompas have no discoverable
feed. **Malaysia**: **Malay Mail** worked as given; **New Straits Times**'s requested `/rss` 404s
but `/feed` is real; Bernama and The Star have no discoverable feed. **Thailand**: none of the
three requested rows (The Nation Thailand, Thai PBS, MCOT) resolved - all three redirect to
plain homepage HTML on every guessed path - so Thailand's provider count is unchanged (still just
the pre-existing BangkokPost). **Vietnam**: all four resolved, one via a fallback - **Vietnam
News**'s requested `/rss.html` 404s, but per-section feeds exist at `/rss/{section}.rss` (no
`rel="alternate"` tag to discover this from; found via the site's other known section-feed
naming convention), `/rss/homepage.rss` is the general one; VietnamPlus, VnExpress, Nhan Dan
worked as given (two of them served gzip-compressed bodies that an initial verification pass
misread as garbled/broken - the same false-negative already documented for O Globo/Folha
elsewhere in this file - re-verified with `curl --compressed` and confirmed real). **Philippines**:
Inquirer and Rappler worked as given; **GMA News**'s requested URL 404s, but a `rel="alternate"`
tag on its `/news/rss/` index page points to a separate legacy data subdomain,
`data.gmanetwork.com/gno/rss/news/nation/feed.xml`; ABS-CBN News is blocked (403). **Pakistan**:
The News International and Geo News worked as given; Dawn's connection times out outright at the
network layer (not an HTTP-level block) and Express Tribune is blocked (403) - both confirmed
dead across multiple retries. **Bangladesh**: only The Daily Star worked; Dhaka Tribune's
homepage itself is blocked (403), BDNews24 is blocked (403), and BSS's `/rss` redirects to a 403
- all three dead. **Nepal**: **Kathmandu Post** worked as given (its feed serves real,
well-formed RSS 2.0 despite mislabeling its own Content-Type as `text/html` - a labeling quirk
confirmed harmless since `BaseRssProvider` never inspects Content-Type before parsing);
**Republica**'s requested `/rss` 404s but a `rel="alternate"` tag points to the real `/feeds`
path; The Himalayan Times has no discoverable feed and RSS Nepal times out at the network layer.
**Sri Lanka**: only **Ada Derana** worked; Daily Mirror serves a Cloudflare "Just a moment..."
challenge page (403) and Daily News/Sunday Observer are both blocked (403) - all three dead.
**Nigeria**: Premium Times, Punch, Vanguard all worked as given; The Nation is blocked (403).
**Kenya**: only **The Standard** worked, via the same "index page lists real per-section feeds"
pattern as GulfTimes/DailySabah elsewhere in this file - its `/rss` page 404s as a direct feed but
lists real links at `/rss/{section}.php`, three wired up (Headlines/Kenya/Politics); Nation
Africa is blocked (403), Capital FM's `/feed/` redirects to `capitalfm.africa` (a domain migration
discovered via a `rel="alternate"` tag) which itself redirects straight back to that new domain's
homepage - confirmed dead despite the promising-looking domain move; Citizen Digital returns
HTTP 400 on every attempt. **Egypt**: only **Daily News Egypt** worked; Ahram Online and Egypt
Today are both blocked (403), and the State Information Service times out at the network layer.
**Taiwan**: **Focus Taiwan**'s requested `/rss` 404s, but a `rel="alternate"` tag points to a
FeedBurner-hosted feed, `feeds.feedburner.com/rsscna/engnews/`, whose own `<title>` confirms it's
CNA's (Central News Agency's) English service - the user's table listed "Focus Taiwan" and
"Central News Agency" as two separate rows sharing the identical requested URL, and since they
resolve to the same one real feed under the same real publisher, only one provider
(`FocusTaiwan`) was wired up rather than two pointing at the same content; **Taipei Times**'s
requested `/rss/rss.xml` 404s, but a `rel="alternate"` tag points to `/xml/index.rss` - RSS
1.0/RDF using `dc:date`, already covered by the existing Dublin Core fallback, no code change
needed; Taiwan News's every guessed path redirects to plain homepage HTML - dead.

**Fourteen new countries - Iran, UAE, Hong Kong, Argentina, Colombia, Venezuela, Myanmar, Peru,
Morocco, Algeria, Ghana, Lebanon, Oman, Jordan - from a twelfth user-supplied publisher table (20
countries requested, 6 came back with zero working feeds). 28 new providers, again mostly
resolved via a fallback URL rather than the one given.** **Iran**: IRNA, Tehran Times, Mehr News
all worked as given; **Tasnim News**'s domain doesn't resolve at all (DNS failure) - unreachable,
not just blocked. **Saudi Arabia**: zero providers wired in - Arab News's own `rel="alternate"`
tag points to the exact same URL that already returns 403, confirming a real block rather than a
wrong-guess; **Saudi Gazette**'s feed is real, valid RSS 2.0 structure but contains zero
`<item>` elements - the same "technically-200-but-empty" trap as HuffPost/O Globo/Korea Times
elsewhere in this file; SPA's `/rss.xml` serves the plain Arabic homepage, no discoverable feed;
Asharq Al-Awsat's homepage has only `hreflang` alternates, no RSS. **UAE**: only **Khaleej
Times** worked - its requested `/rss` 404s, but `rel="alternate"` tags on the homepage point to a
`/api/v1/collections/{section}.rss` scheme, "top-section" being the general one; Gulf News, The
National, and WAM all have no discoverable feed (The National's alternates are just `hreflang`
tags, not RSS - a distinct dead-end shape from the other two's flat 404s). **Hong Kong**: SCMP
and Hong Kong Free Press worked as given; RTHK and The Standard both have no discoverable feed
(RTHK's `/rss.htm` page only self-links, no real `.xml` feed found; The Standard's every guessed
path redirects to its own 404 page). **Argentina**: Buenos Aires Times, La Nacion, Clarin all
worked as given; Ambito is blocked (403, confirmed even with Spanish-locale headers). **Colombia**:
all three requested outlets resolved, two via a fallback - **El Espectador** and **Semana**'s
requested URLs both 404, but each is built on the Arc XP CMS platform (the same one behind
IndianExpress/The Irish Times/La Nacion above - Arc XP is licensed out to several unrelated
publishers internationally) and each declares (or, for Semana, is reachable at) its own
`arc/outboundfeeds/.../rss` path; La República worked as given (named `LaRepublicaColombia` in
this codebase to disambiguate from Peru's own, unrelated "La República" below); El Tiempo's Arc
XP outbound-feeds path 404s too - genuinely dead, not just a wrong guess. **Venezuela**: Caracas
Chronicles and El Nacional worked as given; Últimas Noticias is blocked (403). **Myanmar**: all
three requested outlets worked as given - Myanmar Now, Global New Light of Myanmar (the military
government's official English paper), and Eleven Media. **Iraq**: zero providers wired in - the
Iraqi News Agency's `eng/rss.xml` is real, valid RSS 2.0 (Arabic channel title despite the "eng"
path) but contains zero `<item>` elements, the same empty-feed trap as Saudi Gazette above; Rudaw
and Shafaq both have no discoverable feed, only `hreflang` alternates on their homepages. **Peru**:
Andina and Peru Reports worked as given (Andina's feed is plain RSS 2.0 despite an
`application/atom+xml` Content-Type mislabel, harmless since `BaseRssProvider` never inspects
Content-Type before parsing); the requested "La República" (`larepublica.pe/feed`) 404s and has
no discoverable alternative - not wired in, so Peru's `LaRepublicaColombia`-style naming collision
never actually arises in code. **Chile**: zero providers wired in - BioBioChile and La Tercera
both have no discoverable feed at all (no `rel="alternate"` tag, every guessed WordPress/Arc-XP
path 404s), El Mercurio is blocked (401). **Morocco**: only **Hespress** worked; MAP is blocked
(403, confirmed even with a Moroccan-locale header) and Morocco World News's homepage itself
returns 403 - blocked outright, not just its feed. **Algeria**: TSA Algeria and El Watan worked
as given; APS has no discoverable feed (homepage has only `hreflang` alternates, guessed
WordPress paths 404). **Ethiopia**: zero providers wired in - ENA's homepage has only `hreflang`
alternates, no RSS; Addis Standard and The Reporter Ethiopia are both blocked (403). **Ghana**:
Joy News worked as given; **Graphic Online**'s requested `/feed.html` 404s, but `rel="alternate"`
tags on the homepage point to a Joomla-style query-string feed, `/?format=feed&type=rss` (Graphic
Online runs on Joomla, unlike every other Ghanaian/African WordPress-based outlet seen so far in
this file); Ghana News Agency has no discoverable feed and Citi Newsroom's homepage only exposes
oEmbed links, no real RSS alternate despite being WordPress-shaped. **Lebanon**: LBCI News worked
as given; **Naharnet** is Atom 1.0 (`<feed>`/`<entry>`/`<published>`, no image tags - relies on
the `og:image` fallback, same as Stuff/Roya News below), so `NaharnetRssProvider` extends
`BaseAtomRssProvider` rather than `BaseRssProvider` - its requested homepage has no bare RSS link,
but `rel="alternate"` tags expose per-topic Atom feeds under `/tags/{topic}/en/feed.atom`,
"lebanon" being the general one; The Daily Star Lebanon is blocked (403). **Oman**: only **Times
of Oman** worked - its requested `/rss` 404s, but a `rel="alternate"` tag points to the real
`/feed/`; the Oman News Agency's homepage has only `hreflang` alternates (its `/rss.aspx` serves
the plain Arabic homepage); Muscat Daily's homepage lists several real-looking
`/category/{section}/feed/` links, but following any of them (confirmed on "oman") redirects
straight back to plain homepage HTML despite an initially promising `application/rss+xml`
Content-Type on the redirect response itself - a new variant of the "disguised block" pattern,
where even the HTTP header on the way to the dead end looks like a real feed. **Jordan**: only
**Roya News** worked - also Atom 1.0 (same `BaseAtomRssProvider`, no image tags, `og:image`
fallback), Arabic-language despite the requested URL implying an English feed; Petra News
Agency's site is in a "Website Temporarily Unavailable" maintenance state (not a bot block); The
Jordan Times is blocked (403). **Kuwait**: zero providers wired in - KUNA's domain times out
outright at the network layer (not an HTTP-level block, the fourth+ such network-layer-dead
publisher documented in this file); Kuwait Times and Arab Times both redirect their guessed `/rss`
paths to a plain "Rss" title placeholder page, not real content. **North Korea**: zero providers
wired in - KCNA Watch's `/feed/` redirects straight into a paid-membership signup popup rather
than serving content (a distinct new failure shape: a feed URL that resolves but gates behind a
paywall rather than blocking or erroring); the DPRK's own `kcna.kp` domain is unreachable outright
at the network layer, consistent with the country's general internet inaccessibility. None of the
providers described as dead in this entry are wired into `NewsCrawler.appsettings.json`; Saudi
Arabia, Iraq, Chile, Ethiopia, Kuwait, and North Korea have no country block at all as a result -
every requested outlet in each came back dead.

**Government/parliament/international-org/fact-check sources from a user-supplied 58-row service
table - scoped deliberately to just the rows that fit this app's existing architecture** (the
user chose this scope explicitly over building brand-new Finance/Weather/Disaster/Social/Trends/
AI subsystems, which don't map onto `NewsArticle` at all and were left out of this pass
entirely). Of the table's 58 rows, 13 already existed in this codebase (every JSON news-API
provider already under `NewsApiCrawler`, Google News RSS, PIB, and YouTube's Atom feeds) before
this batch - see each provider's own file for details. **8 new providers added, split across the
two existing pipelines depending on shape:**

RSS/Atom-shaped (wired into `NewsCrawler:Countries`, same as every publisher elsewhere in this
file): **UN News** and **Snopes** aren't tied to any one nation, so a new **"International"**
pseudo-country was created to hold them - the same one-flag-disables-the-group benefit every real
country already gets, rather than forcing a global fact-checker under an arbitrary nation.
**PolitiFact**'s requested bare `/feed`/`/rss` both 404, but a `rel="alternate"` tag on the
homepage points to the real `/rss/factchecks/` - added to the existing United States block.
**GOV.UK**'s news feed is Atom 1.0, not RSS 2.0 (`GovUkNewsRssProvider` extends
`BaseAtomRssProvider`, same reasoning as Naharnet/Roya News/Stuff) - it's actually GOV.UK's own
site-search-as-a-feed endpoint (`gov.uk/search/news-and-communications.atom?keywords=`), so the
configured feed passes `keywords=election` deliberately, to avoid pulling every unrelated
government announcement (park opening hours, import tariffs, etc.) into an election-news app;
added to the existing United Kingdom block.

JSON-API-shaped (wired into `NewsApiCrawler:Providers` at the time this batch was added - since
restructured into `NewsApiCrawler:Countries`, see the entry below): **UK Parliament Bills API**
(`bills-api.parliament.uk`) is fully public with no API key at all (`AuthType: None`, same as
GDELT) - curl-verified live, and its own most-recently-updated bill at verification time was
literally titled "Representation of the People Bill," confirming real election-relevant content.
**FEC** (`api.open.fec.gov`) curl-verified live and returns real candidate-filing data even
against the public rate-limited `DEMO_KEY` - a real production key is free from
`api.data.gov/signup`. **Congress.gov API** (`api.congress.gov`) curl-verified live: an
unauthenticated request returns a documented `API_KEY_MISSING` JSON error (confirming the
endpoint is real, not dead) - a free key comes from `api.congress.gov/sign-up`. **Google Fact
Check Tools API** (`factchecktools.googleapis.com`) curl-verified live the same way - an
unauthenticated request returns a documented `PERMISSION_DENIED` error naming the exact
requirement - a free key comes from enabling "Fact Check Tools API" on a Google Cloud Console
project. All four map a status-update record (a bill's latest stage, a candidate filing, a
fact-check claim) onto `NormalizedArticle` rather than a written story - there's no natural
title/body split for these, so each provider's doc comment explains its own stand-in shape (bill
title + latest action; candidate name + office/party; claim text + fact-checker's rating). As
with every other `NewsApiCrawler` provider, none of these four fetch anything until the parent
`NewsApiCrawler:Enabled` flag is set to `true` **and** their own `NewsApiKeys:{ProviderName}` key
is configured (`NewsApiKeys:FEC`, `NewsApiKeys:CongressGov`, `NewsApiKeys:GoogleFactCheck` -
`UKParliamentBills` needs no key at all, same as GDELT).

**`NewsApiCrawler:Providers` restructured into `NewsApiCrawler:Countries`, mirroring the exact
`NewsCrawler:Countries` fix documented earlier in this file - done immediately after the batch
above, once it became clear more single-country institutional APIs (an ECI-equivalent, a Canada
elections API, etc.) would keep being added over time and would need the same "disable this
whole country's sources with one flag" lever RSS already has.** New
`NewsApiCountryOptions` (`Application/Options`) mirrors `CountryOptions` exactly - `Name` +
`Enabled` + `Providers: List<NewsApiProviderOptions>`.
`NewsApiCrawlerOrchestrator` gained the same `CountryProvider` record-struct flattening pattern
as `NewsCrawlerOrchestrator` (`_options.Countries.Where(c => c.Enabled).SelectMany(c =>
c.Providers.Select(p => new CountryProvider(c.Name, p))).Where(...)`), and now stamps `Country`
onto both `NormalizedArticle` (via a `with` expression before persisting) and `ErrorNotification`
on failure - previously the API pipeline didn't track country at all, so this is a new capability
for it, not just a config reshuffle: API-sourced articles and crawl-failure emails now show a
country the same way RSS-sourced ones always have. `HangfireRecurringJobRegistrar.
RegisterNewsApiRecurringJobs` flattens the same way (`RegisterNewsCrawlerRecurringJobs`'s RSS
counterpart was already doing this). All 18 existing `NewsApiCrawler` providers were regrouped by
what they're actually configured to fetch, not by where the underlying service is headquartered:
the 14 pre-existing global-aggregator providers (NewsAPI.org, GNews, TheNewsAPI, CurrentsAPI,
Mediastack, NewsDataIo, WorldNewsAPI, EventRegistry, NewscatcherAPI, APITube, Guardian, GDELT,
SerpApiGoogleNews, DataGovIn) were - every one of them, on inspection - already configured with
India-specific query parameters (`country=in`, `locale=in`, `"India politics"` keywords, etc.),
so they all moved under a new **India** country block; **FEC** and **CongressGov** (both
inherently USA-only institutions) moved under the existing **United States** block;
**UKParliamentBills** moved under **United Kingdom**; **GoogleFactCheck** (genuinely
country-agnostic) moved under the existing **International** block. No provider's own
configuration (base URL, auth, endpoints, query parameters) changed - this was purely a
regrouping. A new `NewsApiCrawlerOrchestratorTests.cs` was added (this orchestrator had zero test
coverage before this change) covering the same two cases `NewsCrawlerOrchestratorTests.cs`
already covers for RSS: successful persistence stamps the right `Country`, and a
country-disabled block skips its providers entirely without even acquiring a lock.

**Everything else the user chose to defer or that was independently verified dead in this same
pass:** **Election Commission of India (ECI)** - blocked (403), consistent with the earlier PIB
deep-dive's finding that ECI has no working RSS either. **Parliament of India/Sansad** - no RSS,
no discoverable API on either chamber's site; Rajya Sabha remains excluded on the standing
`robots.txt: Disallow: /` policy documented earlier in this file, and Lok Sabha (no blanket
disallow) still has nothing to fetch. **data.gov.in** - left exactly as already documented
(`DataGovInProvider` stays a wired-but-disabled stub; no specific election-relevant dataset
resource id was identified this pass either - the OGD catalog's own search UI is JS-rendered and
not scrapeable the way every RSS/API endpoint elsewhere in this file is). **White House RSS** -
still excluded per its existing entry above (a single stale 2024 test post, not real content).
**World Bank API, IMF API, OECD API, and the EU Open Data Portal** - all four curl-verified as
genuinely live and real, but deliberately not wired in: they're statistics/dataset-catalog APIs
(GDP figures, inflation rates, dataset search results), not news sources - there's no article to
extract, just numbers or catalog metadata, an architectural mismatch rather than a technical
failure. (The IMF's older `dataservices.imf.org` REST endpoint is additionally unreachable
outright at the network layer, and its newer `imf.org/external/datamapper` path returns a WAF
403 - both moot given the mismatch above.) **Bing News Search, WorldNewsAPI's Optional-tier
siblings, Webz.io, Common Crawl CC-News, ContextualWeb News API**, and every Finance/Weather/
Disaster/Social/Trends/AI row - out of scope for this pass per the user's explicit choice, not
evaluated.

**26 more India government/institutional sources from a user-supplied table - only 2 genuinely
new working feeds, the rest either already existed, were already documented dead, or are dead
for a reason verified fresh this pass.** **President of India** (presidentofindia.gov.in/rss.xml)
and **NITI Aayog** (niti.gov.in/rss.xml, a Drupal feed mislabeling its own Content-Type as
text/html - harmless, same as Kathmandu Post - `BaseRssProvider` never inspects Content-Type
before parsing) both curl-verified live with 10 recent, genuinely current items each. NITI Aayog
specifically **corrects** an earlier finding in this file ("NITI Aayog's own website... no
discoverable RSS") - a fresh check this pass turned up a real feed at the direct `/rss.xml` path;
worth remembering that a "dead" finding from an earlier pass isn't necessarily permanent, since
sites do add feeds over time.

**Two rows were already wired in from earlier passes** - MyGov India and NDMA - re-requested here
under slightly different table entries but not duplicated. **Five rows were already documented
dead in this file and reconfirmed unchanged**: PM India (single stale test post), Rajya Sabha
(`robots.txt: Disallow: /`), IMD (network-unreachable), and NIC (no discoverable feed - see its
fresh re-check below).

**Everything else in this batch is dead, each verified fresh this pass:** **PTI** - explicitly
marked "Licensed" (not free) in the user's own table, and independently reconfirmed as the same
Angular SPA HTML shell already documented for it elsewhere in this file. **DD News** and
**Akashvani/All India Radio** - both show a genuinely new failure shape: a `HEAD` request to
their real feed URL (`ddnews.gov.in/en/feed/`, `newsonair.gov.in/feed/`) succeeds with HTTP 200
and a correct `application/rss+xml` Content-Type, but the actual `GET` request that would fetch
the body consistently fails at the connection level (reproduced with multiple retries and a 30s
timeout) - the feed is declared and technically "exists" by every header-level signal, but is not
actually fetchable in practice. **Vice President of India** and **Supreme Court of India** - both
WordPress sites returning an explicit `wp_die` `"No feed available!"` error (HTTP 500) - a
deliberate feed-disable, the same category as Financial Express's stated policy elsewhere in this
file, not a technical failure. **Lok Sabha** (`loksabha.nic.in`) and **Gazette of India**
(`egazette.nic.in`) - both domains fail to resolve at all (confirmed via direct `curl`, not just
the verification script), a different, more definitive network-layer failure than Rajya Sabha's
policy-based exclusion on a working domain. **PRS Legislative Research** - no discoverable feed,
every guessed path 404s. **India Code** - its `/feed` path 302-redirects into a plain HTML page,
not real content. **eCourts** - blocked by a WAF bot-challenge (HTTP 405 "Security Page").
**RBI**, **SEBI** - both 404 on every guessed path, consistent with their existing exclusion
elsewhere in this file. **NSE** - 404. **BSE** - returns its own Angular SPA shell, not a feed,
on every guessed path. **MCA** - blocked outright (403) even on its plain homepage. **Central
Water Commission (CWC)** - technically valid, well-formed RSS 2.0 at `cwc.gov.in/rss.xml`, but
contains exactly one `<item>`, and that item is a static "Welcome to Central Water Commission"
page, not a news item - functionally the same "looks real, delivers nothing" trap as the
genuinely-empty-feed category elsewhere in this file (Saudi Gazette, INA), just with one
placeholder item instead of zero; not wired in. **PIB Fact Check** and **GeM** - both return
plain homepage/HTML shells on every guessed feed path, no real content. **NIC** - reconfirmed
dead: its homepage exposes only WordPress oEmbed alternates (no `rss+xml` link), and its `/feed/`
path redirects away from any feed content. None of the dead entries in this paragraph are wired
into `NewsCrawler.appsettings.json`.

**Seven more JSON-API providers from a user-supplied 25-row news/finance-API table - 12 of the 25
rows already existed (every general aggregator already under `NewsApiCrawler`, plus Guardian),
6 more verified dead/discontinued, 7 new ones added.** All seven follow the established
`NewsApiCrawler:Countries` structure: **WebzIo** (api.webz.io/newsApiLite, query param `token`)
joins the other general aggregators under **India** (curl-verified live: an unauthenticated
request returns a documented "Unknown API token" error, confirming the endpoint). **AP Content
API** (`APContentAPI` - api.ap.org, query param `apikey`), **NYTimesAPI** (api.nytimes.com/svc/
topstories, query param `api-key` - named `...Api`/`NYTimesAPI` specifically to disambiguate from
the already-existing `NyTimesRssProvider`/`"NYTimes"` plain RSS feed, a completely separate
official Developer API product), and **ProPublicaCongress** (api.propublica.org/congress/v1,
header `X-API-Key` - named `...Congress` to disambiguate from the already-existing
`ProPublicaRssProvider`/`"ProPublica"` RSS feed; same "a bill's latest action, not a written
story" shape as `UkParliamentBillsProvider`/`CongressGovProvider`) all joined **United States**.
**FinancialModelingPrep** (financialmodelingprep.com/stable/general-news), **AlphaVantage**
(alphavantage.co, `NEWS_SENTIMENT` function - curl-verified live with real, current articles even
against the public "demo" key, Alpha Vantage's own documented special case for the AAPL example
ticker), and **Finnhub** (finnhub.io/api/v1/news) all joined **International** - genuinely global
market-news services, not tied to one nation, the same reasoning Google Fact Check was placed
there. Finnhub and Financial Modeling Prep both return a bare JSON array at the response root
(no wrapping `articles`/`results` property, unlike every other provider here) - each provider's
`ParseArticles` checks `JsonValueKind.Array` directly rather than looking for a named key. AP
Content API's parser is built from AP's own documented response shape rather than a live
response, since there's no free tier to test against (same "best-effort, confirm once enabled"
caveat as `DataGovInProvider`). All seven need a real key via `NewsApiKeys:{ProviderName}` before
producing anything (`NewsApiKeys:WebzIo`, `:APContentAPI`, `:NYTimesAPI`, `:ProPublicaCongress`,
`:FinancialModelingPrep`, `:AlphaVantage`, `:Finnhub`), same as every other keyed provider.

**Six rows from that same table are dead or have no viable public API, each verified rather than
guessed-and-gave-up:** **Bing News Search API** - Microsoft's own product page for it 404s, with
a cached "no longer available" signal - consistent with Microsoft retiring standalone Bing Search
APIs for new Azure sign-ups in 2023 (existing subscriptions are grandfathered, but there's no
self-serve signup path left to build against). **ContextualWeb News API** - its RapidAPI endpoint
404s outright; the underlying service (ContextualWeb) shut down some years ago. **Reuters Connect**
- both its marketing page (`reutersagency.com/en/reutersconnect/`) and a guessed API base
(`api.reutersconnect.com`) return 404/403 with no self-serve developer path discoverable - the
fifth+ independent confirmation in this file that Reuters has nothing publicly accessible from
this environment, this time for its enterprise product specifically rather than plain RSS.
**Bloomberg API** - there is no public developer REST API at all behind this table entry; "Bloomberg
API" refers to their enterprise Terminal/B-PIPE data products, which require a sales contract, not
a self-serve key - the linked marketing page itself 403s. **USA TODAY API** -
`developer.usatoday.com` silently redirects to the plain usatoday.com homepage; there is no real
developer portal or API product behind this domain. **ABC News Resources API** -
`developer.abcnews.com` fails to resolve at the network layer entirely (unlike `abcnews.go.com`,
the real public site, which is up) - the API-specific subdomain doesn't exist. None of these six
are wired into `NewsCrawler.appsettings.json`.

**Startup took 60-70+ seconds before the app became reachable at all, traced to
`HangfireRecurringJobRegistrar` registering 260+ recurring jobs (RSS + JSON-API providers)
synchronously, one `IRecurringJobManager.AddOrUpdate` Mongo round trip at a time, before
`app.Run()` in `Web/Program.cs` was ever reached - fixed in two layers.** First,
`HangfireRecurringJobRegistrar`'s three registration loops (RSS, News API, dynamic-feed) now run
each provider's `AddOrUpdate` concurrently via `Parallel.ForEach` (`RegistrationConcurrency = 32`)
instead of one at a time, since each is an independent Mongo upsert keyed by its own jobId with no
shared state. A static constructor also calls `ThreadPool.SetMinThreads` once, up front:
`IRecurringJobManager.AddOrUpdate` is synchronous (returns `void`, not `Task` - Hangfire's
scheduling API predates async), so `Parallel.ForEach` dispatches these as blocking work onto the
CLR ThreadPool, which only grows slowly under its own throttled "hill-climbing" algorithm when
starved - without pre-warming, a burst of 200+ blocking calls mostly ran close to sequentially for
the first several seconds regardless of the requested concurrency, confirmed by direct
measurement (236 RSS jobs: 42s at 16-way before the ThreadPool fix, 24.6s after - concurrency
alone wasn't the bottleneck). Per-provider registration log lines dropped from `LogInformation` to
`LogDebug` (200+ lines per startup was pure noise for zero day-to-day diagnostic value); each
registrar now logs one clear summary instead (count + elapsed ms + concurrency).

Second, and the change with the actually decisive effect: **the entire registration block moved
off the startup-blocking path in `Web/Program.cs`**, wrapped in an un-awaited `Task.Run` (with its
own try/catch logging any failure, since an unobserved background-task exception would otherwise
vanish silently) instead of running inline between `builder.Build()` and `app.Run()`. Nothing
about `app.Run()`, the HTTP pipeline, Scalar/Swagger, the Hangfire dashboard, or health checks
actually depends on recurring-job metadata already existing in Mongo - the Hangfire Server's own
`RecurringJobScheduler` dispatcher (started via `AddHangfireServer` earlier in the same file)
polls for and picks up newly-registered jobs on its own schedule regardless of when registration
finishes, so there was never a real ordering requirement forcing this to block startup. Verified
end to end: time to "Application started" dropped from ~68-70s to ~11s, and Scalar/`/hangfire`
both returned HTTP 200 in under a second immediately once reachable, while the 236 RSS jobs
finished registering successfully in the background roughly 26s later with no errors. This also
matters for Render specifically: `render.yaml`'s free-tier deploy needs the container to pass a
health check quickly after starting, and 25+ seconds of synchronous registration blocking that was
a real risk, not just a local dev annoyance.

**A 27-row publisher table deepened United Kingdom coverage and added three new providers - the
first pass in this codebase's history built entirely without curl-verification, because this
session's own sandbox network policy blocks every external host outright (`api.twelvedata.com`,
`api.polygon.io`, `reddit.com`, `t.me`, and every other domain tried all returned a 403 from the
session's own egress proxy, not from the destination) - every claim below is best-effort from
each API's own published documentation, the same "confirm once enabled" caveat already carried by
`ApContentApiProvider`/`DataGovInProvider`, not the usual live-verified standard.** Thirteen
already-existing India-configured aggregators/APIs (NewsAPI.org, GNews, TheNewsAPI, CurrentsAPI,
Mediastack, NewsDataIo, WorldNewsAPI, EventRegistry, NewscatcherAPI, Guardian, GDELT,
SerpApiGoogleNews, WebzIo) each gained a second `NewsApiProviderOptions` block under the existing
**United Kingdom** country entry - same provider class/singleton instance, just a second
config block with UK-flavored query parameters (`country`/`countries`/`source-countries`: `gb`
per each API's own ISO-3166 convention, except GDELT's own `sourcecountry:UK` FIPS-style code and
Google/SerpApi's own `gl=uk` exception to ISO), proving out the `NewsApiCrawlerOrchestrator`'s
existing `providerOptions.Name`-keyed lookup (documented when `Countries` was introduced) actually
supports the same provider fetching for two different countries with two independent configs.
WorldNewsAPI's UK block stays `Enabled: false`, mirroring its already-disabled India entry.

Two new **Finance**-category JSON-API providers joined **International**: **PolygonIo**
(`api.polygon.io/v2/reference/news`, query param `apiKey`, free tier 5 req/min) - a genuine
article-shaped news endpoint (title/description/article_url/published_utc/publisher), unlike
Twelve Data below. **Twelve Data** (also requested) was **not** wired in: its public API surface
is time-series/quote/fundamentals market data with no news-article endpoint at all - the same
architectural mismatch already documented for World Bank/IMF/OECD (numbers, not stories), not a
technical failure.

One new **Social**-category provider, **YouTubeDataApi** (`www.googleapis.com/youtube/v3/search`,
query param `key`), joined **International** - a genuinely different capability from the existing
`YouTubeRssProvider`: that one reads one already-known channel's own keyless Atom feed, while this
one keyword-searches across all of YouTube (`type=video`), so it lives in the JSON-API pipeline
instead of the RSS one, tagging results `"video"` the same way `YouTubeRssProvider` does so both
produce the same shape.

**Reddit** joined **United Kingdom** (`r/unitedkingdom`/`r/ukpolitics`) as the one provider in
this pipeline needing genuine two-legged OAuth2 (`client_credentials` grant against
`www.reddit.com/api/v1/access_token`, HTTP Basic auth of a client id/secret, yielding a
short-lived bearer token for `oauth.reddit.com`) - Reddit killed its unauthenticated `.json`
endpoints in 2026, so the old no-key workaround other providers like GDELT rely on
(`AuthType.None`) no longer exists for Reddit. This doesn't fit
`NewsApiProviderOptions.AuthType`'s single-static-key model at all, so `RedditProvider` implements
`INewsApiProvider` directly (same reasoning `EventRegistryProvider` bypasses
`BaseNewsApiProvider` for a POST body) and manages its own in-memory token cache with expiry.
Because two credentials are needed instead of one, `NewsApiKeys:Reddit` deliberately holds
`"{clientId}:{clientSecret}"` (split on the first colon) rather than a plain key - a Reddit app is
created free at reddit.com/prefs/apps.

**Telegram Bot API was requested but not implemented - a genuine authorization mismatch, not a
missing feature.** The Bot API's `getUpdates` only ever receives messages from chats/channels the
bot has already been added to as a member/admin; there is no method for a bot to passively read an
arbitrary public channel's (PMO India's, the Election Commission's, ...) content without that
channel's own admins first adding the bot - which can't be done unilaterally for channels this
codebase doesn't own. (A separate, unauthenticated technique - scraping the public
`t.me/s/{channel}` HTML preview - exists and sidesteps this, but that is fetching/parsing HTML,
not "the Telegram Bot API," and is a materially different, more fragile mechanism this batch does
not implement without being asked for it specifically.)

**Two new `NewsApiCrawler:Countries` entries - Canada and Australia - from a user-supplied
39-row publisher table, config-only (no new provider classes).** Both new country blocks reuse
the same 12 already-existing general-aggregator provider classes (NewsAPI.org, GNews, TheNewsAPI,
CurrentsAPI, Mediastack, NewsDataIo, WorldNewsAPI, EventRegistry, NewscatcherAPI, GDELT,
SerpApiGoogleNews, WebzIo) with country-specific query parameters (`country`/`countries`/
`source-countries`: `ca`/`CA` for Canada, `au`/`AU` for Australia - both plain ISO-3166 alpha-2,
no quirk exception like the UK's own `gl=uk`/GDELT `sourcecountry:UK` needed here), same
mechanical pattern as the United Kingdom expansion above - proof this per-provider-per-country
config shape scales to a third and fourth country with zero code changes, only
`NewsCrawler.appsettings.json`. WorldNewsAPI stays `Enabled: false` in both new blocks, matching
its already-disabled India/UK entries. Bing News Search API (both countries), Reuters Connect
(both), and Bloomberg API (both) were in the source table again but not re-added - already
verified dead/enterprise-only earlier in this file, and nothing about asking for them per-country
changes that. AP Content API and the three global Finance providers
(FinancialModelingPrep/AlphaVantage/Finnhub) were also in the table for both countries but
deliberately not duplicated per-country - AP is one global newswire endpoint with no country-filter
knob in its existing config, and the Finance APIs are already under **International** precisely
because they're not tied to one nation, same reasoning already documented when GoogleFactCheck
was placed there.

**The Globe and Mail API (Canada) was requested but has no public self-serve developer API** -
their site runs on Arc XP (the same CMS platform already behind IndianExpress/The Irish Times/La
Nacion's RSS feeds elsewhere in this file) but there is no discoverable Globe and Mail developer
portal or content-syndication API a small project can sign up for; the user's own table already
flagged this row `Free Tier: No` / `Paid: Enterprise`, consistent with what turned up - the same
"enterprise-only, no self-serve path" category as Bloomberg API, not a technical failure. Not
wired in.

**Eight more `NewsApiCrawler:Countries` entries - Germany, France, Japan, South Korea, Singapore,
China, Indonesia, Thailand - from a 74-row user-supplied table, config-only again**: each new
block carries the 7 provider rows the table actually asked for this time (a smaller subset than
prior batches - no Mediastack/Currents/WorldNewsAPI/SerpApi/WebzIo here): NewsAPI.org, GNews,
NewsData.io, TheNewsAPI, NewscatcherAPI, EventRegistry, GDELT, all reusing the same already-registered
provider singletons with country-specific query parameters. **This batch caught and fixed a real
bug from the Canada/Australia batch just above**: GDELT's `sourcecountry` filter uses **FIPS 10-4**
codes, not ISO-3166 - they're identical for most countries (Canada's FIPS code is coincidentally
`CA`, same as ISO), which is why the mistake wasn't obvious earlier, but Australia's FIPS code is
`AS`, not the ISO `AU` that was configured - silently querying the wrong/nonexistent country code
rather than erroring, so it would have just returned fewer or zero matching articles forever
without ever surfacing as a failure. Corrected to `sourcecountry:AS`. The eight new countries'
own GDELT queries use their correct FIPS codes throughout: Germany `GM`, Japan `JA`, South Korea
`KS`, Singapore `SN`, China `CH` (all genuinely different from their ISO codes `DE`/`JP`/`KR`/
`SG`/`CN`), while France/Indonesia/Thailand's FIPS codes happen to match ISO (`FR`/`ID`/`TH`).
Every other provider in this batch (NewsAPI.org/GNews/NewsData.io/TheNewsAPI/NewscatcherAPI) keeps
using plain ISO-3166 alpha-2 for its own `country`/`countries`/`locale` parameter, since none of
those APIs use FIPS - only GDELT does.

Reuters Connect and Bloomberg API (requested again for every one of these eight countries) stay
excluded, already documented dead/enterprise-only. AP Content API (Germany) was not duplicated,
same reasoning as every prior batch (one global newswire, no per-country config knob, already
under United States).

**Four country-specific newswire rows were requested and investigated, none wired in - for four
different reasons, not just "blocked" for all of them.** **AFP API / AFP NewsML** (France) is a
real, live public API - confirmed via its own OAuth2 token endpoint
(`afp-apicore-prod.afp.com/oauth/token?grant_type=anonymous` for a no-credential test mode, or a
username/password grant for permanent access) and a documented `v1/api/latest` article-listing
endpoint - genuinely closer to Reddit's shape (real OAuth2 token exchange) than to
Bloomberg/Reuters's "no public product at all." It was still **not implemented**: this session's
network policy blocks every external host, so unlike Polygon.io/YouTube Data API (whose JSON
response *field names* are clearly documented and could be built with reasonable confidence), no
source could confirm AFP's actual response schema (their full API reference lives behind
`v1/docs`, itself unreachable) - implementing blind here risks a provider that authenticates
successfully and returns HTTP 200 forever while silently parsing zero articles because every
guessed field name is wrong, which is worse than not shipping it. Revisit once a real account's
live response is available to build the parser from. **Kyodo News API** (Japan) - `corp.kyodo-d.jp`
is Kyodo News Digital's corporate/B2B site; no discoverable public self-serve developer portal,
appears to be enterprise content distribution only (same category as Bloomberg). **Yonhap News
API** (South Korea) - no distinct public developer API found beyond the Yonhap English RSS feed
already wired in elsewhere in this file; the table's "API" row most likely refers to the same
newswire product this codebase already ingests via RSS, not a separate JSON product. **Xinhua
API** (China) - no official self-serve developer portal found; consistent with Xinhua's own RSS
feed being independently documented elsewhere in this file as frozen since 2017-2018.

**Sixteen more `NewsApiCrawler:Countries` entries - Czech Republic, Romania, Hungary, Greece,
Portugal, Malaysia, Vietnam, Philippines, Pakistan, Bangladesh, Nepal, Sri Lanka, Nigeria, Kenya,
Egypt, Taiwan - from a 91-row user-supplied table, config-only, an even smaller 5-provider subset
than the Germany/France/... batch just above:** NewsAPI.org, GNews, NewsData.io, EventRegistry,
GDELT (no TheNewsAPI/NewscatcherAPI this time). Continuing the FIPS-vs-ISO
discipline the Australia bug (above) forced into practice, each country's GDELT `sourcecountry`
value was looked up individually against an authoritative FIPS-10-4-to-ISO mapping (the
`mysociety/gaze` reference table) rather than assumed equal to its ISO code - seven of these
sixteen genuinely differ: **Czech Republic** `EZ` (not `CZ`), **Portugal** `PO` (not `PT`),
**Vietnam** `VM` (not `VN`, a holdover from the pre-1976 "Democratic Republic of Vietnam" naming),
**Philippines** `RP` (not `PH`, "Republic of the Philippines"), **Bangladesh** `BG` (not `BD` -
coincidentally the same two letters as Bulgaria's unrelated *ISO* code, but FIPS and ISO are
separate namespaces so this isn't a collision in practice), **Sri Lanka** `CE` (not `LK`, a
holdover from "Ceylon"), and **Nigeria** `NI` (not `NG`). The other nine (Romania, Hungary,
Greece, Malaysia, Pakistan, Nepal, Kenya, Egypt, Taiwan) happen to share the same two letters in
both schemes. Every other provider here keeps using plain ISO-3166 for its own country parameter,
same as always - only GDELT needs FIPS.

**Ten country-specific national-newswire "API" rows were requested (Lusa/Portugal,
BERNAMA/Malaysia, VNA/Vietnam, PNA/Philippines, APP/Pakistan, BSS/Bangladesh, Rastriya Samachar
Samiti/Nepal, NAN/Nigeria, KNA/Kenya, MENA/Egypt) plus an eleventh implied by the table
(CNA/Taiwan) - none wired in, all individually checked rather than assumed dead from the pattern
alone.** Every one of the user's own table rows already pre-labeled these `Free Tier: No` /
`Paid: Enterprise`, and individual searches for each confirmed no discoverable public self-serve
developer portal or API documentation - each is a national/state wire agency selling subscription
access directly (phone/email contact, not a signup page), the same category as Bloomberg/Kyodo/
Xinhua above. One partial exception worth recording precisely: Taiwan's **CNA** does have a
`developer.cna.com` portal, but its own contact page reads "Thank you for your interest in CNA's
API..." - a request-a-quote form, not self-serve signup - and separately, CNA's own website
happens to call undocumented endpoints (`cna.com.tw/cna2018api/api/WTopic`,
`.../WNewsList`) to render itself; those aren't a public product either (no confirmed auth
scheme, no stated terms for third-party use, most likely intended only for CNA's own frontend) -
using them would be scraping an internal endpoint, not integrating "the CNA API," so neither path
was wired in. (Taiwan is still covered via the existing English-language `FocusTaiwan`/CNA RSS
feed documented earlier in this file - this only affects the JSON-API pipeline.)

**A full RSS-vs-JSON-API coverage audit, requested directly rather than from a new publisher
table: diff `NewsCrawler:Countries` against `NewsApiCrawler:Countries` by name.** 35 of the 65 RSS
countries had **zero** JSON-API coverage at all - every country added for RSS-only reasons
throughout this file's history (Qatar, Israel, Mexico, Turkey, Ukraine, Russia, South Africa,
Brazil, Italy, Spain, Netherlands, Sweden, Norway, Finland, Belgium, Switzerland, Austria,
Ireland, Denmark, New Zealand, Poland, Iran, UAE, Hong Kong, Argentina, Colombia, Venezuela,
Myanmar, Peru, Morocco, Algeria, Ghana, Lebanon, Oman, Jordan) had never once been carried over to
the JSON-API side. Each now gets the same 5-provider base set the smallest recent batches settled
on (NewsAPI.org, GNews, NewsData.io, EventRegistry, GDELT) - config only, reusing existing
provider classes. **United States was a second, different kind of gap**: its
`NewsApiCrawler:Countries` entry existed but held *only* the US-specific institutional providers
(FEC/CongressGov/APContentAPI/NYTimesAPI/ProPublicaCongress) added in an earlier government-APIs
pass - it had never received the general aggregator set every other country (starting with India)
carries, an inconsistency invisible to a simple by-name diff since the country entry technically
already existed. Same 5-provider base prepended to its existing providers.

**This audit also caught and fixed a real, already-shipped bug spanning two categories.** First,
re-confirming GDELT's `sourcecountry` needs FIPS-10-4 codes (not ISO) turned up far more
divergences among these 35+1 countries than any prior batch - 15 of 36 differ from ISO, several
non-obviously: **Israel** `IS` (not `IL`), **Turkey** `TU` (not `TR`), **Ukraine** `UP` (not `UA`),
**Russia** `RS` (not `RU` - and unrelated to Serbia's *ISO* code `RS`, separate namespaces),
**South Africa** `SF` (not `ZA`), **Spain** `SP` (not `ES`), **Sweden** `SW` (not `SE`),
**Switzerland** `SZ` (not `CH`), **Austria** `AU` (not `AT` - and unrelated to Australia's *ISO*
code `AU`), **Ireland** `EI` (not `IE`), **Denmark** `DA` (not `DK`), **Myanmar** `BM` (not `MM`,
the FIPS scheme still reflects the pre-1989 "Burma" naming), **Morocco** `MO` (not `MA`),
**Algeria** `AG` (not `DZ`), **Oman** `MU` (not `OM`, reflecting the pre-1970 "Muscat and Oman"
naming) - each verified individually against the same `mysociety/gaze`
`fips-10-4-to-iso-country-codes.csv` reference table used to catch the original Australia mistake,
not assumed. Second, and unrelated to GDELT: **NewsAPI.org's `top-headlines` endpoint only accepts
`country` values from its own documented ~54-code allowlist** (confirmed directly from
`newsapi.org/docs/endpoints/top-headlines`) - a fact never checked in any prior batch. Cross-checking
every country added so far turned up **six already-merged countries whose `TopHeadlines` endpoint
was silently misconfigured** with an unsupported code that would 400 on every single cron run:
Vietnam (`vn`), Pakistan (`pk`), Bangladesh (`bd`), Nepal (`np`), Sri Lanka (`lk`), Kenya (`ke`) -
all six now have that one endpoint set `Enabled: false` (their `Everything` endpoint, a plain
keyword search with no country allowlist, is unaffected and stays on). The same allowlist check is
now applied going forward: of the 35+1 countries in this batch, 12 aren't on NewsAPI.org's list
(Qatar, Spain, Finland, Denmark, Iran, Myanmar, Peru, Algeria, Ghana, Lebanon, Oman, Jordan) and
have `TopHeadlines` pre-disabled from the start rather than wired in broken.

**A third crawler pipeline - "Social" - added alongside RSS and JSON-API, for channel lists that
come from MongoDB rather than either `NewsCrawler.appsettings.json` or code.** `SocialMediaSource`
(collection `SocialMediaSources`) is the Social pipeline's counterpart to the existing
Mongo-driven `FeedSource`/`DynamicFeedIngestionService` pattern - one document per channel
(`Platform`, `SourceType` [Politician/Party/Government/News], `Country`/`State`, `Name`,
`Identifier` [platform-specific: a YouTube channel id today], `Handle`, `Url`, `Enabled`,
`Priority`, `PollIntervalMinutes`, `TimeoutSeconds`, `Language`, `Category`, `LastPolledAt`) - but
spans multiple platforms (`SocialPlatform`: YouTube, Rss, Website, Facebook, Telegram) instead of
being RSS-only like `FeedSource`. Only YouTube has a working fetcher
(`Infrastructure/Social/YouTubeChannelFetcher.cs`) - the other four enum values are recognized but
unimplemented, the same "not wired up yet, not an error" state Telegram already has elsewhere in
this file; a `SocialMediaSource` whose platform has no matching `ISocialPlatformFetcher` is simply
logged and skipped.

`SocialMediaIngestionService` (in `Application/Services`, unlike `DynamicFeedIngestionService`
which lives in Infrastructure) dispatches to whichever `ISocialPlatformFetcher` matches a source's
`Platform`, then reuses `ArticlePersister` for dedup/persistence - the same helper
`NewsCrawlerOrchestrator`/`NewsApiCrawlerOrchestrator` already share, rather than a third
hand-rolled upsert loop. It lives in Application specifically because it only ever talks to
abstractions (the actual HTTP/XML work is behind `ISocialPlatformFetcher`, implemented in
Infrastructure), which is what makes reusing the `internal` `ArticlePersister` possible.

Scheduling mirrors `FeedSource`'s exact shape: one Hangfire recurring job per enabled
`SocialMediaSource` document (job id keyed by the source's own Mongo `Id`, since - unlike
`FeedSource` - it has no separate short `SourceCode` field), cron derived from
`PollIntervalMinutes` via the same `BuildCronForInterval` helper, registered by
`HangfireRecurringJobRegistrar.SeedAndRegisterSocialMediaRecurringJobsAsync` (seeds first, then
registers, then sweeps stale jobs - same three-step shape as the dynamic-feed registrar). Tagged
its own `[Queue("social")]` on `HangfireSocialMediaJobExecutor` rather than sharing "rss" - a
genuinely different, multi-platform pipeline that may need to scale independently later, same
reasoning "api" got its own queue; `Hangfire:Queues` default extended to
`["rss", "api", "social", "default"]` so a single instance still processes everything out of the
box.

**`YouTubeChannelFetcher` deliberately doesn't reuse the existing file-configured
`YouTubeRssProvider`'s per-entry parser** - that one is tightly typed to `RssFeedOptions` (a
config-file feed entry), while this one reads the same fields off a `SocialMediaSource` document
instead. Both parse the identical Atom shape (`entry`/`published`/`id`/`link`/`media:group`) for
the same underlying reason (`youtube.com/feeds/videos.xml` isn't RSS 2.0), so the parsing logic is
intentionally near-identical between the two rather than forced into one shared method that would
need an awkward abstraction over two different config shapes. It does reuse
`YouTubeRssProvider.ClientName`'s already-registered named `HttpClient`, though - no reason to
register a second one for the exact same target domain.

**Seeded with two channels to prove the pipeline end-to-end**: Narendra Modi
(`UC1NF71EwP41VdjAU1iXdLkw`, `SourceType: Politician`) and BJP (`UCrwE8kVqtIUVUzKui2WVpuQ`,
`SourceType: Party`), both `Country: India`, via `SocialMediaSourceSeeder` (idempotent by
`Platform`+`Identifier`, same "bootstrap the first documents only" reasoning as
`FeedSourceSeeder`). Modi's channel id was already verified elsewhere in this file (it's the same
one wired into the file-configured YouTube provider's India feed list) - deliberately seeded here
too anyway, to prove out this new pipeline independently, which does mean it's now polled by both
pipelines in parallel; harmless (ordinary Url-based dedup skips whichever copy lands second) but
wasteful, worth collapsing to one pipeline once this one's proven out. BJP's channel id could only
be corroborated via web search (its own search result explicitly labeled "Bharatiya Janata Party -
YouTube") rather than a live feed fetch/title check, since this environment's network policy
blocks youtube.com outright - the first entry in this Mongo-driven pipeline that couldn't be
curl/fetch-verified the way almost everything else in this file was; worth re-confirming once
network access to youtube.com is available.
