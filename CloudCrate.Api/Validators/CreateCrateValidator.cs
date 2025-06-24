using CloudCrate.Api.Requests.Crate;
using FluentValidation;

namespace CloudCrate.Api.Validators;

public class CreateCrateRequestValidator : AbstractValidator<CreateCrateRequest>
{
    public CreateCrateRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100)
            .Matches("^[a-zA-Z0-9 _-]*$").WithMessage("Crate name contains invalid characters.");

        RuleFor(x => x.Color)
            .NotEmpty().WithMessage("Color is required.")
            .Must(IsValidColor).WithMessage("Color is invalid.");
    }

    private bool IsValidColor(string color)
    {
        if (string.IsNullOrEmpty(color)) return false;
        return System.Text.RegularExpressions.Regex.IsMatch(color, "^#([0-9A-Fa-f]{6})$");
    }
}