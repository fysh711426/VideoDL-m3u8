namespace VideoDL_m3u8.Extensions
{
    public static class PathExtension
    {
        public static string FilterFileName(this string fileName)
        {
            return fileName
                .Replace("\r\n", " ")
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Replace("\\", "")
                .Replace("/", "")
                .Replace(":", "")
                .Replace("*", "")
                .Replace("?", "")
                .Replace("\"", "")
                .Replace("<", "")
                .Replace(">", "")
                .Replace("|", "")
                .Replace(".", "")
                .Trim();
        }
    }
}
