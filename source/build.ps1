$ErrorActionPreference = 'Stop'
$project = $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
Add-Type -AssemblyName System.Drawing
$bmp = New-Object Drawing.Bitmap 64,64
$g = [Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = 'AntiAlias'
$g.Clear([Drawing.Color]::FromArgb(30,34,51))
$brush = New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(131,137,255))
$g.FillRectangle($brush,12,14,40,7)
$g.FillRectangle($brush,12,29,40,7)
$g.FillRectangle($brush,12,44,40,7)
$white = [Drawing.Brushes]::White
$g.FillEllipse($white,22,11,12,13)
$g.FillEllipse($white,37,26,12,13)
$g.FillEllipse($white,16,41,12,13)
$icon = [Drawing.Icon]::FromHandle($bmp.GetHicon())
$stream = [IO.File]::Create((Join-Path $project 'app.ico'))
$icon.Save($stream); $stream.Dispose(); $g.Dispose(); $brush.Dispose(); $bmp.Dispose()
$source = @('Program.cs','Native.cs','Wireless.cs','Lunar.cs','DisplayScale.cs','Checks.cs','Extras101.cs','NightRecord.cs','RamCleaner.cs','CpuRanking.cs','BluetoothPanel.cs','HardwareExport.cs','RepairPanel.cs','DisplayCurtain.cs','CameraSwitch.cs','ProcessNetwork.cs','ClockIntegration.cs') | ForEach-Object { Join-Path $project $_ }
$common = @('/nologo','/target:winexe','/platform:x64','/optimize+','/codepage:65001','/r:System.dll','/r:System.Core.dll','/r:System.Drawing.dll','/r:System.Windows.Forms.dll','/r:System.Management.dll','/r:System.Web.Extensions.dll',('/win32icon:'+(Join-Path $project 'app.ico')),('/resource:'+(Join-Path $project 'Repairs.ps1')+',Repairs.ps1'),('/resource:'+(Join-Path $project 'HardwareReport.ps1')+',HardwareReport.ps1'),('/resource:'+(Join-Path $project 'app.ico')+',app.ico'))
$common += @("/lib:$env:windir\Microsoft.NET\Framework64\v4.0.30319\WPF",'/r:UIAutomationClient.dll','/r:UIAutomationTypes.dll','/r:WindowsBase.dll','/r:System.Runtime.dll','/r:System.Runtime.WindowsRuntime.dll','/r:System.Runtime.InteropServices.WindowsRuntime.dll',('/r:'+$env:windir+'\System32\WinMetadata\Windows.Devices.winmd'),('/r:'+$env:windir+'\System32\WinMetadata\Windows.Foundation.winmd'))
& $compiler @common ('/win32manifest:'+(Join-Path $project 'app.manifest')) ('/out:'+(Join-Path $project 'BangDieuKhien_v1.0.16.exe')) @source
if($LASTEXITCODE -ne 0){ throw 'Build thất bại' }
if ($env:BDK_BUILD_TEST -eq '1') {
 $testDir=Join-Path $project 'verification'
 New-Item -ItemType Directory -Force -Path $testDir | Out-Null
 & $compiler @common ('/win32manifest:'+(Join-Path $testDir 'test.manifest')) ('/out:'+(Join-Path $testDir 'BangDieuKhienChecks.exe')) @source
 if($LASTEXITCODE -ne 0){throw 'Build kiểm tra thất bại'}
 $p=Start-Process -FilePath (Join-Path $testDir 'BangDieuKhienChecks.exe') -ArgumentList '--self-test' -WindowStyle Hidden -PassThru -Wait
 Get-Content -LiteralPath (Join-Path $testDir 'test-results.txt')
 if($p.ExitCode -ne 0){throw 'Kiểm tra thất bại'}
}
Get-Item (Join-Path $project 'BangDieuKhien_v1.0.16.exe') | Select-Object FullName,Length,LastWriteTime
