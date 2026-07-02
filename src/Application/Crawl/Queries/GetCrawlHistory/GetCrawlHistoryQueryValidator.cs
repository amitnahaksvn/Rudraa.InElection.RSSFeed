using FluentValidation;

namespace Application.Crawl.Queries.GetCrawlHistory;

public sealed class GetCrawlHistoryQueryValidator : AbstractValidator<GetCrawlHistoryQuery>
{
    public GetCrawlHistoryQueryValidator()
    {
        RuleFor(q => q.Count).GreaterThan(0);
    }
}
