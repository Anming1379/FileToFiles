using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileToFiles.Models;
using FileToFiles.Services;
using FileToFiles.Utils;

namespace FileToFiles.ViewModels;

/// <summary>
/// 主窗口视图模型：输入列表与探测、预设与转换设置、批量转换调度、输出列表、状态栏。
/// </summary>
public partial class MainViewModel : ObservableObject, IDisposable
{
    /// <summary>同时进行的 ffprobe 探测数量上限。</summary>
    private const int MaxProbeConcurrency = 4;

    private readonly FfprobeService _ffprobe = new();
    private readonly ConversionEngine _engine = new();
    private readonly SemaphoreSlim _probeGate = new(MaxProbeConcurrency);
    private readonly HashSet<string> _knownPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly CancellationTokenSource _lifetimeCts = new();
    private readonly ConcurrentDictionary<Task, byte> _probeTasks = new();
    private readonly HashSet<InputFileItem> _conversionTargets = new();
    private CancellationTokenSource? _conversionCts;
    private bool _disposed;

    public MainViewModel(AppSettings settings)
    {
        Settings = settings;

        ContainerChoices = new List<Choice<ContainerFormat>>
        {
            new(ContainerFormat.Mp4, "MP4"),
            new(ContainerFormat.Mkv, "MKV"),
            new(ContainerFormat.Webm, "WebM"),
            new(ContainerFormat.Mov, "MOV"),
            new(ContainerFormat.Mp3, "MP3"),
            new(ContainerFormat.M4a, "M4A"),
            new(ContainerFormat.Flac, "FLAC"),
            new(ContainerFormat.Wav, "WAV"),
            new(ContainerFormat.Ogg, "OGG"),
            new(ContainerFormat.ImageSequence, "PNG 序列")
        };

        VideoCodecChoices = new List<Choice<VideoCodecKind>>
        {
            new(VideoCodecKind.H264, "H.264（libx264）"),
            new(VideoCodecKind.H265, "H.265 / HEVC（libx265）"),
            new(VideoCodecKind.Vp9, "VP9（libvpx-vp9）"),
            new(VideoCodecKind.Av1, "AV1（libsvtav1）"),
            new(VideoCodecKind.Copy, "直接复制视频流")
        };

        QualityModeChoices = new List<Choice<QualityMode>>
        {
            new(Models.QualityMode.Crf, "恒定质量（CRF）"),
            new(Models.QualityMode.Bitrate, "固定码率")
        };

        ResolutionChoices = new List<Choice<ResolutionOption>>
        {
            new(ResolutionOption.Source, "保持原始"),
            new(ResolutionOption.P1080, "1080p"),
            new(ResolutionOption.P720, "720p"),
            new(ResolutionOption.P480, "480p")
        };

        FrameRateChoices = new List<Choice<FrameRateOption>>
        {
            new(FrameRateOption.Source, "保持原始"),
            new(FrameRateOption.Fps60, "60 fps"),
            new(FrameRateOption.Fps30, "30 fps"),
            new(FrameRateOption.Fps25, "25 fps"),
            new(FrameRateOption.Fps24, "24 fps"),
            new(FrameRateOption.Fps15, "15 fps")
        };

        AudioCodecChoices = new List<Choice<AudioCodecKind>>
        {
            new(AudioCodecKind.Aac, "AAC"),
            new(AudioCodecKind.Mp3, "MP3（libmp3lame）"),
            new(AudioCodecKind.Opus, "Opus（libopus）"),
            new(AudioCodecKind.Flac, "FLAC（无损）"),
            new(AudioCodecKind.Pcm16, "PCM 16-bit（无损）"),
            new(AudioCodecKind.Vorbis, "Vorbis（libvorbis）"),
            new(AudioCodecKind.Copy, "直接复制音频流"),
            new(AudioCodecKind.None, "无音频")
        };

        HardwareChoices = new List<Choice<HardwareAcceleration>>
        {
            new(HardwareAcceleration.Auto, "自动"),
            new(HardwareAcceleration.Off, "关闭（软件编码）"),
            new(HardwareAcceleration.Nvenc, "NVIDIA NVENC"),
            new(HardwareAcceleration.Qsv, "Intel QSV"),
            new(HardwareAcceleration.Amf, "AMD AMF")
        };

        foreach (var preset in BuiltInPresets.CreateAll())
        {
            preset.CaptureTemplate();
            Presets.Add(preset);
        }

        foreach (var preset in PresetStore.Load())
        {
            Presets.Add(preset);
        }

        SelectedPreset = Presets.First(p => p.BuiltInKey == BuiltInPresets.DefaultVideoKey);
        OutputDirectory = settings.OutputDirectory;
        MaxConcurrency = settings.MaxConcurrency;

        Files.CollectionChanged += (_, _) =>
        {
            RaiseSummary();
            RaiseProbeState();
            RemoveSelectedCommand.NotifyCanExecuteChanged();
            StartConversionCommand.NotifyCanExecuteChanged();
        };

        Outputs.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasOutputs));
    }

    // ==================== 回调（由视图提供） ====================

    /// <summary>当前选中项（支持 Extended 多选）。</summary>
    public Func<IReadOnlyList<InputFileItem>>? SelectionProvider { get; set; }

    /// <summary>消息展示能力。</summary>
    public Action<string, string>? ShowMessage { get; set; }

    /// <summary>输出详情对话框（由视图实现；为空时退回系统对话框）。</summary>
    public Action<OutputItem>? ShowOutputDetailWindow { get; set; }

    /// <summary>用户设置。</summary>
    public AppSettings Settings { get; private set; }

    /// <summary>一批转换结束（成功 / 失败 / 取消数量）。</summary>
    public event EventHandler<ConversionCompletedEventArgs>? ConversionCompleted;

    // ==================== 集合与选项 ====================

    public ObservableCollection<InputFileItem> Files { get; } = new();

    public ObservableCollection<OutputItem> Outputs { get; } = new();

    public ObservableCollection<ConversionPreset> Presets { get; } = new();

    public IReadOnlyList<Choice<ContainerFormat>> ContainerChoices { get; }

    public IReadOnlyList<Choice<VideoCodecKind>> VideoCodecChoices { get; }

    public IReadOnlyList<Choice<QualityMode>> QualityModeChoices { get; }

    public IReadOnlyList<Choice<ResolutionOption>> ResolutionChoices { get; }

    public IReadOnlyList<Choice<FrameRateOption>> FrameRateChoices { get; }

    public IReadOnlyList<Choice<AudioCodecKind>> AudioCodecChoices { get; }

    public IReadOnlyList<Choice<HardwareAcceleration>> HardwareChoices { get; }

    public IReadOnlyList<Choice<string>> AudioQualityChoices => ConversionPreset.AudioQualityOptions;

    // ==================== 可观察状态 ====================

    [ObservableProperty]
    private ConversionPreset _selectedPreset;

    [ObservableProperty]
    private FfmpegLocation _ffmpeg = FfmpegLocation.NotFound;

    [ObservableProperty]
    private EncoderSupport _encoders = EncoderSupport.None;

    [ObservableProperty]
    private bool _isProbing;

    [ObservableProperty]
    private bool _isConverting;

    [ObservableProperty]
    private double _overallProgress;

    [ObservableProperty]
    private int _finishedCount;

    [ObservableProperty]
    private int _totalToConvert;

    [ObservableProperty]
    private string? _currentFileName;

    [ObservableProperty]
    private string _outputDirectory = AppPaths.DefaultOutputDirectory;

    [ObservableProperty]
    private int _maxConcurrency = 2;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _hasCrashReport;

    [ObservableProperty]
    private string _crashReportText = string.Empty;

    private string? _lastCrashLog;

    public bool IsFfmpegReady => Ffmpeg.IsReady;

    public string FfmpegStatusText => Ffmpeg.Summary;

    public string AppVersionText => "v" + (System.Reflection.Assembly.GetExecutingAssembly()
        .GetName().Version?.ToString(3) ?? "0.2.0");

    public string SettingsTabTitle => $"转换设置 · {SelectedPreset.TargetTitle}";

    public string SummaryText => Files.Count == 0
        ? "尚未导入文件"
        : $"共 {Files.Count} 个文件 · 总大小 {Formatters.FormatSize(Files.Sum(f => f.FileSize))}";

    public string ProbeSummaryText
    {
        get
        {
            if (IsProbing)
            {
                var probing = Files.Count(f => f.Status == InputStatus.Probing);
                var waiting = Files.Count(f => f.Status == InputStatus.Pending);
                return $"正在探测 · 进行中 {probing} · 等待 {waiting}";
            }

            var failed = Files.Count(f => f.Status == InputStatus.Failed);
            return failed > 0 ? $"探测失败 {failed} 个" : string.Empty;
        }
    }

    public string ConversionProgressText => TotalToConvert == 0
        ? "尚未开始"
        : $"已完成 {FinishedCount} / {TotalToConvert}";

    public bool HasOutputs => Outputs.Count > 0;

    public bool HasConvertibleFiles => Files.Any(IsConvertible);

    private static bool IsConvertible(InputFileItem item) => item.Media is not null
        && item.Status is InputStatus.Ready or InputStatus.Failed or InputStatus.Canceled or InputStatus.Completed;

    // ==================== 属性变化通知 ====================

    partial void OnFfmpegChanged(FfmpegLocation value)
    {
        OnPropertyChanged(nameof(IsFfmpegReady));
        OnPropertyChanged(nameof(FfmpegStatusText));
    }

    partial void OnIsProbingChanged(bool value) => OnPropertyChanged(nameof(ProbeSummaryText));

    partial void OnIsConvertingChanged(bool value)
    {
        StartConversionCommand.NotifyCanExecuteChanged();
        StopConversionCommand.NotifyCanExecuteChanged();
    }

    partial void OnFinishedCountChanged(int value) => OnPropertyChanged(nameof(ConversionProgressText));

    partial void OnTotalToConvertChanged(int value) => OnPropertyChanged(nameof(ConversionProgressText));

    partial void OnSelectedPresetChanged(ConversionPreset? oldValue, ConversionPreset newValue)
    {
        if (oldValue is not null)
        {
            oldValue.PropertyChanged -= OnSelectedPresetPropertyChanged;

            // 参数修改只对正在使用的预设有效：切走后恢复原始参数，避免 * 一直挂着。
            if (oldValue.IsDirty)
            {
                oldValue.RevertToTemplate();
            }
        }

        if (newValue is not null)
        {
            newValue.PropertyChanged += OnSelectedPresetPropertyChanged;
        }

        OnPropertyChanged(nameof(SettingsTabTitle));
        OnPropertyChanged(nameof(HasConvertibleFiles));
        StartConversionCommand.NotifyCanExecuteChanged();
    }

    /// <summary>预设参数变化时刷新派生 UI（Tab 标题、按钮状态）。</summary>
    private void OnSelectedPresetPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ConversionPreset.TargetTitle):
            case nameof(ConversionPreset.Container):
            case nameof(ConversionPreset.VideoCodec):
                OnPropertyChanged(nameof(SettingsTabTitle));
                break;
        }
    }

    // ==================== 初始化 ====================

    public async Task InitializeAsync()
    {
        Ffmpeg = await FfmpegLocator.LocateAsync().ConfigureAwait(true);
        LogService.Info("FFmpeg 定位：{State} {Path}", Ffmpeg.Summary, Ffmpeg.FfmpegPath ?? "(未找到)");

        if (Ffmpeg.FfmpegPath is { } ffmpegPath)
        {
            Encoders = await EncoderSupport.DetectAsync(ffmpegPath, _lifetimeCts.Token).ConfigureAwait(true);
            EncoderSupport.Current = Encoders;
            LogService.Info("硬件编码器：{Summary}", Encoders.Summary);
        }
    }

    // ==================== 导入与探测 ====================

    public async Task<int> AddPathsAsync(IEnumerable<string> paths, CancellationToken cancellationToken = default)
    {
        var source = paths as IReadOnlyList<string> ?? paths.ToList();
        if (source.Count == 0)
        {
            return 0;
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetimeCts.Token);
        var expanded = await Task.Run(() => FileImporter.Expand(source, linked.Token).ToList(), linked.Token)
            .ConfigureAwait(true);

        var added = 0;
        foreach (var file in expanded)
        {
            if (!_knownPaths.Add(file))
            {
                continue;
            }

            long size = 0;
            try
            {
                size = new FileInfo(file).Length;
            }
            catch
            {
                // 无法读取大小时显示 0。
            }

            var item = new InputFileItem(file, size);
            Files.Add(item);
            QueueProbe(item);
            added++;
        }

        return added;
    }

    private void QueueProbe(InputFileItem item)
    {
        var task = ProbeItemAsync(item);
        _probeTasks[task] = 0;
        _ = task.ContinueWith(t => _probeTasks.TryRemove(t, out _), TaskScheduler.Default);
    }

    private async Task ProbeItemAsync(InputFileItem item)
    {
        try
        {
            await _probeGate.WaitAsync(_lifetimeCts.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        try
        {
            item.Status = InputStatus.Probing;
            RaiseProbeState();

            var media = await _ffprobe.ProbeAsync(item.FullPath, _lifetimeCts.Token).ConfigureAwait(true);
            item.Error = null;
            item.Media = media;
            item.Status = InputStatus.Ready;
        }
        catch (OperationCanceledException) when (_lifetimeCts.IsCancellationRequested)
        {
            item.Status = InputStatus.Pending;
        }
        catch (ProbeException ex)
        {
            item.Error = ex.Message;
            item.Status = InputStatus.Failed;
        }
        catch (Exception ex)
        {
            item.Error = ex.Message;
            item.Status = InputStatus.Failed;
        }
        finally
        {
            _probeGate.Release();
            RaiseProbeState();
            RaiseSummary();
            StartConversionCommand.NotifyCanExecuteChanged();
        }
    }

    private void RaiseSummary() => OnPropertyChanged(nameof(SummaryText));

    private void RaiseProbeState()
    {
        IsProbing = Files.Any(f => f.Status is InputStatus.Probing or InputStatus.Pending);
        OnPropertyChanged(nameof(ProbeSummaryText));
    }

    // ==================== 转换 ====================

    [RelayCommand(CanExecute = nameof(CanStartConversion))]
    private Task StartConversionAsync() => RunConversionAsync(null);

    private bool CanStartConversion() => !IsConverting && HasConvertibleFiles;

    /// <summary>
    /// 批量转换。<paramref name="subset"/> 为空时转换所有可转换的文件。
    /// </summary>
    private async Task RunConversionAsync(IReadOnlyList<InputFileItem>? subset)
    {
        if (IsConverting)
        {
            return;
        }

        var targets = (subset ?? Files.Where(IsConvertible).ToList()).Where(IsConvertible).ToList();
        if (targets.Count == 0)
        {
            return;
        }

        if (!Ffmpeg.IsReady || Ffmpeg.FfmpegPath is null)
        {
            ShowMessage?.Invoke("无法开始转换", "未找到 FFmpeg，请检查程序目录下的 ffmpeg 文件夹。");
            return;
        }

        try
        {
            Directory.CreateDirectory(OutputDirectory);
        }
        catch (Exception ex)
        {
            ShowMessage?.Invoke("输出目录不可用", $"无法创建输出目录：\n{OutputDirectory}\n\n{ex.Message}");
            return;
        }

        _conversionTargets.Clear();
        foreach (var target in targets)
        {
            _conversionTargets.Add(target);
            target.Progress = 0;
        }

        FinishedCount = 0;
        TotalToConvert = targets.Count;
        OverallProgress = 0;
        IsConverting = true;
        StatusMessage = null;

        _conversionCts = new CancellationTokenSource();
        var token = _conversionCts.Token;
        var preset = SelectedPreset.Clone();
        var ffmpegPath = Ffmpeg.FfmpegPath;
        using var gate = new SemaphoreSlim(Math.Clamp(MaxConcurrency, 1, 8));

        try
        {
            var tasks = targets.Select(item => ConvertOneAsync(item, preset, ffmpegPath, gate, token)).ToList();
            await Task.WhenAll(tasks).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusMessage = $"转换提前结束：{ex.Message}";
        }
        finally
        {
            _conversionCts?.Dispose();
            _conversionCts = null;
            IsConverting = false;
            CurrentFileName = null;
            UpdateOverallProgress();
            if (!token.IsCancellationRequested)
            {
                OverallProgress = 100;
            }

            var failed = _conversionTargets.Count(i => i.Status == InputStatus.Failed);
            var canceled = _conversionTargets.Count(i => i.Status == InputStatus.Canceled);
            StatusMessage = canceled > 0
                ? $"已停止 · 完成 {_conversionTargets.Count - canceled - failed} 个 · 取消 {canceled} 个 · 失败 {failed} 个"
                : failed > 0
                    ? $"转换结束 · 成功 {FinishedCount - failed} 个 · 失败 {failed} 个"
                    : $"转换结束 · 成功 {FinishedCount} 个";

            StartConversionCommand.NotifyCanExecuteChanged();
            StopConversionCommand.NotifyCanExecuteChanged();

            var succeeded = _conversionTargets.Count(i => i.Status == InputStatus.Completed);
            if (succeeded + failed + canceled > 0)
            {
                ConversionCompleted?.Invoke(this, new ConversionCompletedEventArgs(succeeded, failed, canceled));
            }
        }
    }

    private async Task ConvertOneAsync(
        InputFileItem item,
        ConversionPreset preset,
        string ffmpegPath,
        SemaphoreSlim gate,
        CancellationToken cancellationToken)
    {
        try
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            item.Status = InputStatus.Canceled;
            BumpFinished();
            return;
        }

        var finalPath = OutputPathResolver.Resolve(OutputDirectory, item.FullPath, preset);
        var tempPath = OutputPathResolver.TempPath(finalPath, preset);
        var outputTarget = preset.IsImageSequence
            ? OutputPathResolver.ImageSequencePattern(tempPath, item.FullPath)
            : tempPath;

        try
        {
            // 前置校验：目标格式与源文件的流类型不匹配时，直接给出可读原因。
            var validation = ValidateSource(item, preset);
            if (validation is not null)
            {
                item.Progress = 0;
                item.Error = validation;
                item.Status = InputStatus.Failed;
                AddOutput(new OutputItem(item, finalPath, preset.TargetTitle, TimeSpan.Zero, 0, false, validation));
                return;
            }

            item.Status = InputStatus.Running;
            item.Progress = 0;
            item.Error = null;
            CurrentFileName = item.FileName;

            var command = FfmpegCommandBuilder.Build(item.FullPath, outputTarget, preset, item.Media);
            var job = new ConversionJob(
                item.FullPath,
                tempPath,
                finalPath,
                item.Media?.Duration ?? TimeSpan.Zero,
                preset.IsImageSequence);
            var progress = new Progress<double>(value =>
            {
                item.Progress = value;
                UpdateOverallProgress();
            });

            LogService.Info("开始转换 {Source} → {Output}", item.FullPath, finalPath);
            LogService.Info("命令：{Command}", command.DisplayText);

            var result = await _engine.ConvertAsync(ffmpegPath, command, job, progress, cancellationToken)
                .ConfigureAwait(true);

            LogService.Info("转换完成 {Output}，耗时 {Elapsed:0.0}s，大小 {Size} 字节",
                result.FinalPath, result.Elapsed.TotalSeconds, result.SizeBytes);

            item.Progress = 100;
            item.Status = InputStatus.Completed;
            AddOutput(new OutputItem(
                item,
                result.FinalPath,
                preset.TargetTitle,
                result.Elapsed,
                result.SizeBytes,
                true,
                frameCount: result.FrameCount));
        }
        catch (OperationCanceledException)
        {
            item.Status = InputStatus.Canceled;
            item.Progress = 0;
            LogService.Info("转换已取消 {Source}", item.FullPath);
            AddOutput(new OutputItem(item, finalPath, preset.TargetTitle, TimeSpan.Zero, 0, false, "已取消"));
        }
        catch (ConversionException ex)
        {
            item.Status = InputStatus.Failed;
            item.Error = ex.Message;
            LogService.Error("转换失败 {Source}：{Reason}{NewLine}命令：{Command}{NewLine}{Stderr}",
                item.FullPath, ex.Message, ex.CommandLine, ex.Stderr ?? string.Empty);
            AddOutput(new OutputItem(item, finalPath, preset.TargetTitle, TimeSpan.Zero, 0, false, ex.Message,
                commandLine: ex.CommandLine, stderr: ex.Stderr));
        }
        catch (Exception ex)
        {
            item.Status = InputStatus.Failed;
            item.Error = ex.Message;
            AddOutput(new OutputItem(item, finalPath, preset.TargetTitle, TimeSpan.Zero, 0, false, ex.Message));
        }
        finally
        {
            gate.Release();
            BumpFinished();
        }
    }

    /// <summary>检查源文件是否具备目标格式需要的流。</summary>
    private static string? ValidateSource(InputFileItem item, ConversionPreset preset)
    {
        if (item.Media is null)
        {
            return "媒体信息尚未就绪，无法转换。";
        }

        if (preset.IsImageSequence && !item.Media.HasVideo)
        {
            return "源文件没有视频流，无法转换为图像序列。";
        }

        if (!preset.IsVideo && !item.Media.HasAudio)
        {
            return "源文件没有音频流，无法转换为纯音频格式。";
        }

        if (preset.IsVideo && preset.VideoCodec != VideoCodecKind.Copy
            && !preset.IsImageSequence && !item.Media.HasVideo && !item.Media.HasAudio)
        {
            return "源文件不包含可转换的音视频流。";
        }

        return null;
    }

    private void AddOutput(OutputItem output)
    {
        Outputs.Insert(0, output);
        OnPropertyChanged(nameof(HasOutputs));
    }

    private void BumpFinished()
    {
        FinishedCount++;
        UpdateOverallProgress();
    }

    private void UpdateOverallProgress()
    {
        if (TotalToConvert <= 0)
        {
            OverallProgress = 0;
            return;
        }

        var sum = _conversionTargets.Sum(item => item.Progress);
        OverallProgress = Math.Clamp(sum / TotalToConvert, 0, 100);
    }

    [RelayCommand(CanExecute = nameof(CanStopConversion))]
    private void StopConversion()
    {
        StatusMessage = "正在停止…";
        try
        {
            _conversionCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // 已经结束。
        }
    }

    private bool CanStopConversion() => IsConverting;

    // ==================== 预设命令 ====================

    [RelayCommand]
    private void ResetPreset()
    {
        var template = BuiltInPresets.FindTemplate(SelectedPreset.BuiltInKey);
        if (template is null)
        {
            return;
        }

        SelectedPreset.CopyFrom(template);
        OnPropertyChanged(nameof(SettingsTabTitle));
    }

    /// <summary>把当前参数另存为一个自定义预设（本次会话有效）。</summary>
    public ConversionPreset AddCustomPreset(string name)
    {
        var preset = SelectedPreset.Clone(name.Trim());
        preset.CopyFrom(SelectedPreset);
        preset.CaptureTemplate();
        Presets.Add(preset);
        SelectedPreset = preset;
        PresetStore.Save(Presets.Where(p => !p.IsBuiltIn));
        LogService.Info("新增自定义预设 {Name}", preset.Name);
        return preset;
    }

    /// <summary>应用设置窗口的结果。</summary>
    public void ApplySettings(AppSettings settings, IReadOnlyCollection<ConversionPreset> customPresets)
    {
        Settings = settings;
        OutputDirectory = settings.OutputDirectory;
        MaxConcurrency = settings.MaxConcurrency;
        settings.Save();

        // 设置窗口里删掉的自定义预设要同步移除。
        foreach (var preset in Presets.Where(p => !p.IsBuiltIn && !customPresets.Contains(p)).ToList())
        {
            Presets.Remove(preset);
            if (ReferenceEquals(SelectedPreset, preset))
            {
                SelectedPreset = Presets.First(p => p.IsBuiltIn);
            }
        }

        PresetStore.Save(customPresets);
        LogService.Info("设置已保存：输出目录={Directory}，并发={Concurrency}，通知={Notify}，完成后打开={Open}，主题={Theme}",
            settings.OutputDirectory, settings.MaxConcurrency, settings.NotifyWhenDone, settings.OpenOutputWhenDone, settings.Theme);
    }

    // ==================== 列表命令 ====================

    [RelayCommand(CanExecute = nameof(CanRemoveSelected))]
    private void RemoveSelected()
    {
        var selected = SelectionProvider?.Invoke() ?? Array.Empty<InputFileItem>();
        RemoveItems(selected);
    }

    private bool CanRemoveSelected() => (SelectionProvider?.Invoke().Count ?? 0) > 0;

    [RelayCommand]
    private void ClearAll()
    {
        Files.Clear();
        _knownPaths.Clear();
    }

    [RelayCommand]
    private void RemoveItem(InputFileItem? item)
    {
        if (item is null)
        {
            return;
        }

        RemoveItems(new[] { item });
    }

    private void RemoveItems(IEnumerable<InputFileItem> items)
    {
        foreach (var item in items.ToList())
        {
            Files.Remove(item);
            _knownPaths.Remove(item.FullPath);
        }

        RaiseSummary();
        RaiseProbeState();
        RemoveSelectedCommand.NotifyCanExecuteChanged();
        StartConversionCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void RecheckItem(InputFileItem? item)
    {
        if (item is null)
        {
            return;
        }

        item.ResetForProbe();
        QueueProbe(item);
        RaiseProbeState();
    }

    [RelayCommand]
    private void RecheckAll()
    {
        foreach (var item in Files)
        {
            item.ResetForProbe();
            QueueProbe(item);
        }

        RaiseProbeState();
    }

    [RelayCommand]
    private void OpenSourceFolder(InputFileItem? item)
    {
        if (item is null)
        {
            return;
        }

        WindowTheme.RevealInExplorer(item.FullPath);
    }

    [RelayCommand]
    private void ShowDetails(InputFileItem? item)
    {
        if (item is null)
        {
            return;
        }

        if (item.Media is null)
        {
            var body = item.Status == InputStatus.Failed
                ? $"探测失败：{item.Error}"
                : "媒体信息尚未就绪，请稍候或重新探测。";
            ShowMessage?.Invoke(item.FileName, body);
            return;
        }

        ShowMessage?.Invoke(item.FileName, item.Media.Describe(item.FullPath));
    }

    // ==================== 输出列表命令 ====================

    [RelayCommand]
    private void OpenOutputDirectory()
    {
        try
        {
            Directory.CreateDirectory(OutputDirectory);
        }
        catch
        {
            // 目录无法创建时仍然尝试打开。
        }

        WindowTheme.OpenFolder(OutputDirectory);
    }

    [RelayCommand]
    private void RevealOutput(OutputItem? output)
    {
        if (output is null)
        {
            return;
        }

        if (output.IsFolder)
        {
            WindowTheme.OpenFolder(output.OutputPath);
            return;
        }

        WindowTheme.RevealInExplorer(output.OutputPath);
    }

    [RelayCommand]
    private void PlayOutput(OutputItem? output)
    {
        if (output is null || !output.HasFile)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(output.OutputPath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ShowMessage?.Invoke("无法播放", ex.Message);
        }
    }

    [RelayCommand]
    private void ShowOutputDetail(OutputItem? output)
    {
        if (output is null)
        {
            return;
        }

        if (ShowOutputDetailWindow is { } showWindow)
        {
            showWindow(output);
            return;
        }

        ShowMessage?.Invoke(output.FileName, output.DetailText);
    }

    /// <summary>由视图在启动时调用：上次异常退出的提示。</summary>
    public void SetCrashReport(CrashReport report)
    {
        _lastCrashLog = report.LogFile;
        CrashReportText = $"上次运行异常退出（{report.Timestamp:MM-dd HH:mm}），已保存崩溃日志。";
        HasCrashReport = true;
    }

    [RelayCommand]
    private void OpenCrashLog()
    {
        if (_lastCrashLog is not null)
        {
            WindowTheme.RevealInExplorer(_lastCrashLog);
            return;
        }

        CrashReporter.OpenCrashFolder();
    }

    [RelayCommand]
    private void DismissCrashReport() => HasCrashReport = false;

    [RelayCommand]
    private void DeleteOutput(OutputItem? output)
    {
        if (output is null)
        {
            return;
        }

        try
        {
            if (output.IsFolder)
            {
                if (Directory.Exists(output.OutputPath))
                {
                    Directory.Delete(output.OutputPath, recursive: true);
                }
            }
            else if (File.Exists(output.OutputPath))
            {
                File.Delete(output.OutputPath);
            }

            Outputs.Remove(output);
            OnPropertyChanged(nameof(HasOutputs));
        }
        catch (Exception ex)
        {
            ShowMessage?.Invoke("无法删除输出文件", ex.Message);
        }
    }

    [RelayCommand]
    private async Task RetransformOutputAsync(OutputItem? output)
    {
        if (output is null || IsConverting)
        {
            return;
        }

        var source = output.Source;
        if (source.Media is null)
        {
            source.ResetForProbe();
            QueueProbe(source);
            return;
        }

        source.Status = InputStatus.Ready;
        source.Progress = 0;
        await RunConversionAsync(new[] { source }).ConfigureAwait(true);
    }

    // ==================== 收尾 ====================

    public Task WaitForProbesAsync()
    {
        var tasks = _probeTasks.Keys.ToArray();
        return tasks.Length == 0 ? Task.CompletedTask : Task.WhenAll(tasks);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            _conversionCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // 忽略。
        }

        _lifetimeCts.Cancel();
        _lifetimeCts.Dispose();
        _probeGate.Dispose();
    }
}
