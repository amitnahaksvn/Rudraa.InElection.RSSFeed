using FluentValidation;

namespace Application.News.Queries.GetNewsByCategory;

public sealed class GetNewsByCategoryQueryValidator : AbstractValidator<GetNewsByCategoryQuery>
{
    public GetNewsByCategoryQueryValidator()
    {
        RuleFor(q => q.Category).NotEmpty();
        RuleFor(q => q.Count).GreaterThan(0);
    }
}
