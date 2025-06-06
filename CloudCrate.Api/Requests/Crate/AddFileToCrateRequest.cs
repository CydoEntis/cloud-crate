using CloudCrate.Domain.Entities;

namespace CloudCrate.Api.Requests.Crate;

public class AddFileToCrateRequest
{
    public Guid CrateId { get; set; }
    public FileObject File { get; set; }
}