using System.ComponentModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FileToFiles.Models;

/// <summary>目标媒体类型。</summary>
public enum MediaKind
{
    Video,
    Audio,

    /// <summary>静态图片或动图（GIF / 动画 WebP）。</summary>
    Image
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
    ImageSequence,

    /// <summary>JPEG 静态图片。</summary>
    Jpg,

    /// <summary>PNG 静态图片（无损，保留透明）。</summary>
    Png,

    /// <summary>WebP 静态图片（只取第一帧）。</summary>
    Webp,

    /// <summary>WebP 动画（保留所有帧，可循环播放）。</summary>
    WebpAnim,

    /// <summary>GIF 动图。</summary>
    Gif,

    /// <summary>BMP 位图。</summary>
    Bmp,

    /// <summary>TIFF 图片。</summary>
    Tiff
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
        ContainerFormat.Jpg => "jpg",
        ContainerFormat.Png => "png",
        ContainerFormat.Webp => "webp",
        ContainerFormat.WebpAnim => "webp",
        ContainerFormat.Gif => "gif",
        ContainerFormat.Bmp => "bmp",
        ContainerFormat.Tiff => "tiff",
        _ => "mp4"
    };

    public static MediaKind KindOf(this ContainerFormat container) => container switch
    {
        ContainerFormat.Mp4 or ContainerFormat.Mkv or ContainerFormat.Webm or ContainerFormat.Mov
            or ContainerFormat.ImageSequence => MediaKind.Video,
        ContainerFormat.Jpg or ContainerFormat.Png or ContainerFormat.Webp or ContainerFormat.WebpAnim
            or ContainerFormat.Gif or ContainerFormat.Bmp or ContainerFormat.Tiff => MediaKind.Image,
        _ => MediaKind.Audio
    };

    /// <summary>目标容器是否支持多帧动画（可接收视频/动图输入）。</summary>
    public static bool SupportsAnimation(this ContainerFormat container)
        => container is ContainerFormat.Gif or ContainerFormat.WebpAnim;

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
        ContainerFormat.Jpg => "JPG",
        ContainerFormat.Png => "PNG",
        ContainerFormat.Webp => "WebP",
        ContainerFormat.WebpAnim => "WebP 动图",
        ContainerFormat.Gif => "GIF",
        ContainerFormat.Bmp => "BMP",
        ContainerFormat.Tiff => "TIFF",
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
        ContainerFormat.Jpg => "JPG 图片",
        ContainerFormat.Png => "PNG 图片",
        ContainerFormat.Webp => "WebP 图片",
        ContainerFormat.WebpAnim => "WebP 动图",
        ContainerFormat.Gif => "GIF 动图",
        ContainerFormat.Bmp => "BMP 图片",
        ContainerFormat.Tiff => "TIFF 图片",
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

    /// <summary>图片质量（1–100，数值越大画质越好）；仅图片预设使用。</summary>
    [ObservableProperty]
    private int _imageQuality = 90;

    /// <summary>动图是否循环播放。</summary>
    [ObservableProperty]
    private bool _loopAnimation = true;

    /// <summary>参数是否在当前批次中被修改过（工具栏预设名会加 *）。</summary>
    [ObservableProperty]
    private bool _isDirty;

    public MediaKind Kind => Container.KindOf();

    public bool IsVideo => Kind == MediaKind.Video;

    public bool IsAudio => Kind == MediaKind.Audio;

    /// <summary>目标为静态图片或动图。</summary>
    public bool IsImage => Kind == MediaKind.Image;

    /// <summary>目标为图像序列（每帧一个文件）。</summary>
    public bool IsImageSequence => Container == ContainerFormat.ImageSequence;

    /// <summary>目标为可动画的图片格式（GIF / 动画 WebP），可接收视频与动图输入。</summary>
    public bool IsAnimatedImageTarget => IsImage && Container.SupportsAnimation();

    /// <summary>目标图片格式使用"质量"参数（PNG / BMP / TIFF 为无损，不用质量）。</summary>
    public bool HasImageQuality => IsImage
        && Container is ContainerFormat.Jpg or ContainerFormat.Webp or ContainerFormat.WebpAnim;

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
            case nameof(IsAudio):
            case nameof(IsImage):
            case nameof(IsImageSequence):
            case nameof(IsAnimatedImageTarget):
            case nameof(HasImageQuality):
            case nameof(FileExtension):
            case nameof(TargetTitle):
            case nameof(Summary):
            case nameof(AudioQualityKey):
                return;

            case nameof(Container):
                OnPropertyChanged(nameof(Kind));
                OnPropertyChanged(nameof(IsVideo));
                OnPropertyChanged(nameof(IsAudio));
                OnPropertyChanged(nameof(IsImage));
                OnPropertyChanged(nameof(IsImageSequence));
                OnPropertyChanged(nameof(IsAnimatedImageTarget));
                OnPropertyChanged(nameof(HasImageQuality));
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
        ImageQuality = source.ImageQuality;
        LoopAnimation = source.LoopAnimation;
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
            Hardware = Hardware,
            ImageQuality = ImageQuality,
            LoopAnimation = LoopAnimation
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
        if (IsImage)
        {
            var imageParts = new List<string> { TargetTitle };

            if (Container is ContainerFormat.Jpg or ContainerFormat.Webp or ContainerFormat.WebpAnim)
            {
                imageParts.Add($"质量 {Math.Clamp(ImageQuality, 1, 100)}");
            }
            else if (Container == ContainerFormat.Png)
            {
                imageParts.Add("无损压缩");
            }

            imageParts.Add(Resolution switch
            {
                ResolutionOption.P1080 => "缩放到 1080p",
                ResolutionOption.P720 => "缩放到 720p",
                ResolutionOption.P480 => "缩放到 480p",
                _ => "保持原始尺寸"
            });

            if (IsAnimatedImageTarget)
            {
                imageParts.Add(FrameRate switch
                {
                    FrameRateOption.Fps60 => "60 fps",
                    FrameRateOption.Fps30 => "30 fps",
                    FrameRateOption.Fps25 => "25 fps",
                    FrameRateOption.Fps24 => "24 fps",
                    FrameRateOption.Fps15 => "15 fps",
                    _ => "帧率保持（上限 20）"
                });
                imageParts.Add(LoopAnimation ? "循环播放" : "只播放一次");
            }

            return string.Join(" | ", imageParts);
        }

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
    public const string ImageJpg = "image-jpg";
    public const string ImagePng = "image-png";
    public const string ImageWebp = "image-webp";
    public const string AnimatedGif = "animated-gif";
    public const string AnimatedWebp = "animated-webp";

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
        },
        new("图片 · 转 JPG")
        {
            BuiltInKey = ImageJpg,
            Container = ContainerFormat.Jpg,
            ImageQuality = 92
        },
        new("图片 · 转 PNG")
        {
            BuiltInKey = ImagePng,
            Container = ContainerFormat.Png,
            ImageQuality = 100
        },
        new("图片 · 转 WebP")
        {
            BuiltInKey = ImageWebp,
            Container = ContainerFormat.Webp,
            ImageQuality = 90
        },
        new("动图 · GIF")
        {
            BuiltInKey = AnimatedGif,
            Container = ContainerFormat.Gif,
            ImageQuality = 85,
            FrameRate = FrameRateOption.Fps15,
            Resolution = ResolutionOption.Source,
            LoopAnimation = true
        },
        new("动图 · WebP")
        {
            BuiltInKey = AnimatedWebp,
            Container = ContainerFormat.WebpAnim,
            ImageQuality = 80,
            FrameRate = FrameRateOption.Fps15,
            Resolution = ResolutionOption.Source,
            LoopAnimation = true
        }
    };

    /// <summary>取得某个内置预设的原始参数（用于恢复默认）。</summary>
    public static ConversionPreset? FindTemplate(string? builtInKey) => builtInKey is null
        ? null
        : CreateAll().FirstOrDefault(p => p.BuiltInKey == builtInKey);
}
