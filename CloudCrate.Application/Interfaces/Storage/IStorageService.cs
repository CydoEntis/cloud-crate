using CloudCrate.Application.Common.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CloudCrate.Application.DTOs.File.Request;

namespace CloudCrate.Application.Interfaces.Storage;

public interface IStorageService
{
    Task<Result> EnsureBucketExistsAsync(string bucketName);
    Result EnsureDirectory(string userId, Guid crateId, Guid? folderId);

    Task<Result<string>> GetFileUrlAsync(string userId, Guid crateId, Guid? folderId, string fileName,
        TimeSpan? expiry = null);

    Task<Result<string>> SaveFileAsync(string userId, FileUploadRequest request);

    Task<Result<byte[]>> ReadFileAsync(string userId, Guid crateId, Guid? folderId, string fileName);
    Task<Result> DeleteFileAsync(string userId, Guid crateId, Guid? folderId, string fileName);
    bool FileExists(string userId, Guid crateId, Guid? folderId, string fileName);

    Task<Result> DeleteAllFilesInBucketAsync(Guid crateId);

    Task<Result> DeleteBucketAsync(Guid crateId);
}