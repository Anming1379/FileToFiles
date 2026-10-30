using System.Windows;
using Microsoft.Win32;

namespace FileToFiles.Services;

/// <summary>
/// 主题管理：浅色 / 深色 / 跟随系统，切换即时生效，并监听系统主题变化。
/// </summary>
public static class ThemeManager
{
    // 用带程序集名的绝对 pack URI，保证在任何宿主里都能解析到资源。
    private static readonly Uri DarkUri =
        new("pack://application:,,,/FileToFiles;component/Themes/Dark.xaml", UriKind.Absolute);

    private static readonly Uri LightUri =
        new("pack://application:,,,/FileToFiles;component/Themes/Light.xaml", UriKind.Absolute);

    private static bool _applied;
    private static bool _hooked;

    public static AppTheme Mode { get; private set; } = AppTheme.System;

    public static bool IsDark { get; private set; } = true;

    /// <summary>主题实际生效后触发（资源字典已替换）。</summary>
    public static event EventHandler? ThemeChanged;

    public static void Initialize(AppTheme mode)
    {
        Mode = mode;
        HookSystem();
        Apply(force: true);
    }

    /// <summary>切换模式并立即应用。</summary>
    public static void SetMode(AppTheme mode)
    {
        Mode = mode;
        Apply(force: true);
    }

    private static void Apply(bool force = false)
    {
        var dark = Mode switch
        {
            AppTheme.Light => false,
            AppTheme.Dark => true,
            _ => IsSystemDark()
        };

        if (!force && _applied && dark == IsDark)
        {
            return;
        }

        IsDark = dark;
        _applied = true;

        var application = Application.Current;
        if (application is null)
        {
            return;
        }

        try
        {
            var palette = new ResourceDictionary { Source = dark ? DarkUri : LightUri };
            var dictionaries = application.Resources.MergedDictionaries;

            if (dictionaries.Count > 0)
            {
                dictionaries[0] = palette;
            }
            else
            {
                dictionaries.Add(palette);
            }
        }
        catch (Exception ex)
        {
            LogService.Warn(ex, "切换主题失败");
            return;
        }

        LogService.Info("主题切换为 {Theme}（深色={Dark}）", Mode, dark);
        ThemeChanged?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>读取系统"应用模式"设置（0 = 深色）。</summary>
    public static bool IsSystemDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (key?.GetValue("AppsUseLightTheme") is int light)
            {
                return light == 0;
            }
        }
        catch
        {
            // 读不到就按深色处理。
        }

        return true;
    }

    private static void HookSystem()
    {
        if (_hooked)
        {
            return;
        }

        _hooked = true;
        SystemEvents.UserPreferenceChanged += (_, e) =>
        {
            if (Mode != AppTheme.System)
            {
                return;
            }

            if (e.Category is not (UserPreferenceCategory.General or UserPreferenceCategory.Color
                or UserPreferenceCategory.VisualStyle))
            {
                return;
            }

            var application = Application.Current;
            if (application is null)
            {
                return;
            }

            // SystemEvents 在独立线程触发，切回 UI 线程处理。
            application.Dispatcher.BeginInvoke(new Action(() => Apply()));
        };
    }
}
