using FluentValidation;

namespace Application.FilteredArticles.Commands.DeleteFilteredArticle;

public sealed class DeleteFilteredArticleCommandValidator : AbstractValidator<DeleteFilteredArticleCommand>
{
    public DeleteFilteredArticleCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
    }
}
