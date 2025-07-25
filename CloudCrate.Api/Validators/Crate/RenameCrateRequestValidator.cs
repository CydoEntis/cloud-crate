﻿using CloudCrate.Application.DTOs.Crate.Request;
using FluentValidation;

namespace CloudCrate.Api.Validators;

public class RenameCrateRequestValidator : AbstractValidator<RenameCrateRequest>
{
    public RenameCrateRequestValidator()
    {
        RuleFor(x => x.NewName)
            .NotEmpty()
            .MaximumLength(100);
    }
}