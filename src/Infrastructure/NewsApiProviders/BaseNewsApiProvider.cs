using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Application.Abstractions;
using Application.Models;
using Application.Options;
using Domain.Enums;

namespace Infrastructure.NewsApiProviders;

/// <summary>
/// Shared HTTP fetch + auth-injection pipeline for JSON news-API providers. Concrete providers
/// only supply <see cref="Name"/> and <see cref="ParseArticles"/> - everything about building each
/// request (base URL + endpoint path + configured query parameters + auth, entirely from
/// <see cref="NewsApiProviderOptions"/>/<see cref="NewsApiEndpointOptions"/>, never hardcoded per
/// provider) and error handling is common, mirroring how <c>BaseRssProvider</c> centralizes the
/// RSS fetch/parse pipeline across a provider's list of feeds.
/// </summary>
public abstract class BaseNewsApiProvider : INewsApiProvider
{
    /// <summary>Single shared named HttpClient for every news-API provider (see registration in InfrastructureServiceCollectionExtensions) - a per-endpoint timeout is enforced below via a linked CancellationTokenSource, same pattern as DynamicFeedIngestionService.</summary>
    public const string HttpClientName = "NewsApiClient";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger _logger;

    protected BaseNewsApiProvider(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public abstract string Name { get; }

    /// <summary>Parses one endpoint's raw JSON response body into normalized articles - the only thing each concrete provider implements.</summary>
    protected abstract IReadOnlyList<NormalizedArticle> ParseArticles(string json, NewsApiEndpointOptions endpoint);

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
        var url = BuildRequestUrl(options, endpoint, includeAuth: false);

        // Keyed by provider Name (not array index, which would silently break if Providers is
        // ever reordered) under "NewsApiKeys" in appsettings.json (development-tier credentials,
        // kept there by deliberate choice - see CLAUDE.md). AuthType.None (e.g. GDELT's public Doc
        // API) skips this lookup entirely - there's nothing to set.
        var apiKey = options.AuthType == ApiAuthType.None ? null : _configuration[$"NewsApiKeys:{options.Name}"];
        if (options.AuthType != ApiAuthType.None && string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning(
                "No API key configured for news API provider '{Provider}' (NewsApiKeys:{Provider}) - skipping endpoint {Endpoint}",
                options.Name, options.Name, endpoint.Name);
            return new ApiFetchResult
            {
                EndpointName = endpoint.Name,
                EndpointUrl = url,
                Success = false,
                Error = $"No API key configured under NewsApiKeys:{options.Name}",
                FetchedAt = fetchedAt,
                ProcessingDurationMs = stopwatch.ElapsedMilliseconds
            };
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));

        string? responseBody = null;
        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var request = BuildRequest(options, endpoint, apiKey);

            using var response = await client.SendAsync(request, timeoutCts.Token);
            httpStatusCode = (int)response.StatusCode;
            // Body read before the status check throws, not after, so a non-2xx response's body
            // (a JSON error payload, a rate-limit message) is still captured for
            // diagnostics/the monitoring-alert email instead of being discarded.
            responseBody = await response.Content.ReadAsStringAsync(timeoutCts.Token);
            response.EnsureSuccessStatusCode();

            var json = responseBody;
            // Stamped here, once, rather than in every concrete provider's ParseArticles - every
            // JSON-API provider's articles are Api-sourced, so there's nothing provider-specific
            // about this assignment. NormalizedArticle is a record precisely so `with` works here.
            var articles = ParseArticles(json, endpoint)
                .Select(article => article with { SourceType = ArticleSourceType.Api })
                .ToList();

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
            // Same reasoning as BaseRssProvider/DynamicFeedIngestionService: a stray
            // OperationCanceledException from the per-endpoint timeout above (not our caller's own
            // token) is a dead/rate-limited/slow API, not a real shutdown request, and must be
            // recorded as a failed run rather than crash the host.
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

    private static HttpRequestMessage BuildRequest(NewsApiProviderOptions options, NewsApiEndpointOptions endpoint, string? apiKey)
    {
        var url = BuildRequestUrl(options, endpoint, includeAuth: options.AuthType == ApiAuthType.QueryParameter, apiKey);

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (options.AuthType == ApiAuthType.HttpHeader && apiKey is not null)
        {
            request.Headers.TryAddWithoutValidation(options.AuthParamName, apiKey);
        }

        return request;
    }

    private static string BuildRequestUrl(NewsApiProviderOptions options, NewsApiEndpointOptions endpoint, bool includeAuth, string? apiKey = null)
    {
        var baseUrl = options.BaseUrl.TrimEnd('/');
        var path = endpoint.Endpoint.TrimStart('/');
        var queryParameters = new Dictionary<string, string>(endpoint.QueryParameters, StringComparer.OrdinalIgnoreCase);

        if (includeAuth && apiKey is not null)
        {
            queryParameters[options.AuthParamName] = apiKey;
        }

        var query = string.Join('&', queryParameters.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
        return query.Length == 0 ? $"{baseUrl}/{path}" : $"{baseUrl}/{path}?{query}";
    }
}
