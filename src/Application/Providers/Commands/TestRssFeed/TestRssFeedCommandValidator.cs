using FluentValidation;

namespace Application.Providers.Commands.TestRssFeed;

public sealed class TestRssFeedCommandValidator : AbstractValidator<TestRssFeedCommand>
{
    public TestRssFeedCommandValidator()
    {
        RuleFor(c => c.Country).NotEmpty();
        RuleFor(c => c.Provider).NotEmpty();
        RuleFor(c => c.FeedUrl).NotEmpty();
    }
}
