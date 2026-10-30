using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FileToFiles.Models;

/// <summary>
/// 输入文件列表中的一行。
/// </summary>
public partial class InputFileItem : ObservableObject
{
    public InputFileItem(string fullPath, long fileSizeBytes)
    {
        FullPath = fullPath;
        FileSize = fileSizeBytes;
    }

    public string FullPath { get; }

    public string FileName => Path.GetFileName(FullPath);

    /// <summary>导入时记录的文件大小，探测完成后由 ffprobe 结果覆盖。</summary>
    public long FileSize { get; private set; }

    [ObservableProperty]
    private InputStatus _status = InputStatus.Pending;

    [ObservableProperty]
    private MediaInfo? _media;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private string? _error;

    partial void OnStatusChanged(InputStatus value)
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(ProgressText));
        OnPropertyChanged(nameof(IsRunning));
    }

    partial void OnProgressChanged(double value)
    {
        OnPropertyChanged(nameof(ProgressText));
    }

    public bool IsRunning => Status == InputStatus.Running;

    partial void OnMediaChanged(MediaInfo? value)
    {
        if (value is { SizeBytes: > 0 })
        {
            FileSize = value.SizeBytes;
        }

        // 媒体信息变化会影响所有派生列。
        OnPropertyChanged(string.Empty);
    }

    public string StatusText => Status switch
    {
        InputStatus.Pending => "待处理",
        InputStatus.Probing => "探测中",
        InputStatus.Ready => "就绪",
        InputStatus.Running => "转换中",
        InputStatus.Completed => "已完成",
        InputStatus.Failed => "失败",
        InputStatus.Canceled => "已取消",
        _ => "-"
    };

    public string ContainerText => Media?.Container ?? "-";

    public string VideoCodecText => Media is null ? "-" : Media.VideoCodec ?? "无";

    public string AudioCodecText => Media is null ? "-" : Media.AudioCodec ?? "无";

    public string DurationText => Media is { Duration.TotalSeconds: > 0 } m
        ? (m.Duration.TotalHours >= 1 ? m.Duration.ToString(@"h\:mm\:ss") : m.Duration.ToString(@"m\:ss"))
        : "-";

    public string ResolutionText => Media is { HasVideo: true } m ? $"{m.Width}x{m.Height}" : "-";

    public string SizeText => Utils.Formatters.FormatSize(FileSize);

    public string ProgressText => Status switch
    {
        InputStatus.Running => $"{Progress:0}%",
        InputStatus.Completed => "100%",
        _ => "-"
    };

    /// <summary>Material Symbols 字形：视频 / 音频 / 未知。</summary>
    public string KindGlyph => Media is null
        ? "\uE24D" // insert_drive_file
        : Media.HasVideo
            ? "\uE02C" // movie
            : "\uE405"; // music_note

    /// <summary>重新探测前清空状态。</summary>
    public void ResetForProbe()
    {
        Media = null;
        Error = null;
        Progress = 0;
        Status = InputStatus.Pending;
    }
}
