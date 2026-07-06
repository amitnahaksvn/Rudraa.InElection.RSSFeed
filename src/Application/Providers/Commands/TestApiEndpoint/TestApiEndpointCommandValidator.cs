using FluentValidation;

namespace Application.Providers.Commands.TestApiEndpoint;

public sealed class TestApiEndpointCommandValidator : AbstractValidator<TestApiEndpointCommand>
{
    public TestApiEndpointCommandValidator()
    {
        RuleFor(c => c.Country).NotEmpty();
        RuleFor(c => c.Provider).NotEmpty();
        RuleFor(c => c.EndpointName).NotEmpty();
    }
}
