using CloudCrate.Api.Common.Extensions;
using CloudCrate.Api.Models;
using CloudCrate.Api.Requests.File;
using FluentValidation;

namespace CloudCrate.Api.Validators;

public class UploadFileRequestValidator : AbstractValidator<UploadFileRequest>
{
    public UploadFileRequestValidator()
    {
        RuleFor(x => x.File)
            .NotNull().WithMessage("A file must be provided.")
            .Must(file =>
            {
                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                return AllowedFileExtensions.Extensions.Contains(ext);
            })
            .WithMessage("Invalid file type. Only allowed file types can be uploaded.");
    }
}