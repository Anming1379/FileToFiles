; ============================================================================
;  ALambda File To Files - Inno Setup 脚本
;  编译：ISCC.exe /DMyAppVersion=1.1.0 FileToFiles.iss
;  或直接运行 packaging\build-installer.ps1
; ============================================================================

#ifndef MyAppVersion
  #define MyAppVersion "0.0.0"
#endif

#define MyAppName "ALambda File To Files"
#define MyAppShortName "File To Files"
#define MyAppPublisher "Anming_cat"
#define MyAppExeName "FileToFiles.exe"
#define MyAppId "{{9E4B7C2A-1D36-4F58-8A0E-2C5D9B7F4E31}"
#define SourceDir "..\dist\installer-app"

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
VersionInfoVersion={#MyAppVersion}
VersionInfoProductName={#MyAppName}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} 安装程序

DefaultDirName={autopf}\{#MyAppShortName}
DefaultGroupName={#MyAppShortName}
DisableProgramGroupPage=yes
AllowNoIcons=yes

OutputDir=..\dist
OutputBaseFilename=FileToFiles-v{#MyAppVersion}-setup
SetupIconFile=..\assets\icon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}

LicenseFile=..\LICENSE
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
CloseApplications=yes
RestartApplications=no
MinVersion=10.0.17763

[Languages]
Name: "chinesesimp"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "dotnet"; Description: "下载并安装 .NET 10 桌面运行时（需要联网）"; GroupDescription: "运行环境："; Check: NeedsDotNet

[Files]
; 主程序与依赖（框架依赖发布，不含 .NET 运行时）
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; 运行时安装助手
Source: "install-dotnet.cmd"; DestDir: "{app}"; Flags: ignoreversion
Source: "install-dotnet.ps1"; DestDir: "{app}"; Flags: ignoreversion
; 许可与合规文件
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSING.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\THIRD-PARTY-NOTICES.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\SOURCE-OFFER.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\AI-GENERATED-NOTICE.txt"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
; 缺少运行时时，默认勾选安装（需联网）
Filename: "{cmd}"; Parameters: "/c ""{app}\install-dotnet.cmd"" /quiet"; Description: "安装 .NET 10 桌面运行时（需要联网）"; Flags: waituntilterminated; Tasks: dotnet
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; 只清理安装目录本身留下的内容，用户配置（%LOCALAPPDATA%）保留
Type: dirifempty; Name: "{app}\ffmpeg"

[Code]
{ ---------------------------------------------------------------
  检测 .NET 10 桌面运行时（Microsoft.WindowsDesktop.App 10.x）
  --------------------------------------------------------------- }
function IsDotNet10DesktopInstalled: Boolean;
var
  FindRec: TFindRec;
  SearchPath: String;
begin
  Result := False;
  SearchPath := ExpandConstant('{commonpf}\dotnet\shared\Microsoft.WindowsDesktop.App');

  if FindFirst(SearchPath + '\10.*', FindRec) then
  begin
    try
      repeat
        if (FindRec.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0 then
          Result := True;
      until Result or (not FindNext(FindRec));
    finally
      FindClose(FindRec);
    end;
  end;
end;

function NeedsDotNet: Boolean;
begin
  Result := not IsDotNet10DesktopInstalled;
end;

function InitializeSetup: Boolean;
begin
  Result := True;

  if not IsDotNet10DesktopInstalled then
  begin
    if MsgBox('检测到本机尚未安装 .NET 10 桌面运行时。' + #13#10 + #13#10 +
              '本软件需要它才能启动。安装程序最后一步可以帮你自动下载安装（需要联网）。' + #13#10 +
              '如果暂时不装，也可以稍后双击安装目录里的 install-dotnet.cmd。' + #13#10 + #13#10 +
              '是否继续安装？', mbConfirmation, MB_YESNO) = IDNO then
      Result := False;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    { 安装完成后再次确认运行时状态，写进日志便于排查 }
    if IsDotNet10DesktopInstalled then
      Log('.NET 10 Desktop Runtime: detected')
    else
      Log('.NET 10 Desktop Runtime: NOT detected');
  end;
end;
