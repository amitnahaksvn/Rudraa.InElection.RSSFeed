using FluentValidation;

namespace Application.Crawl.Queries.GetCrawlHistoryById;

public sealed class GetCrawlHistoryByIdQueryValidator : AbstractValidator<GetCrawlHistoryByIdQuery>
{
    public GetCrawlHistoryByIdQueryValidator()
    {
        RuleFor(q => q.Id).NotEmpty();
    }
}
