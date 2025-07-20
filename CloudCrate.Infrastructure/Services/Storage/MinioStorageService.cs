using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using CloudCrate.Application.Common.Errors;
using CloudCrate.Application.Common.Models;
using CloudCrate.Application.Interfaces.Storage;

namespace CloudCrate.Infrastructure.Services.Storage;

public class MinioStorageService : IStorageService
{
    private readonly IAmazonS3 _s3Client;

    public MinioStorageService(IAmazonS3 s3Client)
    {
        _s3Client = s3Client;
    }

    private string GetObjectKey(string userId, Guid crateId, Guid? folderId, string fileName)
    {
        var parts = new List<string> { userId, crateId.ToString() };
        if (folderId.HasValue)
            parts.Add(folderId.Value.ToString());
        parts.Add(fileName);
        return string.Join("/", parts);
    }

    public async Task<Result> SaveFileAsync(string userId, Guid crateId, Guid? folderId, string fileName,
        Stream content)
    {
        var bucketName = $"crate-{crateId}".ToLowerInvariant();

        try
        {
            if (!await AmazonS3Util.DoesS3BucketExistV2Async(_s3Client, bucketName))
            {
                await _s3Client.PutBucketAsync(bucketName);
            }

            var key = GetObjectKey(userId, crateId, folderId, fileName);

            var putRequest = new PutObjectRequest
            {
                BucketName = bucketName,
                Key = key,
                InputStream = content
            };

            await _s3Client.PutObjectAsync(putRequest);
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
            return Result<byte[]>.Failure(Errors.Files.NotFound);
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
        var bucketName = $"crate-{crateId}".ToLowerInvariant();
        var key = GetObjectKey(userId, crateId, folderId, fileName);

        try
        {
            await _s3Client.DeleteObjectAsync(bucketName, key);
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
        // Optional: Implement if needed. For now, just return true as placeholder.
        return true;
    }

    public Result EnsureDirectory(string userId, Guid crateId, Guid? folderId)
    {
        // S3-compatible storage doesn't require directory creation, so success by default.
        return Result.Success();
    }

    public async Task<Result> DeleteFilesAsync(string bucketName, IEnumerable<string> objectKeys)
    {
        try
        {
            var deleteRequest = new DeleteObjectsRequest
            {
                BucketName = bucketName,
                Objects = objectKeys.Select(key => new KeyVersion { Key = key }).ToList()
            };

            var response = await _s3Client.DeleteObjectsAsync(deleteRequest);

            if (response.DeleteErrors.Any())
            {
                var errorMessages = string.Join("; ", response.DeleteErrors.Select(e => $"{e.Key}: {e.Message}"));
                return Result.Failure(Errors.Files.DeleteFailed with
                {
                    Message = $"{Errors.Files.DeleteFailed.Message}: {errorMessages}"
                });
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