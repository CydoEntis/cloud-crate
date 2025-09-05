using CloudCrate.Application.DTOs;
using CloudCrate.Application.Models;

namespace CloudCrate.Application.Interfaces.Bulk;

public interface IBulkService
{
    Task<Result> MoveAsync(MultipleMoveRequest request, string userId);
    Task<Result> DeleteAsync(MultipleDeleteRequest request, string userId);
    Task<Result> RestoreAsync(MultipleRestoreRequest request, string userId);
}