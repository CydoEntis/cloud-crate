namespace CloudCrate.Application.Common.Interfaces;

public interface IFileStorageService
{
    Task<string> UploadAsync(Stream fileStream, string fileName);
    Task<Stream> DownloadAsync(string fileName);
}