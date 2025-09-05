using CloudCrate.Application.DTOs.File.Request;
using CloudCrate.Application.Models;

namespace CloudCrate.Application.Interfaces.File
{
    public interface IFileValidatorService
    {
        Task<Result> ValidateUploadAsync(FileUploadRequest request, string userId);
    }
}