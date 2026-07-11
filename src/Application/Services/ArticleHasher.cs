using System.Security.Cryptography;
using System.Text;

namespace Application.Services;

/// <summary>
/// Computes the last-resort duplicate-detection signature (Title + PublishedAt) used when a
/// source republishes the same story under a different Url/OriginalGuid.
/// </summary>
public static class ArticleHasher
{
    public static string ComputeHash(string title, DateTimeOffset? publishedAt)
    {
        var normalizedTitle = title.Trim().ToLowerInvariant();
        var publishedKey = publishedAt?.ToUnixTimeSeconds().ToString() ?? "unknown";
        var raw = $"{normalizedTitle}|{publishedKey}";

        return Hash(raw);
    }

    /// <summary>
    /// Computes a single signature over every field an in-place update actually checks
    /// (Title/Summary/Content/ImageUrl) - lets <c>ArticleFingerprint.ContentHash</c> answer
    /// "did the content change?" from the lean fingerprint collection alone, without loading the
    /// full article just to compare its fields one by one.
    /// </summary>
    public static string ComputeContentHash(string title, string? summary, string? content, string? imageUrl)
    {
        var raw = string.Join(
            '|',
            title.Trim().ToLowerInvariant(),
            summary?.Trim().ToLowerInvariant() ?? string.Empty,
            content?.Trim().ToLowerInvariant() ?? string.Empty,
            imageUrl?.Trim() ?? string.Empty);

        return Hash(raw);
    }

    private static string Hash(string raw)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
