using Hangfire;
using Worker.Infrastructure;

namespace PoliticalNews.Tests.Application;

public class RecurringJobOptionsFactoryTests
{
    [Fact]
    public void Create_SetsMisfireHandlingToIgnore()
    {
        var options = RecurringJobOptionsFactory.Create(TimeZoneInfo.Utc);

        Assert.Equal(TimeZoneInfo.Utc, options.TimeZone);
        Assert.Equal(MisfireHandlingMode.Ignorable, options.MisfireHandling);
    }
}
