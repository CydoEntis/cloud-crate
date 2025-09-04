using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using CloudCrate.Application.Common.Errors;
using CloudCrate.Application.Common.Models;
using CloudCrate.Application.DTOs.File.Request;
using CloudCrate.Application.Interfaces.Storage;
using Microsoft.Extensions.Logging;

namespace CloudCrate.Infrastructure.Services.Storage;

public class MinioStorageService : IStorageService
{
    private readonly IAmazonS3 _s3Client;
    private readonly ILogger<MinioStorageService> _logger;

    public MinioStorageService(IAmazonS3 s3Client, ILogger<MinioStorageService> logger)
    {
        _s3Client = s3Client;
        _logger = logger;
    }

    public async Task<Result> EnsureBucketExistsAsync(string bucketName)
    {
        try
        {
            var exists = await AmazonS3Util.DoesS3BucketExistV2Async(_s3Client, bucketName);
            if (!exists)
                await _s3Client.PutBucketAsync(bucketName);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create bucket {Bucket}", bucketName);
            return Result.Failure(new StorageError($"Failed to create bucket {bucketName}. ({ex.Message})"));
        }
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

            var url = _s3Client.GetPreSignedURL(request);
            return Result<string>.Success(url);
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
        try
        {
            var bucketResult = await EnsureBucketExistsAsync(bucketName);
            if (bucketResult.IsFailure)
                return Result<string>.Failure(bucketResult.Error!);

            var key = GetObjectKey(userId, request.CrateId, request.FolderId, request.FileName);

            var putRequest = new PutObjectRequest
            {
                BucketName = bucketName,
                Key = key,
                InputStream = request.Content
            };

            await _s3Client.PutObjectAsync(putRequest);
            return Result<string>.Success(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save file {FileName} in {Bucket}", request.FileName, bucketName);
            return Result<string>.Failure(new FileSaveError($"Failed to save file {request.FileName}. ({ex.Message})"));
        }
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

    public async Task<Result> DeleteFileAsync(string userId, Guid crateId, Guid? folderId, string fileName)
    {
        var bucketName = $"crate-{crateId}".ToLowerInvariant();
        var key = GetObjectKey(userId, crateId, folderId, fileName);

        try
        {
            await _s3Client.DeleteObjectAsync(bucketName, key);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete file {Key}", key);
            return Result.Failure(new FileDeleteError($"Failed to delete file {fileName}. ({ex.Message})"));
        }
    }

    public bool FileExists(string userId, Guid crateId, Guid? folderId, string fileName)
    {
        var bucketName = $"crate-{crateId}".ToLowerInvariant();
        var key = GetObjectKey(userId, crateId, folderId, fileName);

        try
        {
            _s3Client.GetObjectMetadataAsync(bucketName, key).GetAwaiter().GetResult();
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not verify file existence for {Key}", key);
            return false;
        }
    }

    public Result EnsureDirectory(string userId, Guid crateId, Guid? folderId)
    {
        return Result.Success(); // S3 folders are virtual
    }

    public async Task<Result> DeleteAllFilesInBucketAsync(Guid crateId)
    {
        var bucketName = $"crate-{crateId}".ToLowerInvariant();

        try
        {
            string? continuationToken = null;

            do
            {
                var request = new ListObjectsV2Request
                {
                    BucketName = bucketName,
                    ContinuationToken = continuationToken,
                    MaxKeys = 1000
                };

                var listResponse = await _s3Client.ListObjectsV2Async(request);
                if (listResponse?.S3Objects == null || listResponse.S3Objects.Count == 0)
                    break;

                var keysToDelete = listResponse.S3Objects
                    .Where(obj => !string.IsNullOrEmpty(obj.Key))
                    .Select(obj => new KeyVersion { Key = obj.Key })
                    .ToList();

                if (keysToDelete.Count > 0)
                {
                    var deleteRequest = new DeleteObjectsRequest
                    {
                        BucketName = bucketName,
                        Objects = keysToDelete
                    };

                    var deleteResponse = await _s3Client.DeleteObjectsAsync(deleteRequest);

                    if (deleteResponse.DeleteErrors != null && deleteResponse.DeleteErrors.Count != 0)
                    {
                        _logger.LogError("Errors deleting objects in bucket {Bucket}: {Errors}", bucketName,
                            deleteResponse.DeleteErrors);
                        return Result.Failure(
                            new StorageError($"Errors deleting some objects in bucket {bucketName}."));
                    }
                }

                continuationToken = listResponse.IsTruncated.GetValueOrDefault()
                    ? listResponse.NextContinuationToken
                    : null;
            } while (continuationToken != null);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete all files in bucket {Bucket}", bucketName);
            return Result.Failure(
                new StorageError($"Failed to delete all files in bucket {bucketName}. ({ex.Message})"));
        }
    }

    public async Task<Result> DeleteBucketAsync(Guid crateId)
    {
        var bucketName = $"crate-{crateId}".ToLowerInvariant();

        try
        {
            var deleteFilesResult = await DeleteAllFilesInBucketAsync(crateId);
            if (deleteFilesResult.IsFailure)
                return deleteFilesResult;

            await _s3Client.DeleteBucketAsync(bucketName);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete bucket {Bucket}", bucketName);
            return Result.Failure(new StorageError($"Failed to delete bucket {bucketName}. ({ex.Message})"));
        }
    }

    private static string GetObjectKey(string userId, Guid crateId, Guid? folderId, string fileName)
    {
        var parts = new List<string> { userId, crateId.ToString() };
        if (folderId.HasValue)
            parts.Add(folderId.Value.ToString());
        parts.Add(fileName);
        return string.Join("/", parts);
    }
}