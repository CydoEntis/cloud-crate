using CloudCrate.Api.Models;
using FluentValidation;

namespace CloudCrate.Api.Validators;

public class UploadFileRequestValidator : AbstractValidator<UploadFileRequest>
{
    private readonly string[] _permittedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff" };

    public UploadFileRequestValidator()
    {
        RuleFor(x => x.File).NotNull().WithMessage("A file must be provided.");

        RuleFor(x => x.File)
            .Must(file =>
                file != null && _permittedExtensions.Contains(Path.GetExtension(file.FileName).ToLowerInvariant()))
            .WithMessage("Invalid file type. Only image files are allowed.");
    }
}