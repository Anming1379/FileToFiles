using System.Diagnostics;
using System.Text;
using FileToFiles.Models;

namespace FileToFiles.Services;

/// <summary>
/// 检测当前 FFmpeg 构建里可用的硬件编码器（是否编译进来，不代表设备一定可用）。
/// </summary>
public sealed record EncoderSupport(bool Nvenc, bool Qsv, bool Amf)
{
    public static EncoderSupport None { get; } = new(false, false, false);

    /// <summary>最近一次检测结果（供 XAML 转换器判断选项是否可用）。</summary>
    public static EncoderSupport Current { get; set; } = None;

    public string Summary =>
        $"NVENC {(Nvenc ? "可用" : "不可用")} · QSV {(Qsv ? "可用" : "不可用")} · AMF {(Amf ? "可用" : "不可用")}";

    public bool Supports(HardwareAcceleration hardware) => hardware switch
    {
        HardwareAcceleration.Nvenc => Nvenc,
        HardwareAcceleration.Qsv => Qsv,
        HardwareAcceleration.Amf => Amf,
        _ => true
    };

    public string Describe(HardwareAcceleration hardware) => hardware switch
    {
        HardwareAcceleration.Nvenc => Nvenc ? "NVENC 可用" : "未检测到 NVENC",
        HardwareAcceleration.Qsv => Qsv ? "QSV 可用" : "未检测到 QSV",
        HardwareAcceleration.Amf => Amf ? "AMF 可用" : "未检测到 AMF",
        HardwareAcceleration.Off => "使用软件编码",
        _ => "自动（软件编码）"
    };

    public static async Task<EncoderSupport> DetectAsync(string ffmpegPath, CancellationToken cancellationToken = default)
    {
        try
        {
            var psi = new ProcessStartInfo(ffmpegPath)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            psi.ArgumentList.Add("-hide_banner");
            psi.ArgumentList.Add("-encoders");

            using var process = new Process { StartInfo = psi };
            process.Start();

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            using var registration = timeout.Token.Register(() => FfmpegLocator.TryKill(process));

            var stdout = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var stderr = process.StandardError.ReadToEndAsync(timeout.Token);
            _ = await stderr.ConfigureAwait(false);
            var output = await stdout.ConfigureAwait(false);
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);

            return new EncoderSupport(
                output.Contains("h264_nvenc", StringComparison.Ordinal),
                output.Contains("h264_qsv", StringComparison.Ordinal),
                output.Contains("h264_amf", StringComparison.Ordinal));
        }
        catch
        {
            return None;
        }
    }
}
