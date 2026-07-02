# Political News Crawler

A scalable RSS news aggregation service for an India political news platform. Phase 1 crawls
Aaj Tak's public RSS feeds on a schedule, deduplicates against MongoDB, and exposes the result
through a read/query API. The architecture is deliberately built so a Phase 2 provider (ANI,
NDTV, PIB, The Hindu, Indian Express, PTI, ...) is a config block plus one new class - nothing
else changes.

## Architecture

Clean Architecture, dependencies point inward only:

```
Rudraa.InElection.RSSFeed.slnx

src/
 ├── Domain              entities/enums only, zero dependencies
 ├── Application         CQRS (Mediator + FluentValidation), abstractions, options
 ├── Infrastructure      MongoDB, RSS providers, repositories
 ├── Web                 ASP.NET Core Minimal API (IEndpointGroup -> Mediator)
 ├── Worker              BackgroundService console host running the cron scheduler
 ├── ServiceDefaults     Aspire shared defaults (OpenTelemetry, health, resilience)
 └── AppHost             Aspire AppHost: orchestrates Web + Worker + Mongo locally

tests/
 └── PoliticalNews.Tests             xUnit + Moq
```

**Two composition roots share one crawl engine.** `Worker` has no HTTP surface -
it's a `BackgroundService` that wakes up on the configured cron schedule. `Web`
exposes read endpoints plus a manual trigger. Both call the same
`INewsCrawlerService.RunCrawlAsync` (`NewsCrawlerOrchestrator`), which acquires a MongoDB-backed
distributed lock before running - a scheduled tick and a manually triggered API call, or two
Worker instances, can never crawl concurrently.

**Application layer is CQRS**, using the source-generator-based `Mediator` library (not MediatR,
to avoid its commercial licensing) plus FluentValidation. Every query/command lives in its own
file (`Application/News/Queries/*`, `Application/Crawl/Queries|Commands/*`) containing the
request record, its handler, and a sibling validator. A pipeline runs Logging ->
UnhandledException -> Validation -> Performance around every request.

**Web is Minimal API, not MVC controllers.** `Web/Endpoints/{Feature}.cs` (`News.cs`, `Crawl.cs`)
are static classes implementing `IEndpointGroup` with a `public static void Map(RouteGroupBuilder)`
and `public static` handler methods that only build a request and call `ISender.Send(...)` -
no repository or business logic lives in the Web project. `WebApplicationExtensions.MapEndpoints`
reflection-discovers every `IEndpointGroup` at startup, so adding a feature's endpoints is one new
class, nothing to wire up in `Program.cs`. A `RequestLoggingMiddleware` logs every request/response
first in the pipeline, and a single `ProblemDetailsExceptionHandler` turns every unhandled
exception - including FluentValidation failures and Minimal API's own parameter-binding failures -
into an RFC7807 ProblemDetails response.

**RSS providers are pluggable.** `IRssProvider` is implemented once, generically, by
`BaseRssProvider` (HTTP fetch, RSS 2.0 parsing, image extraction, normalization). A concrete
provider like `AajTakRssProvider` supplies only a name and an `HttpClient`. See
[Adding a new RSS provider](#adding-a-new-rss-provider).

**Duplicate detection** checks `Url` -> `OriginalGuid` -> `Hash` (SHA-256 of normalized title +
published date) in that order. A match with changed content (title/summary/content/image)
updates the existing document; anything else is a no-op duplicate skip. Articles are never
duplicated.

## Data model (MongoDB)

- **NewsArticles** - `Url` (unique index), `OriginalGuid`, `Hash`, `PublishedAt` (desc),
  `Provider`, `Category` indexes. Fields: Provider, FeedName, Category, Title, Summary, Content,
  Url, OriginalGuid, Author, Language, ImageUrl, PublishedAt, CrawledAt, UpdatedAt, Tags, Source,
  Hash, Metadata, IsActive.
- **CrawlHistory** - one document per crawl run: start/end time, duration, feed count, new/
  updated/duplicate counts, failed feeds, status (`Running`/`Completed`/`CompletedWithErrors`/
  `Failed`/`Skipped`), error.
- **CrawlLock** - single-document distributed mutex with a TTL index, so a crashed instance's
  lock self-expires instead of blocking the crawler forever.

## Configuration

Nothing is hardcoded. Two sections in `appsettings.json` (mirrored in both `Web` and `Worker`,
since the Web project's manual-trigger endpoint runs the same crawl):

```jsonc
"MongoDb": {
  "ConnectionString": "mongodb://localhost:27017",
  "DatabaseName": "PoliticalNewsDb",
  "NewsArticlesCollection": "NewsArticles",
  "CrawlHistoryCollection": "CrawlHistory",
  "CrawlLockCollection": "CrawlLock"
},
"NewsCrawler": {
  "Enabled": true,
  "Cron": "*/5 * * * *",
  "BatchSize": 100,
  "LockName": "news-crawler",
  "LockTtl": "00:15:00",
  "Providers": [
    { "Name": "AajTak", "Enabled": true, "Feeds": [ { "Name": "Home", "Url": "...", "Category": "General", "Language": "hi", "Enabled": true } ] }
  ]
}
```

Real credentials never belong in `appsettings.json` - use user-secrets locally, environment
variables/secret manager in deployment:

```bash
dotnet user-secrets set "MongoDb:ConnectionString" "mongodb+srv://..." --project src/Worker
dotnet user-secrets set "MongoDb:DatabaseName" "YourDbName" --project src/Worker
# repeat for src/Web if you run it outside the Aspire AppHost
```

> MongoDB database names cannot contain `.` (or `/ \ " $ * < > : | ?` / spaces). A name like
> `Foo.Bar` will be rejected by the server - use `FooBar` or `Foo_Bar` instead.

When launched through the Aspire AppHost, `ConnectionStrings:mongodb` (injected automatically)
takes priority over `MongoDb:ConnectionString`, so the same code runs unchanged either way.

### A note on the Aaj Tak feed list

Only one Aaj Tak (`aajtak.in`) RSS feed is publicly documented/discoverable:
`https://www.aajtak.in/rssfeeds/?id=home`. No category-slug or numeric-id variant resolves. The
remaining feeds under the `AajTak` provider block are `tak.live` feeds - India Today Group's
sister "Tak" video network (`news-tak`, `crime-tak`, `bharat-tak`, `biz-tak`, `up-tak`,
`bihar-tak`, `life-tak`, `sahitya-tak`, `fit-tak`, `mp-tak`, `astro-tak`, `dilli-tak`,
`rajasthan-tak`, `gujarat-tak`, `punjab-tak`, `haryana-tak`, `uttarakhand-tak`, `karnataka-tak`),
each individually verified to return valid RSS 2.0 XML. Three tak.live slugs that 404
(`sports-tak`, `mumbai-tak`, `short-videos`) are intentionally excluded.

## Running locally

**Option A - Aspire AppHost (recommended for local dev):** spins up a local Mongo container plus
Web and Worker, with the Aspire dashboard for logs/traces/metrics. Requires Docker running.

```bash
dotnet run --project src/AppHost
```

To point the AppHost at an existing cluster (e.g. Atlas) instead of a local container, set
`UseLocalMongo` to `false` and provide a `ConnectionStrings:mongodb` value via user-secrets on
`AppHost`.

**Option B - run projects directly:**

```bash
dotnet run --project src/Worker   # scheduled crawler, no HTTP
dotnet run --project src/Web      # read/query API + Swagger UI in Development
```

**Option C - Docker Compose** (Mongo container + both services, no Aspire/Docker-in-loop needed):

```bash
docker compose up --build
```

## MongoDB setup

Any reachable MongoDB 6+ instance works - a local container (`docker compose up mongo` or the
Aspire AppHost's local option), or a managed cluster such as Atlas. No manual schema setup is
required: `MongoIndexInitializerHostedService` (registered once, in `Infrastructure`, so both
Web and Worker get it automatically) creates every index on startup, and MongoDB implicitly
creates the corresponding collections on first write - the very first run against a brand-new
database is enough. Web's health check additionally pings the cluster at `/health`.

## Scheduler

`Worker/HostedServices/CrawlerBackgroundService` parses `NewsCrawler:Cron` (Cronos,
standard 5-field cron) and sleeps until the next occurrence, then calls
`INewsCrawlerService.RunCrawlAsync`. It supports `CancellationToken`-driven graceful shutdown,
logs every tick, and a run that throws is logged and simply waits for the next tick rather than
crashing the host. Overlap prevention is handled inside the orchestrator via the distributed
Mongo lock, not by the scheduler itself - so a scheduled tick and a manual API trigger can't run
concurrently either.

## API

Enable/disable Swagger, default/max page size via the `Api` config section. Every route below is
a Minimal API endpoint (`Web/Endpoints/News.cs`, `Web/Endpoints/Crawl.cs`), not a controller
action:

| Method | Route | Description |
|---|---|---|
| GET | `/api/news/latest?count=` | Latest articles across every provider |
| GET | `/api/news/provider/{provider}?count=` | Latest articles from one provider |
| GET | `/api/news/category/{category}?count=` | Latest articles in one category |
| GET | `/api/news/search?q=&count=` | Title/summary search |
| GET | `/api/crawl/history?count=` | Recent crawl runs |
| POST | `/api/crawl/trigger` | Run a crawl now (409 if one is already in progress) |
| GET | `/health`, `/alive` | Health checks (Mongo connectivity + liveness) |

Swagger UI is available at `/swagger` in Development.

## Adding a new RSS provider

1. Add a class in `Infrastructure/RssProviders` deriving from `BaseRssProvider`, supplying `Name`
   and an `HttpClientName`.
2. Register it in `InfrastructureServiceCollectionExtensions.AddInfrastructure`:
   `services.AddHttpClient(YourProvider.ClientName, ...)` and
   `services.AddSingleton<IRssProvider, YourProvider>()`.
3. Add a new block under `NewsCrawler:Providers` in `appsettings.json` with that provider's
   `Name` and feed URLs.

No other code changes - the orchestrator, repositories, and API automatically pick up the new
provider's articles.

## Deployment

Build the container images (one Dockerfile, parameterized by project):

```bash
docker build --build-arg PROJECT=Worker -t politicalnews-worker .
docker build --build-arg PROJECT=Web       -t politicalnews-web    .
```

Run each with `MongoDb__ConnectionString` / `MongoDb__DatabaseName` environment variables
pointing at your MongoDB instance (see `docker-compose.yml` for a working example against a
local Mongo container).

## Future expansion

The architecture already accommodates, without structural changes: additional RSS providers
(config + one class), AI-generated summaries/tagging/sentiment/entity-extraction (extra fields
already exist on `NewsArticle.Metadata`), breaking-news detection, push notifications,
Elasticsearch/OpenSearch, Redis caching, a recommendation engine, cross-provider deduplication,
and multilingual content (`Language` is already tracked per article/feed).
