using CloudCrate.Domain.Entities;

namespace CloudCrate.Application.DTOs.Crate;

public class AddFileToCrateRequest
{
    public Guid CrateId { get; set; }
    public FileObject File { get; set; }
}