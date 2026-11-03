# ALambda File To Files

注：这个 README 以及软件相关代码都是 AI 写的（我好懒，用的是 deepseek 加 Pi 搭把手），
经我测试软件是没有问题的！哼哼！软件我和烤鱼调了好久的

基于 FFmpeg 的 Windows 桌面音视频与图片格式转换器。整批文件统一选择一个目标格式，一键批量转换。

- 平台：Windows 10 1809+ / Windows 11（win-x64）
- 许可：本项目代码以 **MIT** 开源；内置 FFmpeg 为 **GPLv3** 独立组件（聚合分发，见下）
- 技术栈：C# / .NET 10 / WPF / MVVM CommunityToolkit
- 需求文档：`项目规划.txt`
- 下载：[最新发布版](https://github.com/Anming1379/FileToFiles/releases/latest)（便携版 / 安装版）

---

## 代码来源（AI 生成）

本项目的**全部源代码由 AI 编程助手生成**：Pi（coding agent harness）配合云端大语言模型，
于 2026 年 10 月完成。人类负责需求定义、方案取舍、代码审阅、实机测试与发布决策
（维护者：Anming，GitHub [@Anming1379](https://github.com/Anming1379)）。

代码已人工审阅、编译通过，并在 Windows 11 x64 + .NET 10 上用真实音视频文件做过转换验证；
但 AI 生成的代码仍可能存在未被发现的缺陷，请自行评估风险，处理重要文件时保留原始素材。
本项目按 MIT 协议“按原样”提供，不附带任何担保。

详细声明见 [`AI-GENERATED-NOTICE.txt`](AI-GENERATED-NOTICE.txt)（中英双语）。该声明仅用于信息透明，
不改变项目的 MIT 许可条款。

---

## 构建与运行

```powershell
dotnet build
dotnet run
```

也可以把文件或文件夹直接拖到 `FileToFiles.exe` 图标上打开：

```powershell
.\bin\Debug\net10.0-windows\FileToFiles.exe "D:\some\folder"
```

构建时会把内置 FFmpeg 复制到输出目录（约 315 MB），默认来源为
`ffmpeg-n9.0-latest-win64-gpl-9.0\`，可用属性覆盖：

```powershell
dotnet build -p:FfmpegSourceDir=D:\ffmpeg-custom
```

运行期 FFmpeg 查找顺序：设置里的自定义目录 → `程序目录\ffmpeg` → `程序目录\ffmpeg\bin`
→ `程序目录` → 系统 `PATH`。

---

## 目录结构

```text
App.xaml(.cs)              应用入口：主题字典合并、崩溃日志
app.manifest               PerMonitorV2 DPI + longPathAware
MainWindow.xaml(.cs)       主窗口：工具栏 / 三个选项卡 / 状态栏 / 拖放
Views/PresetNameDialog     另存为自定义预设的命名对话框
Models/
  InputStatus.cs           输入文件状态机
  InputFileItem.cs         输入列表行模型（派生列、状态、进度）
  MediaInfo.cs             ffprobe 探测结果
  ConversionPreset.cs      预设/转换参数、内置预设清单、格式目录
  OutputItem.cs            输出列表行模型
Services/
  CompletionSound.cs       转换完成音效（内嵌 assets/complete.wav）
  SingleInstance.cs        单实例互斥 + 第二个实例把路径交给已运行实例
  CrashReporter.cs         会话标记与上次崩溃检测
  AppSettings.cs           用户设置模型与 JSON 持久化
  ThemeManager.cs          浅色/深色/跟随系统，即时切换并监听系统主题
  LogService.cs            Serilog 滚动日志（按天，保留 7 天）
  PresetStore.cs           自定义预设持久化
  WindowFlash.cs           转换完成后任务栏闪烁提醒
  FfmpegLocator.cs         ffmpeg / ffprobe 定位与版本探测
  FfprobeService.cs        ffprobe JSON 解析（超时、取消、可读错误）
  FfmpegCommandBuilder.cs  预设 → FFmpeg 命令行；输出/临时文件命名
  ConversionEngine.cs      单任务执行：进度解析、stderr 收集、取消、改名
  EncoderSupport.cs        硬件编码器可用性检测
  FileImporter.cs          拖放/对话框路径展开，文件夹递归扫描
  AppPaths.cs              配置、日志、崩溃目录
  WindowTheme.cs           深色标题栏 + 资源管理器定位
ViewModels/
  MainViewModel.cs         输入列表与探测、转换调度、输出列表、状态栏
  SettingsViewModel.cs     设置窗口的数据与命令
Views/
  SettingsWindow.xaml      设置与教程（两个选项卡 + 关于信息）
  ErrorDetailDialog.xaml   转换详情 / 失败原因 + FFmpeg 命令
Themes/
  Dark.xaml / Light.xaml   两套调色板（同名 Brush.* 键）
  Controls.xaml            控件样式与模板（直线风格，直角）
Utils/Formatters.cs        文件大小与时长的可读格式
Utils/Converters.cs        可见性 / 可用性等绑定转换器
Utils/TabSlide.cs          选项卡切换的内容滑入动画
assets/MSS-default.ttf     Material Symbols Sharp 图标字体（内嵌资源）
assets/complete.wav        转换完成提示音（内嵌资源）
assets/icon.ico            应用图标（16/32/48 位图帧 + 64/128/256 PNG 帧）
```

图标使用 Material Symbols Sharp 字形，XAML 中写法：

```xml
<TextBlock Style="{StaticResource Icon}" Text="&#xE145;" FontSize="17" />
```

常用字形：`E145` add、`E2C8` folder_open、`E037` play_arrow、`E047` stop、
`E8B8` settings、`E5D5` refresh、`E872` delete、`E16C` delete_sweep、`E02C` movie、
`E405` music_note、`E88E` info、`E429` tune、`E14D` content_copy。

---

## 阶段进度

| 阶段 | 内容 | 状态 |
|---|---|---|
| 一 | 应用骨架、主题资源与控件模板、FFmpeg 定位、ffprobe 探测、输入列表（拖放/多选/右键菜单/虚拟化）、状态栏 | 已完成 |
| 二 | 预设清单（含 AV1）、FFmpeg 命令构建、转换引擎（进度/取消/临时文件）、转换设置页、输出列表、批量调度 | 已完成 |
| 三 | 设置窗口（输出目录 / 并发数 / 完成提醒 / 主题 / 预设管理 / 日志目录）、主题即时切换与跟随系统、Serilog 滚动日志、自定义预设持久化 | 已完成 |
| 四 | 转换完成音效、错误详情对话框（可复制命令）、单实例唤醒、异常退出提示、按钮/窗口过渡动画、关于信息 | 已完成 |
| 五 | 便携自包含 zip | 已完成（180 MB） |
| 五 | Inno Setup 安装包（框架依赖 90 MB） | 已完成 |
| 五 | .NET 10 一键安装脚本 | 已完成 |
| 五 | 许可与合规文件（GPL 义务、专利提示、源码承诺） | 已完成 |

| 六 | 图片与动图支持：JPG / PNG / WebP / GIF 互转，动图保留动画，音频转图片自动跳过 | 已完成（v1.1.0） |

> 当前版本 v1.1.0

---

## 许可与合规

本程序内置的 FFmpeg 是 **GPLv3** 构建（`--enable-gpl --enable-version3`，启用 libx264 / libx265 / libxvid），
因此分发时必须履行 GPLv3 义务。相关文件：

| 文件 | 作用 |
|---|---|
| `LICENSING.txt` | 许可与合规总说明：第三方清单、GPL 义务、程序聚合边界、专利风险、开源协议选型建议 |
| `THIRD-PARTY-NOTICES.txt` | 第三方组件与许可声明（FFmpeg / Mvvm / Serilog / 图标字体 / 运行时） |
| `SOURCE-OFFER.txt` | FFmpeg 对应源码的提供承诺（版本、提交号、构建脚本、三年书面承诺） |
| `AI-GENERATED-NOTICE.txt` | 代码由 AI 生成的来源声明（中英双语，不改变 MIT 许可） |
| `ffmpeg/LICENSE.txt` | FFmpeg 的 GPLv3 许可证全文（随包分发） |

关键结论：

- 本程序**不链接** FFmpeg 任何库，仅以独立进程 + 命令行参数调用，也未修改其源码；
  依 FSF 的 GPL FAQ（MereAggregation）属于“聚合”，因此本项目代码不必随之 GPL 化。
  若将来改成链接 libav* 或把 FFmpeg 编译进主程序，整体就必须以 GPL 发布。
- FFmpeg 部分必须随包提供许可证全文 + 对应源码或书面承诺，本仓库已补齐。
- GPL 不授予专利许可：H.264 / H.265 / AAC 涉及专利池，商业发行前需自行评估；
  AV1 / VP9 / Opus / Vorbis / FLAC / MP3 风险较低。
- 本项目自身代码以 **MIT** 开源（`LICENSE`）：可自由使用、修改、再分发，只需保留版权与许可声明。
  MIT 只覆盖本项目代码；随包的 FFmpeg 仍是 GPLv3，两者为“程序聚合”关系。
- 代码为 AI 生成，来源声明见 `AI-GENERATED-NOTICE.txt`（不构成额外许可限制）。

> 源码索取联系方式已填写：`lyric_x@outlook.com`（见 `SOURCE-OFFER.txt`）。

---

## 打包（便携自包含版）

一条命令产出可分发 zip：

```powershell
powershell -ExecutionPolicy Bypass -File .\packaging\build-portable.ps1
```

脚本会：从 csproj 读版本号 → `dotnet publish -c Release -r win-x64 --self-contained true` →
附上 `THIRD-PARTY-NOTICES.txt` 与 `PORTABLE-README.txt` → 压缩成
`dist/FileToFiles-v<版本>-win-x64-portable.zip`。

当前产物：

```text
dist/FileToFiles-v0.7.0-win-x64-portable.zip    179.6 MB（解压 448 MB，251 项）
FileToFiles/
  FileToFiles.exe              主程序（已含图标）
  ffmpeg/ffmpeg.exe            转码（157.5 MB）
  ffmpeg/ffprobe.exe           探测（157.3 MB）
  ffmpeg/LICENSE.txt           FFmpeg GPL 许可
  PORTABLE-README.txt          使用说明
  THIRD-PARTY-NOTICES.txt      第三方组件声明
  ... .NET 10 自包含运行时（免安装运行时）
```

目录结构可用 `-SkipPublish` 复用已有发布结果只重新压缩。

### 安装包（框架依赖，不含 .NET 运行时）

```powershell
powershell -ExecutionPolicy Bypass -File .\packaging\build-installer.ps1
```

- 用 `dotnet publish -r win-x64 --self-contained false` 发布，安装包 **约 90 MB**
- Inno Setup 脚本：`packaging/FileToFiles.iss`（简体中文界面、许可证页、桌面/开始菜单快捷方式、标准卸载）
- 安装程序会检测 `Microsoft.WindowsDesktop.App 10.x`：
  缺失时给出提示，并在最后一步提供「下载并安装 .NET 10 桌面运行时」勾选项（需要联网）
- 找不到编译器时用 `-IsccPath` 指定 `ISCC.exe`

### .NET 10 一键安装脚本

面向不懂电脑的用户，放在 `packaging/` 并在安装包里随附到安装目录：

```text
install-dotnet.cmd   纯 ASCII 引导（把 /quiet 转成 -Quiet）
install-dotnet.ps1   实际逻辑：检测 → 提权 → 下载 → 静默安装 → 验证
```

特点：先检测是否已安装（有则直接退出）、自动请求管理员权限、
用 `curl`（失败退回 `Invoke-WebRequest`）从 aka.ms 官方地址下载、
静默安装后再次验证，失败时自动打开微软官方下载页。

### 当前发布产物

```text
dist/FileToFiles-v1.1.0-setup.exe                    90.1 MB  （框架依赖，需 .NET 10 Desktop Runtime）
dist/FileToFiles-v1.1.0-win-x64-portable.zip        179.8 MB  （自包含，解压即用）
```

---

## 行为约定

- 拖放：文件与文件夹都可拖入，文件夹递归扫描受支持扩展名，路径去重。
- 探测：4 路并发调用 ffprobe，单文件 60 秒超时；失败时状态变为「失败」并保存可读原因。
- 状态：`Pending → Probing → Ready`，转换时 `Running → Completed / Failed / Canceled`。
- 目标格式：整批统一一个预设；修改参数后当前预设名称前会加 `*`，切换到别的预设时上一套修改自动复原（参数只对正在使用的预设有效）。
- 输出命名：`{源文件名}.{目标扩展名}`，同目录同名直接覆盖；图像序列输出到 `{源文件名}_frames` 文件夹。
- 中间文件：`{源文件名}.part.{扩展名}`，成功后才改名为正式文件，中断不留半成品。
- 进度：解析 ffmpeg `-progress` 的 `out_time`，节流 400ms，状态栏显示总体进度。
- 取消：先请求退出，2 秒未退出则强制结束进程树，并删除临时文件。
- 硬件加速：优先使用所选后端，VP9 / AV1 或不可用时自动回退软件编码。
- 流类型校验：纯音频目标遇到无音频流的源、图像序列遇到无视频流的源时，直接给出可读原因。
- 编码器上限：libopus 只支持 6–256 kbps、libmp3lame 上限 320 kbps，超出会被自动收敛到合法区间。
- 界面风格：直角（无圆角）深色主题，三个选项卡等分占满整栏；设置页在宽窗口下为「预设 + 视频/音频双栏 + 高级」布局，
  纯音频目标时自动合并为单栏。
- 配置与日志目录：`%LOCALAPPDATA%\FileToFiles\{config,logs,crash}`。
- 设置项：默认输出目录、最大并发数（1–8）、完成后提醒（任务栏闪烁）、完成后打开输出目录、主题（跟随系统 / 浅色 / 深色）。
- 主题：切换即时生效（替换调色板资源字典），"跟随系统"会监听 `AppsUseLightTheme` 的变化；标题栏同步跟随。
- 日志：Serilog 写入 `logs/app-yyyyMMdd.log`，保留 7 天；每次转换记录源路径、输出路径、完整命令、耗时、大小与失败原因。
- 自定义预设：保存在 `config\presets.json`，可在设置窗口删除。
- 完成提醒：播放 `assets/complete.wav` 并闪烁任务栏（可在设置里关闭）。
- 单实例：重复启动会把文件路径交给已运行的窗口并退出（`Local\FileToFiles.SingleInstance.*`）。
- 异常退出：启动时若发现上次没有正常退出，窗口顶部会显示提示条，可直接查看崩溃日志。
- 失败条目：双击或右键「查看详情」可看到可读原因、完整 FFmpeg 命令（一键复制）与 stderr。
- 界面动画（全部为位移/缩放，不使用淡入淡出）：
  - 选项卡切换：内容按方向滑入 ±32px，200ms `CubicEase/EaseOut`；先在同一个 UI 周期写入起始偏移再起动画，避免先闪一帧原位内容；选中下划线从中间展开
  - 列表行：渲染时从下方 10px 归位，180ms
  - 按钮：按下缩放 0.972（图标按钮 0.9），松开用 `BackEase` 回弹
  - 窗口：内容自上而下 14px 归位入场
  - 崩溃提示条：从上方滑入
- 不使用自定义标题栏，保持系统标题栏 + 深色适配。
- 应用图标：`assets/icon.ico` 含 16/32/48（位图帧）与 64/128/256（PNG 帧）共 6 种尺寸，
  同时作为 exe 图标（`ApplicationIcon`）与所有窗口的 `Icon`，设置页「关于」里也会显示。
- 设置窗口含两个选项卡：「设置」与「教程」。教程用序号步骤、两列术语表和问答式排版，覆盖按用途选格式、容器/编码通俗解释、CRF 旋钮（16–18 / 20–23 / 26–30 / 35+）、硬件加速与常见问题。

---

## 内置预设

| 预设 | 容器 | 视频 | 音频 |
|---|---|---|---|
| 高质量 MP4（H.264）（视频默认） | mp4 | libx264 CRF 18，yuv420p | AAC 320k |
| 兼容 H.264 MP4 | mp4 | libx264 CRF 23，yuv420p | AAC 320k |
| 高压缩 H.265 MP4 | mp4 | libx265 CRF 24，hvc1 | AAC 320k |
| AV1 MP4 | mp4 | libsvtav1 CRF 32 | AAC 320k |
| WebM VP9 | webm | libvpx-vp9 CRF 32 | Opus 256k（libopus 上限） |
| 转为图像序列 | png 序列 | PNG 每帧一个文件 | 无 |
| 高质量 MP3（音频默认） | mp3 | — | libmp3lame 320k |
| AAC / M4A | m4a | — | AAC 320k |
| 无损 FLAC | flac | — | flac |
| WAV 无损 | wav | — | pcm_s16le |
| OGG Vorbis | ogg | — | libvorbis 320k |
| 图片 · 转 JPG | jpg | — | 质量 92，透明区域自动铺白 |
| 图片 · 转 PNG | png | — | 无损，保留透明通道 |
| 图片 · 转 WebP | webp | — | 质量 90，只取第一帧 |
| 动图 · GIF | gif | — | 15 fps（源帧率更低则跟随源），调色板优化，可循环 |
| 动图 · WebP | webp 动画 | — | 质量 80，libwebp_anim，可循环 |

图片与动图说明：

- 支持的输入：`jpg / jpeg / png / bmp / gif / webp / tif / tiff / tga / avif`
- 静态图目标（JPG / PNG / BMP / TIFF / 静态 WebP）只取第一帧，因此视频文件也能用来"抽封面"
- 动图目标（GIF / 动画 WebP）接受图片、动图、视频输入；静态图片会得到单帧动图
- 音频文件选择图片/动图预设时会**自动跳过**（状态显示"已跳过"），不会报错也不会中断整批任务
- 图片转 JPG 时透明背景自动铺白，奇数宽高自动补齐为偶数（mjpeg 要求）

图像序列的输出结构：

```text
输出目录/
  源文件名_frames/
    源文件名_00001.png
    源文件名_00002.png
    ...
```

转换过程中先写入 `源文件名_frames.part\`，全部成功后才改名为正式文件夹；失败或取消时整个临时文件夹会被删除。
