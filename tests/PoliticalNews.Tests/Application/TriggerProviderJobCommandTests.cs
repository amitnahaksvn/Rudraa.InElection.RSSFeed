using Moq;
using Application.Abstractions;
using Application.Crawl.Commands.TriggerProviderJob;
using Domain.Entities;
using Domain.Enums;

namespace PoliticalNews.Tests.Application;

public class TriggerProviderJobCommandTests
{
    private static Mock<ICrawlCountryRepository> BuildCountryRepo()
    {
        var repo = new Mock<ICrawlCountryRepository>();
        repo.Setup(c => c.GetByNameAsync(CrawlPipeline.Rss, "India", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CrawlCountry { Name = "India", Enabled = true, Pipeline = CrawlPipeline.Rss });
        return repo;
    }

    private static Mock<IProviderScheduleRepository> BuildScheduleRepo()
    {
        var repo = new Mock<IProviderScheduleRepository>();
        repo.Setup(s => s.GetAsync(CrawlPipeline.Rss, "AajTak", "India", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderSchedule { Provider = "AajTak", Country = "India", Enabled = true, Cron = "*/5 * * * *" });
        repo.Setup(s => s.GetAsync(CrawlPipeline.Rss, "ABPNews", "India", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderSchedule { Provider = "ABPNews", Country = "India", Enabled = true, Cron = "*/5 * * * *" });
        repo.Setup(s => s.GetAsync(CrawlPipeline.Rss, "Disabled", "India", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderSchedule { Provider = "Disabled", Country = "India", Enabled = false, Cron = "*/5 * * * *" });
        repo.Setup(s => s.GetAsync(CrawlPipeline.Rss, "NoCron", "India", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderSchedule { Provider = "NoCron", Country = "India", Enabled = true, Cron = "" });
        repo.Setup(s => s.GetAsync(CrawlPipeline.Rss, "NoSuchProvider", "India", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProviderSchedule?)null);
        return repo;
    }

    [Fact]
    public async Task Handle_TriggersJobAndReturnsProviderAndJobId()
    {
        var trigger = new Mock<ICrawlJobTrigger>();
        trigger.Setup(t => t.TriggerNow(CrawlPipeline.Rss, "AajTak", "India")).Returns("news-crawl-AajTak::India");

        var handler = new TriggerProviderJobCommandHandler(trigger.Object);

        var result = await handler.Handle(new TriggerProviderJobCommand(CrawlPipeline.Rss, "AajTak", "India"), CancellationToken.None);

        Assert.Equal("AajTak", result.Provider);
        Assert.Equal("news-crawl-AajTak::India", result.JobId);
        trigger.Verify(t => t.TriggerNow(CrawlPipeline.Rss, "AajTak", "India"), Times.Once);
    }

    [Theory]
    [InlineData("AajTak", true)]
    [InlineData("ABPNews", true)]
    [InlineData("Disabled", false)]
    [InlineData("NoCron", false)]
    [InlineData("NoSuchProvider", false)]
    [InlineData("", false)]
    public async Task Validator_OnlyAcceptsEnabledProvidersWithACron(string provider, bool expectedValid)
    {
        var validator = new TriggerProviderJobCommandValidator(BuildCountryRepo().Object, BuildScheduleRepo().Object);

        var result = await validator.ValidateAsync(new TriggerProviderJobCommand(CrawlPipeline.Rss, provider, "India"));

        Assert.Equal(expectedValid, result.IsValid);
    }
}
