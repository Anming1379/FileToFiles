# 打包 Windows 安装包（框架依赖，不含 .NET 运行时）
#
# 用法：powershell -ExecutionPolicy Bypass -File .\packaging\build-installer.ps1
# 可选参数：
#   -SkipPublish        复用已有 dist\installer-app，只重新编译安装包
#   -IsccPath <路径>    指定 ISCC.exe 位置

param(
    [switch]$SkipPublish,
    [string]$IsccPath = "D:\Program Files (x86)\Inno Setup 6\ISCC.exe"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$csproj = Join-Path $root "FileToFiles.csproj"
$appDir = Join-Path $root "dist\installer-app"

$version = (Select-String -Path $csproj -Pattern '<Version>([^<]+)</Version>').Matches[0].Groups[1].Value
Write-Host "版本: $version"

if (-not (Test-Path $IsccPath)) {
    throw "找不到 Inno Setup 编译器：$IsccPath`n可用 -IsccPath 指定 ISCC.exe 路径。"
}

if (-not $SkipPublish) {
    Write-Host "正在发布框架依赖版本（不含 .NET 运行时）..."
    if (Test-Path $appDir) {
        Remove-Item $appDir -Recurse -Force
    }

    dotnet publish $csproj `
        -c Release `
        -r win-x64 `
        --self-contained false `
        -p:DebugType=None `
        -p:DebugSymbols=false `
        -o $appDir `
        -v m -nologo

    if ($LASTEXITCODE -ne 0) {
        throw "发布失败，退出码 $LASTEXITCODE"
    }
}

# 附带合规文件与运行时安装助手
Copy-Item (Join-Path $root "LICENSE") $appDir -Force
Copy-Item (Join-Path $root "LICENSING.txt") $appDir -Force
Copy-Item (Join-Path $root "THIRD-PARTY-NOTICES.txt") $appDir -Force
Copy-Item (Join-Path $root "SOURCE-OFFER.txt") $appDir -Force
Copy-Item (Join-Path $root "AI-GENERATED-NOTICE.txt") $appDir -Force
Copy-Item (Join-Path $root "packaging\install-dotnet.cmd") $appDir -Force
Copy-Item (Join-Path $root "packaging\install-dotnet.ps1") $appDir -Force

Write-Host "正在编译安装包..."
$iss = Join-Path $root "packaging\FileToFiles.iss"
$sw = [System.Diagnostics.Stopwatch]::StartNew()
& $IsccPath "/DMyAppVersion=$version" $iss
$sw.Stop()

if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup 编译失败，退出码 $LASTEXITCODE"
}

$setup = Join-Path $root "dist\FileToFiles-v$version-setup.exe"
if (-not (Test-Path $setup)) {
    throw "未找到输出文件：$setup"
}

$sizeMb = (Get-Item $setup).Length / 1MB
$appMb = (Get-ChildItem $appDir -Recurse -File | Measure-Object Length -Sum).Sum / 1MB

Write-Host ""
Write-Host "完成:"
Write-Host ("  应用目录 {0:N0} MB，安装包 {1:N1} MB，耗时 {2:N0} 秒" -f $appMb, $sizeMb, $sw.Elapsed.TotalSeconds)
Write-Host ("  输出: {0}" -f $setup)
