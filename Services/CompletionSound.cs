using System.IO;
using System.Media;
using System.Windows;
using System.Windows.Media;

namespace FileToFiles.Services;

/// <summary>
/// 转换完成音效（内嵌 assets/complete.wav）。
/// MediaPlayer 负责正常播放，失败时退回 SoundPlayer。
/// </summary>
public static class CompletionSound
{
    private static readonly Uri ResourceUri =
        new("pack://application:,,,/FileToFiles;component/assets/complete.wav", UriKind.Absolute);

    private static MediaPlayer? _player;
    private static SoundPlayer? _fallback;
    private static string? _tempFile;

    public static void Initialize()
    {
        try
        {
            var resource = Application.GetResourceStream(ResourceUri);
            if (resource is null)
            {
                LogService.Warn("未找到完成音效资源");
                return;
            }

            // Media Foundation 更擅长处理 float PCM，先把资源释放成临时文件再播放。
            var directory = Path.Combine(Path.GetTempPath(), "FileToFiles");
            Directory.CreateDirectory(directory);
            _tempFile = Path.Combine(directory, "complete.wav");

            using (var file = File.Create(_tempFile))
            {
                resource.Stream.CopyTo(file);
            }

            _player = new MediaPlayer();
            _player.Open(new Uri(_tempFile));
            _player.Volume = 1.0;
            _player.MediaFailed += (_, e) =>
            {
                LogService.Warn("完成音效播放失败：{Error}", e.ErrorException?.Message ?? "未知错误");
                EnsureFallback();
            };
        }
        catch (Exception ex)
        {
            LogService.Warn(ex, "初始化完成音效失败");
        }
    }

    public static void Play()
    {
        try
        {
            if (_player is not null)
            {
                _player.Position = TimeSpan.Zero;
                _player.Play();
                return;
            }

            EnsureFallback();
            _fallback?.Play();
        }
        catch (Exception ex)
        {
            LogService.Warn(ex, "播放完成音效失败");
        }
    }

    private static void EnsureFallback()
    {
        if (_fallback is not null || _tempFile is null || !File.Exists(_tempFile))
        {
            return;
        }

        try
        {
            _fallback = new SoundPlayer(_tempFile);
            _fallback.Load();
        }
        catch (Exception ex)
        {
            LogService.Warn(ex, "完成音效回退方案不可用");
        }
    }
}
