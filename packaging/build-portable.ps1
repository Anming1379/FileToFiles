# 打包便携自包含版（win-x64）
#
# 用法：在项目根目录运行
#   powershell -ExecutionPolicy Bypass -File .\packaging\build-portable.ps1
# 可选参数：
#   -Configuration Release   构建配置（默认 Release）
#   -SkipPublish             复用已有 dist\portable 内容，只重新打包 zip

param(
    [string]$Configuration = "Release",
    [switch]$SkipPublish
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$csproj = Join-Path $root "FileToFiles.csproj"

# 版本号从 csproj 读取
$version = (Select-String -Path $csproj -Pattern '<Version>([^<]+)</Version>').Matches[0].Groups[1].Value
Write-Host "版本: $version"

$portableDir = Join-Path $root "dist\portable\FileToFiles"
$zipPath = Join-Path $root "dist\FileToFiles-v$version-win-x64-portable.zip"

if (-not $SkipPublish) {
    Write-Host "正在发布自包含版本（首次会下载运行时包，可能较慢）..."
    if (Test-Path (Join-Path $root "dist\portable")) {
        Remove-Item (Join-Path $root "dist\portable") -Recurse -Force
    }

    dotnet publish $csproj `
        -c $Configuration `
        -r win-x64 `
        --self-contained true `
        -p:DebugType=None `
        -p:DebugSymbols=false `
        -o $portableDir `
        -v m -nologo

    if ($LASTEXITCODE -ne 0) {
        throw "发布失败，退出码 $LASTEXITCODE"
    }
}

# 附带说明与合规文件
Copy-Item (Join-Path $root "LICENSE") $portableDir -Force
Copy-Item (Join-Path $root "THIRD-PARTY-NOTICES.txt") $portableDir -Force
Copy-Item (Join-Path $root "LICENSING.txt") $portableDir -Force
Copy-Item (Join-Path $root "SOURCE-OFFER.txt") $portableDir -Force
Copy-Item (Join-Path $root "AI-GENERATED-NOTICE.txt") $portableDir -Force
Copy-Item (Join-Path $root "packaging\PORTABLE-README.txt") $portableDir -Force

New-Item -ItemType Directory -Path (Split-Path $zipPath) -Force | Out-Null
if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

Write-Host "正在压缩..."
Add-Type -AssemblyName System.IO.Compression.FileSystem
$sw = [System.Diagnostics.Stopwatch]::StartNew()
[System.IO.Compression.ZipFile]::CreateFromDirectory(
    $portableDir,
    $zipPath,
    [System.IO.Compression.CompressionLevel]::Optimal,
    $true)
$sw.Stop()

$sizeMb = (Get-Item $zipPath).Length / 1MB
$unpackedMb = (Get-ChildItem $portableDir -Recurse -File | Measure-Object Length -Sum).Sum / 1MB

Write-Host ""
Write-Host "完成:"
Write-Host ("  目录大小 {0:N0} MB，压缩包 {1:N1} MB，耗时 {2:N0} 秒" -f $unpackedMb, $sizeMb, $sw.Elapsed.TotalSeconds)
Write-Host ("  输出: {0}" -f $zipPath)
