using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.Configuration.Json;

namespace WebPlatform;

/// <summary>
/// Merges one or more pipelines' base appsettings files (shared scalar settings for exactly one
/// pipeline each - e.g. <c>NewsCrawler</c> for the RSS tree, <c>NewsApiCrawler</c> for the API
/// tree - plus an initially empty <c>Countries</c> array) with every per-country JSON file under
/// that pipeline's own Countries directory (each holding just that one pipeline's own country
/// slice, shape <c>{ "&lt;sectionName&gt;": { "Name", "Enabled", "Providers": [...] } }</c>) into
/// one merged in-memory config blob. Has to happen in code rather than relying on
/// IConfiguration's own multi-source JSON merging because config binds arrays by index, not by
/// appending - every country file's own "Countries:0" would otherwise land at the same config key
/// and just overwrite each other across sources instead of accumulating. Inserted before the
/// environment-variables source (rather than appended, which is CreateBuilder's default for a
/// source added afterwards) so e.g. <c>NewsCrawler__*</c>/<c>NewsApiCrawler__*</c> env vars can
/// still override this file, not the reverse.
///
/// Takes every pipeline in <em>one</em> call rather than one call per pipeline deliberately:
/// <see cref="WebApplicationBuilder"/>'s <c>Configuration</c> is a <c>ConfigurationManager</c>,
/// which rebuilds (re-reading every registered source's stream) on every single
/// <c>Sources.Insert</c>/<c>Add</c> call, not just once at the end - a second call here would
/// re-read the first call's already-exhausted <see cref="JsonStreamConfigurationSource.Stream"/>
/// a second time and throw <c>ArgumentException: Stream was not readable</c> (confirmed directly:
/// this is exactly what happened when WebApp - the only caller needing both pipelines - first
/// called this twice).
/// </summary>
public static class SplitCountryConfigLoader
{
    public readonly record struct Pipeline(string BaseFileName, string CountriesDirectoryName, string SectionName);

    public static void InsertBeforeEnvironmentVariables(
        IConfigurationBuilder configuration, string baseDirectory, params Pipeline[] pipelines)
    {
        var source = new JsonStreamConfigurationSource
        {
            Stream = BuildMergedConfigStream(baseDirectory, pipelines)
        };

        var envVariablesIndex = configuration.Sources.ToList().FindIndex(s => s is EnvironmentVariablesConfigurationSource);
        if (envVariablesIndex < 0)
        {
            configuration.Add(source);
            return;
        }

        configuration.Sources.Insert(envVariablesIndex, source);
    }

    private static Stream BuildMergedConfigStream(string baseDirectory, Pipeline[] pipelines)
    {
        var root = new JsonObject();

        foreach (var pipeline in pipelines)
        {
            var pipelineRoot = JsonNode.Parse(File.ReadAllText(Path.Combine(baseDirectory, pipeline.BaseFileName)))!.AsObject();
            var section = pipelineRoot[pipeline.SectionName]!.AsObject();
            var countries = section["Countries"]!.AsArray();

            var countriesDirectory = Path.Combine(baseDirectory, pipeline.CountriesDirectoryName);
            if (Directory.Exists(countriesDirectory))
            {
                foreach (var file in Directory.EnumerateFiles(countriesDirectory, "*.json").OrderBy(f => f, StringComparer.Ordinal))
                {
                    var country = JsonNode.Parse(File.ReadAllText(file))!.AsObject();
                    if (country[pipeline.SectionName] is JsonObject countrySection)
                    {
                        countries.Add(countrySection.DeepClone());
                    }
                }
            }

            // DeepClone rather than reassigning `section` directly - a JsonNode can only ever
            // have one parent, and `section` is still (nominally) owned by the now-discarded
            // `pipelineRoot`.
            root[pipeline.SectionName] = section.DeepClone();
        }

        return new MemoryStream(Encoding.UTF8.GetBytes(root.ToJsonString()));
    }
}
