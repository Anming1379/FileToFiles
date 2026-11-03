using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using FileToFiles.Utils;

namespace FileToFiles.Models;

/// <summary>
/// 输出文件列表中的一行。
/// </summary>
public partial class OutputItem : ObservableObject
{
    public OutputItem(
        InputFileItem source,
        string outputPath,
        string format,
        TimeSpan elapsed,
        long sizeBytes,
        bool success,
        string? error = null,
        int? frameCount = null,
        string? commandLine = null,
        string? stderr = null,
        bool skipped = false)
    {
        Source = source;
        OutputPath = outputPath;
        Format = format;
        Elapsed = elapsed;
        SizeBytes = sizeBytes;
        IsSuccess = success;
        IsSkipped = skipped;
        Error = error;
        FrameCount = frameCount;
        CommandLine = commandLine;
        Stderr = stderr;
    }

    public InputFileItem Source { get; }

    public string OutputPath { get; }

    public string Format { get; }

    public TimeSpan Elapsed { get; }

    public long SizeBytes { get; }

    public bool IsSuccess { get; }

    /// <summary>因源与目标类型不兼容而跳过（不算失败）。</summary>
    public bool IsSkipped { get; }

    public string? Error { get; }

    /// <summary>图像序列的帧数；其他格式为 null。</summary>
    public int? FrameCount { get; }

    /// <summary>失败时的完整 FFmpeg 命令（便于排查与复制）。</summary>
    public string? CommandLine { get; }

    /// <summary>失败时的 FFmpeg 标准错误输出。</summary>
    public string? Stderr { get; }

    public bool HasCommand => !string.IsNullOrWhiteSpace(CommandLine);

    public bool HasStderr => !string.IsNullOrWhiteSpace(Stderr);

    public string FileName => FrameCount is { } frames
        ? $"{Path.GetFileName(OutputPath)}（{frames} 帧）"
        : Path.GetFileName(OutputPath);

    public string SizeText => IsSuccess && SizeBytes > 0 ? Formatters.FormatSize(SizeBytes) : "-";

    public string ElapsedText => Elapsed <= TimeSpan.Zero ? "-" : $"{Elapsed.TotalSeconds:0.#} 秒";

    public string StatusText => IsSkipped ? "已跳过" : IsSuccess ? "已完成" : "失败";

    public bool HasFile => IsSuccess && !IsSkipped
        && (FrameCount is not null ? Directory.Exists(OutputPath) : File.Exists(OutputPath));

    public bool IsFolder => FrameCount is not null;

    public string DetailText => IsSkipped
        ? $"源：{Source.FullPath}{Environment.NewLine}跳过原因：{Error}"
        : IsSuccess
            ? $"输出：{OutputPath}" + (FrameCount is { } frames ? $"{Environment.NewLine}帧数：{frames}" : string.Empty)
            : $"输出：{OutputPath}{Environment.NewLine}错误：{Error}";
}
