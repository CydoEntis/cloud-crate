using Microsoft.Extensions.Options;
using CloudCrate.Application.Common.Interfaces;
using CloudCrate.Application.Common.Models;
using CloudCrate.Application.Common.Settings;

namespace CloudCrate.Infrastructure.Services;

public class LocalFileStorageService : IFileStorageService
{
    private readonly string _storagePath;

    public LocalFileStorageService(IOptions<StorageSettings> storageSettings)
    {
        _storagePath = storageSettings.Value.RootPath;

        if (!Directory.Exists(_storagePath))
            Directory.CreateDirectory(_storagePath);
    }

    public async Task<Result<string>> UploadAsync(Stream fileStream, string storedName)
    {
        try
        {
            var filePath = Path.Combine(_storagePath, storedName);
            await using var file = File.Create(filePath);
            await fileStream.CopyToAsync(file);
            return Result<string>.Success(storedName);
        }
        catch (Exception ex)
        {
            return Result<string>.Failure($"File upload failed: {ex.Message}");
        }
    }

    public Task<Result<Stream>> DownloadAsync(string storedName)
    {
        try
        {
            var filePath = Path.Combine(_storagePath, storedName);
            if (!File.Exists(filePath))
                return Task.FromResult(Result<Stream>.Failure("File not found"));

            var stream = File.OpenRead(filePath);
            return Task.FromResult(Result<Stream>.Success(stream));
        }
        catch (Exception ex)
        {
            // Log exception here as needed
            return Task.FromResult(Result<Stream>.Failure($"File download failed: {ex.Message}"));
        }
    }

    public Task<Result> DeleteAsync(string storedName)
    {
        try
        {
            var filePath = Path.Combine(_storagePath, storedName);
            if (!File.Exists(filePath))
                return Task.FromResult(Result.Failure("File not found"));

            File.Delete(filePath);
            return Task.FromResult(Result.Success());
        }
        catch (Exception ex)
        {
            // Log exception here as needed
            return Task.FromResult(Result.Failure($"File delete failed: {ex.Message}"));
        }
    }
}