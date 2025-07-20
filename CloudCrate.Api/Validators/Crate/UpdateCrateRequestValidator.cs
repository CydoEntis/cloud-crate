using CloudCrate.Application.DTOs.Crate.Request;
using FluentValidation;

namespace CloudCrate.Api.Validators.Crate;

public class UpdateCrateRequestValidator : AbstractValidator<UpdateCrateRequest>
{
    public UpdateCrateRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name cannot be empty")
            .MinimumLength(3).WithMessage("Crate name must be at least 3 characters long.")
            .MaximumLength(25)
            .When(x => x.Name != null);

        RuleFor(x => x.Color)
            .NotEmpty().WithMessage("Color cannot be empty")
            .Matches("^#(?:[0-9a-fA-F]{3}){1,2}$")
            .WithMessage("Color must be a valid hex code.")
            .When(x => x.Color != null);

        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.Name) || !string.IsNullOrWhiteSpace(x.Color))
            .WithMessage("You must update either the name or color.");
    }
}