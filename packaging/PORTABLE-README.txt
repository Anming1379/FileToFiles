ALambda File To Files — 便携版说明
==================================

版本：v0.7.0
平台：Windows 10 1809+ / Windows 11（x64）

使用方法
--------
1. 把整个文件夹解压到任意位置（路径含中文、空格都可以）。
2. 双击 FileToFiles.exe 运行，无需安装，也不需要额外装 .NET 运行时。
3. 把视频/音频文件拖进窗口，选一个目标格式，点「开始转换」。
4. 第一次使用建议先看「设置 → 教程」页。

目录说明
--------
FileToFiles.exe        主程序
ffmpeg\                内置 FFmpeg（请勿删除或改名）
  ffmpeg.exe           转码
  ffprobe.exe          媒体信息探测
  LICENSE.txt          FFmpeg 许可（GPL）
LICENSING.txt            许可与合规说明（商用/开源必读）
THIRD-PARTY-NOTICES.txt  第三方组件与许可声明
SOURCE-OFFER.txt         FFmpeg 对应源码提供承诺

数据位置（与便携目录分离，卸载时直接删文件夹即可）
--------------------------------------------------
%LOCALAPPDATA%\FileToFiles\
  config\settings.json   用户设置
  config\presets.json    自定义预设
  logs\                  转换日志（按天滚动，保留 7 天）
  crash\                 崩溃日志

小提示
------
- 可以把文件或文件夹直接拖到 FileToFiles.exe 图标上打开。
- 程序已在运行时再次双击，会把文件交给已打开的窗口，不会重复启动。
- 转换完成的提示音与任务栏提醒可以在「设置 → 转换」里关闭。
