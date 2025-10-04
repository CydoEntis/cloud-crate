using CloudCrate.Domain.Enums;
using CloudCrate.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CloudCrate.Infrastructure.Services.Database;

public class DatabaseSeederService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DatabaseSeederService> _logger;

    public DatabaseSeederService(
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        ILogger<DatabaseSeederService> logger)
    {
        _userManager = userManager;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        try
        {
            await SeedDefaultAdminAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while seeding database");
            throw; // Re-throw to prevent app startup if seeding fails
        }
    }

    private async Task SeedDefaultAdminAsync()
    {
        var existingAdmins = _userManager.Users.Where(u => u.IsAdmin).ToList();

        if (existingAdmins.Any())
        {
            _logger.LogInformation("Admin users already exist. Skipping default admin creation");
            return;
        }

        var adminEmail = _configuration["DefaultAdmin:Email"];
        var adminPassword = _configuration["DefaultAdmin:Password"];
        var adminDisplayName = _configuration["DefaultAdmin:DisplayName"] ?? "System Administrator";

        if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
        {
            _logger.LogWarning("Default admin credentials not configured. Skipping admin creation");
            return;
        }

        var existingUser = await _userManager.FindByEmailAsync(adminEmail);
        if (existingUser != null)
        {
            _logger.LogInformation("User {Email} exists but is not admin. Promoting to admin", adminEmail);
            existingUser.IsAdmin = true;
            existingUser.UpdatedAt = DateTime.UtcNow;

            var updateResult = await _userManager.UpdateAsync(existingUser);
            if (updateResult.Succeeded)
            {
                _logger.LogInformation("Successfully promoted user {Email} to admin", adminEmail);
            }
            else
            {
                _logger.LogError("Failed to promote user {Email} to admin: {Errors}",
                    adminEmail, string.Join(", ", updateResult.Errors.Select(e => e.Description)));
            }

            return;
        }

        var adminUser = new ApplicationUser
        {
            Email = adminEmail,
            UserName = adminEmail,
            DisplayName = adminDisplayName,
            ProfilePictureUrl =
                $"https://api.dicebear.com/7.x/fun-emoji/svg?seed={Uri.EscapeDataString(adminDisplayName)}",
            IsAdmin = true,
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Plan = SubscriptionPlan.Max
        };

        var createResult = await _userManager.CreateAsync(adminUser, adminPassword);

        if (createResult.Succeeded)
        {
            _logger.LogInformation("Successfully created default admin user: {Email}", adminEmail);
        }
        else
        {
            _logger.LogError("Failed to create default admin user: {Errors}",
                string.Join(", ", createResult.Errors.Select(e => e.Description)));

            throw new InvalidOperationException(
                $"Failed to create default admin user: {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
        }
    }
}