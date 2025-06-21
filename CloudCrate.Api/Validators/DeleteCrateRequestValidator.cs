using CloudCrate.Application.DTOs.Crate;
using FluentValidation;

namespace CloudCrate.Api.Validators;

public class DeleteCrateRequestValidator : AbstractValidator<DeleteCrateRequest>
{
    public DeleteCrateRequestValidator()
    {
        RuleFor(x => x.CrateId)
            .NotEmpty().WithMessage("Crate ID is required.");
    }
}