namespace CloudCrate.Application.DTOs.Storage.Response;

public class StorageSummaryResponse
{
    public double TotalStorageMb { get; set; }
    public double UsedStorageMb { get; set; }
    public double AvailableStorageMb => TotalStorageMb - UsedStorageMb;
}