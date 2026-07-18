using System.Text.Json;
using Cronos;

namespace PoliticalNews.Tests.Infrastructure;

/// <summary>
/// Guards against a malformed "Cron" string ever landing in the checked-in provider config -
/// every RSS provider's Cron (in src/WebRssFeed.appsettings.json plus every per-country
/// src/Countries.Rss/*.json file merged into it) and every API provider's Cron (the same shape
/// under src/WebApiFeed.appsettings.json/src/Countries.Api/*.json), see
/// WebPlatform/SplitCountryConfigLoader's merge logic, must parse as a valid 5-field cron
/// expression, the same parser (Cronos) the live "create/update recurring job" endpoint validates
/// against.
/// </summary>
public class ProviderCronConfigTests
{
    [Fact]
    public void EveryConfiguredProviderCron_ParsesAsAValidCronExpression()
    {
        var repoRoot = FindRepoRoot();
        var configFiles = new List<string>
        {
            Path.Combine(repoRoot, "src", "WebRssFeed.appsettings.json"),
            Path.Combine(repoRoot, "src", "WebApiFeed.appsettings.json")
        };
        configFiles.AddRange(Directory.GetFiles(Path.Combine(repoRoot, "src", "Countries.Rss"), "*.json"));
        configFiles.AddRange(Directory.GetFiles(Path.Combine(repoRoot, "src", "Countries.Api"), "*.json"));

        var failures = new List<string>();

        foreach (var file in configFiles)
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(file));
            foreach (var (pipelineSection, fileName) in new[] { ("NewsCrawler", file), ("NewsApiCrawler", file) })
            {
                if (!doc.RootElement.TryGetProperty(pipelineSection, out var section))
                {
                    continue;
                }

                // The shared base file nests every country under "Countries"; a per-country file
                // under src/Countries/ *is* a single country object directly (see Program.cs's
                // merge logic) - both shapes are walked the same way here.
                IEnumerable<JsonElement> countryElements = section.TryGetProperty("Countries", out var countries)
                    ? countries.EnumerateArray()
                    : [section];

                foreach (var country in countryElements)
                {
                    if (!country.TryGetProperty("Providers", out var providers))
                    {
                        continue;
                    }

                    foreach (var provider in providers.EnumerateArray())
                    {
                        var name = provider.TryGetProperty("Name", out var n) ? n.GetString() : "(unnamed)";
                        var cron = provider.TryGetProperty("Cron", out var c) ? c.GetString() : null;

                        if (string.IsNullOrWhiteSpace(cron))
                        {
                            continue;
                        }

                        try
                        {
                            CronExpression.Parse(cron);
                        }
                        catch (CronFormatException ex)
                        {
                            failures.Add($"{Path.GetFileName(fileName)} [{pipelineSection}] '{name}': '{cron}' - {ex.Message}");
                        }
                    }
                }
            }
        }

        Assert.True(failures.Count == 0, "Invalid Cron expression(s) found:\n" + string.Join("\n", failures));
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Rudraa.InElection.Feed.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Could not locate repo root from test base directory.");
    }
}
