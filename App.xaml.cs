using System.IO;
using System.Windows;
using System.Windows.Threading;
using FileToFiles.Services;

namespace FileToFiles;

/// <summary>
/// 应用入口：初始化目录、注册全局异常处理。
/// </summary>
public partial class App : Application
{
    /// <summary>命令行传入的待导入路径（支持把文件/文件夹拖到 exe 图标上打开）。</summary>
    public static IReadOnlyList<string> StartupPaths { get; private set; } = Array.Empty<string>();

    /// <summary>全局用户设置。</summary>
    public static AppSettings Settings { get; private set; } = new();

    /// <summary>上次运行的崩溃信息（正常启动时为 null）。</summary>
    public static CrashReport? PreviousCrash { get; private set; }

    // 只有拿到单实例锁的进程才负责会话标记，避免第二个实例误删标记。
    private bool _isPrimaryInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
        AppPaths.EnsureCreated();
        LogService.Initialize();

        // 单实例：已有实例在跑时把路径交给它并退出。
        if (!SingleInstance.TryAcquire())
        {
            SingleInstance.NotifyExistingInstance(e.Args);
            Shutdown();
            return;
        }

        StartupPaths = e.Args.Where(a => !a.StartsWith('-')).ToArray();
        Settings = AppSettings.Load();
        ThemeManager.Initialize(Settings.Theme);
        CompletionSound.Initialize();

        _isPrimaryInstance = true;
        PreviousCrash = CrashReporter.DetectPreviousCrash();
        CrashReporter.MarkSessionStart();

        SingleInstance.Listen(OnSecondInstanceActivated);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        base.OnStartup(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        WriteCrashLog(e.Exception);
        MessageBox.Show(
            $"发生了一个未处理的错误：{e.Exception.Message}\n\n错误日志已写入：\n{AppPaths.CrashDirectory}",
            "File To Files",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            WriteCrashLog(exception);
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        WriteCrashLog(e.Exception);
        e.SetObserved();
    }

    private void OnSecondInstanceActivated(IReadOnlyList<string> paths)
    {
        if (MainWindow is not FileToFiles.MainWindow window)
        {
            return;
        }

        window.RestoreAndActivate();

        if (paths.Count > 0)
        {
            _ = window.ImportExternalAsync(paths);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_isPrimaryInstance)
        {
            CrashReporter.MarkCleanExit();
        }

        LogService.Info("---- 退出 ----");
        LogService.Shutdown();
        base.OnExit(e);
    }

    private static void WriteCrashLog(Exception exception)
    {
        LogService.Error(exception, "未处理异常");

        try
        {
            Directory.CreateDirectory(AppPaths.CrashDirectory);
            var file = Path.Combine(AppPaths.CrashDirectory, $"crash-{DateTime.Now:yyyyMMdd-HHmmss}.log");
            File.WriteAllText(file, $"{DateTime.Now:O}{Environment.NewLine}{exception}");
        }
        catch
        {
            // 记录崩溃日志失败时不再抛出。
        }
    }
}
