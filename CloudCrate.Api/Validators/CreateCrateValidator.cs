using CloudCrate.Api.Requests.Crate;
using FluentValidation;

namespace CloudCrate.Api.Validators;

public class CreateCrateRequestValidator : AbstractValidator<CreateCrateRequest>
{
    public CreateCrateRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);
    }
}