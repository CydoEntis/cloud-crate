namespace CloudCrate.Application.DTOs.Storage.Response;

public class StorageSummaryResponse
{
    public double TotalStorageMb { get; set; }
    public double UsedStorageMb { get; set; }
    public double AllocatedStorageMb { get; set; }

    public double AvailableStorageMb => Math.Max(0, TotalStorageMb - UsedStorageMb);
    public double RemainingAllocatableMb => Math.Max(0, TotalStorageMb - AllocatedStorageMb);
}