using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace FileToFiles.Services;

/// <summary>转换失败，携带可读原因与完整命令。</summary>
public sealed class ConversionException : Exception
{
    public ConversionException(string message, string commandLine, string? stderr = null, int? exitCode = null)
        : base(message)
    {
        CommandLine = commandLine;
        Stderr = stderr;
        ExitCode = exitCode;
    }

    public string CommandLine { get; }

    public string? Stderr { get; }

    public int? ExitCode { get; }
}

/// <summary>
/// 一次转换任务：输入、临时输出、最终输出与总时长。
/// <see cref="IsImageSequence"/> 为 true 时 TempPath/FinalPath 表示文件夹。
/// </summary>
public sealed record ConversionJob(
    string InputPath,
    string TempPath,
    string FinalPath,
    TimeSpan Duration,
    bool IsImageSequence = false);

/// <summary>转换结果。</summary>
public sealed record ConversionResult(string FinalPath, TimeSpan Elapsed, long SizeBytes, int? FrameCount = null);

/// <summary>
/// 单个文件的 ffmpeg 执行：进度解析、stderr 收集、取消、临时文件改名。
/// </summary>
public sealed class ConversionEngine
{
    private const int StderrTailLines = 24;
    private static readonly TimeSpan ProgressThrottle = TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan StopKillGrace = TimeSpan.FromSeconds(2);

    /// <summary>
    /// 执行转换。进度为 0-100；成功时把临时文件重命名为最终文件。
    /// </summary>
    public async Task<ConversionResult> ConvertAsync(
        string ffmpegPath,
        FfmpegCommand command,
        ConversionJob job,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        if (job.IsImageSequence)
        {
            EnsureDirectory(job.TempPath);
        }
        else
        {
            EnsureOutputDirectory(job.TempPath);
        }

        var stderrTail = new Queue<string>();
        var stderrLock = new object();

        var psi = new ProcessStartInfo(ffmpegPath)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        foreach (var argument in command.Arguments)
        {
            psi.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = psi };

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            CleanupJob(job);
            throw new ConversionException($"无法启动 FFmpeg：{ex.Message}", command.DisplayText);
        }

        var stopwatch = Stopwatch.StartNew();

        // stderr 只保留末尾若干行，避免长任务占用内存。
        var stderrTask = Task.Run(async () =>
        {
            while (await process.StandardError.ReadLineAsync().ConfigureAwait(false) is { } line)
            {
                lock (stderrLock)
                {
                    stderrTail.Enqueue(line);
                    while (stderrTail.Count > StderrTailLines)
                    {
                        stderrTail.Dequeue();
                    }
                }
            }
        });

        using var registration = cancellationToken.Register(() => TryStop(process));

        var lastReport = TimeSpan.Zero;
        try
        {
            while (await process.StandardOutput.ReadLineAsync().ConfigureAwait(false) is { } line)
            {
                if (!line.StartsWith("out_time=", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!TimeSpan.TryParse(line.AsSpan("out_time=".Length), CultureInfo.InvariantCulture, out var position))
                {
                    continue;
                }

                if (stopwatch.Elapsed - lastReport < ProgressThrottle)
                {
                    continue;
                }

                lastReport = stopwatch.Elapsed;
                if (job.Duration > TimeSpan.Zero)
                {
                    var percent = Math.Clamp(position.TotalMilliseconds / job.Duration.TotalMilliseconds * 100.0, 0, 99.5);
                    progress?.Report(percent);
                }
            }
        }
        catch (Exception)
        {
            // stdout 管道可能因进程被终止而中断。
        }

        await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
        await stderrTask.ConfigureAwait(false);
        stopwatch.Stop();

        if (cancellationToken.IsCancellationRequested)
        {
            CleanupJob(job);
            throw new OperationCanceledException(cancellationToken);
        }

        string stderrText;
        lock (stderrLock)
        {
            stderrText = string.Join(Environment.NewLine, stderrTail);
        }

        if (process.ExitCode != 0)
        {
            CleanupJob(job);
            throw new ConversionException(
                DescribeFailure(stderrText, process.ExitCode),
                command.DisplayText,
                stderrText,
                process.ExitCode);
        }

        if (job.IsImageSequence)
        {
            return CommitImageSequence(job, command, stderrText, process.ExitCode, stopwatch.Elapsed, progress);
        }

        if (!File.Exists(job.TempPath))
        {
            throw new ConversionException(
                "FFmpeg 报告成功但没有生成输出文件，请检查输出目录权限。",
                command.DisplayText,
                stderrText,
                process.ExitCode);
        }

        long size;
        try
        {
            // 覆盖同名文件：先删除再改名。
            if (File.Exists(job.FinalPath))
            {
                File.Delete(job.FinalPath);
            }

            File.Move(job.TempPath, job.FinalPath);
            size = new FileInfo(job.FinalPath).Length;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            CleanupJob(job);
            throw new ConversionException(
                $"无法写入输出文件：{DescribeIoError(ex)}",
                command.DisplayText,
                stderrText,
                process.ExitCode);
        }

        progress?.Report(100);
        return new ConversionResult(job.FinalPath, stopwatch.Elapsed, size);
    }

    /// <summary>把临时文件夹里的帧移动到最终文件夹，并删除临时文件夹。</summary>
    private static ConversionResult CommitImageSequence(
        ConversionJob job,
        FfmpegCommand command,
        string stderrText,
        int exitCode,
        TimeSpan elapsed,
        IProgress<double>? progress)
    {
        if (!Directory.Exists(job.TempPath))
        {
            throw new ConversionException(
                "FFmpeg 报告成功但没有生成图像帧，请检查输出目录权限。",
                command.DisplayText,
                stderrText,
                exitCode);
        }

        var frames = Directory.GetFiles(job.TempPath, "*.png");
        if (frames.Length == 0)
        {
            CleanupJob(job);
            throw new ConversionException(
                "FFmpeg 报告成功但没有生成图像帧。",
                command.DisplayText,
                stderrText,
                exitCode);
        }

        long totalSize = 0;
        try
        {
            if (Directory.Exists(job.FinalPath))
            {
                Directory.Delete(job.FinalPath, recursive: true);
            }

            Directory.CreateDirectory(job.FinalPath);
            foreach (var frame in frames)
            {
                var target = Path.Combine(job.FinalPath, Path.GetFileName(frame));
                File.Move(frame, target, overwrite: true);
                totalSize += new FileInfo(target).Length;
            }

            Directory.Delete(job.TempPath, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            CleanupJob(job);
            throw new ConversionException(
                $"无法写入输出文件夹：{DescribeIoError(ex)}",
                command.DisplayText,
                stderrText,
                exitCode);
        }

        progress?.Report(100);
        return new ConversionResult(job.FinalPath, elapsed, totalSize, frames.Length);
    }

    private static void EnsureDirectory(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            throw new ConversionException($"输出目录不可用：{DescribeIoError(ex)}", "ffmpeg");
        }
    }

    private static void EnsureOutputDirectory(string tempPath)
    {
        var directory = Path.GetDirectoryName(tempPath);
        if (string.IsNullOrEmpty(directory))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(directory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            throw new ConversionException($"输出目录不可用：{DescribeIoError(ex)}", "ffmpeg");
        }
    }

    private static void TryStop(Process process)
    {
        try
        {
            if (process.HasExited)
            {
                return;
            }

            if (!process.WaitForExit((int)StopKillGrace.TotalMilliseconds))
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // 进程可能已退出。
        }
    }

    private static void CleanupJob(ConversionJob job)
    {
        try
        {
            if (job.IsImageSequence)
            {
                if (Directory.Exists(job.TempPath))
                {
                    Directory.Delete(job.TempPath, recursive: true);
                }

                return;
            }

            if (File.Exists(job.TempPath))
            {
                File.Delete(job.TempPath);
            }
        }
        catch
        {
            // 清理失败不影响结果展示。
        }
    }

    /// <summary>把 FFmpeg 的英文 stderr 翻译成用户能看懂的原因。</summary>
    public static string DescribeFailure(string stderr, int exitCode)
    {
        var text = stderr ?? string.Empty;

        if (text.Contains("Permission denied", StringComparison.OrdinalIgnoreCase))
        {
            return "输出目录没有写入权限。";
        }

        if (text.Contains("No space left on device", StringComparison.OrdinalIgnoreCase))
        {
            return "磁盘空间不足。";
        }

        if (text.Contains("No such file or directory", StringComparison.OrdinalIgnoreCase))
        {
            return "输入文件不存在或已被移动。";
        }

        if (text.Contains("Unknown encoder", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Encoder not found", StringComparison.OrdinalIgnoreCase))
        {
            return "当前 FFmpeg 构建不包含所需的编码器。";
        }

        if (text.Contains("does not contain any stream", StringComparison.OrdinalIgnoreCase))
        {
            return "源文件中没有可转换的音视频流。";
        }

        if (text.Contains("Invalid data found when processing input", StringComparison.OrdinalIgnoreCase))
        {
            return "无法解析输入文件，文件可能已损坏或格式不受支持。";
        }

        if (text.Contains("height not divisible", StringComparison.OrdinalIgnoreCase))
        {
            return "分辨率无法被编码器接受，请换一个目标分辨率。";
        }

        var lastLine = text
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault();

        return string.IsNullOrWhiteSpace(lastLine)
            ? $"FFmpeg 退出码 {exitCode}。"
            : lastLine;
    }

    private static string DescribeIoError(Exception ex) => ex switch
    {
        UnauthorizedAccessException => "没有写入权限。",
        DirectoryNotFoundException => "目录不存在。",
        PathTooLongException => "路径过长。",
        IOException io when io.Message.Contains("used by another process", StringComparison.OrdinalIgnoreCase)
            => "文件正被其他程序占用。",
        _ => ex.Message
    };
}
