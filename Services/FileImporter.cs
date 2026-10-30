using System.IO;

namespace FileToFiles.Services;

/// <summary>
/// 负责把拖放 / 对话框得到的路径展开成受支持的媒体文件列表（文件夹递归扫描）。
/// </summary>
public static class FileImporter
{
    /// <summary>受支持的输入扩展名（小写，含点）。</summary>
    public static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        // 视频
        ".mp4", ".mkv", ".mov", ".avi", ".wmv", ".flv", ".webm", ".m4v", ".mpg", ".mpeg",
        ".ts", ".m2ts", ".mts", ".3gp", ".3g2", ".ogv", ".rmvb", ".rm", ".vob", ".asf", ".f4v", ".divx",
        // 音频
        ".mp3", ".wav", ".flac", ".aac", ".m4a", ".ogg", ".oga", ".opus", ".wma", ".aiff", ".aif",
        ".ape", ".alac", ".ac3", ".dts", ".amr", ".mka", ".wv"
    };

    public static readonly string[] DialogFilter =
    {
        "媒体文件|*.mp4;*.mkv;*.mov;*.avi;*.wmv;*.flv;*.webm;*.m4v;*.mpg;*.mpeg;*.ts;*.m2ts;*.mts;*.3gp;*.3g2;*.ogv;*.rmvb;*.rm;*.vob;*.asf;*.f4v;*.divx;*.mp3;*.wav;*.flac;*.aac;*.m4a;*.ogg;*.oga;*.opus;*.wma;*.aiff;*.aif;*.ape;*.alac;*.ac3;*.dts;*.amr;*.mka;*.wv",
        "视频文件|*.mp4;*.mkv;*.mov;*.avi;*.wmv;*.flv;*.webm;*.m4v;*.mpg;*.mpeg;*.ts;*.m2ts;*.mts;*.3gp;*.3g2;*.ogv;*.rmvb;*.rm;*.vob;*.asf;*.f4v;*.divx",
        "音频文件|*.mp3;*.wav;*.flac;*.aac;*.m4a;*.ogg;*.oga;*.opus;*.wma;*.aiff;*.aif;*.ape;*.alac;*.ac3;*.dts;*.amr;*.mka;*.wv",
        "所有文件|*.*"
    };

    /// <summary>
    /// 展开路径集合：文件直接返回（限定受支持扩展名），文件夹递归枚举。
    /// </summary>
    public static IEnumerable<string> Expand(IEnumerable<string> paths, CancellationToken cancellationToken = default)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in paths)
        {
            foreach (var file in ExpandOne(path, cancellationToken))
            {
                if (seen.Add(file))
                {
                    yield return file;
                }
            }
        }
    }

    private static IEnumerable<string> ExpandOne(string path, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            yield break;
        }

        if (File.Exists(path))
        {
            if (IsSupported(path))
            {
                yield return Path.GetFullPath(path);
            }

            yield break;
        }

        if (Directory.Exists(path))
        {
            foreach (var file in EnumerateDirectory(path, cancellationToken))
            {
                yield return file;
            }
        }
    }

    /// <summary>递归枚举目录，遇到无权限的子目录直接跳过而不是整体失败。</summary>
    public static IEnumerable<string> EnumerateDirectory(string directory, CancellationToken cancellationToken = default)
    {
        var stack = new Stack<string>();
        stack.Push(directory);

        while (stack.Count > 0)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                yield break;
            }

            var current = stack.Pop();

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(current);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or DirectoryNotFoundException)
            {
                continue;
            }

            foreach (var file in files)
            {
                if (IsSupported(file))
                {
                    yield return file;
                }
            }

            IEnumerable<string> subDirectories;
            try
            {
                subDirectories = Directory.EnumerateDirectories(current);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or DirectoryNotFoundException)
            {
                continue;
            }

            foreach (var subDirectory in subDirectories)
            {
                // 跳过系统目录与符号链接，避免死循环。
                try
                {
                    var info = new DirectoryInfo(subDirectory);
                    if (info.Attributes.HasFlag(FileAttributes.ReparsePoint))
                    {
                        continue;
                    }
                }
                catch
                {
                    continue;
                }

                stack.Push(subDirectory);
            }
        }
    }

    public static bool IsSupported(string path)
    {
        return SupportedExtensions.Contains(Path.GetExtension(path));
    }
}
