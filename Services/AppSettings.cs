using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FileToFiles.Services;

/// <summary>主题模式。</summary>
public enum AppTheme
{
    System,
    Light,
    Dark
}

/// <summary>
/// 用户设置（持久化到 %LOCALAPPDATA%\FileToFiles\config\settings.json）。
/// </summary>
public sealed class AppSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>默认输出目录。</summary>
    public string OutputDirectory { get; set; } = AppPaths.DefaultOutputDirectory;

    /// <summary>最大并发转换数（1–8）。</summary>
    public int MaxConcurrency { get; set; } = 2;

    /// <summary>转换完成后闪烁任务栏提醒。</summary>
    public bool NotifyWhenDone { get; set; } = true;

    /// <summary>转换完成后自动打开输出目录。</summary>
    public bool OpenOutputWhenDone { get; set; }

    /// <summary>主题。</summary>
    public AppTheme Theme { get; set; } = AppTheme.System;

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(AppPaths.SettingsFile))
            {
                var json = File.ReadAllText(AppPaths.SettingsFile);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (settings is not null)
                {
                    return settings.Normalize();
                }
            }
        }
        catch (Exception ex)
        {
            LogService.Warn(ex, "读取设置失败，使用默认设置");
        }

        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(AppPaths.ConfigDirectory);
            File.WriteAllText(AppPaths.SettingsFile, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch (Exception ex)
        {
            LogService.Warn(ex, "保存设置失败");
        }
    }

    public AppSettings Clone() => new()
    {
        OutputDirectory = OutputDirectory,
        MaxConcurrency = MaxConcurrency,
        NotifyWhenDone = NotifyWhenDone,
        OpenOutputWhenDone = OpenOutputWhenDone,
        Theme = Theme
    };

    public AppSettings Normalize()
    {
        MaxConcurrency = Math.Clamp(MaxConcurrency, 1, 8);

        if (string.IsNullOrWhiteSpace(OutputDirectory))
        {
            OutputDirectory = AppPaths.DefaultOutputDirectory;
        }

        if (!Enum.IsDefined(Theme))
        {
            Theme = AppTheme.System;
        }

        return this;
    }
}
