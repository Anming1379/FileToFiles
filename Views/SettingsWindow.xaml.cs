using System.Windows;
using FileToFiles.Models;
using FileToFiles.Services;
using FileToFiles.ViewModels;

namespace FileToFiles.Views;

/// <summary>设置窗口。主题改动会即时预览，取消时还原。</summary>
public partial class SettingsWindow : Window
{
    public SettingsWindow(AppSettings settings, IEnumerable<ConversionPreset> customPresets)
    {
        InitializeComponent();

        ViewModel = new SettingsViewModel(settings, customPresets);
        DataContext = ViewModel;

        WindowTheme.Apply(this, ThemeManager.IsDark);
        ThemeManager.ThemeChanged += OnThemeChanged;
    }

    public SettingsViewModel ViewModel { get; }

    private int _lastTabIndex;

    /// <summary>确认后是否保存。</summary>
    public bool Saved { get; private set; }

    private void OnThemeChanged(object? sender, EventArgs e) => WindowTheme.Apply(this, ThemeManager.IsDark);

    /// <summary>选项卡切换时内容滑入。</summary>
    private void OnTabSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (sender is not System.Windows.Controls.TabControl tab || !ReferenceEquals(e.Source, tab))
        {
            return;
        }

        var newIndex = tab.SelectedIndex;
        if (newIndex == _lastTabIndex)
        {
            return;
        }

        Utils.TabSlide.Play(tab, _lastTabIndex, newIndex);
        _lastTabIndex = newIndex;
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        var directory = ViewModel.OutputDirectory?.Trim();
        if (string.IsNullOrWhiteSpace(directory))
        {
            MessageBox.Show(this, "请填写默认输出目录。", "设置", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            System.IO.Directory.CreateDirectory(directory);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"输出目录不可用：{ex.Message}", "设置", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ViewModel.Settings.OutputDirectory = directory;
        Saved = true;
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        ViewModel.RevertTheme();
        Saved = false;
        DialogResult = false;
    }

    protected override void OnClosed(EventArgs e)
    {
        ThemeManager.ThemeChanged -= OnThemeChanged;

        // 直接关闭窗口（未保存）时也要还原主题预览。
        if (!Saved)
        {
            ViewModel.RevertTheme();
        }

        base.OnClosed(e);
    }
}
