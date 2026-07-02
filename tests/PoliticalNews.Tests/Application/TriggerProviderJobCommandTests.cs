using Microsoft.Extensions.Options;
using Moq;
using Application.Abstractions;
using Application.Crawl.Commands.TriggerProviderJob;
using Application.Options;

namespace PoliticalNews.Tests.Application;

public class TriggerProviderJobCommandTests
{
    private static NewsCrawlerOptions BuildOptions() => new()
    {
        Providers =
        [
            new RssProviderOptions { Name = "AajTak", Enabled = true, Cron = "*/5 * * * *" },
            new RssProviderOptions { Name = "ABPNews", Enabled = true, Cron = "*/5 * * * *" },
            new RssProviderOptions { Name = "Disabled", Enabled = false, Cron = "*/5 * * * *" },
            new RssProviderOptions { Name = "NoCron", Enabled = true, Cron = "" }
        ]
    };

    [Fact]
    public async Task Handle_TriggersJobAndReturnsProviderAndJobId()
    {
        var trigger = new Mock<ICrawlJobTrigger>();
        trigger.Setup(t => t.TriggerNow("AajTak")).Returns("news-crawl-AajTak");

        var handler = new TriggerProviderJobCommandHandler(trigger.Object);

        var result = await handler.Handle(new TriggerProviderJobCommand("AajTak"), CancellationToken.None);

        Assert.Equal("AajTak", result.Provider);
        Assert.Equal("news-crawl-AajTak", result.JobId);
        trigger.Verify(t => t.TriggerNow("AajTak"), Times.Once);
    }

    [Theory]
    [InlineData("AajTak", true)]
    [InlineData("ABPNews", true)]
    [InlineData("Disabled", false)]
    [InlineData("NoCron", false)]
    [InlineData("NoSuchProvider", false)]
    [InlineData("", false)]
    public void Validator_OnlyAcceptsEnabledProvidersWithACron(string provider, bool expectedValid)
    {
        var validator = new TriggerProviderJobCommandValidator(Options.Create(BuildOptions()));

        var result = validator.Validate(new TriggerProviderJobCommand(provider));

        Assert.Equal(expectedValid, result.IsValid);
    }
}
