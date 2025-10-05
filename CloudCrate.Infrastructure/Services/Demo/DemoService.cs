using CloudCrate.Application.DTOs.File.Request;
using CloudCrate.Application.Interfaces.Storage;
using CloudCrate.Domain.Entities;
using CloudCrate.Domain.Enums;
using CloudCrate.Domain.ValueObjects;
using CloudCrate.Infrastructure.Identity;
using CloudCrate.Infrastructure.Persistence;
using CloudCrate.Infrastructure.Persistence.Mappers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CloudCrate.Infrastructure.Services.Demo;

public class DemoService
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<DemoService> _logger;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IStorageService _storageService;

    public DemoService(
        AppDbContext context,
        IWebHostEnvironment env,
        ILogger<DemoService> logger,
        UserManager<ApplicationUser> userManager,
        IStorageService storageService)
    {
        _context = context;
        _env = env;
        _logger = logger;
        _userManager = userManager;
        _storageService = storageService;
    }

    public async Task SeedDemoAccountsAsync()
    {
        if (await _context.Users.AnyAsync(u => u.IsDemoAccount))
        {
            _logger.LogInformation("Demo accounts already exist, skipping seed");
            return;
        }

        _logger.LogInformation("Seeding demo accounts...");

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var demoUsers = await CreateDemoUsersAsync();
            var mainDemoUser = demoUsers.First(u => u.Email == "demo@cloudcrate.com");
            await SeedDemoCratesAsync(mainDemoUser, demoUsers);

            await transaction.CommitAsync();
            _logger.LogInformation("Demo accounts seeded successfully");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Failed to seed demo accounts");
            throw;
        }
    }

    public async Task ResetDemoAccountAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);

        if (user == null || !user.IsDemoAccount)
        {
            return;
        }

        _logger.LogInformation("Resetting demo account: {Email}", user.Email);

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            await DeleteAllDemoDataAsync(userId);
            await ResetUserStorageAsync(user);

            var allDemoUsers = await _userManager.Users
                .Where(u => u.IsDemoAccount)
                .ToListAsync();

            await SeedDemoCratesAsync(user, allDemoUsers);

            await transaction.CommitAsync();
            _logger.LogInformation("Demo account reset successfully: {Email}", user.Email);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Failed to reset demo account: {UserId}", userId);
            throw;
        }
    }

    private async Task<List<ApplicationUser>> CreateDemoUsersAsync()
    {
        var demoUsers = new List<ApplicationUser>
        {
            new ApplicationUser
            {
                Email = "demo@cloudcrate.com",
                UserName = "demo@cloudcrate.com",
                DisplayName = "Demo User",
                EmailConfirmed = true,
                IsDemoAccount = true,
                IsAdmin = false,
                Plan = SubscriptionPlan.Mini,
                AllocatedStorageBytes = 0,
                UsedStorageBytes = 0,
                ProfilePictureUrl =
                    $"https://api.dicebear.com/7.x/fun-emoji/svg?seed={Uri.EscapeDataString("Demo User")}",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ApplicationUser
            {
                Email = "alice@example.com",
                UserName = "alice@example.com",
                DisplayName = "Alice Cooper",
                EmailConfirmed = true,
                IsDemoAccount = true,
                IsAdmin = false,
                Plan = SubscriptionPlan.Free,
                ProfilePictureUrl =
                    $"https://api.dicebear.com/7.x/fun-emoji/svg?seed={Uri.EscapeDataString("Alice Cooper")}",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ApplicationUser
            {
                Email = "bob@example.com",
                UserName = "bob@example.com",
                DisplayName = "Bob Smith",
                EmailConfirmed = true,
                IsDemoAccount = true,
                IsAdmin = false,
                Plan = SubscriptionPlan.Free,
                ProfilePictureUrl =
                    $"https://api.dicebear.com/7.x/fun-emoji/svg?seed={Uri.EscapeDataString("Bob Smith")}",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ApplicationUser
            {
                Email = "charlie@example.com",
                UserName = "charlie@example.com",
                DisplayName = "Charlie Davis",
                EmailConfirmed = true,
                IsDemoAccount = true,
                IsAdmin = false,
                Plan = SubscriptionPlan.Free,
                ProfilePictureUrl =
                    $"https://api.dicebear.com/7.x/fun-emoji/svg?seed={Uri.EscapeDataString("Charlie Davis")}",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        foreach (var user in demoUsers)
        {
            var result = await _userManager.CreateAsync(user, "Demo123!");
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Failed to create demo user {user.Email}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }
        }

        return demoUsers;
    }

    private async Task SeedDemoCratesAsync(ApplicationUser mainUser, List<ApplicationUser> allDemoUsers)
    {
        var alice = allDemoUsers.First(u => u.Email == "alice@example.com");
        var bob = allDemoUsers.First(u => u.Email == "bob@example.com");

        var crateConfigs = new[]
        {
            new
            {
                Name = "Demo Crate",
                Color = "#6DCC55",
                StorageGB = 5,
                Members = new[] { (alice.Id, CrateRole.Manager), (bob.Id, CrateRole.Member) }
            },
        };

        foreach (var config in crateConfigs)
        {
            var crate = Crate.Create(config.Name, mainUser.Id, config.StorageGB, config.Color);
            var crateEntity = crate.ToEntity();

            _context.Crates.Add(crateEntity);
            await _context.SaveChangesAsync();
            _context.Entry(crateEntity).State = EntityState.Detached;

            foreach (var (userId, role) in config.Members)
            {
                var member = CrateMember.Create(crate.Id, userId, role);
                var memberEntity = member.ToEntity(crate.Id);
                _context.CrateMembers.Add(memberEntity);
                await _context.SaveChangesAsync();
                _context.Entry(memberEntity).State = EntityState.Detached;
            }

            mainUser.AllocatedStorageBytes += StorageSize.FromGigabytes(config.StorageGB).Bytes;

            var rootFolder = crate.Folders.First(f => f.IsRoot);

            await SeedCrateContentAsync(crate.Id, rootFolder.Id, mainUser.Id);
        }

        await _userManager.UpdateAsync(mainUser);
    }

    private async Task SeedCrateContentAsync(Guid crateId, Guid rootFolderId, string userId)
    {
        var folder1 = CrateFolder.Create("Documents", crateId, rootFolderId, "#3B82F6", userId);
        var folder2 = CrateFolder.Create("Images", crateId, rootFolderId, "#10B981", userId);
        var folder3 = CrateFolder.Create("Audio", crateId, rootFolderId, "#A855F7", userId);
        var folder4 = CrateFolder.Create("Videos", crateId, rootFolderId, "#374151", userId);
        var folder5 = CrateFolder.Create("Reports", crateId, rootFolderId, "#FFBC2D", userId);
        var folder6 = CrateFolder.Create("SpreadSheets", crateId, rootFolderId, "#06B6D4", userId);

        _context.CrateFolders.AddRange(
            folder1.ToEntity(crateId),
            folder2.ToEntity(crateId),
            folder3.ToEntity(crateId),
            folder4.ToEntity(crateId),
            folder5.ToEntity(crateId),
            folder6.ToEntity(crateId)
        );

        await _context.SaveChangesAsync();

        await UploadAllFilesFromFolderAsync(crateId, folder1.Id, "Documents", userId);
        await UploadAllFilesFromFolderAsync(crateId, folder2.Id, "Images", userId);
        await UploadAllFilesFromFolderAsync(crateId, folder3.Id, "Audio", userId);
        await UploadAllFilesFromFolderAsync(crateId, folder4.Id, "Videos", userId);
        await UploadAllFilesFromFolderAsync(crateId, folder5.Id, "Reports", userId);
        await UploadAllFilesFromFolderAsync(crateId, folder6.Id, "SpreadSheets", userId);
    }

    private async Task UploadAllFilesFromFolderAsync(Guid crateId, Guid folderId, string folderName, string userId)
    {
        var assembly = typeof(DemoService).Assembly;
        var prefix = $"CloudCrate.Infrastructure.DemoData.Files.{folderName}.";

        var filesInFolder = assembly.GetManifestResourceNames()
            .Where(r => r.StartsWith(prefix))
            .ToList();

        _logger.LogInformation("Found {Count} files in {FolderName} folder", filesInFolder.Count, folderName);

        foreach (var resourceName in filesInFolder)
        {
            var fileName = resourceName.Substring(prefix.Length);

            await CreateDemoFileFromEmbeddedAsync(crateId, folderId, folderName, fileName, userId);
        }
    }

    private async Task CreateDemoFileFromEmbeddedAsync(Guid crateId, Guid folderId, string folderName, string fileName,
        string userId)
    {
        try
        {
            var fileBytes = await LoadEmbeddedFileAsync(folderName, fileName);

            if (fileBytes.Length < 100 &&
                System.Text.Encoding.UTF8.GetString(fileBytes).StartsWith("[Demo file placeholder"))
            {
                _logger.LogWarning("Skipping placeholder file: {FileName}", fileName);
                return;
            }

            var mimeType = GetMimeType(fileName);

            var uploadRequest = new FileUploadRequest
            {
                FileName = fileName,
                CrateId = crateId,
                FolderId = folderId,
                SizeInBytes = fileBytes.Length,
                MimeType = mimeType,
                Content = new MemoryStream(fileBytes)
            };

            var storageResult = await _storageService.SaveFileAsync(uploadRequest);
            if (storageResult.IsFailure)
            {
                _logger.LogWarning("Failed to save demo file {FileName}: {Error}",
                    fileName, storageResult.GetError().Message);
                return;
            }

            var file = CrateFile.Create(
                fileName,
                StorageSize.FromBytes(fileBytes.Length),
                mimeType,
                crateId,
                userId,
                folderId
            );
            file.SetObjectKey(storageResult.GetValue());

            _context.CrateFiles.Add(file.ToEntity(crateId));
            await _context.SaveChangesAsync();

            var crateEntity = await _context.Crates.FindAsync(crateId);
            if (crateEntity != null)
            {
                var crate = crateEntity.ToDomain();
                crate.ConsumeStorage(StorageSize.FromBytes(fileBytes.Length));
                crateEntity.UpdateEntity(crate);
                await _context.SaveChangesAsync();
            }

            var userEntity = await _userManager.FindByIdAsync(userId);
            if (userEntity != null)
            {
                userEntity.UsedStorageBytes += fileBytes.Length;
                userEntity.UpdatedAt = DateTime.UtcNow;
                await _userManager.UpdateAsync(userEntity);
            }

            _logger.LogInformation("Successfully uploaded {FileName} ({Size} bytes) to {FolderName}",
                fileName, fileBytes.Length, folderName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create demo file {FileName} in folder {FolderName}",
                fileName, folderName);
        }
    }

    private async Task<byte[]> LoadEmbeddedFileAsync(string folderName, string fileName)
    {
        var assembly = typeof(DemoService).Assembly;
        var resourceName = $"CloudCrate.Infrastructure.DemoData.Files.{folderName}.{fileName}";

        await using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            _logger.LogWarning("Embedded file not found: {ResourceName}", resourceName);
            return System.Text.Encoding.UTF8.GetBytes($"[Demo file placeholder: {fileName}]");
        }

        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream);
        return memoryStream.ToArray();
    }

    private string GetMimeType(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".txt" => "text/plain",
            ".md" => "text/markdown",
            ".pdf" => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            ".mp4" => "video/mp4",
            ".mov" => "video/quicktime",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".csv" => "text/csv",
            ".json" => "application/json",
            _ => "application/octet-stream"
        };
    }

    private async Task DeleteAllDemoDataAsync(string userId)
    {
        var ownedCrateIds = await _context.CrateMembers
            .Where(m => m.UserId == userId && m.Role == CrateRole.Owner)
            .Select(m => m.CrateId)
            .ToListAsync();

        foreach (var crateId in ownedCrateIds)
        {
            var storageResult = await _storageService.DeleteAllFilesForCrateAsync(crateId);
            if (storageResult.IsFailure)
            {
                _logger.LogWarning("Failed to delete storage for crate {CrateId}: {Error}",
                    crateId, storageResult.GetError().Message);
            }

            await _context.CrateFiles.Where(f => f.CrateId == crateId).ExecuteDeleteAsync();
            await _context.CrateFolders.Where(f => f.CrateId == crateId).ExecuteDeleteAsync();
            await _context.CrateMembers.Where(m => m.CrateId == crateId).ExecuteDeleteAsync();
            await _context.Crates.Where(c => c.Id == crateId).ExecuteDeleteAsync();
        }
    }

    private async Task ResetUserStorageAsync(ApplicationUser user)
    {
        user.AllocatedStorageBytes = 0;
        user.UsedStorageBytes = 0;
        user.UpdatedAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);
    }

    public async Task<bool> IsDemoAccountAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        return user?.IsDemoAccount == true;
    }
}