using CloudCrate.Domain.Entities;

namespace CloudCrate.Application.DTOs.Crate.Request;

public class AddFileToCrateRequest
{
    public Guid CrateId { get; set; }
    public CrateFile File { get; set; }
}