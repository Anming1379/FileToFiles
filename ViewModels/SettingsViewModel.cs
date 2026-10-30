using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileToFiles.Models;
using FileToFiles.Services;

namespace FileToFiles.ViewModels;

/// <summary>设置窗口的视图模型。</summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly AppSettings _original;

    public SettingsViewModel(AppSettings settings, IEnumerable<ConversionPreset> customPresets)
    {
        _original = settings.Clone();
        Settings = settings;

        CustomPresets = new ObservableCollection<ConversionPreset>(customPresets);
        SelectedPreset = CustomPresets.FirstOrDefault();

        ThemeChoices = new List<Choice<AppTheme>>
        {
            new(AppTheme.System, "跟随系统"),
            new(AppTheme.Light, "浅色"),
            new(AppTheme.Dark, "深色")
        };

        ConcurrencyChoices = Enumerable.Range(1, 8).ToList();
    }

    public AppSettings Settings { get; }

    public ObservableCollection<ConversionPreset> CustomPresets { get; }

    public IReadOnlyList<Choice<AppTheme>> ThemeChoices { get; }

    public IReadOnlyList<int> ConcurrencyChoices { get; }

    [ObservableProperty]
    private int _selectedTabIndex;

    [ObservableProperty]
    private ConversionPreset? _selectedPreset;

    /// <summary>底部「保存 / 取消」只在设置页显示。</summary>
    public bool IsSettingsTab => SelectedTabIndex == 0;

    partial void OnSelectedTabIndexChanged(int value) => OnPropertyChanged(nameof(IsSettingsTab));

    [ObservableProperty]
    private string? _statusText;

    public string OutputDirectory
    {
        get => Settings.OutputDirectory;
        set
        {
            if (value == Settings.OutputDirectory)
            {
                return;
            }

            Settings.OutputDirectory = value;
            OnPropertyChanged();
        }
    }

    public int MaxConcurrency
    {
        get => Settings.MaxConcurrency;
        set
        {
            var clamped = Math.Clamp(value, 1, 8);
            if (clamped == Settings.MaxConcurrency)
            {
                return;
            }

            Settings.MaxConcurrency = clamped;
            OnPropertyChanged();
        }
    }

    public bool NotifyWhenDone
    {
        get => Settings.NotifyWhenDone;
        set
        {
            if (value == Settings.NotifyWhenDone)
            {
                return;
            }

            Settings.NotifyWhenDone = value;
            OnPropertyChanged();
        }
    }

    public bool OpenOutputWhenDone
    {
        get => Settings.OpenOutputWhenDone;
        set
        {
            if (value == Settings.OpenOutputWhenDone)
            {
                return;
            }

            Settings.OpenOutputWhenDone = value;
            OnPropertyChanged();
        }
    }

    /// <summary>主题改动立即生效（取消时由窗口还原）。</summary>
    public AppTheme Theme
    {
        get => Settings.Theme;
        set
        {
            if (value == Settings.Theme)
            {
                return;
            }

            Settings.Theme = value;
            ThemeManager.SetMode(value);
            OnPropertyChanged();
        }
    }

    public string AppVersion => "v" + (System.Reflection.Assembly.GetExecutingAssembly()
        .GetName().Version?.ToString(3) ?? "0.5.0");

    public string LogDirectory => AppPaths.LogDirectory;

    public string ConfigDirectory => AppPaths.ConfigDirectory;

    public bool HasCustomPresets => CustomPresets.Count > 0;

    public string PresetCountText => CustomPresets.Count == 0
        ? "还没有自定义预设。"
        : $"共 {CustomPresets.Count} 个自定义预设。";

    [RelayCommand]
    private void BrowseOutputDirectory()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "选择默认输出目录",
            Multiselect = false
        };

        try
        {
            if (Directory.Exists(OutputDirectory))
            {
                dialog.InitialDirectory = OutputDirectory;
            }
        }
        catch
        {
            // 忽略无效路径。
        }

        if (dialog.ShowDialog() == true)
        {
            OutputDirectory = dialog.FolderName;
        }
    }

    [RelayCommand]
    private void ResetOutputDirectory() => OutputDirectory = AppPaths.DefaultOutputDirectory;

    [RelayCommand]
    private void DeletePreset()
    {
        if (SelectedPreset is null)
        {
            return;
        }

        CustomPresets.Remove(SelectedPreset);
        SelectedPreset = CustomPresets.FirstOrDefault();
        OnPropertyChanged(nameof(HasCustomPresets));
        OnPropertyChanged(nameof(PresetCountText));
    }

    [RelayCommand]
    private void OpenLogFolder() => WindowTheme.OpenFolder(AppPaths.LogDirectory);

    /// <summary>打开 MIT 许可证全文（LICENSE）。</summary>
    [RelayCommand]
    private void OpenLicense()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "LICENSE");
        if (File.Exists(path))
        {
            WindowTheme.RevealInExplorer(path);
            return;
        }

        WindowTheme.OpenFolder(AppContext.BaseDirectory);
    }

    /// <summary>打开许可与合规说明（LICENSING.txt）。</summary>
    [RelayCommand]
    private void OpenLicensing()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "LICENSING.txt"),
            Path.Combine(AppContext.BaseDirectory, "SOURCE-OFFER.txt")
        };

        var file = candidates.FirstOrDefault(File.Exists);
        if (file is not null)
        {
            WindowTheme.RevealInExplorer(file);
            return;
        }

        WindowTheme.OpenFolder(AppContext.BaseDirectory);
    }

    /// <summary>打开随包附带的 FFmpeg 许可文件。</summary>
    [RelayCommand]
    private void OpenFfmpegLicense()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "ffmpeg", "LICENSE.txt"),
            Path.Combine(AppContext.BaseDirectory, "ffmpeg", "LICENSE"),
            Path.Combine(AppContext.BaseDirectory, "LICENSE.txt")
        };

        var license = candidates.FirstOrDefault(File.Exists);
        if (license is not null)
        {
            WindowTheme.RevealInExplorer(license);
            return;
        }

        WindowTheme.OpenFolder(Path.Combine(AppContext.BaseDirectory, "ffmpeg"));
    }

    [RelayCommand]
    private void OpenConfigFolder() => WindowTheme.OpenFolder(AppPaths.ConfigDirectory);

    /// <summary>用户取消：还原主题预览。</summary>
    public void RevertTheme() => ThemeManager.SetMode(_original.Theme);
}
