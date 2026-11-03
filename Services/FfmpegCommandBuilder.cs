using System.Globalization;
using System.IO;
using System.Text;
using FileToFiles.Models;

namespace FileToFiles.Services;

/// <summary>构建好的 FFmpeg 调用（参数列表 + 可复制的命令行文本）。</summary>
public sealed record FfmpegCommand(IReadOnlyList<string> Arguments, string DisplayText)
{
    public string Executable { get; init; } = "ffmpeg";
}

/// <summary>
/// 根据预设把转换参数翻译成 FFmpeg 命令行。
/// </summary>
public static class FfmpegCommandBuilder
{
    /// <summary>
    /// 构建命令。-progress 与 -nostats 会被插入到输出路径之前，
    /// 这样调用方可以直接解析 stdout 上的进度信息。
    /// </summary>
    public static FfmpegCommand Build(string inputPath, string outputPath, ConversionPreset preset, MediaInfo? source)
    {
        var args = new List<string>
        {
            "-hide_banner",
            "-nostdin",
            "-y",
            "-i", inputPath
        };

        if (preset.IsImage)
        {
            AddImage(args, preset, source);
        }
        else if (preset.IsImageSequence)
        {
            AddImageSequence(args, preset);
        }
        else if (preset.IsVideo)
        {
            AddVideoInput(args, preset, source);
            AddVideoCodec(args, preset, source);
            AddAudioCodec(args, preset, source, allowNoAudio: true);
            AddContainerFlags(args, preset);
        }
        else
        {
            // 纯音频输出：丢弃视频流。
            args.Add("-vn");
            args.Add("-sn");
            args.Add("-dn");
            AddAudioCodec(args, preset, source, allowNoAudio: false);
        }

        args.Add("-progress");
        args.Add("pipe:1");
        args.Add("-nostats");
        args.Add(outputPath);

        return new FfmpegCommand(args, BuildDisplayText(args));
    }

    /// <summary>
    /// 图片与动图：静态目标只取第一帧，GIF / 动画 WebP 保留全部帧。
    /// </summary>
    private static void AddImage(List<string> args, ConversionPreset preset, MediaInfo? source)
    {
        args.Add("-map");
        args.Add("0:v:0?");
        args.Add("-an");
        args.Add("-sn");
        args.Add("-dn");

        var quality = Math.Clamp(preset.ImageQuality, 1, 100);
        var animated = preset.IsAnimatedImageTarget;

        // 编码器与质量参数。
        switch (preset.Container)
        {
            case ContainerFormat.Jpg:
                args.Add("-c:v");
                args.Add("mjpeg");
                args.Add("-q:v");
                args.Add(JpegQScale(quality).ToString(CultureInfo.InvariantCulture));
                break;

            case ContainerFormat.Png:
                args.Add("-c:v");
                args.Add("png");
                break;

            case ContainerFormat.Bmp:
                args.Add("-c:v");
                args.Add("bmp");
                break;

            case ContainerFormat.Tiff:
                args.Add("-c:v");
                args.Add("tiff");
                break;

            case ContainerFormat.Webp:
                args.Add("-c:v");
                args.Add("libwebp");
                args.Add("-quality");
                args.Add(quality.ToString(CultureInfo.InvariantCulture));
                args.Add("-compression_level");
                args.Add("4");
                break;

            case ContainerFormat.WebpAnim:
                args.Add("-c:v");
                args.Add("libwebp_anim");
                args.Add("-quality");
                args.Add(quality.ToString(CultureInfo.InvariantCulture));
                args.Add("-compression_level");
                args.Add("4");
                args.Add("-loop");
                args.Add(preset.LoopAnimation ? "0" : "1");
                break;

            case ContainerFormat.Gif:
                args.Add("-c:v");
                args.Add("gif");
                args.Add("-loop");
                args.Add(preset.LoopAnimation ? "0" : "-1");
                break;
        }

        var filter = BuildImageFilter(preset, source, animated);
        if (filter is not null)
        {
            args.Add("-vf");
            args.Add(filter);
        }

        if (!animated)
        {
            // 静态图片：只输出一帧，并使用 -update 避免 image2 序列命名报错。
            args.Add("-frames:v");
            args.Add("1");

            if (preset.Container is ContainerFormat.Jpg or ContainerFormat.Png
                or ContainerFormat.Bmp or ContainerFormat.Tiff)
            {
                args.Add("-update");
                args.Add("1");
            }

            if (preset.Container == ContainerFormat.Jpg)
            {
                // JPEG 不支持透明通道，统一转成常见播放器兼容的 4:2:0。
                args.Add("-pix_fmt");
                args.Add("yuvj420p");
            }
        }
    }

    /// <summary>图片滤镜链：缩放、（动图）帧率、GIF 调色板优化。</summary>
    private static string? BuildImageFilter(ConversionPreset preset, MediaInfo? source, bool animated)
    {
        var parts = new List<string>();

        if (animated)
        {
            parts.Add($"fps={ResolveAnimationFps(preset, source)}");
        }

        var targetHeight = preset.Resolution switch
        {
            ResolutionOption.P1080 => 1080,
            ResolutionOption.P720 => 720,
            ResolutionOption.P480 => 480,
            _ => 0
        };

        if (targetHeight > 0)
        {
            parts.Add($"scale=-2:{targetHeight}:flags=lanczos");
        }
        else if (preset.Container == ContainerFormat.Jpg)
        {
            // mjpeg 需要偶数宽高，避免“奇数尺寸”报错。
            parts.Add("scale=trunc(iw/2)*2:trunc(ih/2)*2");
        }

        if (preset.Container == ContainerFormat.Gif)
        {
            var chain = string.Join(",", parts);
            chain = string.IsNullOrEmpty(chain) ? string.Empty : chain + ",";

            // 单遍生成调色板：颜色数更准，体积比默认显著小。
            return chain + "split[s0][s1];[s0]palettegen=stats_mode=diff:max_colors=256[p];"
                   + "[s1][p]paletteuse=dither=bayer:bayer_scale=3";
        }

        if (preset.Container == ContainerFormat.Jpg)
        {
            // JPEG 不支持透明：先铺一层白底再把原图叠上去，避免透明区域变黑。
            var prefix = parts.Count == 0 ? string.Empty : string.Join(",", parts) + ",";
            return prefix + "split[bg][fg];[bg]drawbox=color=white:t=fill[bgw];"
                   + "[bgw][fg]overlay=format=auto";
        }

        return parts.Count == 0 ? null : string.Join(",", parts);
    }

    /// <summary>动图帧率：用户选了就用，否则保持原始但限制在 20 fps 以内。</summary>
    private static int ResolveAnimationFps(ConversionPreset preset, MediaInfo? source)
    {
        var chosen = preset.FrameRate switch
        {
            FrameRateOption.Fps60 => 60,
            FrameRateOption.Fps30 => 30,
            FrameRateOption.Fps25 => 25,
            FrameRateOption.Fps24 => 24,
            FrameRateOption.Fps15 => 15,
            _ => 0
        };

        if (chosen > 0)
        {
            // 不要给低帧率源做无意义的上采样（12 fps 的 GIF 不需要变成 15 fps）。
            var sourceFps = source?.FrameRate ?? 0;
            return sourceFps > 0
                ? Math.Clamp((int)Math.Round(sourceFps), 1, chosen)
                : chosen;
        }

        var fallback = source?.FrameRate ?? 0;
        return fallback > 0
            ? (int)Math.Clamp(Math.Round(fallback), 1, 20)
            : 15;
    }

    /// <summary>图片质量 1–100 映射到 mjpeg 的 -q:v 1–31（1 最好）。</summary>
    private static int JpegQScale(int quality)
    {
        var normalized = Math.Clamp(quality, 1, 100);
        return Math.Clamp((int)Math.Round((100 - normalized) / 100.0 * 30) + 1, 1, 31);
    }

    /// <summary>PNG 图像序列：丢弃音频，保留每一帧。</summary>
    private static void AddImageSequence(List<string> args, ConversionPreset preset)
    {
        args.Add("-map");
        args.Add("0:v:0?");
        args.Add("-an");
        args.Add("-sn");
        args.Add("-dn");
        args.Add("-c:v");
        args.Add("png");
        args.Add("-f");
        args.Add("image2");
        args.Add("-start_number");
        args.Add("1");
        args.Add("-fps_mode");
        args.Add("passthrough");

        AddScaleAndFrameRate(args, preset);
    }

    /// <summary>分辨率与帧率参数（视频与图像序列共用）。</summary>
    private static void AddScaleAndFrameRate(List<string> args, ConversionPreset preset)
    {
        var targetHeight = preset.Resolution switch
        {
            ResolutionOption.P1080 => 1080,
            ResolutionOption.P720 => 720,
            ResolutionOption.P480 => 480,
            _ => 0
        };

        if (targetHeight > 0)
        {
            args.Add("-vf");
            args.Add($"scale=-2:{targetHeight}");
        }

        var fps = preset.FrameRate switch
        {
            FrameRateOption.Fps60 => 60,
            FrameRateOption.Fps30 => 30,
            FrameRateOption.Fps25 => 25,
            FrameRateOption.Fps24 => 24,
            FrameRateOption.Fps15 => 15,
            _ => 0
        };

        if (fps > 0)
        {
            args.Add("-r");
            args.Add(fps.ToString(CultureInfo.InvariantCulture));
        }
    }

    private static void AddVideoInput(List<string> args, ConversionPreset preset, MediaInfo? source)
    {
        // 只取第一路视频和第一路音频，忽略字幕与多音轨；源无视频时也不报错。
        args.Add("-map");
        args.Add("0:v:0?");
        args.Add("-map");
        args.Add("0:a:0?");
        args.Add("-sn");
        args.Add("-dn");

        _ = source;
    }

    private static void AddVideoCodec(List<string> args, ConversionPreset preset, MediaInfo? source)
    {
        var encoder = ResolveVideoEncoder(preset.VideoCodec, preset.Hardware);
        args.Add("-c:v");
        args.Add(encoder);

        if (preset.VideoCodec is VideoCodecKind.Copy or VideoCodecKind.Png)
        {
            return;
        }

        var hardware = preset.Hardware is HardwareAcceleration.Nvenc or HardwareAcceleration.Qsv or HardwareAcceleration.Amf;

        if (!hardware)
        {
            switch (preset.VideoCodec)
            {
                case VideoCodecKind.H264:
                case VideoCodecKind.H265:
                    args.Add("-preset");
                    args.Add("medium");
                    break;
                case VideoCodecKind.Vp9:
                    args.Add("-row-mt");
                    args.Add("1");
                    args.Add("-cpu-used");
                    args.Add("2");
                    break;
                case VideoCodecKind.Av1:
                    args.Add("-preset");
                    args.Add("8");
                    break;
            }

            if (preset.QualityMode == QualityMode.Crf)
            {
                args.Add("-crf");
                args.Add(preset.Crf.ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                args.Add("-b:v");
                args.Add($"{Math.Max(1, preset.VideoBitrateKbps)}k");
            }
        }
        else
        {
            AddHardwareQuality(args, preset, encoder);
        }

        // 像素格式：H.264 / H.265 / AV1 固定为 yuv420p，保证通用播放器兼容。
        if (preset.VideoCodec is VideoCodecKind.H264 or VideoCodecKind.H265 or VideoCodecKind.Av1)
        {
            args.Add("-pix_fmt");
            args.Add("yuv420p");
        }

        AddScaleAndFrameRate(args, preset);
    }

    private static void AddHardwareQuality(List<string> args, ConversionPreset preset, string encoder)
    {
        var isNvenc = encoder.EndsWith("_nvenc", StringComparison.Ordinal);
        var isQsv = encoder.EndsWith("_qsv", StringComparison.Ordinal);
        var isAmf = encoder.EndsWith("_amf", StringComparison.Ordinal);
        var crf = Math.Clamp(preset.Crf, 0, 51);

        if (preset.QualityMode == QualityMode.Crf)
        {
            if (isNvenc)
            {
                args.Add("-rc");
                args.Add("vbr");
                args.Add("-cq");
                args.Add(crf.ToString(CultureInfo.InvariantCulture));
                args.Add("-b:v");
                args.Add("0");
            }
            else if (isQsv)
            {
                args.Add("-global_quality");
                args.Add(crf.ToString(CultureInfo.InvariantCulture));
            }
            else if (isAmf)
            {
                args.Add("-rc");
                args.Add("cqp");
                args.Add("-qp_i");
                args.Add(crf.ToString(CultureInfo.InvariantCulture));
                args.Add("-qp_p");
                args.Add(crf.ToString(CultureInfo.InvariantCulture));
            }

            return;
        }

        args.Add("-b:v");
        args.Add($"{Math.Max(1, preset.VideoBitrateKbps)}k");

        if (isAmf)
        {
            args.Add("-rc");
            args.Add("cbr");
        }
    }

    private static void AddAudioCodec(List<string> args, ConversionPreset preset, MediaInfo? source, bool allowNoAudio)
    {
        _ = allowNoAudio;

        switch (preset.AudioCodec)
        {
            case AudioCodecKind.None:
                args.Add("-an");
                return;

            case AudioCodecKind.Copy:
                args.Add("-c:a");
                args.Add("copy");
                return;

            case AudioCodecKind.Flac:
                args.Add("-c:a");
                args.Add("flac");
                if (preset.AudioBitrateKbps > 0 && preset.AudioVbrQuality is not null)
                {
                    args.Add("-compression_level");
                    args.Add("8");
                }

                return;

            case AudioCodecKind.Pcm16:
                args.Add("-c:a");
                args.Add("pcm_s16le");
                return;

            case AudioCodecKind.Aac:
            {
                args.Add("-c:a");
                args.Add("aac");
                var bitrate = preset.AudioVbrQuality is not null || preset.AudioBitrateKbps <= 0
                    ? 192
                    : preset.AudioBitrateKbps;
                args.Add("-b:a");
                args.Add($"{bitrate}k");
                return;
            }

            case AudioCodecKind.Mp3:
            {
                args.Add("-c:a");
                args.Add("libmp3lame");
                if (preset.AudioVbrQuality is { } quality)
                {
                    args.Add("-q:a");
                    args.Add(Math.Clamp(quality, 0, 9).ToString(CultureInfo.InvariantCulture));
                }
                else
                {
                    // libmp3lame 的上限是 320 kbps。
                    args.Add("-b:a");
                    args.Add($"{Math.Clamp(preset.AudioBitrateKbps, 32, 320)}k");
                }

                return;
            }

            case AudioCodecKind.Opus:
            {
                args.Add("-c:a");
                args.Add("libopus");
                var bitrate = preset.AudioVbrQuality is not null || preset.AudioBitrateKbps <= 0
                    ? 128
                    : preset.AudioBitrateKbps;

                // libopus 只接受 6–256 kbps。
                args.Add("-b:a");
                args.Add($"{Math.Clamp(bitrate, 6, 256)}k");
                return;
            }

            case AudioCodecKind.Vorbis:
            {
                args.Add("-c:a");
                args.Add("libvorbis");
                if (preset.AudioVbrQuality is { } quality)
                {
                    args.Add("-q:a");
                    args.Add(Math.Clamp(quality, 0, 9).ToString(CultureInfo.InvariantCulture));
                }
                else
                {
                    args.Add("-b:a");
                    args.Add($"{Math.Max(64, preset.AudioBitrateKbps)}k");
                }

                return;
            }
        }
    }

    private static void AddContainerFlags(List<string> args, ConversionPreset preset)
    {
        switch (preset.Container)
        {
            case ContainerFormat.Mp4:
            case ContainerFormat.M4a:
                args.Add("-movflags");
                args.Add("+faststart");
                if (preset.VideoCodec == VideoCodecKind.H265)
                {
                    // hvc1 标签让 Windows / QuickTime 能识别 HEVC 视频轨。
                    args.Add("-tag:v");
                    args.Add("hvc1");
                }

                break;

            case ContainerFormat.Webm:
                // WebM 只接受 VP8/VP9/AV1 + Vorbis/Opus。
                if (preset.VideoCodec is not (VideoCodecKind.Vp9 or VideoCodecKind.Av1))
                {
                    args.Add("-f");
                    args.Add("webm");
                }

                break;
        }

        _ = preset;
    }

    /// <summary>视频编码器名称；硬件加速不可用时回退到软件编码。</summary>
    public static string ResolveVideoEncoder(VideoCodecKind codec, HardwareAcceleration hardware)
    {
        if (codec == VideoCodecKind.Copy)
        {
            return "copy";
        }

        if (codec == VideoCodecKind.Png)
        {
            return "png";
        }

        var useHardware = hardware is HardwareAcceleration.Nvenc or HardwareAcceleration.Qsv or HardwareAcceleration.Amf;

        if (!useHardware)
        {
            return codec switch
            {
                VideoCodecKind.H264 => "libx264",
                VideoCodecKind.H265 => "libx265",
                VideoCodecKind.Vp9 => "libvpx-vp9",
                VideoCodecKind.Av1 => "libsvtav1",
                _ => "libx264"
            };
        }

        if (codec is VideoCodecKind.Vp9 or VideoCodecKind.Av1)
        {
            // VP9 / AV1 暂无通用的硬件编码路径，回退到软件编码。
            return ResolveVideoEncoder(codec, HardwareAcceleration.Off);
        }

        var prefix = codec == VideoCodecKind.H264 ? "h264" : "hevc";
        return hardware switch
        {
            HardwareAcceleration.Nvenc => $"{prefix}_nvenc",
            HardwareAcceleration.Qsv => $"{prefix}_qsv",
            HardwareAcceleration.Amf => $"{prefix}_amf",
            _ => ResolveVideoEncoder(codec, HardwareAcceleration.Off)
        };
    }

    private static string BuildDisplayText(IReadOnlyList<string> args)
    {
        var builder = new StringBuilder("ffmpeg");
        foreach (var arg in args)
        {
            builder.Append(' ');
            if (arg.Contains(' ') || arg.Contains('"'))
            {
                builder.Append('"').Append(arg.Replace("\"", "\\\"")).Append('"');
            }
            else
            {
                builder.Append(arg);
            }
        }

        return builder.ToString();
    }
}

/// <summary>输出路径与临时文件命名。</summary>
public static class OutputPathResolver
{
    /// <summary>
    /// 输出路径。普通格式为 {源文件名}.{目标扩展名}，同目录同名直接覆盖；
    /// 图像序列为 {源文件名}_frames 文件夹。
    /// </summary>
    public static string Resolve(string outputDirectory, string inputPath, ConversionPreset preset)
    {
        var name = Path.GetFileNameWithoutExtension(inputPath);
        return preset.IsImageSequence
            ? Path.Combine(outputDirectory, name + "_frames")
            : Path.Combine(outputDirectory, name + "." + preset.FileExtension);
    }

    /// <summary>
    /// 临时路径：普通格式保留真实扩展名（"a.part.mp4"）让 ffmpeg 推断容器；
    /// 图像序列使用 ".part" 子文件夹。
    /// </summary>
    public static string TempPath(string finalPath, ConversionPreset? preset = null)
    {
        if (preset?.IsImageSequence == true)
        {
            return finalPath + ".part";
        }

        var directory = Path.GetDirectoryName(finalPath) ?? ".";
        var name = Path.GetFileNameWithoutExtension(finalPath);
        var extension = Path.GetExtension(finalPath);
        return Path.Combine(directory, $"{name}.part{extension}");
    }

    /// <summary>图像序列的 ffmpeg 输出模式，例如 "dir/name_%05d.png"。</summary>
    public static string ImageSequencePattern(string directory, string inputPath)
    {
        var name = Path.GetFileNameWithoutExtension(inputPath);
        return Path.Combine(directory, name + "_%05d.png");
    }
}
