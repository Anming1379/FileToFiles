using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace FileToFiles.Services;

/// <summary>
/// 让系统绘制的标题栏跟随应用主题（Windows 10 1809+ 支持深色标题栏）。
/// </summary>
public static class WindowTheme
{
    private const int DwmwaUseImmersiveDarkMode = 20;

    // 20H1 之前的版本使用 19。
    private const int DwmwaUseImmersiveDarkModeLegacy = 19;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    public static void Apply(Window window, bool dark)
    {
        if (window is null)
        {
            return;
        }

        if (new WindowInteropHelper(window).Handle == IntPtr.Zero)
        {
            window.SourceInitialized += (_, _) => ApplyToHandle(new WindowInteropHelper(window).Handle, dark);
            return;
        }

        ApplyToHandle(new WindowInteropHelper(window).Handle, dark);
    }

    private static void ApplyToHandle(IntPtr handle, bool dark)
    {
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var value = dark ? 1 : 0;
        try
        {
            if (DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode, ref value, sizeof(int)) != 0)
            {
                DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkModeLegacy, ref value, sizeof(int));
            }
        }
        catch (EntryPointNotFoundException)
        {
            // 老系统不支持，忽略。
        }
        catch (DllNotFoundException)
        {
            // 忽略。
        }
    }

    /// <summary>在资源管理器中定位文件；文件不存在时退回打开所在目录。</summary>
    public static void RevealInExplorer(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
                return;
            }

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
            {
                System.Diagnostics.Process.Start("explorer.exe", $"\"{directory}\"");
            }
        }
        catch
        {
            // 忽略：打开资源管理器失败不应崩溃。
        }
    }

    public static void OpenFolder(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                System.Diagnostics.Process.Start("explorer.exe", $"\"{directory}\"");
            }
        }
        catch
        {
            // 忽略。
        }
    }
}
