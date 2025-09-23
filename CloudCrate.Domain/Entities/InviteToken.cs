// CloudCrate.Domain.Entities/InviteToken.cs

using System.Security.Cryptography;

namespace CloudCrate.Domain.Entities;

public class InviteToken
{
    public string Id { get; private set; } = string.Empty;
    public string Token { get; private set; } = string.Empty;
    public string CreatedByUserId { get; private set; } = string.Empty;
    public string? Email { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime? UsedAt { get; private set; }
    public string? UsedByUserId { get; private set; }

    public bool IsUsed => UsedAt.HasValue;
    public bool IsExpired => DateTime.UtcNow > ExpiresAt;
    public bool IsValid => !IsUsed && !IsExpired;

    private InviteToken()
    {
    }

    internal InviteToken(string id, string token, string createdByUserId, string? email,
        DateTime createdAt, DateTime expiresAt, DateTime? usedAt, string? usedByUserId)
    {
        Id = id;
        Token = token;
        CreatedByUserId = createdByUserId;
        Email = email;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
        UsedAt = usedAt;
        UsedByUserId = usedByUserId;
    }

    public static InviteToken Rehydrate(string id, string token, string createdByUserId, string? email,
        DateTime createdAt, DateTime expiresAt, DateTime? usedAt, string? usedByUserId)
    {
        return new InviteToken(id, token, createdByUserId, email, createdAt, expiresAt, usedAt, usedByUserId);
    }

    public static InviteToken
        Create(string createdByUserId, string? email = null, int expiryHours = 168) // 7 days default
    {
        return new InviteToken
        {
            Id = Guid.NewGuid().ToString(),
            Token = GenerateSecureToken(),
            CreatedByUserId = createdByUserId,
            Email = email,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(expiryHours),
            UsedAt = null,
            UsedByUserId = null
        };
    }

    public void MarkAsUsed(string userId)
    {
        if (IsUsed)
            throw new InvalidOperationException("Invite token has already been used");

        if (IsExpired)
            throw new InvalidOperationException("Invite token has expired");

        UsedAt = DateTime.UtcNow;
        UsedByUserId = userId;
    }

    public bool CanBeUsedBy(string email)
    {
        if (!IsValid) return false;

        if (string.IsNullOrWhiteSpace(Email)) return true;

        return Email.Equals(email, StringComparison.OrdinalIgnoreCase);
    }

    private static string GenerateSecureToken()
    {
        var randomBytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
    }
}