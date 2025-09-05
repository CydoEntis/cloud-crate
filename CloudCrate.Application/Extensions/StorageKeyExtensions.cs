namespace CloudCrate.Application.Extensions;

public static class StorageKeyExtensions
{
    public static string GetObjectKey(this string userId, Guid crateId, Guid? folderId, string fileName)
    {
        var parts = new List<string> { userId, crateId.ToString() };
        if (folderId.HasValue)
            parts.Add(folderId.Value.ToString());
        parts.Add(fileName);
        return string.Join("/", parts);
    }
}