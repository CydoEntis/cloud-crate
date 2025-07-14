namespace CloudCrate.Api.Common.Extensions;

public static class AllowedFileExtensions
{
    public static readonly string[] Extensions = new[]
    {
        // 📄 Text & Document
        ".txt", ".doc", ".docx", ".rtf", ".md", ".rmd", ".adoc", ".pdf",

        // 📊 Spreadsheets
        ".xls", ".xlsx", ".csv", ".ods",

        // 🖼️ Images
        ".jpg", ".jpeg", ".png", ".gif", ".svg", ".webp", ".bmp", ".tif", ".tiff", ".ico", ".heic",

        // 🎧 Audio
        ".mp3", ".wav", ".flac", ".ogg", ".m4a",

        // 🎥 Video
        ".mp4", ".mov", ".avi", ".mkv", ".webm",

        // 🗜️ Archives
        ".zip", ".rar", ".7z", ".tar", ".gz", ".bz2",

        // ⚙️ Config / Build / Markup / Code / Scripts
        ".json", ".xml", ".yaml", ".yml", ".toml", ".ini", ".env", ".dockerfile", ".makefile", ".tsconfig",
        ".prettierrc", ".eslintignore", ".gitignore", ".mdx", ".njk", ".hbs", ".html", ".css", ".scss", ".sass",
        ".js", ".jsx", ".ts", ".tsx", ".vue", ".svelte", ".astro", ".solid", ".java", ".kt", ".kotlin", ".scala",
        ".cs", ".c", ".cpp", ".h", ".hpp", ".swift", ".m", ".objc", ".py", ".pyw", ".ipynb", ".sh", ".bash", ".zsh",
        ".bat", ".ps1", ".cmd", ".vb", ".hs", ".ml", ".clj", ".cljs", ".ex", ".exs", ".erl", ".r", ".jl", ".tex",
        ".go", ".rs", ".dart", ".pl", ".lua", ".asm", ".s", ".pom", ".gradle", ".lock", ".yarn", ".npmrc",
        ".packagejson", ".rollup", ".vite", ".webpack", ".ahk", ".mathematica", ".parquet", ".db", ".sql", ".log",
        ".bin", ".wasm", ".githubactions", ".circleci", ".travis"
    };
}