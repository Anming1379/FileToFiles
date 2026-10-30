using System.ComponentModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FileToFiles.Models;

/// <summary>目标媒体类型。</summary>
public enum MediaKind
{
    Video,
    Audio
}

/// <summary>输出容器。</summary>
public enum ContainerFormat
{
    Mp4,
    Mkv,
    Webm,
    Mov,
    Mp3,
    M4a,
    Flac,
    Wav,
    Ogg,

    /// <summary>PNG 图像序列（每帧一个文件，输出到同名文件夹）。</summary>
    ImageSequence
}

public enum VideoCodecKind
{
    H264,
    H265,
    Vp9,
    Av1,
    Copy,

    /// <summary>PNG 图像序列编码。</summary>
    Png
}

public enum AudioCodecKind
{
    Aac,
    Mp3,
    Opus,
    Flac,
    Pcm16,
    Vorbis,
    Copy,
    None
}

public enum QualityMode
{
    Crf,
    Bitrate
}

public enum ResolutionOption
{
    Source,
    P1080,
    P720,
    P480
}

public enum FrameRateOption
{
    Source,
    Fps60,
    Fps30,
    Fps25,
    Fps24,
    Fps15
}

public enum HardwareAcceleration
{
    Auto,
    Off,
    Nvenc,
    Qsv,
    Amf
}

/// <summary>下拉框选项：值 + 显示文本。</summary>
public sealed record Choice<T>(T Value, string Display)
{
    // 保证即使没有绑定 ItemTemplate 也不会显示成记录类型的默认字符串。
    public override string ToString() => Display;
}

/// <summary>音频质量选项（固定码率或 VBR 质量）。</summary>
public sealed record AudioQualityOption(string Key, string Display, int BitrateKbps, int? VbrQuality);

/// <summary>容器与编码器的显示文本、扩展名等辅助方法。</summary>
public static class FormatCatalog
{
    public static string ToExtension(this ContainerFormat container) => container switch
    {
        ContainerFormat.Mp4 => "mp4",
        ContainerFormat.Mkv => "mkv",
        ContainerFormat.Webm => "webm",
        ContainerFormat.Mov => "mov",
        ContainerFormat.Mp3 => "mp3",
        ContainerFormat.M4a => "m4a",
        ContainerFormat.Flac => "flac",
        ContainerFormat.Wav => "wav",
        ContainerFormat.Ogg => "ogg",
        ContainerFormat.ImageSequence => "png",
        _ => "mp4"
    };

    public static MediaKind KindOf(this ContainerFormat container) => container
        is ContainerFormat.Mp4 or ContainerFormat.Mkv or ContainerFormat.Webm or ContainerFormat.Mov
        or ContainerFormat.ImageSequence
        ? MediaKind.Video
        : MediaKind.Audio;

    public static string ContainerName(this ContainerFormat container) => container switch
    {
        ContainerFormat.Mp4 => "MP4",
        ContainerFormat.Mkv => "MKV",
        ContainerFormat.Webm => "WebM",
        ContainerFormat.Mov => "MOV",
        ContainerFormat.Mp3 => "MP3",
        ContainerFormat.M4a => "M4A",
        ContainerFormat.Flac => "FLAC",
        ContainerFormat.Wav => "WAV",
        ContainerFormat.Ogg => "OGG",
        ContainerFormat.ImageSequence => "PNG 序列",
        _ => "MP4"
    };

    /// <summary>例如 "H.264 MP4"、"AV1 MP4"、"MP3"。</summary>
    public static string TargetTitle(this ContainerFormat container, VideoCodecKind videoCodec) => container switch
    {
        ContainerFormat.Mp4 => videoCodec switch
        {
            VideoCodecKind.H264 => "H.264 MP4",
            VideoCodecKind.H265 => "H.265 MP4",
            VideoCodecKind.Vp9 => "VP9 MP4",
            VideoCodecKind.Av1 => "AV1 MP4",
            _ => "复制流 MP4"
        },
        ContainerFormat.Mkv => videoCodec switch
        {
            VideoCodecKind.H264 => "H.264 MKV",
            VideoCodecKind.H265 => "H.265 MKV",
            VideoCodecKind.Vp9 => "VP9 MKV",
            VideoCodecKind.Av1 => "AV1 MKV",
            _ => "复制流 MKV"
        },
        ContainerFormat.Webm => "VP9 WebM",
        ContainerFormat.Mov => videoCodec switch
        {
            VideoCodecKind.H264 => "H.264 MOV",
            VideoCodecKind.H265 => "H.265 MOV",
            VideoCodecKind.Av1 => "AV1 MOV",
            _ => "复制流 MOV"
        },
        ContainerFormat.M4a => "AAC M4A",
        ContainerFormat.Ogg => "OGG Vorbis",
        ContainerFormat.ImageSequence => "PNG 图像序列",
        _ => container.ContainerName()
    };
}

/// <summary>
/// 一套转换参数。既是内置/自定义预设，也是当前批次正在使用的参数（可被用户就地修改）。
/// </summary>
public partial class ConversionPreset : ObservableObject
{
    // 构造期间不标记为“已修改”：只有记录过模板快照后，用户的改动才算修改。
    private bool _suspendDirty = true;

    public ConversionPreset(string name)
    {
        Name = name;
    }

    /// <summary>预设名称。</summary>
    public string Name { get; set; }

    /// <summary>内置预设标识，用于“恢复默认”；自定义预设为 null。</summary>
    public string? BuiltInKey { get; init; }

    public bool IsBuiltIn => BuiltInKey is not null;

    [ObservableProperty]
    private ContainerFormat _container = ContainerFormat.Mp4;

    [ObservableProperty]
    private VideoCodecKind _videoCodec = VideoCodecKind.H264;

    [ObservableProperty]
    private QualityMode _qualityMode = QualityMode.Crf;

    [ObservableProperty]
    private int _crf = 18;

    [ObservableProperty]
    private int _videoBitrateKbps = 4000;

    [ObservableProperty]
    private ResolutionOption _resolution = ResolutionOption.Source;

    [ObservableProperty]
    private FrameRateOption _frameRate = FrameRateOption.Source;

    [ObservableProperty]
    private AudioCodecKind _audioCodec = AudioCodecKind.Aac;

    [ObservableProperty]
    private int _audioBitrateKbps = 192;

    /// <summary>VBR 质量值（0 最高），null 表示使用固定码率。</summary>
    [ObservableProperty]
    private int? _audioVbrQuality;

    [ObservableProperty]
    private HardwareAcceleration _hardware = HardwareAcceleration.Auto;

    /// <summary>参数是否在当前批次中被修改过（工具栏预设名会加 *）。</summary>
    [ObservableProperty]
    private bool _isDirty;

    public MediaKind Kind => Container.KindOf();

    public bool IsVideo => Kind == MediaKind.Video;

    /// <summary>目标为 PNG 图像序列（每帧一个文件）。</summary>
    public bool IsImageSequence => Container == ContainerFormat.ImageSequence;

    public string FileExtension => Container.ToExtension();

    /// <summary>目标格式标题，例如 "H.264 MP4"、"MP3"。</summary>
    public string TargetTitle => Container.TargetTitle(VideoCodec);

    /// <summary>工具栏与下拉框显示的文本（被修改时带 *）。</summary>
    public string DisplayName => IsDirty ? "*" + Name : Name;

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        switch (e.PropertyName)
        {
            // 派生属性：只在被显式通知时返回，避免递归。
            case nameof(IsDirty):
            case nameof(DisplayName):
            case nameof(Kind):
            case nameof(IsVideo):
            case nameof(IsImageSequence):
            case nameof(FileExtension):
            case nameof(TargetTitle):
            case nameof(Summary):
            case nameof(AudioQualityKey):
                return;

            case nameof(Container):
                OnPropertyChanged(nameof(Kind));
                OnPropertyChanged(nameof(IsVideo));
                OnPropertyChanged(nameof(IsImageSequence));
                OnPropertyChanged(nameof(FileExtension));
                OnPropertyChanged(nameof(TargetTitle));
                OnPropertyChanged(nameof(Summary));
                SyncCodecWithContainer();
                break;

            case nameof(VideoCodec):
                OnPropertyChanged(nameof(TargetTitle));
                OnPropertyChanged(nameof(Summary));
                break;

            case nameof(AudioBitrateKbps):
            case nameof(AudioVbrQuality):
                OnPropertyChanged(nameof(AudioQualityKey));
                OnPropertyChanged(nameof(Summary));
                break;

            default:
                OnPropertyChanged(nameof(Summary));
                break;
        }

        if (!_suspendDirty)
        {
            MarkDirty();
        }
    }

    /// <summary>记录当前参数作为“原始值”，用户修改后可按需复原。</summary>
    public void CaptureTemplate()
    {
        _template ??= Clone();
        _suspendDirty = false;
        ClearDirty();
    }

    /// <summary>恢复到记录下来的原始参数（切换预设时用）。</summary>
    public void RevertToTemplate()
    {
        if (_template is null)
        {
            ClearDirty();
            return;
        }

        CopyFrom(_template);
    }

    private ConversionPreset? _template;

    /// <summary>容器切换到图像序列时需要同步视频编码，反之亦然。</summary>
    private void SyncCodecWithContainer()
    {
        if (IsImageSequence && VideoCodec != VideoCodecKind.Png)
        {
            VideoCodec = VideoCodecKind.Png;
        }
        else if (!IsImageSequence && VideoCodec == VideoCodecKind.Png)
        {
            VideoCodec = VideoCodecKind.H264;
        }
    }

    public void MarkDirty()
    {
        if (IsDirty)
        {
            return;
        }

        IsDirty = true;
        OnPropertyChanged(nameof(DisplayName));
    }

    /// <summary>复制另一套参数到当前实例（用于切换预设 / 恢复默认）。</summary>
    public void CopyFrom(ConversionPreset source)
    {
        _suspendDirty = true;
        Container = source.Container;
        VideoCodec = source.VideoCodec;
        QualityMode = source.QualityMode;
        Crf = source.Crf;
        VideoBitrateKbps = source.VideoBitrateKbps;
        Resolution = source.Resolution;
        FrameRate = source.FrameRate;
        AudioCodec = source.AudioCodec;
        AudioBitrateKbps = source.AudioBitrateKbps;
        AudioVbrQuality = source.AudioVbrQuality;
        Hardware = source.Hardware;
        _suspendDirty = false;

        IsDirty = false;
        OnPropertyChanged(nameof(DisplayName));
    }

    public ConversionPreset Clone(string? name = null)
    {
        var clone = new ConversionPreset(name ?? Name)
        {
            BuiltInKey = BuiltInKey,
            Container = Container,
            VideoCodec = VideoCodec,
            QualityMode = QualityMode,
            Crf = Crf,
            VideoBitrateKbps = VideoBitrateKbps,
            Resolution = Resolution,
            FrameRate = FrameRate,
            AudioCodec = AudioCodec,
            AudioBitrateKbps = AudioBitrateKbps,
            AudioVbrQuality = AudioVbrQuality,
            Hardware = Hardware
        };

        clone.ClearDirty();
        return clone;
    }

    public void ClearDirty()
    {
        IsDirty = false;
        OnPropertyChanged(nameof(DisplayName));
    }

    /// <summary>音频质量下拉框的全部选项（键 + 显示文本）。</summary>
    public static IReadOnlyList<Choice<string>> AudioQualityOptions { get; } = new List<Choice<string>>
    {
        new("320", "320 kbps"),
        new("256", "256 kbps"),
        new("192", "192 kbps"),
        new("160", "160 kbps"),
        new("128", "128 kbps"),
        new("96", "96 kbps"),
        new("V0", "V0（最高 VBR）"),
        new("V2", "V2（约 190 kbps）"),
        new("V6", "V6（约 115 kbps）")
    };

    /// <summary>音频质量下拉框的选中键，例如 "192" 或 "V0"。</summary>
    public string AudioQualityKey
    {
        get => AudioVbrQuality is { } quality
            ? "V" + quality.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : Math.Max(0, AudioBitrateKbps).ToString(System.Globalization.CultureInfo.InvariantCulture);
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            if (value.StartsWith('V') && int.TryParse(value.AsSpan(1), out var vbr))
            {
                AudioVbrQuality = Math.Clamp(vbr, 0, 9);
                return;
            }

            if (int.TryParse(value, out var bitrate))
            {
                AudioVbrQuality = null;
                AudioBitrateKbps = bitrate;
            }
        }
    }

    /// <summary>输出文件扩展名（含点）。</summary>
    public string OutputSuffix => "." + FileExtension;

    /// <summary>参数摘要（用于设置页底部说明）。</summary>
    public string Summary => Describe();

    public override string ToString() => DisplayName;

    /// <summary>用于日志的一行参数摘要。</summary>
    public string Describe()
    {
        var video = IsVideo
            ? $"{(QualityMode == QualityMode.Crf ? $"CRF {Crf}" : $"{VideoBitrateKbps} kbps")}"
            : "无视频";
        var audio = AudioCodec switch
        {
            AudioCodecKind.None => "无音频",
            AudioCodecKind.Copy => "复制音频",
            _ => AudioVbrQuality is { } q ? $"V{q}" : $"{AudioBitrateKbps} kbps"
        };
        return $"{TargetTitle} | 视频 {VideoCodec} {video} | 音频 {AudioCodec} {audio}";
    }
}

/// <summary>内置预设清单（PRD 第 6 节）。</summary>
public static class BuiltInPresets
{
    public const string HqH264Mp4 = "hq-h264-mp4";
    public const string CompatibleH264Mp4 = "compat-h264-mp4";
    public const string CompactH265Mp4 = "compact-h265-mp4";
    public const string Av1Mp4 = "av1-mp4";
    public const string WebmVp9 = "webm-vp9";
    public const string ImageSequence = "image-sequence";
    public const string HqMp3 = "hq-mp3";
    public const string AacM4a = "aac-m4a";
    public const string LosslessFlac = "lossless-flac";
    public const string LosslessWav = "lossless-wav";
    public const string OggVorbis = "ogg-vorbis";

    public const string DefaultVideoKey = HqH264Mp4;
    public const string DefaultAudioKey = HqMp3;

    /// <summary>返回全新的实例（每次调用都是独立对象，可安全修改）。</summary>
    public static IReadOnlyList<ConversionPreset> CreateAll() => new List<ConversionPreset>
    {
        new("高质量 MP4（H.264）")
        {
            BuiltInKey = HqH264Mp4,
            Container = ContainerFormat.Mp4,
            VideoCodec = VideoCodecKind.H264,
            QualityMode = QualityMode.Crf,
            Crf = 18,
            AudioCodec = AudioCodecKind.Aac,
            AudioBitrateKbps = 320
        },
        new("兼容 H.264 MP4")
        {
            BuiltInKey = CompatibleH264Mp4,
            Container = ContainerFormat.Mp4,
            VideoCodec = VideoCodecKind.H264,
            QualityMode = QualityMode.Crf,
            Crf = 23,
            AudioCodec = AudioCodecKind.Aac,
            AudioBitrateKbps = 320
        },
        new("高压缩 H.265 MP4")
        {
            BuiltInKey = CompactH265Mp4,
            Container = ContainerFormat.Mp4,
            VideoCodec = VideoCodecKind.H265,
            QualityMode = QualityMode.Crf,
            Crf = 24,
            AudioCodec = AudioCodecKind.Aac,
            AudioBitrateKbps = 320
        },
        new("AV1 MP4")
        {
            BuiltInKey = Av1Mp4,
            Container = ContainerFormat.Mp4,
            VideoCodec = VideoCodecKind.Av1,
            QualityMode = QualityMode.Crf,
            Crf = 32,
            AudioCodec = AudioCodecKind.Aac,
            AudioBitrateKbps = 320
        },
        new("WebM VP9")
        {
            BuiltInKey = WebmVp9,
            Container = ContainerFormat.Webm,
            VideoCodec = VideoCodecKind.Vp9,
            QualityMode = QualityMode.Crf,
            Crf = 32,
            AudioCodec = AudioCodecKind.Opus,
            AudioBitrateKbps = 320
        },
        new("转为图像序列")
        {
            BuiltInKey = ImageSequence,
            Container = ContainerFormat.ImageSequence,
            VideoCodec = VideoCodecKind.Png,
            QualityMode = QualityMode.Crf,
            AudioCodec = AudioCodecKind.None
        },
        new("高质量 MP3")
        {
            BuiltInKey = HqMp3,
            Container = ContainerFormat.Mp3,
            AudioCodec = AudioCodecKind.Mp3,
            AudioBitrateKbps = 320
        },
        new("AAC / M4A")
        {
            BuiltInKey = AacM4a,
            Container = ContainerFormat.M4a,
            AudioCodec = AudioCodecKind.Aac,
            AudioBitrateKbps = 320
        },
        new("无损 FLAC")
        {
            BuiltInKey = LosslessFlac,
            Container = ContainerFormat.Flac,
            AudioCodec = AudioCodecKind.Flac
        },
        new("WAV 无损")
        {
            BuiltInKey = LosslessWav,
            Container = ContainerFormat.Wav,
            AudioCodec = AudioCodecKind.Pcm16
        },
        new("OGG Vorbis")
        {
            BuiltInKey = OggVorbis,
            Container = ContainerFormat.Ogg,
            AudioCodec = AudioCodecKind.Vorbis,
            AudioBitrateKbps = 320
        }
    };

    /// <summary>取得某个内置预设的原始参数（用于恢复默认）。</summary>
    public static ConversionPreset? FindTemplate(string? builtInKey) => builtInKey is null
        ? null
        : CreateAll().FirstOrDefault(p => p.BuiltInKey == builtInKey);
}
