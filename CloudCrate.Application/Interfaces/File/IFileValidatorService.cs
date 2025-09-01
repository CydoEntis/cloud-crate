using CloudCrate.Application.Common.Models;
using CloudCrate.Application.DTOs.File.Request;

namespace CloudCrate.Application.Interfaces.File
{
    public interface IFileValidatorService
    {
        Task<Result> ValidateUploadAsync(FileUploadRequest request, string userId);
    }
}