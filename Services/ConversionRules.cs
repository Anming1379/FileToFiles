using FileToFiles.Models;

namespace FileToFiles.Services;

/// <summary>兼容性检查结果。</summary>
public enum CompatibilityResult
{
    /// <summary>可以转换。</summary>
    Ok,

    /// <summary>源与目标类型不兼容，应跳过（不算失败）。</summary>
    Skip,

    /// <summary>无法转换，给出可读原因。</summary>
    Error
}

/// <summary>一次兼容性检查的结论。</summary>
public sealed record CompatibilityCheck(CompatibilityResult Result, string? Message)
{
    public static readonly CompatibilityCheck Ok = new(CompatibilityResult.Ok, null);

    public static CompatibilityCheck Skip(string message) => new(CompatibilityResult.Skip, message);

    public static CompatibilityCheck Error(string message) => new(CompatibilityResult.Error, message);

    public bool IsSkip => Result == CompatibilityResult.Skip;

    public bool IsError => Result == CompatibilityResult.Error;
}

/// <summary>
/// 判断源文件的流与目标格式是否匹配。
/// 视频/图片/音频之间的“无意义组合”直接跳过，而不是交给 ffmpeg 报错。
/// </summary>
public static class ConversionRules
{
    public static CompatibilityCheck Evaluate(MediaInfo? media, ConversionPreset preset)
    {
        if (media is null)
        {
            return CompatibilityCheck.Error("媒体信息尚未就绪，无法转换。");
        }

        // 图片与视频都有画面，音频没有。
        var hasPicture = media.HasVideo;
        var hasAudio = media.HasAudio;

        // 目标：图片 / 动图
        if (preset.IsImage)
        {
            if (!hasPicture)
            {
                return CompatibilityCheck.Skip("音频文件没有画面，无法转换为图片，已跳过。");
            }

            return CompatibilityCheck.Ok;
        }

        // 目标：PNG 图像序列
        if (preset.IsImageSequence)
        {
            return hasPicture
                ? CompatibilityCheck.Ok
                : CompatibilityCheck.Skip("源文件没有画面，无法转换为图像序列，已跳过。");
        }

        // 目标：纯音频
        if (preset.IsAudio)
        {
            if (!hasAudio)
            {
                return CompatibilityCheck.Skip(media.IsImage
                    ? "图片文件没有音频流，无法转换为音频格式，已跳过。"
                    : "源文件没有音频流，已跳过。");
            }

            return CompatibilityCheck.Ok;
        }

        // 目标：视频
        if (preset.VideoCodec == VideoCodecKind.Copy)
        {
            return CompatibilityCheck.Ok;
        }

        if (!hasPicture && !hasAudio)
        {
            return CompatibilityCheck.Error("源文件不包含可转换的音视频流。");
        }

        return CompatibilityCheck.Ok;
    }
}
