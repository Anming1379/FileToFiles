using System.Globalization;
using System.Windows;
using System.Windows.Data;
using FileToFiles.Models;
using FileToFiles.Services;

namespace FileToFiles.Utils;

/// <summary>bool → Visibility；ConverterParameter="invert" 时取反。</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var flag = value is true;
        if (string.Equals(parameter as string, "invert", StringComparison.OrdinalIgnoreCase))
        {
            flag = !flag;
        }

        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>bool 取反（用于 IsEnabled 等）。</summary>
public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;
}

/// <summary>QualityMode == Crf 时可见；参数 "invert" 时相反。</summary>
public sealed class QualityModeToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isCrf = value is QualityMode.Crf;
        if (string.Equals(parameter as string, "invert", StringComparison.OrdinalIgnoreCase))
        {
            isCrf = !isCrf;
        }

        return isCrf ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>音频编码是否支持码率 / 质量设置。</summary>
public sealed class AudioQualityAvailableConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is AudioCodecKind codec && codec is not (AudioCodecKind.Flac
            or AudioCodecKind.Pcm16
            or AudioCodecKind.Copy
            or AudioCodecKind.None);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>硬件加速选项是否可用（未编译进 FFmpeg 的选项置灰）。</summary>
public sealed class HardwareOptionEnabledConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Choice<HardwareAcceleration> choice)
        {
            return EncoderSupport.Current.Supports(choice.Value);
        }

        return true;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
