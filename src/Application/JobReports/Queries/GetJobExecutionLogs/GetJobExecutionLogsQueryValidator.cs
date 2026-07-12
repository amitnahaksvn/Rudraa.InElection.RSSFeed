using FluentValidation;

namespace Application.JobReports.Queries.GetJobExecutionLogs;

public sealed class GetJobExecutionLogsQueryValidator : AbstractValidator<GetJobExecutionLogsQuery>
{
    public GetJobExecutionLogsQueryValidator()
    {
        RuleFor(q => q.Count).GreaterThan(0).LessThanOrEqualTo(500);

        RuleFor(q => q)
            .Must(q => q.From is null || q.To is null || q.From <= q.To)
            .WithMessage("'From' must be less than or equal to 'To'.")
            .WithName("From");
    }
}
