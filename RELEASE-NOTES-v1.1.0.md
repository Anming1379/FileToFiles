# ALambda File To Files v1.1.0 — 支持图片与动图

在 v1.0.0 的音视频转换基础上，新增**图片与动图格式转换**：JPG / PNG / WebP / GIF 互转，动画可完整保留。

- 平台：Windows 10 1809+ / Windows 11（win-x64）
- 技术栈：C# / .NET 10 / WPF / MVVM CommunityToolkit
- 许可：项目代码 **MIT**；内置 FFmpeg 为独立 **GPLv3** 组件（聚合分发）
- 代码来源：**全部由 AI 生成**，人类负责需求、审阅与测试，详见 `AI-GENERATED-NOTICE.txt`

## 下载

| 文件 | 大小 | 说明 |
|---|---|---|
| `FileToFiles-v1.1.0-win-x64-portable.zip` | 179.7 MB | **便携版**，自包含 .NET 运行时，解压双击 `FileToFiles.exe` 即用 |
| `FileToFiles-v1.1.0-setup.exe` | 90.1 MB | **安装版**，框架依赖，需 .NET 10 桌面运行时 |

安装版缺少运行时会提示并可选一键安装（安装目录内含 `install-dotnet.cmd`），也可手动下载：
https://dotnet.microsoft.com/download/dotnet/10.0

## 本次新增

### 图片与动图预设（5 个）

| 预设 | 说明 |
|---|---|
| 图片 · 转 JPG | 质量 92（可调 1–100），透明背景自动铺白，奇数尺寸自动补齐为偶数 |
| 图片 · 转 PNG | 无损，保留透明通道 |
| 图片 · 转 WebP | 质量 90，只取第一帧 |
| 动图 · GIF | 15 fps（源帧率更低则跟随源），单遍调色板优化，可循环播放 |
| 动图 · WebP | 质量 80，`libwebp_anim` 编码，可循环播放 |

### 支持的图片输入

`jpg / jpeg / jpe / jfif / png / bmp / dib / gif / webp / tif / tiff / tga / avif`

### 类型不匹配自动跳过

- 音频文件选择图片或动图预设：状态显示「已跳过」，记录原因，**不报错、不中断整批任务**
- 图片文件选择纯音频预设：同样跳过
- 跳过数量会在转换结束时汇总显示（如：`转换结束 · 成功 8 个 · 跳过 2 个`）

### 界面

- 转换设置页新增「图片」参数分组：目标格式、尺寸、质量、帧率、循环播放
- 输入列表新增图片与动图图标，静态图时长列显示「静态图」
- 状态圆点新增灰色「已跳过」

### 其他改进

- 静态图片目标可接收视频输入，等于从视频抽取封面帧
- 视频 → GIF / 动画 WebP 全程支持（带调色板优化与帧率上限保护）
- 自定义预设会持久化图片质量与循环设置

## 完整功能一览

- 预设总数 16 个：6 个视频、5 个音频、5 个图片/动图
- 批量导入：拖放、文件夹递归扫描、多选、右键菜单
- 并发转换 1–8 个任务，实时进度与速度
- 取消任务自动清理临时文件，输出先写 `.part` 再提交
- 错误详情对话框可复制完整 FFmpeg 命令
- 深色 / 浅色主题跟随系统，直角风格界面，系统标题栏
- 完成音效、系统通知、单实例运行、崩溃日志、Serilog 轮转日志

## 系统要求

- Windows 10 1809 或更高（推荐 Windows 11）
- 安装版需要 .NET 10 桌面运行时；便携版无需预装
- 约 450 MB 磁盘空间（含 FFmpeg）

## 已知说明

- 仅支持 Windows x64
- 内置 FFmpeg 为 GPL 全功能构建（含 libx264 / libx265 / libxvid），分发时已随包提供许可证全文与源码获取承诺
- H.264 / H.265 / AAC 涉及专利池，商业用途请自行评估
- 不支持 HEIC / HEIF（内置 FFmpeg 未包含 libheif 解码器）

## 校验

```text
FileToFiles-v1.1.0-setup.exe
  3f4555d42165811fa8c4fc68c16d09d451b43d85fe114c02e09733c97b3ecbbc

FileToFiles-v1.1.0-win-x64-portable.zip
  005a5faa75c77483eb761e8bcf6ac5d40156eea3e3d6aa684caa6980e2bab317
```

本地校验：`certutil -hashfile FileToFiles-v1.1.0-setup.exe SHA256`

## 许可与合规

| 文件 | 说明 |
|---|---|
| `LICENSE` | 本项目 MIT 许可证 |
| `LICENSING.txt` | 许可与合规总说明（第三方清单、GPL 义务、聚合边界、专利风险） |
| `THIRD-PARTY-NOTICES.txt` | 第三方组件声明（FFmpeg / Mvvm / Serilog / 图标字体 / 运行时） |
| `SOURCE-OFFER.txt` | FFmpeg 对应源码的提供承诺 |
| `AI-GENERATED-NOTICE.txt` | 代码由 AI 生成的来源声明 |
