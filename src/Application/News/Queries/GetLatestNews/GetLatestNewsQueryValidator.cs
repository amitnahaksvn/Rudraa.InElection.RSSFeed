using FluentValidation;

namespace Application.News.Queries.GetLatestNews;

public sealed class GetLatestNewsQueryValidator : AbstractValidator<GetLatestNewsQuery>
{
    public GetLatestNewsQueryValidator()
    {
        RuleFor(q => q.Count).GreaterThan(0);
    }
}
