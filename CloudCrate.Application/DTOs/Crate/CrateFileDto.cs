using CloudCrate.Domain.Entities;

namespace CloudCrate.Application.DTOs.Crate;

public class CrateFileDto
{
    public Guid CrateId { get; set; }
    public FileObject File { get; set; } = null!;
}