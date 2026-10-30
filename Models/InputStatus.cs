namespace FileToFiles.Models;

/// <summary>
/// 输入文件的处理状态。
/// </summary>
public enum InputStatus
{
    /// <summary>已导入，等待探测。</summary>
    Pending,

    /// <summary>正在用 ffprobe 探测媒体信息。</summary>
    Probing,

    /// <summary>探测完成，可以转换。</summary>
    Ready,

    /// <summary>正在转换。</summary>
    Running,

    /// <summary>转换完成。</summary>
    Completed,

    /// <summary>失败（探测失败或转换失败）。</summary>
    Failed,

    /// <summary>用户取消。</summary>
    Canceled
}
