using FluentValidation;
using Domain.Entities;

namespace Application.Validators;

/// <summary>
/// Validates a <see cref="SocialMediaSource"/> document before it's seeded - a
/// <see cref="SocialMediaSource"/> is inserted directly into MongoDB rather than through a
/// Mediator command, so this runs at that call site instead of a pipeline behaviour, the same
/// reasoning as <see cref="FeedSourceValidator"/>.
/// </summary>
public sealed class SocialMediaSourceValidator : AbstractValidator<SocialMediaSource>
{
    public SocialMediaSourceValidator()
    {
        RuleFor(s => s.Name).NotEmpty();
        RuleFor(s => s.Identifier).NotEmpty();
        RuleFor(s => s.Country).NotEmpty();
        RuleFor(s => s.Language).NotEmpty();
        RuleFor(s => s.Category).NotEmpty();
        RuleFor(s => s.PollIntervalMinutes).GreaterThan(0);
        RuleFor(s => s.TimeoutSeconds).GreaterThan(0);
    }
}
