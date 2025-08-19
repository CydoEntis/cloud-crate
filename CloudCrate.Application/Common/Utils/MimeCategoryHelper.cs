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

        // PDFs
        if (mimeType == "application/pdf") return "PDF";

        // Text & Document
        if (mimeType == "text/plain" ||
            mimeType == "application/msword" ||
            mimeType == "application/vnd.openxmlformats-officedocument.wordprocessingml.document" ||
            mimeType == "application/rtf" ||
            mimeType == "text/markdown" ||
            mimeType == "text/x-r-markdown" ||
            mimeType == "text/adoc") return "Text";

        // Spreadsheets
        if (mimeType == "application/vnd.ms-excel" ||
            mimeType == "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" ||
            mimeType == "text/csv" ||
            mimeType == "application/vnd.oasis.opendocument.spreadsheet") return "Spreadsheets";

        // Code / Markup
        if (mimeType.StartsWith("text/") || mimeType == "application/javascript" ||
            mimeType == "application/json" || mimeType == "application/xml") return "Code";


        return "Other";
    }
}