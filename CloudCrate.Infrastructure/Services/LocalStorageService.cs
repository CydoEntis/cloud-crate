using CloudCrate.Application.Common.Errors;
using CloudCrate.Application.Common.Interfaces;
using CloudCrate.Application.Common.Models;
using Microsoft.Extensions.Options;
using CloudCrate.Application.Common.Settings;

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
            return Result.Failure(Errors.FolderCreationFailed with
            {
                Message = $"{Errors.FolderCreationFailed.Message} ({ex.Message})"
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
            return Result.Failure(Errors.FileSaveFailed with
            {
                Message = $"{Errors.FileSaveFailed.Message} ({ex.Message})"
            });
        }
    }

    public async Task<Result<byte[]>> ReadFileAsync(string userId, Guid crateId, Guid? folderId, string fileName)
    {
        try
        {
            var filePath = GetFilePath(userId, crateId, folderId, fileName);

            if (!File.Exists(filePath))
                return Result<byte[]>.Failure(Errors.FileNotFound);

            var bytes = await File.ReadAllBytesAsync(filePath);
            return Result<byte[]>.Success(bytes);
        }
        catch (Exception ex)
        {
            return Result<byte[]>.Failure(Errors.FileReadFailed with
            {
                Message = $"{Errors.FileReadFailed.Message} ({ex.Message})"
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
            return Result.Failure(Errors.FileDeleteFailed with
            {
                Message = $"{Errors.FileDeleteFailed.Message} ({ex.Message})"
            });
        }
    }

    public bool FileExists(string userId, Guid crateId, Guid? folderId, string fileName)
    {
        var filePath = GetFilePath(userId, crateId, folderId, fileName);
        return File.Exists(filePath);
    }
}