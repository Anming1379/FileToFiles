using System.Diagnostics;
using System.IO;
using System.Globalization;
using System.Text;
using System.Text.Json;
using FileToFiles.Models;

namespace FileToFiles.Services;

/// <summary>
/// 探测失败时抛出，携带可读原因与 ffprobe 的 stderr。
/// </summary>
public sealed class ProbeException : Exception
{
    public ProbeException(string message, string? stderr = null, int? exitCode = null)
        : base(message)
    {
        Stderr = stderr;
        ExitCode = exitCode;
    }

    public string? Stderr { get; }

    public int? ExitCode { get; }
}

/// <summary>
/// 调用 ffprobe 读取媒体信息。
/// </summary>
public sealed class FfprobeService
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(60);

    public async Task<MediaInfo> ProbeAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var ffprobe = FfmpegLocator.Current.FfprobePath
                      ?? throw new ProbeException("未找到 ffprobe，无法探测媒体信息。");

        if (!File.Exists(filePath))
        {
            throw new ProbeException("文件不存在或已被移动。");
        }

        var psi = new ProcessStartInfo(ffprobe)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        psi.ArgumentList.Add("-v");
        psi.ArgumentList.Add("error");
        psi.ArgumentList.Add("-print_format");
        psi.ArgumentList.Add("json");
        psi.ArgumentList.Add("-show_format");
        psi.ArgumentList.Add("-show_streams");
        psi.ArgumentList.Add(filePath);

        using var process = new Process { StartInfo = psi };
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ProbeTimeout);

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            throw new ProbeException($"无法启动 ffprobe：{ex.Message}");
        }

        using var registration = timeout.Token.Register(() => FfmpegLocator.TryKill(process));

        var stdoutTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var stderrTask = process.StandardError.ReadToEndAsync(timeout.Token);

        // 先读 stderr，避免缓冲区死锁。
        var stderr = await stderrTask.ConfigureAwait(false);
        var stdout = await stdoutTask.ConfigureAwait(false);

        await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);

        if (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }

        if (timeout.IsCancellationRequested)
        {
            throw new ProbeException("探测超时（60 秒）。");
        }

        if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(stdout))
        {
            var reason = string.IsNullOrWhiteSpace(stderr)
                ? "ffprobe 未返回任何媒体信息，文件可能已损坏或格式不受支持。"
                : stderr.Trim();
            throw new ProbeException(reason, stderr, process.ExitCode);
        }

        try
        {
            return Parse(filePath, stdout);
        }
        catch (JsonException)
        {
            throw new ProbeException("ffprobe 输出无法解析。", stderr, process.ExitCode);
        }
    }

    private static MediaInfo Parse(string filePath, string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        string? formatName = null;
        var duration = TimeSpan.Zero;
        long size = 0;
        long bitRate = 0;

        if (root.TryGetProperty("format", out var format))
        {
            formatName = GetString(format, "format_name");
            if (double.TryParse(GetString(format, "duration"), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds > 0)
            {
                duration = TimeSpan.FromSeconds(seconds);
            }

            if (long.TryParse(GetString(format, "size"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var formatSize))
            {
                size = formatSize;
            }

            if (long.TryParse(GetString(format, "bit_rate"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var formatBitRate))
            {
                bitRate = formatBitRate;
            }
        }

        int width = 0, height = 0;
        string? videoCodec = null, videoProfile = null;
        double? frameRate = null;
        string? audioCodec = null;
        int? audioChannels = null, audioSampleRate = null;
        long? audioBitRate = null;
        var firstStreamDuration = TimeSpan.Zero;

        if (root.TryGetProperty("streams", out var streams) && streams.ValueKind == JsonValueKind.Array)
        {
            foreach (var stream in streams.EnumerateArray())
            {
                var type = GetString(stream, "codec_type");
                if (duration == TimeSpan.Zero
                    && double.TryParse(GetString(stream, "duration"), NumberStyles.Float, CultureInfo.InvariantCulture, out var streamSeconds)
                    && streamSeconds > 0)
                {
                    firstStreamDuration = firstStreamDuration < TimeSpan.FromSeconds(streamSeconds)
                        ? TimeSpan.FromSeconds(streamSeconds)
                        : firstStreamDuration;
                }

                switch (type)
                {
                    case "video" when videoCodec is null:
                        videoCodec = GetString(stream, "codec_name");
                        videoProfile = GetString(stream, "profile");
                        width = GetInt(stream, "width");
                        height = GetInt(stream, "height");
                        frameRate = ParseFrameRate(GetString(stream, "avg_frame_rate"))
                                     ?? ParseFrameRate(GetString(stream, "r_frame_rate"));

                        // 封面图之类的附加视频流不应覆盖主视频流。
                        if (stream.TryGetProperty("disposition", out var disposition)
                            && TryGetInt(disposition, "attached_pic") == 1)
                        {
                            videoCodec = null;
                            videoProfile = null;
                            width = 0;
                            height = 0;
                            frameRate = null;
                        }

                        break;

                    case "audio" when audioCodec is null:
                        audioCodec = GetString(stream, "codec_name");
                        audioChannels = GetIntOrNull(stream, "channels");
                        audioSampleRate = GetIntOrNull(stream, "sample_rate");
                        audioBitRate = GetLongOrNull(stream, "bit_rate");
                        break;
                }
            }
        }

        if (duration == TimeSpan.Zero)
        {
            duration = firstStreamDuration;
        }

        if (size == 0 && File.Exists(filePath))
        {
            try
            {
                size = new FileInfo(filePath).Length;
            }
            catch
            {
                // 忽略。
            }
        }

        return new MediaInfo
        {
            Container = ContainerLabel(filePath, formatName),
            FormatName = formatName,
            Duration = duration,
            SizeBytes = size,
            BitRate = bitRate,
            Width = width,
            Height = height,
            VideoCodec = videoCodec,
            VideoProfile = videoProfile,
            FrameRate = frameRate,
            AudioCodec = audioCodec,
            AudioChannels = audioChannels,
            AudioSampleRate = audioSampleRate,
            AudioBitRate = audioBitRate
        };
    }

    private static string ContainerLabel(string filePath, string? formatName)
    {
        var extension = Path.GetExtension(filePath).TrimStart('.');
        if (!string.IsNullOrEmpty(extension))
        {
            return extension.ToUpperInvariant();
        }

        var first = formatName?.Split(',', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return string.IsNullOrEmpty(first) ? "-" : first.ToUpperInvariant();
    }

    private static double? ParseFrameRate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var parts = value.Split('/');
        if (parts.Length == 2
            && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var numerator)
            && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var denominator)
            && denominator > 0)
        {
            var fps = numerator / denominator;
            return fps is > 0 and < 1000 ? fps : null;
        }

        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var direct) && direct > 0
            ? direct
            : null;
    }

    private static string? GetString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.ToString(),
            _ => null
        };
    }

    private static int GetInt(JsonElement element, string name) => TryGetInt(element, name) ?? 0;

    private static int? GetIntOrNull(JsonElement element, string name) => TryGetInt(element, name);

    private static long? GetLongOrNull(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number => value.TryGetInt64(out var number) ? number : null,
            JsonValueKind.String => long.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var text) ? text : null,
            _ => null
        };
    }

    /// <summary>兼容 ffprobe 把数值写成字符串的情况（不同版本/不同字段并不统一）。</summary>
    private static int? TryGetInt(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number => value.TryGetInt32(out var number) ? number : null,
            JsonValueKind.String => int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var text) ? text : null,
            _ => null
        };
    }
}
