using System.IO;

namespace FileToFiles.Services;

/// <summary>
/// 应用使用的各种目录（配置、日志、预设）。
/// </summary>
public static class AppPaths
{
    public static string AppDataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FileToFiles");

    public static string ConfigDirectory => Path.Combine(AppDataDirectory, "config");

    public static string LogDirectory => Path.Combine(AppDataDirectory, "logs");

    public static string CrashDirectory => Path.Combine(AppDataDirectory, "crash");

    public static string SettingsFile => Path.Combine(ConfigDirectory, "settings.json");

    public static string UserPresetsFile => Path.Combine(ConfigDirectory, "presets.json");

    public static void EnsureCreated()
    {
        foreach (var directory in new[] { AppDataDirectory, ConfigDirectory, LogDirectory, CrashDirectory })
        {
            try
            {
                Directory.CreateDirectory(directory);
            }
            catch
            {
                // 目录不可创建时不影响主流程，后续写文件会再报错。
            }
        }
    }

    /// <summary>默认输出目录：用户"视频"目录下 File To Files 文件夹。</summary>
    public static string DefaultOutputDirectory
    {
        get
        {
            var videos = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
            if (string.IsNullOrEmpty(videos))
            {
                videos = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            }

            return Path.Combine(videos, "File To Files");
        }
    }
}
