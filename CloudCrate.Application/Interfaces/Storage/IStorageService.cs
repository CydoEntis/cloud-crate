using CloudCrate.Application.Common.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace CloudCrate.Application.Interfaces.Storage
{
    public interface IStorageService
    {
        Task<Result> EnsureBucketExistsAsync(string bucketName);
        Result EnsureDirectory(string userId, Guid crateId, Guid? folderId);
        Task<Result> SaveFileAsync(string userId, Guid crateId, Guid? folderId, string fileName, Stream content);
        Task<Result<byte[]>> ReadFileAsync(string userId, Guid crateId, Guid? folderId, string fileName);
        Task<Result> DeleteFileAsync(string userId, Guid crateId, Guid? folderId, string fileName);
        bool FileExists(string userId, Guid crateId, Guid? folderId, string fileName);

        Task<Result> DeleteAllFilesInBucketAsync(Guid crateId);

        Task<Result> DeleteBucketAsync(Guid crateId);

        Task<Result> BatchDeleteFilesAsync(string userId, Guid crateId, List<(Guid? folderId, string fileName)> files);
    }
}