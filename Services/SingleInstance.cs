using System.IO;
using System.Threading;

namespace FileToFiles.Services;

/// <summary>
/// 单实例控制：第二个实例把命令行路径交给第一个实例，然后退出。
/// </summary>
public static class SingleInstance
{
    private const string MutexName = @"Local\FileToFiles.SingleInstance.Mutex";
    private const string EventName = @"Local\FileToFiles.SingleInstance.Activate";

    private static Mutex? _mutex;
    private static Semaphore? _signal;
    private static Thread? _listener;

    private static string DropFile => Path.Combine(AppPaths.ConfigDirectory, "pending-drop.txt");

    /// <summary>尝试成为唯一实例。</summary>
    public static bool TryAcquire()
    {
        try
        {
            _mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
            if (createdNew)
            {
                // 用信号量计数：第二个实例每 Release 一次，这里就只唤醒一次。
                _signal = new Semaphore(0, int.MaxValue, EventName);

                // 上个实例被强杀时可能残留计数，先清干净。
                while (_signal.WaitOne(0))
                {
                }

                return true;
            }

            _mutex.Dispose();
            _mutex = null;
            return false;
        }
        catch (Exception ex)
        {
            // 无法创建互斥体时按单实例放行，避免应用完全打不开。
            LogService.Warn(ex, "单实例检测失败，按普通启动处理");
            return true;
        }
    }

    /// <summary>
    /// 第一个实例监听激活信号；收到后把第二个实例留下的路径交给 <paramref name="onActivated"/>。
    /// </summary>
    public static void Listen(Action<IReadOnlyList<string>> onActivated)
    {
        if (_signal is null)
        {
            return;
        }

        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return;
        }

        // 用专用后台线程等待事件：AutoReset 事件每 Set 一次只会触发一次，不会重复回调。
        _listener = new Thread(() =>
        {
            while (true)
            {
                try
                {
                    if (!_signal.WaitOne())
                    {
                        return;
                    }
                }
                catch (ObjectDisposedException)
                {
                    return;
                }

                var paths = ReadPendingPaths();
                dispatcher.BeginInvoke(new Action(() => onActivated(paths)));
            }
        })
        {
            IsBackground = true,
            Name = "FileToFiles.SingleInstance"
        };

        _listener.Start();
    }

    /// <summary>第二个实例：把路径写入暂存文件并唤醒已有实例。</summary>
    public static void NotifyExistingInstance(IEnumerable<string> paths)
    {
        try
        {
            var list = paths.ToList();
            if (list.Count > 0)
            {
                Directory.CreateDirectory(AppPaths.ConfigDirectory);
                File.WriteAllLines(DropFile, list);
            }

            if (Semaphore.TryOpenExisting(EventName, out var handle))
            {
                using (handle)
                {
                    handle.Release();
                }
            }
        }
        catch (Exception ex)
        {
            LogService.Warn(ex, "通知已有实例失败");
        }
    }

    private static IReadOnlyList<string> ReadPendingPaths()
    {
        try
        {
            if (!File.Exists(DropFile))
            {
                return Array.Empty<string>();
            }

            var lines = File.ReadAllLines(DropFile)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToList();
            File.Delete(DropFile);
            return lines;
        }
        catch (Exception ex)
        {
            LogService.Warn(ex, "读取待导入路径失败");
            return Array.Empty<string>();
        }
    }
}
