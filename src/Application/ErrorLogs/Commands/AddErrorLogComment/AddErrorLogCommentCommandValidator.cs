using FluentValidation;

namespace Application.ErrorLogs.Commands.AddErrorLogComment;

public sealed class AddErrorLogCommentCommandValidator : AbstractValidator<AddErrorLogCommentCommand>
{
    public AddErrorLogCommentCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.Comment).NotEmpty().MaximumLength(500);
    }
}
