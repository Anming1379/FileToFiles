using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using FileToFiles.Models;

namespace FileToFiles.Services;

/// <summary>
/// 自定义预设的持久化（%LOCALAPPDATA%\FileToFiles\config\presets.json）。
/// </summary>
public static class PresetStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static List<ConversionPreset> Load()
    {
        try
        {
            if (!File.Exists(AppPaths.UserPresetsFile))
            {
                return new List<ConversionPreset>();
            }

            var json = File.ReadAllText(AppPaths.UserPresetsFile);
            var items = JsonSerializer.Deserialize<List<PresetDto>>(json, JsonOptions) ?? new List<PresetDto>();
            var presets = new List<ConversionPreset>();

            foreach (var dto in items)
            {
                if (string.IsNullOrWhiteSpace(dto.Name))
                {
                    continue;
                }

                var preset = new ConversionPreset(dto.Name)
                {
                    Container = dto.Container,
                    VideoCodec = dto.VideoCodec,
                    QualityMode = dto.QualityMode,
                    Crf = Math.Clamp(dto.Crf, 0, 51),
                    VideoBitrateKbps = Math.Max(1, dto.VideoBitrateKbps),
                    Resolution = dto.Resolution,
                    FrameRate = dto.FrameRate,
                    AudioCodec = dto.AudioCodec,
                    AudioBitrateKbps = Math.Max(0, dto.AudioBitrateKbps),
                    AudioVbrQuality = dto.AudioVbrQuality,
                    ImageQuality = Math.Clamp(dto.ImageQuality, 1, 100),
                    LoopAnimation = dto.LoopAnimation,
                    Hardware = dto.Hardware
                };

                preset.CaptureTemplate();
                presets.Add(preset);
            }

            return presets;
        }
        catch (Exception ex)
        {
            LogService.Warn(ex, "读取自定义预设失败");
            return new List<ConversionPreset>();
        }
    }

    public static void Save(IEnumerable<ConversionPreset> presets)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.ConfigDirectory);
            var dtos = presets.Select(p => new PresetDto
            {
                Name = p.Name,
                Container = p.Container,
                VideoCodec = p.VideoCodec,
                QualityMode = p.QualityMode,
                Crf = p.Crf,
                VideoBitrateKbps = p.VideoBitrateKbps,
                Resolution = p.Resolution,
                FrameRate = p.FrameRate,
                AudioCodec = p.AudioCodec,
                AudioBitrateKbps = p.AudioBitrateKbps,
                AudioVbrQuality = p.AudioVbrQuality,
                Hardware = p.Hardware,
                ImageQuality = p.ImageQuality,
                LoopAnimation = p.LoopAnimation
            }).ToList();

            File.WriteAllText(AppPaths.UserPresetsFile, JsonSerializer.Serialize(dtos, JsonOptions));
        }
        catch (Exception ex)
        {
            LogService.Warn(ex, "保存自定义预设失败");
        }
    }

    private sealed class PresetDto
    {
        public string Name { get; set; } = string.Empty;

        public ContainerFormat Container { get; set; } = ContainerFormat.Mp4;

        public int ImageQuality { get; set; } = 90;

        public bool LoopAnimation { get; set; } = true;

        public VideoCodecKind VideoCodec { get; set; } = VideoCodecKind.H264;

        public QualityMode QualityMode { get; set; } = QualityMode.Crf;

        public int Crf { get; set; } = 18;

        public int VideoBitrateKbps { get; set; } = 4000;

        public ResolutionOption Resolution { get; set; } = ResolutionOption.Source;

        public FrameRateOption FrameRate { get; set; } = FrameRateOption.Source;

        public AudioCodecKind AudioCodec { get; set; } = AudioCodecKind.Aac;

        public int AudioBitrateKbps { get; set; } = 320;

        public int? AudioVbrQuality { get; set; }

        public HardwareAcceleration Hardware { get; set; } = HardwareAcceleration.Auto;
    }
}
