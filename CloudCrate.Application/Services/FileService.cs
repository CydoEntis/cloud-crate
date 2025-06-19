// using CloudCrate.Application.Common.Interfaces;
// using CloudCrate.Application.Common.Models;
// using CloudCrate.Application.DTOs.File;
// using Microsoft.EntityFrameworkCore;
// using CloudCrate.Domain.Entities;
//
// public class FileService : IFileService
// {
//     private readonly IFileStorageService _fileStorageService;
//     private readonly IAppDbContext _context;
//
//     public FileService(IFileStorageService fileStorageService, IAppDbContext context)
//     {
//         _fileStorageService = fileStorageService;
//         _context = context;
//     }
//
//     public async Task<Result<FileDto>> UploadFileAsync(string userId, Guid crateId, FileDto fileData)
//     {
//         var crate = await _context.Crates
//             .Include(c => c.Files)
//             .FirstOrDefaultAsync(c => c.Id == crateId && c.OwnerId == userId);
//
//         if (crate == null)
//             return Result<FileDto>.Failure("Crate not found or access denied.");
//
//         var storedName = $"{Guid.NewGuid()}_{fileData.FileName}";
//         var uploadResult = await _fileStorageService.UploadAsync(fileData.FileStream, storedName);
//         if (!uploadResult.Succeeded)
//             return Result<FileDto>.Failure(uploadResult.Errors.FirstOrDefault() ?? "File upload failed.");
//
//         var fileEntity = new FileObject
//         {
//             Id = Guid.NewGuid(),
//             FileName = fileData.FileName,
//             StoredName = storedName,
//             ContentType = fileData.ContentType,
//             Size = fileData.Size,
//             CrateId = crateId
//         };
//
//         crate.AddFile(fileEntity);
//         await _context.SaveChangesAsync();
//
//         var fileDto = new FileDto
//         {
//             FileId = fileEntity.Id,
//             CrateId = crateId,
//             FileName = fileEntity.FileName,
//             ContentType = fileEntity.ContentType,
//             Size = fileEntity.Size
//         };
//
//         return Result<FileDto>.Success(fileDto);
//     }
//
//     public async Task<Result<FileDto>> DownloadFileAsync(string userId, Guid crateId, Guid fileId)
//     {
//         var crate = await _context.Crates
//             .Include(c => c.Files)
//             .FirstOrDefaultAsync(c => c.Id == crateId && c.OwnerId == userId);
//
//         if (crate == null)
//             return Result<FileDto>.Failure("Crate not found or access denied.");
//
//         var fileEntity = crate.Files.FirstOrDefault(f => f.Id == fileId);
//         if (fileEntity == null)
//             return Result<FileDto>.Failure("File not found.");
//
//         var streamResult = await _fileStorageService.DownloadAsync(fileEntity.StoredName);
//         if (!streamResult.Succeeded || streamResult.Data == null)
//             return Result<FileDto>.Failure(streamResult.Errors.ToArray());
//
//         var downloadedFile = new FileDto
//         {
//             FileId = fileEntity.Id,
//             CrateId = crateId,
//             FileStream = streamResult.Data,
//             FileName = fileEntity.FileName,
//             ContentType = fileEntity.ContentType
//         };
//
//         return Result<FileDto>.Success(downloadedFile);
//     }
//
//     public async Task<Result<IEnumerable<FileDto>>> GetFilesInCrateAsync(string userId, Guid crateId)
//     {
//         var crate = await _context.Crates
//             .Include(c => c.Files)
//             .FirstOrDefaultAsync(c => c.Id == crateId && c.OwnerId == userId);
//
//         if (crate == null)
//             return Result<IEnumerable<FileDto>>.Failure("Crate not found or access denied.");
//
//         var filesDto = crate.Files.Select(f => new FileDto
//         {
//             FileId = f.Id,
//             CrateId = f.CrateId,
//             FileName = f.FileName,
//             ContentType = f.ContentType,
//             Size = f.Size
//         });
//
//         return Result<IEnumerable<FileDto>>.Success(filesDto);
//     }
//
//     public async Task<Result> DeleteFileAsync(string userId, Guid crateId, Guid fileId)
//     {
//         var crate = await _context.Crates
//             .Include(c => c.Files)
//             .FirstOrDefaultAsync(c => c.Id == crateId && c.OwnerId == userId);
//
//         if (crate == null)
//             return Result.Failure("Crate not found or access denied.");
//
//         var fileEntity = crate.Files.FirstOrDefault(f => f.Id == fileId);
//         if (fileEntity == null)
//             return Result.Failure("File not found.");
//
//         _context.FileObjects.Remove(fileEntity);
//         await _fileStorageService.DeleteAsync(fileEntity.StoredName);
//         await _context.SaveChangesAsync();
//
//         return Result.Success();
//     }
// }