using FluentValidation;

namespace Application.News.Queries.SearchNews;

public sealed class SearchNewsQueryValidator : AbstractValidator<SearchNewsQuery>
{
    public SearchNewsQueryValidator()
    {
        RuleFor(q => q.Query).NotEmpty();
        RuleFor(q => q.Count).GreaterThan(0);
    }
}
