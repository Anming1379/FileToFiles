# ALambda File To Files v1.0.0 — 首个正式版

基于 FFmpeg 的 Windows 桌面音视频批量转换器。选好一个目标格式，把整批文件拖进来，一键转换。

- 平台：Windows 10 1809+ / Windows 11（win-x64）
- 技术栈：C# / .NET 10 / WPF / MVVM CommunityToolkit
- 许可：项目代码 **MIT**；内置 FFmpeg 为独立 **GPLv3** 组件（聚合分发）
- 代码来源：**全部由 AI 生成**（Pi + 云端大模型），人类负责需求、审阅与测试，详见 `AI-GENERATED-NOTICE.txt`

## 下载

| 文件 | 大小 | 说明 |
|---|---|---|
| `FileToFiles-v1.0.0-win-x64-portable.zip` | 179.7 MB | **便携版**，自包含 .NET 运行时，解压双击 `FileToFiles.exe` 即用 |
| `FileToFiles-v1.0.0-setup.exe` | 90.1 MB | **安装版**，框架依赖，需 .NET 10 桌面运行时 |

安装版如果缺少运行时会提示并可选一键安装（安装目录内含 `install-dotnet.cmd`），也可以手动下载：
https://dotnet.microsoft.com/download/dotnet/10.0

## 主要功能

- 11 个内置预设：高质量 MP4（H.264）、兼容 H.264 MP4、高压缩 H.265 MP4、AV1 MP4、WebM VP9、图像序列、高质量 MP3、AAC M4A、无损 FLAC、WAV 无损、OGG Vorbis
- 批量导入：拖放、文件夹递归扫描、多选、右键菜单
- 并发转换 1–8 个任务，实时进度与速度、剩余时间
- 取消任务自动清理临时文件，输出先写 `.part` 再提交，避免半成品
- 输出列表记录每次转换结果，可打开所在目录、重试失败项
- 错误详情对话框可复制完整 FFmpeg 命令，便于排查
- 深色 / 浅色主题，跟随系统；界面为直角风格，使用系统标题栏
- 完成音效、系统通知、单实例运行、崩溃日志
- 自定义预设的保存与持久化

## 系统要求

- Windows 10 1809 或更高（推荐 Windows 11）
- 安装版需要 .NET 10 桌面运行时；便携版无需预装
- 约 450 MB 磁盘空间（含 FFmpeg）

## 已知说明

- 仅支持 Windows x64
- 内置 FFmpeg 为 GPL 全功能构建（含 libx264 / libx265 / libxvid），分发时已随包提供许可证全文与源码获取承诺
- H.264 / H.265 / AAC 涉及专利池，商业用途请自行评估
- 程序不提供自定义 FFmpeg 路径，内置版本随应用更新

## 许可与合规

| 文件 | 说明 |
|---|---|
| `LICENSE` | 本项目 MIT 许可证 |
| `LICENSING.txt` | 许可与合规总说明（第三方清单、GPL 义务、聚合边界、专利风险） |
| `THIRD-PARTY-NOTICES.txt` | 第三方组件声明（FFmpeg / Mvvm / Serilog / 图标字体 / 运行时） |
| `SOURCE-OFFER.txt` | FFmpeg 对应源码的提供承诺 |
| `AI-GENERATED-NOTICE.txt` | 代码由 AI 生成的来源声明 |

## 校验

下载后可用 SHA-256 校验完整性：

```text
FileToFiles-v1.0.0-setup.exe
  e492e0f1655036205bb51e96e0b88d17a9c973eb71bf48d92aeafb647a5b872a

FileToFiles-v1.0.0-win-x64-portable.zip
  de6d8cd286c765c94c0a2e78180738c603b1e12ec7a6d586e5931f51c633a599
```

本地校验：`certutil -hashfile FileToFiles-v1.0.0-setup.exe SHA256`
