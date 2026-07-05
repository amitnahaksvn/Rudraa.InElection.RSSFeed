using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Application.Abstractions;
using Application.Models;
using Application.Options;
using Domain.Enums;

namespace Infrastructure.NewsApiProviders;

/// <summary>
/// Reddit's official API (oauth.reddit.com) - a genuinely different auth shape from every other
/// provider in this pipeline, so it implements <see cref="INewsApiProvider"/> directly instead of
/// extending <see cref="BaseNewsApiProvider"/>, same reasoning <see cref="EventRegistryProvider"/>
/// bypasses it for a POST body. Reddit killed its unauthenticated <c>.json</c> endpoints in 2026 -
/// every request now needs an app-only OAuth2 token via the two-legged
/// <c>client_credentials</c> grant (HTTP Basic auth of client id/secret against
/// <c>www.reddit.com/api/v1/access_token</c>, yielding a short-lived bearer token for
/// <c>oauth.reddit.com</c>), which <see cref="NewsApiProviderOptions.AuthType"/>'s single-static-key
/// model has no room for - so unlike every other provider, <c>NewsApiKeys:Reddit</c> holds
/// <c>"{clientId}:{clientSecret}"</c> (split on the first colon below) rather than one plain key,
/// and the resulting bearer token is cached in-memory with its own expiry rather than looked up
/// per request. Reddit's API terms require a descriptive User-Agent identifying the app - reused
/// as a constant here rather than the shared crawler User-Agent every other HttpClient in this
/// pipeline sends. A free/"non-commercial" Reddit app (client id + secret) is created at
/// reddit.com/prefs/apps. This environment's egress policy blocks reddit.com outright, so - same
/// "best-effort, confirm once enabled" caveat as <see cref="ApContentApiProvider"/> - the listing
/// JSON shape below (Reddit's own documented <c>Listing</c>/<c>t3</c> structure) should be
/// re-checked against a live response before relying on this in production.
/// </summary>
public sealed class RedditProvider : INewsApiProvider
{
    public const string ProviderName = "Reddit";
    private const string TokenUrl = "https://www.reddit.com/api/v1/access_token";
    private const string UserAgent = "web:rudraa-inelection-rssfeed:v1.0 (by /u/rudraa-inelection-rssfeed)";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RedditProvider> _logger;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private string? _cachedAccessToken;
    private DateTimeOffset _cachedTokenExpiresAt = DateTimeOffset.MinValue;

    public RedditProvider(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<RedditProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public string Name => ProviderName;

    public async Task<IReadOnlyList<ApiFetchResult>> FetchAllEndpointsAsync(NewsApiProviderOptions options, CancellationToken cancellationToken)
    {
        var enabledEndpoints = options.Endpoints.Where(e => e.Enabled).ToList();
        var results = new List<ApiFetchResult>(enabledEndpoints.Count);

        foreach (var endpoint in enabledEndpoints)
        {
            results.Add(await FetchEndpointAsync(options, endpoint, cancellationToken));
        }

        return results;
    }

    private async Task<ApiFetchResult> FetchEndpointAsync(NewsApiProviderOptions options, NewsApiEndpointOptions endpoint, CancellationToken cancellationToken)
    {
        var fetchedAt = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        int? httpStatusCode = null;
        var path = endpoint.Endpoint.TrimStart('/');
        var query = string.Join('&', endpoint.QueryParameters.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
        var url = query.Length == 0 ? $"{options.BaseUrl.TrimEnd('/')}/{path}" : $"{options.BaseUrl.TrimEnd('/')}/{path}?{query}";

        var credentials = _configuration[$"NewsApiKeys:{options.Name}"];
        var separatorIndex = credentials?.IndexOf(':') ?? -1;
        if (string.IsNullOrWhiteSpace(credentials) || separatorIndex <= 0)
        {
            _logger.LogWarning(
                "No 'clientId:clientSecret' credential configured for news API provider '{Provider}' (NewsApiKeys:{Provider}) - skipping endpoint {Endpoint}",
                options.Name, options.Name, endpoint.Name);
            return new ApiFetchResult
            {
                EndpointName = endpoint.Name,
                EndpointUrl = url,
                Success = false,
                Error = $"No 'clientId:clientSecret' credential configured under NewsApiKeys:{options.Name}",
                FetchedAt = fetchedAt,
                ProcessingDurationMs = stopwatch.ElapsedMilliseconds
            };
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));

        string? responseBody = null;
        try
        {
            var clientId = credentials[..separatorIndex];
            var clientSecret = credentials[(separatorIndex + 1)..];
            var accessToken = await GetAccessTokenAsync(clientId, clientSecret, timeoutCts.Token);

            var client = _httpClientFactory.CreateClient(BaseNewsApiProvider.HttpClientName);
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd(UserAgent);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await client.SendAsync(request, timeoutCts.Token);
            httpStatusCode = (int)response.StatusCode;
            responseBody = await response.Content.ReadAsStringAsync(timeoutCts.Token);
            response.EnsureSuccessStatusCode();

            var articles = ParseArticles(responseBody, endpoint);

            return new ApiFetchResult
            {
                EndpointName = endpoint.Name,
                EndpointUrl = url,
                Success = true,
                Articles = articles,
                FetchedAt = fetchedAt,
                HttpStatusCode = httpStatusCode,
                ProcessingDurationMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Failed to fetch/parse news API endpoint {Provider}/{Endpoint}", options.Name, endpoint.Name);
            return new ApiFetchResult
            {
                EndpointName = endpoint.Name,
                EndpointUrl = url,
                Success = false,
                Error = ex.Message,
                ExceptionType = ex.GetType().FullName ?? ex.GetType().Name,
                StackTrace = ex.StackTrace,
                InnerException = ex.InnerException is { } inner ? $"{inner.GetType().FullName}: {inner.Message}" : null,
                ResponseBody = responseBody,
                FetchedAt = fetchedAt,
                HttpStatusCode = httpStatusCode,
                ProcessingDurationMs = stopwatch.ElapsedMilliseconds
            };
        }
    }

    /// <summary>Reddit's app-only <c>client_credentials</c> grant - cached until shortly before <c>expires_in</c> elapses so every endpoint fetch doesn't re-authenticate.</summary>
    private async Task<string> GetAccessTokenAsync(string clientId, string clientSecret, CancellationToken cancellationToken)
    {
        if (_cachedAccessToken is not null && DateTimeOffset.UtcNow < _cachedTokenExpiresAt)
        {
            return _cachedAccessToken;
        }

        await _tokenLock.WaitAsync(cancellationToken);
        try
        {
            if (_cachedAccessToken is not null && DateTimeOffset.UtcNow < _cachedTokenExpiresAt)
            {
                return _cachedAccessToken;
            }

            var client = _httpClientFactory.CreateClient(BaseNewsApiProvider.HttpClientName);
            var tokenRequestBody = new Dictionary<string, string> { ["grant_type"] = "client_credentials" };
            using var request = new HttpRequestMessage(HttpMethod.Post, TokenUrl)
            {
                Content = new FormUrlEncodedContent(tokenRequestBody)
            };
            request.Headers.UserAgent.ParseAdd(UserAgent);
            var basicAuth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{clientId}:{clientSecret}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicAuth);

            using var response = await client.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            response.EnsureSuccessStatusCode();

            using var document = JsonDocument.Parse(body);
            var token = document.RootElement.GetStringOrNull("access_token")
                ?? throw new InvalidOperationException("Reddit token response had no access_token field");
            var expiresInSeconds = document.RootElement.TryGetProperty("expires_in", out var expiresIn) && expiresIn.ValueKind == JsonValueKind.Number
                ? expiresIn.GetInt32()
                : 3600;

            _cachedAccessToken = token;
            // A minute of slack so a request that starts just before expiry doesn't get a token
            // that dies mid-flight.
            _cachedTokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(60, expiresInSeconds - 60));
            return token;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private IReadOnlyList<NormalizedArticle> ParseArticles(string json, NewsApiEndpointOptions endpoint)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("data", out var data) ||
            !data.TryGetProperty("children", out var childrenElement) ||
            childrenElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var articles = new List<NormalizedArticle>();
        foreach (var child in childrenElement.EnumerateArray())
        {
            if (!child.TryGetProperty("data", out var post))
            {
                continue;
            }

            var title = post.GetStringOrNull("title");
            var permalink = post.GetStringOrNull("permalink");
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(permalink))
            {
                continue;
            }

            var thumbnail = post.GetStringOrNull("thumbnail");
            var subreddit = post.GetStringOrNull("subreddit_name_prefixed");
            var createdUtc = post.TryGetProperty("created_utc", out var createdElement) && createdElement.ValueKind == JsonValueKind.Number
                ? DateTimeOffset.FromUnixTimeSeconds((long)createdElement.GetDouble())
                : (DateTimeOffset?)null;

            articles.Add(new NormalizedArticle
            {
                Provider = Name,
                FeedName = subreddit ?? Name,
                Category = endpoint.Category,
                Title = title,
                Summary = post.GetStringOrNull("selftext"),
                Url = $"https://www.reddit.com{permalink}",
                OriginalGuid = post.GetStringOrNull("id"),
                Author = post.GetStringOrNull("author"),
                Language = endpoint.Language,
                ImageUrl = thumbnail is { Length: > 0 } && thumbnail.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? thumbnail : null,
                PublishedAt = createdUtc,
                Source = subreddit ?? "Reddit",
                SourceType = ArticleSourceType.Api
            });
        }

        return articles;
    }
}
