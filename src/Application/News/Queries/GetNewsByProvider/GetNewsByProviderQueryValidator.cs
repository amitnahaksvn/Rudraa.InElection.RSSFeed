using FluentValidation;

namespace Application.News.Queries.GetNewsByProvider;

public sealed class GetNewsByProviderQueryValidator : AbstractValidator<GetNewsByProviderQuery>
{
    public GetNewsByProviderQueryValidator()
    {
        RuleFor(q => q.Provider).NotEmpty();
        RuleFor(q => q.Count).GreaterThan(0);
    }
}
