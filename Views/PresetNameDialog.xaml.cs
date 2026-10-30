using System.Windows;
using FileToFiles.Services;

namespace FileToFiles.Views;

/// <summary>输入自定义预设名称的小对话框。</summary>
public partial class PresetNameDialog : Window
{
    public PresetNameDialog()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            NameBox.SelectAll();
            NameBox.Focus();
        };

        WindowTheme.Apply(this, dark: true);
    }

    public string PresetName
    {
        get => NameBox.Text.Trim();
        set => NameBox.Text = value;
    }

    private void OnConfirmClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            MessageBox.Show(this, "请输入预设名称。", "File To Files", MessageBoxButton.OK, MessageBoxImage.Information);
            NameBox.Focus();
            return;
        }

        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
}
