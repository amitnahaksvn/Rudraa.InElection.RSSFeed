using FluentValidation;

namespace Application.Crawl.Queries.GetCrawlJobStatus;

public sealed class GetCrawlJobStatusQueryValidator : AbstractValidator<GetCrawlJobStatusQuery>
{
    public GetCrawlJobStatusQueryValidator()
    {
        RuleFor(q => q.Provider).NotEmpty();
        RuleFor(q => q.Country).NotEmpty();
    }
}
