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
