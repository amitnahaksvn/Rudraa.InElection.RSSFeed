using Hangfire;
using Infrastructure.Scheduling;

namespace PoliticalNews.Tests.Infrastructure;

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
