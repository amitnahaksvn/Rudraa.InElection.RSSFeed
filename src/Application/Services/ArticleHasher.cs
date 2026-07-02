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

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
