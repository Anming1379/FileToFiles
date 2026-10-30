using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace FileToFiles.Services;

/// <summary>任务栏闪烁提醒（转换完成后调用）。</summary>
public static class WindowFlash
{
    private const uint FlashwStop = 0x00000000;
    private const uint FlashwTray = 0x00000002;
    private const uint FlashwTimerNoFg = 0x0000000C;

    [StructLayout(LayoutKind.Sequential)]
    private struct FlashInfo
    {
        public uint Size;
        public IntPtr Hwnd;
        public uint Flags;
        public uint Count;
        public uint Timeout;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FlashWindowEx(ref FlashInfo info);

    public static void Flash(Window window)
    {
        try
        {
            var handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero || window.IsActive)
            {
                return;
            }

            var info = new FlashInfo
            {
                Size = (uint)Marshal.SizeOf<FlashInfo>(),
                Hwnd = handle,
                Flags = FlashwTray | FlashwTimerNoFg,
                Count = 3,
                Timeout = 0
            };

            FlashWindowEx(ref info);
        }
        catch
        {
            // 提醒失败不影响主流程。
        }
    }

    public static void Stop(Window window)
    {
        try
        {
            var handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero)
            {
                return;
            }

            var info = new FlashInfo
            {
                Size = (uint)Marshal.SizeOf<FlashInfo>(),
                Hwnd = handle,
                Flags = FlashwStop
            };

            FlashWindowEx(ref info);
        }
        catch
        {
            // 忽略。
        }
    }
}
