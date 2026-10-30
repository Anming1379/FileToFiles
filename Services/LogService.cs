using System.IO;
using Serilog;
using Serilog.Events;

namespace FileToFiles.Services;

/// <summary>
/// Serilog 滚动日志：按天分文件，保留 7 天。
/// </summary>
public static class LogService
{
    private static bool _initialized;

    public static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(AppPaths.LogDirectory);
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.File(
                    Path.Combine(AppPaths.LogDirectory, "app-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    shared: false,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            _initialized = true;
            Info("---- File To Files {Version} 启动 ----",
                typeof(LogService).Assembly.GetName().Version?.ToString(3) ?? "0.3.0");
        }
        catch
        {
            // 日志不可用时不影响应用运行。
        }
    }

    public static void Info(string messageTemplate, params object?[] args)
    {
        if (_initialized)
        {
            Log.Information(messageTemplate, args);
        }
    }

    public static void Warn(string messageTemplate, params object?[] args)
    {
        if (_initialized)
        {
            Log.Warning(messageTemplate, args);
        }
    }

    public static void Warn(Exception exception, string messageTemplate, params object?[] args)
    {
        if (_initialized)
        {
            Log.Warning(exception, messageTemplate, args);
        }
    }

    public static void Error(Exception exception, string messageTemplate, params object?[] args)
    {
        if (_initialized)
        {
            Log.Error(exception, messageTemplate, args);
        }
    }

    public static void Error(string messageTemplate, params object?[] args)
    {
        if (_initialized)
        {
            Log.Error(messageTemplate, args);
        }
    }

    public static void Shutdown()
    {
        if (_initialized)
        {
            Log.CloseAndFlush();
            _initialized = false;
        }
    }

    /// <summary>保留给将来分级使用。</summary>
    public static bool IsEnabled(LogEventLevel level) => _initialized && Log.IsEnabled(level);
}
