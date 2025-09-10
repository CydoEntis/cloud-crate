using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using CloudCrate.Application.DTOs.File.Request;
using CloudCrate.Application.Errors;
using CloudCrate.Application.Extensions;
using CloudCrate.Application.Interfaces.Storage;
using CloudCrate.Application.Models;
using Microsoft.Extensions.Logging;

namespace CloudCrate.Infrastructure.Services.Storage;

public class MinioStorageService : IStorageService
{
    private readonly IAmazonS3 _s3Client;
    private readonly ILogger<MinioStorageService> _logger;
    private const int DeleteBatchSize = 1000;
    private const int MaxParallelBatches = 5;

    public MinioStorageService(IAmazonS3 s3Client, ILogger<MinioStorageService> logger)
    {
        _s3Client = s3Client;
        _logger = logger;
    }

    public async Task<Result<bool>> BucketExistsAsync(string bucketName)
    {
        try
        {
            var exists = await AmazonS3Util.DoesS3BucketExistV2Async(_s3Client, bucketName);
            return Result<bool>.Success(exists);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check if bucket {Bucket} exists", bucketName);
            return Result<bool>.Failure(new StorageError($"Failed to check bucket {bucketName}. ({ex.Message})"));
        }
    }

    public async Task<Result> CreateBucketAsync(string bucketName)
    {
        try
        {
            await _s3Client.PutBucketAsync(bucketName);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create bucket {Bucket}", bucketName);
            return Result.Failure(new StorageError($"Failed to create bucket {bucketName}. ({ex.Message})"));
        }
    }

    public async Task<Result> GetOrCreateBucketAsync(string bucketName)
    {
        var existsResult = await BucketExistsAsync(bucketName);
        if (!existsResult.IsSuccess) return Result.Failure(existsResult.Error);
        if (!existsResult.Value)
        {
            var createResult = await CreateBucketAsync(bucketName);
            if (!createResult.IsSuccess) return Result.Failure(createResult.Error);
        }

        return Result.Success();
    }

    public async Task<Result<string>> GetFileUrlAsync(string userId, Guid crateId, Guid? folderId, string fileName,
        TimeSpan? expiry = null)
    {
        var bucketName = $"crate-{crateId}".ToLowerInvariant();
        var key = GetObjectKey(userId, crateId, folderId, fileName);
        try
        {
            var urlExpiry = expiry ?? TimeSpan.FromMinutes(15);
            var request = new GetPreSignedUrlRequest
            {
                BucketName = bucketName,
                Key = key,
                Expires = DateTime.UtcNow.Add(urlExpiry),
                Verb = HttpVerb.GET
            };
            return Result<string>.Success(_s3Client.GetPreSignedURL(request));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate presigned URL for file {Key} in bucket {Bucket}", key, bucketName);
            return Result<string>.Failure(
                new FileAccessError($"Failed to generate access URL for file. ({ex.Message})"));
        }
    }

    public async Task<Result<string>> SaveFileAsync(string userId, FileUploadRequest request)
    {
        var bucketName = $"crate-{request.CrateId}".ToLowerInvariant();
        var key = GetObjectKey(userId, request.CrateId, request.FolderId, request.FileName);
        try
        {
            var bucketResult = await GetOrCreateBucketAsync(bucketName);
            if (!bucketResult.IsSuccess) return Result<string>.Failure(bucketResult.Error!);

            await _s3Client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = bucketName,
                Key = key,
                InputStream = request.Content
            });

            return Result<string>.Success(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save file {FileName} in {Bucket}", request.FileName, bucketName);
            return Result<string>.Failure(new FileSaveError($"Failed to save file {request.FileName}. ({ex.Message})"));
        }
    }

    public async Task<Result<List<string>>> SaveFilesAsync(string userId, List<FileUploadRequest> requests)
    {
        var uploadedKeys = new List<string>();
        foreach (var request in requests)
        {
            var result = await SaveFileAsync(userId, request);
            if (!result.IsSuccess) return Result<List<string>>.Failure(result.Error!);
            uploadedKeys.Add(result.Value);
        }

        return Result<List<string>>.Success(uploadedKeys);
    }

    public async Task<Result<byte[]>> ReadFileAsync(string userId, Guid crateId, Guid? folderId, string fileName)
    {
        var bucketName = $"crate-{crateId}".ToLowerInvariant();
        var key = GetObjectKey(userId, crateId, folderId, fileName);
        try
        {
            var response = await _s3Client.GetObjectAsync(bucketName, key);
            using var ms = new MemoryStream();
            await response.ResponseStream.CopyToAsync(ms);
            return Result<byte[]>.Success(ms.ToArray());
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return Result<byte[]>.Failure(new FileNotFoundError($"File {fileName} not found in bucket {bucketName}"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read file {Key}", key);
            return Result<byte[]>.Failure(new FileReadError($"Failed to read file {fileName}. ({ex.Message})"));
        }
    }

    public async Task<bool> FileExistsAsync(string userId, Guid crateId, Guid? folderId, string fileName)
    {
        try
        {
            await _s3Client.GetObjectMetadataAsync($"crate-{crateId}".ToLowerInvariant(),
                GetObjectKey(userId, crateId, folderId, fileName));
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not verify file existence for {Key}", fileName);
            return false;
        }
    }

    public async Task<Result> DeleteFileAsync(string userId, Guid crateId, Guid? folderId, string fileName)
    {
        var bucketName = $"crate-{crateId}".ToLowerInvariant();
        var key = GetObjectKey(userId, crateId, folderId, fileName);
        return await DeleteKeysAsync(bucketName, new List<string> { key });
    }

    public async Task<Result> DeleteFolderAsync(string userId, Guid crateId, Guid folderId)
    {
        return await DeleteFoldersAsync(userId, crateId, new[] { folderId });
    }

    public async Task<Result> DeleteBucketAsync(Guid crateId)
    {
        var bucketName = $"crate-{crateId}".ToLowerInvariant();
        var filesResult = await DeleteAllFilesInBucketAsync(crateId);
        if (!filesResult.IsSuccess) return filesResult;

        try
        {
            await _s3Client.DeleteBucketAsync(bucketName);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete bucket {Bucket}", bucketName);
            return Result.Failure(new StorageError($"Failed to delete bucket {bucketName}. ({ex.Message})"));
        }
    }

    public async Task<Result> DeleteFilesAsync(string userId, Guid crateId, Guid? folderId,
        IEnumerable<string> fileNames)
    {
        var bucketName = $"crate-{crateId}".ToLowerInvariant();
        var keys = fileNames.Select(f => GetObjectKey(userId, crateId, folderId, f)).ToList();
        return await DeleteKeysAsync(bucketName, keys);
    }

    public async Task<Result> DeleteFoldersAsync(string userId, Guid crateId, IEnumerable<Guid> folderIds)
    {
        if (!folderIds.Any()) return Result.Success();
        var bucketName = $"crate-{crateId}".ToLowerInvariant();
        var allKeys = new List<string>();

        foreach (var batch in folderIds.Batch(50))
        {
            foreach (var folderId in batch)
            {
                string prefix = $"{userId}/{crateId}/{folderId}/";
                string? continuationToken = null;
                do
                {
                    var listResponse = await _s3Client.ListObjectsV2Async(new ListObjectsV2Request
                    {
                        BucketName = bucketName,
                        Prefix = prefix,
                        ContinuationToken = continuationToken,
                        MaxKeys = DeleteBatchSize
                    });

                    allKeys.AddRange(listResponse.S3Objects.Where(o => !string.IsNullOrEmpty(o.Key))
                        .Select(o => o.Key));
                    continuationToken = listResponse.IsTruncated == true ? listResponse.NextContinuationToken : null;
                } while (continuationToken != null);
            }
        }

        return await DeleteKeysAsync(bucketName, allKeys);
    }

    public async Task<Result> DeleteBucketsAsync(IEnumerable<Guid> crateIds)
    {
        foreach (var crateId in crateIds)
        {
            var result = await DeleteBucketAsync(crateId);
            if (!result.IsSuccess) return result;
        }

        return Result.Success();
    }

    public async Task<Result> DeleteAllFilesInBucketAsync(Guid crateId)
    {
        var bucketName = $"crate-{crateId}".ToLowerInvariant();
        string? continuationToken = null;
        var allKeys = new List<string>();

        do
        {
            var listResponse = await _s3Client.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = bucketName,
                ContinuationToken = continuationToken,
                MaxKeys = DeleteBatchSize
            });

            allKeys.AddRange(listResponse.S3Objects.Select(o => o.Key));
            continuationToken = listResponse.IsTruncated == true ? listResponse.NextContinuationToken : null;
        } while (continuationToken != null);

        return await DeleteKeysAsync(bucketName, allKeys);
    }

    private async Task<Result> DeleteKeysAsync(string bucketName, List<string> keys)
    {
        var batches = keys.Batch(DeleteBatchSize).ToList();

        foreach (var batchGroup in batches.Batch(MaxParallelBatches))
        {
            var tasks = batchGroup.Select(batch => DeleteBatchAsync(bucketName, batch.ToList())).ToList();
            var results = await Task.WhenAll(tasks);

            var result = results.FirstOrDefault(r => !r.IsSuccess);
            if (result.IsFailure) return result;
        }

        return Result.Success();
    }

    private async Task<Result> DeleteBatchAsync(string bucketName, List<string> batch)
    {
        try
        {
            var response = await _s3Client.DeleteObjectsAsync(new DeleteObjectsRequest
            {
                BucketName = bucketName,
                Objects = batch.Select(k => new KeyVersion { Key = k }).ToList()
            });

            if (response.DeleteErrors?.Count > 0)
            {
                _logger.LogError("Errors deleting objects in bucket {Bucket}: {Errors}", bucketName,
                    response.DeleteErrors);
                return Result.Failure(new StorageError($"Failed to delete some objects in bucket {bucketName}."));
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete objects in bucket {Bucket}", bucketName);
            return Result.Failure(new StorageError($"Failed to delete objects in bucket {bucketName}. ({ex.Message})"));
        }
    }

    private static string GetObjectKey(string userId, Guid crateId, Guid? folderId, string fileName)
    {
        var parts = new List<string> { userId, crateId.ToString() };
        if (folderId.HasValue) parts.Add(folderId.Value.ToString());
        parts.Add(fileName);
        return string.Join("/", parts);
    }
}