namespace FileToFiles.Services;

/// <summary>一批转换结束后的统计。</summary>
public sealed class ConversionCompletedEventArgs : EventArgs
{
    public ConversionCompletedEventArgs(int succeeded, int failed, int canceled, int skipped = 0)
    {
        Succeeded = succeeded;
        Failed = failed;
        Canceled = canceled;
        Skipped = skipped;
    }

    public int Succeeded { get; }

    public int Failed { get; }

    public int Canceled { get; }

    /// <summary>因源与目标类型不兼容而跳过的数量。</summary>
    public int Skipped { get; }
}
