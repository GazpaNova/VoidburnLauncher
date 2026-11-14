param([string]$Output=(Join-Path $PSScriptRoot 'VoidburnSetup.exe'))
$ErrorActionPreference='Stop'
$bootstrap=Get-Content -Raw (Join-Path $PSScriptRoot 'rsprox-bootstrap.json')|ConvertFrom-Json
$manifest=($bootstrap.artifacts|ForEach-Object {$_.hash.ToUpperInvariant()+'|'+$_.name+'|'+$_.path}) -join "`n"
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'RuntimeFiles.txt'),$manifest+"`n")
$direct=Get-Content -Raw (Join-Path $PSScriptRoot 'DirectRuntime.cs')
$direct=[regex]::Replace($direct,'BootstrapHash = "[^"]+"','BootstrapHash = "'+(Get-FileHash (Join-Path $PSScriptRoot 'VoidburnBootstrap.jar')).Hash+'"')
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'DirectRuntime.cs'),$direct)
$compiler='C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
Push-Location $PSScriptRoot
try{
 & $compiler /nologo /target:winexe /platform:x64 /optimize+ /win32icon:Voidburn.ico ("/out:"+$Output) /reference:System.Security.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:Microsoft.CSharp.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll /reference:vendor/yaml/lib/net47/YamlDotNet.dll /resource:vendor/yaml/lib/net47/YamlDotNet.dll,YamlDotNet.dll /resource:RuntimeFiles.txt,RuntimeFiles.txt /resource:VoidburnBootstrap.jar,VoidburnBootstrap.jar /resource:RevenantOnlineStatus.jar,RevenantOnlineStatus.jar /resource:VoidburnHiscores.jar,VoidburnHiscores.jar /resource:RevenantDropAll.jar,RevenantDropAll.jar /resource:VoidburnPing.jar,VoidburnPing.jar /resource:VoidburnBarrows.jar,VoidburnBarrows.jar /resource:LoginUtilities.jar,LoginUtilities.jar /resource:VoidburnMotd.jar,VoidburnMotd.jar /resource:Voidburn.png,Voidburn.png /resource:proxy-targets.yaml,proxy-targets.yaml /resource:Readme.txt,Readme.txt /resource:Licenses.txt,Licenses.txt Voidburn.cs DirectRuntime.cs SetupForm.cs LaunchSplash.cs AutoUpdate.cs DependencyDownload.cs RuntimeDownload.cs RememberedAccount.cs LoginToken.cs JavaRuntime.cs MotdAddon.cs
 if($LASTEXITCODE -ne 0){throw 'Voidburn compilation failed'}
}finally{Pop-Location}

