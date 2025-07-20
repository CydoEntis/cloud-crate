using CloudCrate.Api.Common.Extensions;
using CloudCrate.Api.DTOs.File.Request;
using CloudCrate.Application.DTOs.File.Request;
using FluentValidation;

namespace CloudCrate.Api.Validators.File;

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