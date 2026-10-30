namespace FileToFiles.Utils;

public static class Formatters
{
    private static readonly string[] Units = { "B", "KB", "MB", "GB", "TB" };

    /// <summary>把字节数格式化为可读的尺寸文本，例如 "1.35 GB"。</summary>
    public static string FormatSize(long bytes)
    {
        if (bytes <= 0)
        {
            return "0 B";
        }

        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0
            ? $"{value:0} {Units[unit]}"
            : $"{value:0.##} {Units[unit]}";
    }

    /// <summary>把时长格式化为 mm:ss 或 h:mm:ss。</summary>
    public static string FormatDuration(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            return "-";
        }

        return duration.TotalHours >= 1
            ? duration.ToString(@"h\:mm\:ss")
            : duration.ToString(@"m\:ss");
    }
}
