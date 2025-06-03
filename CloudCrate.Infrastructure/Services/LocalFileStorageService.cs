using Microsoft.Extensions.Options;
using CloudCrate.Application.Common.Interfaces;
using CloudCrate.Application.Common.Settings;

namespace CloudCrate.Infrastructure.Services;

public class LocalFileStorageService : IFileStorageService
{
    private readonly string _storagePath;

    public LocalFileStorageService(IOptions<StorageSettings> storageSettings)
    {
        _storagePath = storageSettings.Value.RootPath;

        if (!Directory.Exists(_storagePath))
        {
            Directory.CreateDirectory(_storagePath);
        }
    }

    public async Task<string> UploadAsync(Stream fileStream, string fileName)
    {
        var filePath = Path.Combine(_storagePath, fileName);

        await using var outputStream = File.Create(filePath);
        await fileStream.CopyToAsync(outputStream);

        return fileName;
    }

    public async Task<Stream> DownloadAsync(string fileName)
    {
        var filePath = Path.Combine(_storagePath, fileName);

        if (!File.Exists(filePath))
            throw new FileNotFoundException("File not found", fileName);

        var memoryStream = new MemoryStream();
        await using var fileStream = File.OpenRead(filePath);
        await fileStream.CopyToAsync(memoryStream);

        memoryStream.Position = 0;

        return memoryStream;
    }
}