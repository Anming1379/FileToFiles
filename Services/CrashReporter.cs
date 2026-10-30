using System.IO;

namespace FileToFiles.Services;

/// <summary>上次运行的崩溃信息。</summary>
public sealed record CrashReport(string LogFile, DateTime Timestamp);

/// <summary>
/// 会话标记：正常退出会删除标记文件，启动时若发现标记仍在，说明上次是异常退出。
/// </summary>
public static class CrashReporter
{
    private static string SessionFlag => Path.Combine(AppPaths.ConfigDirectory, "session.lock");

    /// <summary>检测上次是否为异常退出。</summary>
    public static CrashReport? DetectPreviousCrash()
    {
        try
        {
            if (!File.Exists(SessionFlag))
            {
                return null;
            }

            var latest = LatestCrashLog();
            return latest is null ? null : new CrashReport(latest, File.GetLastWriteTime(latest));
        }
        catch (Exception ex)
        {
            LogService.Warn(ex, "检测上次崩溃失败");
            return null;
        }
    }

    public static void MarkSessionStart()
    {
        try
        {
            Directory.CreateDirectory(AppPaths.ConfigDirectory);
            File.WriteAllText(SessionFlag, DateTime.Now.ToString("O"));
        }
        catch (Exception ex)
        {
            LogService.Warn(ex, "写入会话标记失败");
        }
    }

    public static void MarkCleanExit()
    {
        try
        {
            if (File.Exists(SessionFlag))
            {
                File.Delete(SessionFlag);
            }
        }
        catch (Exception ex)
        {
            LogService.Warn(ex, "清理会话标记失败");
        }
    }

    private static string? LatestCrashLog()
    {
        try
        {
            if (!Directory.Exists(AppPaths.CrashDirectory))
            {
                return null;
            }

            return Directory.EnumerateFiles(AppPaths.CrashDirectory, "crash-*.log")
                .OrderByDescending(File.GetLastWriteTime)
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    public static void OpenCrashFolder() => WindowTheme.OpenFolder(AppPaths.CrashDirectory);
}
