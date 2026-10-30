namespace FileToFiles.Services;

/// <summary>一批转换结束后的统计。</summary>
public sealed class ConversionCompletedEventArgs : EventArgs
{
    public ConversionCompletedEventArgs(int succeeded, int failed, int canceled)
    {
        Succeeded = succeeded;
        Failed = failed;
        Canceled = canceled;
    }

    public int Succeeded { get; }

    public int Failed { get; }

    public int Canceled { get; }
}
