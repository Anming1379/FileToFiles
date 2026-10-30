using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace FileToFiles.Services;

/// <summary>
/// FFmpeg 定位结果。
/// </summary>
public sealed record FfmpegLocation(string? FfmpegPath, string? FfprobePath, string? Version)
{
    public static FfmpegLocation NotFound { get; } = new(null, null, null);

    public bool IsReady => !string.IsNullOrEmpty(FfmpegPath) && !string.IsNullOrEmpty(FfprobePath);

    public string Summary => IsReady
        ? (string.IsNullOrWhiteSpace(Version) ? "就绪" : $"就绪 · {Version}")
        : "未找到";
}

/// <summary>
/// 负责定位 ffmpeg.exe / ffprobe.exe 并读取版本。
/// 查找顺序：自定义目录 → 程序目录\ffmpeg → 程序目录\ffmpeg\bin → 程序目录 → PATH。
/// </summary>
public static class FfmpegLocator
{
    private static readonly Regex VersionRegex = new(@"ffmpeg version (\S+)", RegexOptions.Compiled);

    public static FfmpegLocation Current { get; private set; } = FfmpegLocation.NotFound;

    public static async Task<FfmpegLocation> LocateAsync(CancellationToken cancellationToken = default)
    {
        foreach (var directory in CandidateDirectories())
        {
            var ffmpeg = Path.Combine(directory, "ffmpeg.exe");
            var ffprobe = Path.Combine(directory, "ffprobe.exe");
            if (File.Exists(ffmpeg) && File.Exists(ffprobe))
            {
                var version = await ReadVersionAsync(ffmpeg, cancellationToken).ConfigureAwait(false);
                return Current = new FfmpegLocation(ffmpeg, ffprobe, version);
            }
        }

        // 最后尝试 PATH。
        var fromPath = FindOnPath("ffmpeg.exe");
        var probeFromPath = FindOnPath("ffprobe.exe");
        if (fromPath is not null && probeFromPath is not null)
        {
            var version = await ReadVersionAsync(fromPath, cancellationToken).ConfigureAwait(false);
            return Current = new FfmpegLocation(fromPath, probeFromPath, version);
        }

        return Current = FfmpegLocation.NotFound;
    }

    private static IEnumerable<string> CandidateDirectories()
    {
        // FFmpeg 随应用一起打包，只从程序目录查找（另有 PATH 兜底）。
        var baseDir = AppContext.BaseDirectory;
        yield return Path.Combine(baseDir, "ffmpeg");
        yield return Path.Combine(baseDir, "ffmpeg", "bin");
        yield return baseDir;
    }

    private static string? FindOnPath(string fileName)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        foreach (var part in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(part.Trim('"'), fileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch
            {
                // 忽略非法 PATH 项。
            }
        }

        return null;
    }

    /// <summary>执行 "ffmpeg -version" 取第一行的版本号。</summary>
    public static async Task<string?> ReadVersionAsync(string ffmpegPath, CancellationToken cancellationToken = default)
    {
        try
        {
            var psi = new ProcessStartInfo(ffmpegPath)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add("-version");

            using var process = Process.Start(psi);
            if (process is null)
            {
                return null;
            }

            var firstLine = await process.StandardOutput.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try
            {
                await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                TryKill(process);
            }

            if (string.IsNullOrWhiteSpace(firstLine))
            {
                return null;
            }

            var match = VersionRegex.Match(firstLine);
            return match.Success ? match.Groups[1].Value : firstLine.Trim();
        }
        catch
        {
            return null;
        }
    }

    internal static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // 进程可能已退出。
        }
    }
}
