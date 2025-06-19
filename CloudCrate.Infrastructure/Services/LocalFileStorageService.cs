using CloudCrate.Application.Common.Interfaces;
using CloudCrate.Application.Common.Models;
using CloudCrate.Application.Common.Settings;
using Microsoft.Extensions.Options;

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
            return Result<string>.Failure("file", $"File upload failed: {ex.Message}");
        }
    }


    public Task<Result<Stream>> DownloadAsync(string storedName)
    {
        try
        {
            var filePath = Path.Combine(_storagePath, storedName);
            if (!File.Exists(filePath))
                return Task.FromResult(Result<Stream>.Failure("file", "File not found"));

            var stream = File.OpenRead(filePath);
            return Task.FromResult(Result<Stream>.Success(stream));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result<Stream>.Failure("file", $"File download failed: {ex.Message}"));
        }
    }

    public Task<Result> DeleteAsync(string storedName)
    {
        try
        {
            var filePath = Path.Combine(_storagePath, storedName);
            if (!File.Exists(filePath))
                return Task.FromResult(Result.Failure("file", "File not found"));

            File.Delete(filePath);
            return Task.FromResult(Result.Success());
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result.Failure("file", $"File delete failed: {ex.Message}"));
        }
    }
}