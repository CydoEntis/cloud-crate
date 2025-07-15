using CloudCrate.Application.DTOs.Crate;

namespace CloudCrate.Api.Validators;

using FluentValidation;

public class UpdateCrateRequestValidator : AbstractValidator<UpdateCrateRequest>
{
    public UpdateCrateRequestValidator()
    {
        RuleFor(x => x.Name)
            .MaximumLength(25)
            .When(x => !string.IsNullOrWhiteSpace(x.Name));

        RuleFor(x => x.Color)
            .Matches("^#(?:[0-9a-fA-F]{3}){1,2}$")
            .WithMessage("Color must be a valid hex code.")
            .When(x => !string.IsNullOrWhiteSpace(x.Color));

        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.Name) || !string.IsNullOrWhiteSpace(x.Color))
            .WithMessage("You must update either the name or color.");
    }
}