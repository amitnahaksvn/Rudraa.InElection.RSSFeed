using System.Globalization;
using System.Text.Json;

namespace Infrastructure.NewsApiProviders;

/// <summary>
/// Null-safe JSON reading helpers shared by every news-API provider's <c>ParseArticles</c>, since
/// <see cref="JsonElement.GetProperty"/> throws on a missing property instead of returning null -
/// and a missing/differently-typed field (a free-tier response omitting an optional field, a
/// provider returning <c>null</c> instead of omitting the key) must never crash a parse.
/// </summary>
internal static class JsonElementExtensions
{
    public static string? GetStringOrNull(this JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(propertyName, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    // A provider's own published-date field carries whatever offset it reported - converted with
    // .ToUniversalTime() so what's actually stored in NewsArticle.PublishedAt is consistently UTC
    // (Offset=00:00), same as every other provider's date parsing, rather than varying per API.
    public static DateTimeOffset? GetDateTimeOrNull(this JsonElement element, string propertyName)
    {
        var raw = element.GetStringOrNull(propertyName);
        return !string.IsNullOrWhiteSpace(raw) &&
            DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
                ? parsed.ToUniversalTime()
                : null;
    }

    /// <summary>First non-empty string out of a JSON array property, e.g. NewsData.io's <c>creator</c>/WorldNewsAPI's <c>authors</c>.</summary>
    public static string? GetFirstStringInArrayOrNull(this JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(propertyName, out var value) &&
        value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray()
                .Select(e => e.ValueKind == JsonValueKind.String ? e.GetString() : null)
                .FirstOrDefault(s => !string.IsNullOrWhiteSpace(s))
            : null;

    /// <summary>Every string in a JSON array property, e.g. TheNewsAPI's <c>categories</c>/NewsData.io's <c>category</c>.</summary>
    public static List<string> GetStringArrayOrEmpty(this JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(propertyName, out var value) &&
        value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray()
                .Select(e => e.ValueKind == JsonValueKind.String ? e.GetString() : null)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s!)
                .ToList()
            : [];
}
