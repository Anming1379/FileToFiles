using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;
using FileToFiles.Models;
using FileToFiles.Services;
using FileToFiles.ViewModels;
using FileToFiles.Utils;
using FileToFiles.Views;
using Microsoft.Win32;

namespace FileToFiles;

/// <summary>
/// 主窗口：工具栏 + 三个选项卡 + 状态栏。
/// 视图层只负责对话框、拖放与选择状态，业务逻辑都在 <see cref="MainViewModel"/>。
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        ViewModel = new MainViewModel(App.Settings);
        DataContext = ViewModel;

        ViewModel.SelectionProvider = () => FileList.SelectedItems.Cast<InputFileItem>().ToList();
        ViewModel.ShowMessage = (title, text) =>
            MessageBox.Show(this, text, title, MessageBoxButton.OK, MessageBoxImage.Information);
        ViewModel.ShowOutputDetailWindow = ShowOutputDetail;

        FileList.SelectionChanged += (_, _) => ViewModel.RemoveSelectedCommand.NotifyCanExecuteChanged();

        // Ctrl+A 全选
        FileList.InputBindings.Add(new KeyBinding(new RelayCommand(FileList.SelectAll), Key.A, ModifierKeys.Control));

        Loaded += OnWindowLoaded;
        Closed += OnWindowClosed;

        ThemeManager.ThemeChanged += OnThemeChanged;
        ViewModel.ConversionCompleted += OnConversionCompleted;

        WindowTheme.Apply(this, ThemeManager.IsDark);
    }

    public MainViewModel ViewModel { get; }

    private int _lastTabIndex;

    private async void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        PlayStartupEntrance();

        if (App.PreviousCrash is { } crash)
        {
            ViewModel.SetCrashReport(crash);
        }

        await ViewModel.InitializeAsync();

        if (App.StartupPaths.Count > 0)
        {
            await AddPathsAsync(App.StartupPaths);
        }
    }

    /// <summary>窗口入场：内容自上而下归位（不使用淡入淡出）。</summary>
    private void PlayStartupEntrance()
    {
        try
        {
            if (RootLayer.RenderTransform is not TranslateTransform transform)
            {
                transform = new TranslateTransform();
                RootLayer.RenderTransform = transform;
            }

            var animation = new DoubleAnimation(-14, 0, TimeSpan.FromMilliseconds(240))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            transform.BeginAnimation(TranslateTransform.YProperty, animation);
        }
        catch
        {
            // 动画失败不影响使用。
        }
    }

    /// <summary>选项卡切换：内容按方向滑入。</summary>
    private void OnTabSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not TabControl tab || !ReferenceEquals(e.Source, tab))
        {
            return;
        }

        var newIndex = tab.SelectedIndex;
        if (newIndex == _lastTabIndex)
        {
            return;
        }

        // 同步播放：先写入起始偏移再起动画，避免先闪一帧原位内容。
        TabSlide.Play(tab, _lastTabIndex, newIndex);
        _lastTabIndex = newIndex;
    }

    /// <summary>被第二个实例唤醒时：还原并置前。</summary>
    public void RestoreAndActivate()
    {
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Show();
        Activate();

        // 顶部置前一次，强制抢到前台。
        Topmost = true;
        Topmost = false;
        Focus();
    }

    /// <summary>外部（第二个实例 / 拖到 exe 上）传入的路径。</summary>
    public Task ImportExternalAsync(IEnumerable<string> paths) => AddPathsAsync(paths);

    private void ShowOutputDetail(OutputItem item)
    {
        var dialog = new ErrorDetailDialog(item) { Owner = this };
        dialog.ShowDialog();
    }

    private void OnOutputListMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (OutputList.SelectedItem is OutputItem item)
        {
            ShowOutputDetail(item);
        }
    }

    // ==================== 导入 ====================

    private async void OnAddFilesClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择要转换的媒体文件",
            Filter = string.Join("|", FileImporter.DialogFilter),
            Multiselect = true,
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) == true)
        {
            await AddPathsAsync(dialog.FileNames);
        }
    }

    private async void OnAddFolderClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "选择要导入的文件夹（会递归扫描子目录）",
            Multiselect = false
        };

        if (dialog.ShowDialog(this) == true)
        {
            await AddPathsAsync(new[] { dialog.FolderName });
        }
    }

    private async Task AddPathsAsync(IEnumerable<string> paths)
    {
        try
        {
            var added = await ViewModel.AddPathsAsync(paths);
            if (added == 0 && FileList.HasItems)
            {
                MessageBox.Show(this,
                    "没有导入新文件：所选文件可能已在列表中，或不是受支持的媒体格式。",
                    "File To Files",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            else if (added == 0 && !FileList.HasItems)
            {
                MessageBox.Show(this,
                    "没有找到受支持的媒体文件。\n请确认文件格式（例如 mp4 / mkv / mov / mp3 / wav 等）是否正确。",
                    "File To Files",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
        catch (OperationCanceledException)
        {
            // 关闭窗口时取消，无需提示。
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"导入文件失败：{ex.Message}", "File To Files", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ==================== 拖放 ====================

    private void OnWindowDragEnter(object sender, DragEventArgs e) => UpdateDropState(e);

    private void OnWindowDragOver(object sender, DragEventArgs e) => UpdateDropState(e);

    private void OnWindowDragLeave(object sender, DragEventArgs e)
    {
        var position = e.GetPosition(this);
        if (position.X <= 0 || position.Y <= 0 || position.X >= ActualWidth || position.Y >= ActualHeight)
        {
            DropOverlay.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateDropState(DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            DropOverlay.Visibility = Visibility.Visible;
        }
        else
        {
            e.Effects = DragDropEffects.None;
            DropOverlay.Visibility = Visibility.Collapsed;
        }

        e.Handled = true;
    }

    private async void OnWindowDrop(object sender, DragEventArgs e)
    {
        DropOverlay.Visibility = Visibility.Collapsed;
        e.Handled = true;

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths || paths.Length == 0)
        {
            return;
        }

        await AddPathsAsync(paths);
    }

    // ==================== 列表交互 ====================

    /// <summary>右键点击时先选中该项，让右键菜单的 CommandParameter 指向正确的行。</summary>
    private void OnFileListPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        => SelectRowUnderMouse(FileList, e);

    private void OnOutputListPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        => SelectRowUnderMouse(OutputList, e);

    private void OnThemeChanged(object? sender, EventArgs e) => WindowTheme.Apply(this, ThemeManager.IsDark);

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        ThemeManager.ThemeChanged -= OnThemeChanged;
        ViewModel.ConversionCompleted -= OnConversionCompleted;
        ViewModel.Dispose();
    }

    /// <summary>一批转换结束：按设置打开输出目录 / 闪烁任务栏提醒。</summary>
    private void OnConversionCompleted(object? sender, ConversionCompletedEventArgs e)
    {
        if (ViewModel.Settings.OpenOutputWhenDone && e.Succeeded > 0)
        {
            WindowTheme.OpenFolder(ViewModel.OutputDirectory);
        }

        if (ViewModel.Settings.NotifyWhenDone)
        {
            CompletionSound.Play();
            WindowFlash.Flash(this);
        }
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        var settings = ViewModel.Settings.Clone();
        var dialog = new SettingsWindow(settings, ViewModel.Presets.Where(p => !p.IsBuiltIn))
        {
            Owner = this
        };

        var result = dialog.ShowDialog();
        if (result == true && dialog.Saved)
        {
            ViewModel.ApplySettings(dialog.ViewModel.Settings, dialog.ViewModel.CustomPresets);
        }
    }

    private static void SelectRowUnderMouse(ListView list, MouseButtonEventArgs e)
    {
        // 注意：单选模式下不能改 SelectedItems（会抛 InvalidOperationException），
        // 直接设置 IsSelected 由 Selector 自己处理互斥即可。
        if (ItemsControl.ContainerFromElement(list, (DependencyObject)e.OriginalSource) is ListViewItem item
            && !item.IsSelected)
        {
            item.IsSelected = true;
        }
    }

    // ==================== 预设 ====================

    private void OnSavePresetClick(object sender, RoutedEventArgs e)
    {
        var dialog = new PresetNameDialog
        {
            Owner = this,
            PresetName = ViewModel.SelectedPreset.Name + " 副本"
        };

        if (dialog.ShowDialog() == true)
        {
            ViewModel.AddCustomPreset(dialog.PresetName);
        }
    }
}
