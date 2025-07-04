namespace CloudCrate.Application.Common.Utils;

public static class MimeCategoryHelper
{
    public static string GetMimeCategory(string mimeType)
    {
        if (string.IsNullOrEmpty(mimeType))
            return "Other";

        if (mimeType.StartsWith("image/")) return "Images";
        if (mimeType.StartsWith("video/")) return "Videos";
        if (mimeType.StartsWith("audio/")) return "Audio";
        if (mimeType == "application/pdf") return "PDF";
        if (mimeType == "text/plain" || mimeType == "application/msword" || mimeType == "application/rtf")
            return "Text";
        if (mimeType == "application/vnd.ms-excel" ||
            mimeType == "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" || mimeType == "text/csv")
            return "Spreadsheets";
        if (mimeType == "application/javascript" || mimeType == "text/javascript" ||
            mimeType == "text/css" || mimeType == "application/json" ||
            mimeType == "text/html" || mimeType == "application/xml")
            return "Code";
        if (mimeType == "application/zip" || mimeType == "application/x-rar-compressed" ||
            mimeType == "application/x-7z-compressed" || mimeType == "application/x-tar")
            return "Archives";

        return "Other";
    }
}