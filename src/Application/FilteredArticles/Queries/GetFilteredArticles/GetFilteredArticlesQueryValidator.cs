using FluentValidation;

namespace Application.FilteredArticles.Queries.GetFilteredArticles;

public sealed class GetFilteredArticlesQueryValidator : AbstractValidator<GetFilteredArticlesQuery>
{
    public GetFilteredArticlesQueryValidator()
    {
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1);
        RuleFor(q => q.PageSize).GreaterThan(0).LessThanOrEqualTo(200);
    }
}
