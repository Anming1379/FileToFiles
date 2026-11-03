using System.IO;

namespace FileToFiles.Models;

/// <summary>
/// ffprobe 探测得到的媒体信息快照。
/// </summary>
public sealed class MediaInfo
{
    /// <summary>容器格式标签，例如 MP4 / MKV / MOV。</summary>
    public string Container { get; init; } = "-";

    /// <summary>ffprobe 报告的原始 format_name，例如 "mov,mp4,m4a,3gp,3g2,mj2"。</summary>
    public string? FormatName { get; init; }

    /// <summary>时长，未知时为 <see cref="TimeSpan.Zero"/>。</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>文件大小（字节）。</summary>
    public long SizeBytes { get; init; }

    /// <summary>总码率（bit/s），未知时为 0。</summary>
    public long BitRate { get; init; }

    public int Width { get; init; }

    public int Height { get; init; }

    public string? VideoCodec { get; init; }

    public string? VideoProfile { get; init; }

    public double? FrameRate { get; init; }

    public string? AudioCodec { get; init; }

    public int? AudioChannels { get; init; }

    public int? AudioSampleRate { get; init; }

    public long? AudioBitRate { get; init; }

    /// <summary>视频/图像的帧数；未知时为 null。</summary>
    public int? FrameCount { get; init; }

    /// <summary>源文件是图片（静态图或 GIF / 动画 WebP）。</summary>
    public bool IsImage { get; init; }

    public bool HasVideo => Width > 0 && Height > 0 && !string.IsNullOrEmpty(VideoCodec);

    public bool HasAudio => !string.IsNullOrEmpty(AudioCodec);

    /// <summary>
    /// 源文件是动图。ffprobe 对部分格式不报告 nb_frames，
    /// 因此除了帧数 > 1，还看编码器类型与时长。
    /// </summary>
    public bool IsAnimatedImage => IsImage
        && (FrameCount is > 1
            || (VideoCodec is "gif" or "webp_anim" or "apng" && Duration > TimeSpan.FromMilliseconds(80)));

    /// <summary>源文件类型描述：图片 / 动图 / 视频 / 音频。</summary>
    public string KindText => IsImage
        ? (IsAnimatedImage ? "动图" : "图片")
        : HasVideo ? "视频"
        : HasAudio ? "音频"
        : "未知";

    /// <summary>供“查看媒体详情”使用的可读文本。</summary>
    public string Describe(string filePath)
    {
        var lines = new List<string>
        {
            $"文件：{filePath}",
            $"容器：{Container}" + (string.IsNullOrEmpty(FormatName) ? string.Empty : $"（{FormatName}）"),
            $"时长：{(Duration > TimeSpan.Zero ? Duration.ToString(@"hh\:mm\:ss\.fff") : "未知")}",
            $"大小：{Utils.Formatters.FormatSize(SizeBytes)}",
            $"总码率：{(BitRate > 0 ? $"{BitRate / 1000} kbps" : "未知")}"
        };

        if (HasVideo)
        {
            var fps = FrameRate is > 0 ? $"，{FrameRate:0.###} fps" : string.Empty;
            var profile = string.IsNullOrEmpty(VideoProfile) ? string.Empty : $"，{VideoProfile}";
            var frames = FrameCount is > 0 ? $"，{FrameCount} 帧" : string.Empty;
            lines.Add($"{(IsImage ? "图像" : "视频")}：{VideoCodec}，{Width}x{Height}{fps}{profile}{frames}");
        }
        else
        {
            lines.Add("图像：无");
        }

        if (HasAudio)
        {
            var ch = AudioChannels is > 0 ? $"，{AudioChannels} 声道" : string.Empty;
            var sr = AudioSampleRate is > 0 ? $"，{AudioSampleRate} Hz" : string.Empty;
            var abr = AudioBitRate is > 0 ? $"，{AudioBitRate / 1000} kbps" : string.Empty;
            lines.Add($"音频：{AudioCodec}{ch}{sr}{abr}");
        }
        else
        {
            lines.Add("音频：无");
        }

        return string.Join(Environment.NewLine, lines);
    }
}
