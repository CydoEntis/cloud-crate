namespace CloudCrate.Domain.ValueObjects;

public class StorageSize
{
    public long Bytes { get; }

    private StorageSize(long bytes) => Bytes = bytes;

    public static StorageSize FromGigabytes(long gb)
        => new StorageSize(gb * 1024L * 1024 * 1024);

    public static StorageSize FromBytes(long bytes)
        => new StorageSize(bytes);

    public double ToGigabytes()
        => Bytes / 1024d / 1024d / 1024d;
}