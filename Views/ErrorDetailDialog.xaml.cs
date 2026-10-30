using System.Windows;
using System.Windows.Media;
using FileToFiles.Models;
using FileToFiles.Services;

namespace FileToFiles.Views;

/// <summary>输出条目的详情（错误原因 / FFmpeg 命令 / stderr）。</summary>
public partial class ErrorDetailDialog : Window
{
    private readonly OutputItem _item;

    public ErrorDetailDialog(OutputItem item)
    {
        InitializeComponent();
        _item = item;

        Title = item.IsSuccess ? "转换详情" : "转换失败";
        TitleText.Text = item.FileName;
        SourceText.Text = item.Source.FullPath;
        OutputText.Text = item.OutputPath;
        ReasonText.Text = item.IsSuccess
            ? "转换已完成。"
            : item.Error ?? "未知错误。";
        ReasonText.Foreground = item.IsSuccess
            ? (Brush)FindResource("Brush.Success")
            : (Brush)FindResource("Brush.Danger");
        StatusIcon.Text = item.IsSuccess ? "\uE86C" : "\uE000";
        StatusIcon.Foreground = item.IsSuccess
            ? (Brush)FindResource("Brush.Success")
            : (Brush)FindResource("Brush.Danger");

        CommandBox.Text = item.CommandLine ?? string.Empty;
        CommandSection.Visibility = item.HasCommand ? Visibility.Visible : Visibility.Collapsed;

        StderrBox.Text = item.Stderr ?? string.Empty;
        StderrSection.Visibility = item.HasStderr ? Visibility.Visible : Visibility.Collapsed;

        WindowTheme.Apply(this, ThemeManager.IsDark);
        ThemeManager.ThemeChanged += OnThemeChanged;
    }

    private void OnThemeChanged(object? sender, EventArgs e) => WindowTheme.Apply(this, ThemeManager.IsDark);

    private void OnCopyCommandClick(object sender, RoutedEventArgs e) => CopyToClipboard(_item.CommandLine);

    private void OnCopyAllClick(object sender, RoutedEventArgs e)
    {
        var text = string.Join(Environment.NewLine,
            $"文件：{_item.Source.FullPath}",
            $"输出：{_item.OutputPath}",
            $"状态：{_item.StatusText}",
            $"耗时：{_item.ElapsedText}",
            string.IsNullOrWhiteSpace(_item.Error) ? string.Empty : $"原因：{_item.Error}",
            string.IsNullOrWhiteSpace(_item.CommandLine) ? string.Empty : $"命令：{_item.CommandLine}",
            string.IsNullOrWhiteSpace(_item.Stderr) ? string.Empty : $"输出：{_item.Stderr}");

        CopyToClipboard(text);
    }

    private void OnOpenSourceFolderClick(object sender, RoutedEventArgs e)
        => WindowTheme.RevealInExplorer(_item.Source.FullPath);

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private static void CopyToClipboard(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        try
        {
            Clipboard.SetText(text);
        }
        catch
        {
            // 剪贴板被占用时忽略。
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        ThemeManager.ThemeChanged -= OnThemeChanged;
        base.OnClosed(e);
    }
}
