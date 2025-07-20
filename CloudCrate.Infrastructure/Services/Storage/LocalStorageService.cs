using CloudCrate.Application.Common.Errors;
using CloudCrate.Application.Common.Models;
using CloudCrate.Application.Common.Settings;
using CloudCrate.Application.Interfaces.Storage;
using Microsoft.Extensions.Options;

namespace CloudCrate.Infrastructure.Services.Storage;

public class LocalStorageService : IStorageService
{
    private readonly StorageSettings _storageSettings;

    public LocalStorageService(IOptions<StorageSettings> storageSettings)
    {
        _storageSettings = storageSettings.Value;
    }

    private string GetDirectoryPath(string userId, Guid crateId, Guid? folderId)
    {
        var path = Path.Combine(_storageSettings.RootPath, userId, crateId.ToString());
        if (folderId.HasValue)
            path = Path.Combine(path, folderId.Value.ToString());
        return path;
    }

    private string GetFilePath(string userId, Guid crateId, Guid? folderId, string fileName)
    {
        var dirPath = GetDirectoryPath(userId, crateId, folderId);
        return Path.Combine(dirPath, fileName);
    }

    public Result EnsureDirectory(string userId, Guid crateId, Guid? folderId)
    {
        try
        {
            var dirPath = GetDirectoryPath(userId, crateId, folderId);
            Directory.CreateDirectory(dirPath);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(Errors.Folders.CreationFailed with
            {
                Message = $"{Errors.Folders.CreationFailed.Message} ({ex.Message})"
            });
        }
    }

    public async Task<Result> SaveFileAsync(string userId, Guid crateId, Guid? folderId, string fileName,
        Stream content)
    {
        try
        {
            var dirResult = EnsureDirectory(userId, crateId, folderId);
            if (!dirResult.Succeeded)
                return dirResult;

            var filePath = GetFilePath(userId, crateId, folderId, fileName);

            using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            await content.CopyToAsync(stream);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(Errors.Files.SaveFailed with
            {
                Message = $"{Errors.Files.SaveFailed.Message} ({ex.Message})"
            });
        }
    }

    public async Task<Result<byte[]>> ReadFileAsync(string userId, Guid crateId, Guid? folderId, string fileName)
    {
        try
        {
            var filePath = GetFilePath(userId, crateId, folderId, fileName);

            if (!File.Exists(filePath))
                return Result<byte[]>.Failure(Errors.Files.NotFound);

            var bytes = await File.ReadAllBytesAsync(filePath);
            return Result<byte[]>.Success(bytes);
        }
        catch (Exception ex)
        {
            return Result<byte[]>.Failure(Errors.Files.ReadFailed with
            {
                Message = $"{Errors.Files.ReadFailed.Message} ({ex.Message})"
            });
        }
    }

    public async Task<Result> DeleteFileAsync(string userId, Guid crateId, Guid? folderId, string fileName)
    {
        try
        {
            var filePath = GetFilePath(userId, crateId, folderId, fileName);

            if (File.Exists(filePath))
                File.Delete(filePath);

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(Errors.Files.DeleteFailed with
            {
                Message = $"{Errors.Files.DeleteFailed.Message} ({ex.Message})"
            });
        }
    }

    public bool FileExists(string userId, Guid crateId, Guid? folderId, string fileName)
    {
        var filePath = GetFilePath(userId, crateId, folderId, fileName);
        return File.Exists(filePath);
    }

    public async Task<Result> DeleteFilesAsync(string bucketName, IEnumerable<string> keys)
    {
        try
        {
            foreach (var key in keys)
            {
                var filePath = Path.Combine(_storageSettings.RootPath,
                    key.Replace("/", Path.DirectorySeparatorChar.ToString()));

                if (File.Exists(filePath))
                    File.Delete(filePath);
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(Errors.Files.DeleteFailed with
            {
                Message = $"{Errors.Files.DeleteFailed.Message} ({ex.Message})"
            });
        }
    }
}