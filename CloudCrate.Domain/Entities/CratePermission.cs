using CloudCrate.Domain.Enums;

namespace CloudCrate.Domain.Entities;

public class CratePermission
{
    public Guid Id { get; set; }

    public Guid CrateId { get; set; }
    public Crate Crate { get; set; }

    public string UserId { get; set; }

    public CrateRole Role { get; set; }

    public bool? CanUpload { get; set; }
    public bool? CanDownload { get; set; }
    public bool? CanDelete { get; set; }

    public DateTime CreatedAt { get; set; }
}