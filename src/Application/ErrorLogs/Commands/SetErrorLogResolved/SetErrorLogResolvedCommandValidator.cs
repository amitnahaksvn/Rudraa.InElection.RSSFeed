using FluentValidation;

namespace Application.ErrorLogs.Commands.SetErrorLogResolved;

public sealed class SetErrorLogResolvedCommandValidator : AbstractValidator<SetErrorLogResolvedCommand>
{
    public SetErrorLogResolvedCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.Comment)
            .NotEmpty().WithMessage("A comment is required when changing an error's resolved status.")
            .MaximumLength(500);
    }
}
