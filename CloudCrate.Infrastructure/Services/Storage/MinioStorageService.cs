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
    private const string BucketName = "cloud-crate";
    private const int DeleteBatchSize = 1000;
    private const int MaxParallelBatches = 5;

    public MinioStorageService(IAmazonS3 s3Client, ILogger<MinioStorageService> logger)
    {
        _s3Client = s3Client;
        _logger = logger;
    }

    public async Task<Result> EnsureBucketExistsAsync()
    {
        try
        {
            var exists = await AmazonS3Util.DoesS3BucketExistV2Async(_s3Client, BucketName);
            if (!exists)
            {
                await _s3Client.PutBucketAsync(BucketName);
                _logger.LogInformation("Created S3 bucket {BucketName}", BucketName);
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure bucket exists");
            return Result.Failure(new StorageError($"Failed to ensure bucket exists: {ex.Message}"));
        }
    }

    public async Task<Result<string>> SaveFileAsync(FileUploadRequest request)
    {
        var bucketResult = await EnsureBucketExistsAsync();
        if (!bucketResult.IsSuccess) return Result<string>.Failure(bucketResult.GetError());

        var key = GetObjectKey(request.CrateId, request.FolderId, request.FileName);

        try
        {
            await _s3Client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = BucketName,
                Key = key,
                InputStream = request.Content
            });

            return Result<string>.Success(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save file {FileName}", request.FileName);
            return Result<string>.Failure(new FileSaveError($"Failed to save file {request.FileName}. ({ex.Message})"));
        }
    }

    public async Task<Result<List<string>>> SaveFilesAsync(List<FileUploadRequest> requests)
    {
        var uploadedKeys = new List<string>();

        try
        {
            foreach (var request in requests)
            {
                var result = await SaveFileAsync(request);
                if (!result.IsSuccess)
                {
                    if (uploadedKeys.Any())
                    {
                        _logger.LogWarning("Upload failed, cleaning up {Count} uploaded files", uploadedKeys.Count);
                        await DeleteKeysAsync(uploadedKeys);
                    }

                    return Result<List<string>>.Failure(result.GetError());
                }

                uploadedKeys.Add(result.GetValue());
            }

            return Result<List<string>>.Success(uploadedKeys);
        }
        catch (Exception ex)
        {
            // Cleanup on exception
            if (uploadedKeys.Any())
            {
                await DeleteKeysAsync(uploadedKeys);
            }

            return Result<List<string>>.Failure(new StorageError($"Bulk upload failed: {ex.Message}"));
        }
    }

    public async Task<Result<byte[]>> ReadFileAsync(Guid crateId, Guid? folderId, string fileName)
    {
        var key = GetObjectKey(crateId, folderId, fileName);
        try
        {
            var response = await _s3Client.GetObjectAsync(BucketName, key);
            using var ms = new MemoryStream();
            await response.ResponseStream.CopyToAsync(ms);
            return Result<byte[]>.Success(ms.ToArray());
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return Result<byte[]>.Failure(new FileNotFoundError($"File {fileName} not found"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read file {Key}", key);
            return Result<byte[]>.Failure(new FileReadError($"Failed to read file {fileName}. ({ex.Message})"));
        }
    }

    public async Task<bool> FileExistsAsync(Guid crateId, Guid? folderId, string fileName)
    {
        var key = GetObjectKey(crateId, folderId, fileName);
        try
        {
            await _s3Client.GetObjectMetadataAsync(BucketName, key);
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

    public async Task<Result> DeleteFileAsync(Guid crateId, Guid? folderId, string fileName)
    {
        var key = GetObjectKey(crateId, folderId, fileName);
        return await DeleteKeysAsync(new List<string> { key });
    }

    public async Task<Result> DeleteFilesAsync(Guid crateId, Guid? folderId, IEnumerable<string> fileNames)
    {
        var keys = fileNames.Select(f => GetObjectKey(crateId, folderId, f)).ToList();
        return await DeleteKeysAsync(keys);
    }

    public async Task<Result> DeleteAllFilesForCrateAsync(Guid crateId)
    {
        var prefix = $"{crateId}/";
        string? continuationToken = null;

        do
        {
            try
            {
                var listResponse = await _s3Client.ListObjectsV2Async(new ListObjectsV2Request
                {
                    BucketName = BucketName,
                    Prefix = prefix,
                    ContinuationToken = continuationToken,
                    MaxKeys = DeleteBatchSize
                });

                if (listResponse.S3Objects.Any())
                {
                    var keys = listResponse.S3Objects.Select(o => o.Key).ToList();
                    var deleteResult = await DeleteKeysAsync(keys);
                    if (!deleteResult.IsSuccess)
                    {
                        _logger.LogError("Failed to delete batch for crate {CrateId}: {Error}",
                            crateId, deleteResult.GetError().Message);
                        return deleteResult;
                    }
                }

                continuationToken = listResponse.IsTruncated.GetValueOrDefault()
                    ? listResponse.NextContinuationToken
                    : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to list objects for crate {CrateId}", crateId);
                return Result.Failure(new StorageError($"Failed to list files for crate: {ex.Message}"));
            }
        } while (continuationToken != null);

        return Result.Success();
    }

    private async Task<Result> DeleteKeysAsync(List<string> keys)
    {
        var batches = keys.Batch(DeleteBatchSize).ToList();

        foreach (var batchGroup in batches.Batch(MaxParallelBatches))
        {
            var tasks = batchGroup.Select(batch => DeleteBatchAsync(batch.ToList())).ToList();
            var results = await Task.WhenAll(tasks);

            var result = results.FirstOrDefault(r => !r.IsSuccess);
            if (result.IsFailure) return result;
        }

        return Result.Success();
    }

    private async Task<Result> DeleteBatchAsync(List<string> batch)
    {
        try
        {
            var response = await _s3Client.DeleteObjectsAsync(new DeleteObjectsRequest
            {
                BucketName = BucketName,
                Objects = batch.Select(k => new KeyVersion { Key = k }).ToList()
            });

            if (response.DeleteErrors?.Count > 0)
            {
                _logger.LogError("Errors deleting objects in bucket {Bucket}: {Errors}", BucketName,
                    response.DeleteErrors);
                return Result.Failure(new StorageError($"Failed to delete some objects in bucket {BucketName}."));
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete objects in bucket {Bucket}", BucketName);
            return Result.Failure(new StorageError($"Failed to delete objects in bucket {BucketName}. ({ex.Message})"));
        }
    }

    public async Task<Result<string>> GetFileUrlAsync(Guid crateId, Guid? folderId, string fileName,
        TimeSpan? expiry = null)
    {
        var key = GetObjectKey(crateId, folderId, fileName);

        try
        {
            var urlExpiry = expiry ?? TimeSpan.FromMinutes(15);
            var request = new GetPreSignedUrlRequest
            {
                BucketName = BucketName,
                Key = key,
                Expires = DateTime.UtcNow.Add(urlExpiry),
                Verb = HttpVerb.GET
            };
            return Result<string>.Success(_s3Client.GetPreSignedURL(request));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate presigned URL for {Key}", key);
            return Result<string>.Failure(new FileAccessError($"Failed to generate access URL. ({ex.Message})"));
        }
    }

    public async Task<Result> MoveFileAsync(Guid crateId, Guid? oldFolderId, Guid? newFolderId, string fileName)
    {
        var oldKey = GetObjectKey(crateId, oldFolderId, fileName);
        var newKey = GetObjectKey(crateId, newFolderId, fileName);

        try
        {
            await _s3Client.CopyObjectAsync(BucketName, oldKey, BucketName, newKey);
            await _s3Client.DeleteObjectAsync(BucketName, oldKey);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to move file {FileName} from {OldKey} to {NewKey}", fileName, oldKey, newKey);
            return Result.Failure(new StorageError($"Failed to move file: {ex.Message}"));
        }
    }

    public async Task<Result> MoveFolderAsync(Guid crateId, Guid folderId, Guid? newParentId)
    {
        var oldPrefix = $"{crateId}/{folderId}/";
        var newPrefix = newParentId.HasValue
            ? $"{crateId}/{newParentId}/{folderId}/"
            : $"{crateId}/{folderId}/";

        string? continuationToken = null;
        var allKeys = new List<string>();

        do
        {
            var listResponse = await _s3Client.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = BucketName,
                Prefix = oldPrefix,
                ContinuationToken = continuationToken,
                MaxKeys = DeleteBatchSize
            });

            allKeys.AddRange(listResponse.S3Objects.Select(o => o.Key));
            continuationToken =
                listResponse.IsTruncated.GetValueOrDefault() ? listResponse.NextContinuationToken : null;
        } while (continuationToken != null);

        foreach (var oldKey in allKeys)
        {
            var newKey = oldKey.Replace(oldPrefix, newPrefix);
            await _s3Client.CopyObjectAsync(BucketName, oldKey, BucketName, newKey);
        }

        return await DeleteKeysAsync(allKeys);
    }

    private static string GetObjectKey(Guid crateId, Guid? folderId, string fileName)
    {
        var parts = new List<string> { crateId.ToString() };
        if (folderId.HasValue) parts.Add(folderId.Value.ToString());
        parts.Add(fileName);
        return string.Join("/", parts);
    }
}