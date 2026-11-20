$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
$inputValue = $env:BDK_INPUT
function Step($message){[Console]::WriteLine("@@"+$message)}
function Set-Number($path,$name,$value) {
    Step ("Áp dụng thiết lập "+$name); if (!(Test-Path -LiteralPath $path)) { New-Item -Path $path -Force | Out-Null }
    New-ItemProperty -LiteralPath $path -Name $name -PropertyType DWord -Value $value -Force | Out-Null
}
function With-ExplorerStopped([scriptblock]$action) {
    Step "Bước 1/3 • Dừng Explorer của phiên hiện tại"; $session = (Get-Process -Id $PID).SessionId
    Get-Process explorer -ErrorAction SilentlyContinue | Where-Object SessionId -eq $session | Stop-Process -Force
    try { & $action } finally { Step "Bước 3/3 • Mở lại Explorer"; Start-Process "$env:windir\explorer.exe" }
}
function With-SpoolerStopped([scriptblock]$action) {
    try { Step "Dừng dịch vụ in"; Stop-Service Spooler; & $action } finally { Step "Khởi động lại dịch vụ in"; Start-Service Spooler }
}
try {
 switch ($env:BDK_COMMAND) {
 {$_ -in "updateOff","updateOn"} {
    $off=$_ -eq "updateOff"
    $record="HKLM:\SOFTWARE\LeQuocViet\BangDieuKhien"
    $svcKey="HKLM:\SYSTEM\CurrentControlSet\Services\wuauserv"
    Step "10% • Đọc cấu hình Windows Update"
    $saved=(Get-ItemProperty -LiteralPath $record -Name UpdateSnapshot -ErrorAction SilentlyContinue).UpdateSnapshot
    if(!$saved -and !$off){throw "Chưa có cấu hình do công cụ lưu để khôi phục."}
    if(!$saved){
        $service=Get-Service wuauserv
        if($service.Status -notin "Running","Stopped"){throw "Dịch vụ đang chuyển trạng thái. Hãy thử lại."}
        $config=Get-ItemProperty -LiteralPath $svcKey
        $saved=@{start=[int]$config.Start;delay=$config.DelayedAutoStart;running=($service.Status -eq "Running")} | ConvertTo-Json -Compress
        if(!(Test-Path $record)){New-Item $record -Force | Out-Null}
        New-ItemProperty $record -Name UpdateSnapshot -Value $saved -PropertyType String -Force | Out-Null
    }
    $state=$saved | ConvertFrom-Json
    if($off){
        Step "40% • Tắt khởi động dịch vụ Windows Update"
        Set-Service wuauserv -StartupType Disabled
        Step "70% • Dừng Windows Update"
        Stop-Service wuauserv
        if((Get-Service wuauserv).Status -ne "Stopped" -or (Get-ItemProperty $svcKey).Start -ne 4){throw "Windows chưa áp dụng. Dùng Bật Update để khôi phục."}
        "100% • Đã dừng Windows Update."
    }else{
        Step "40% • Khôi phục kiểu khởi động Windows Update"
        $modes=@{2="Automatic";3="Manual";4="Disabled"}
        Set-Service wuauserv -StartupType $modes[[int]$state.start]
        if($null -ne $state.delay){Set-ItemProperty $svcKey DelayedAutoStart ([int]$state.delay)}
        elseif((Get-ItemProperty $svcKey).PSObject.Properties["DelayedAutoStart"]){Remove-ItemProperty $svcKey DelayedAutoStart}
        Step "75% • Khôi phục trạng thái dịch vụ"
        if($state.running){Start-Service wuauserv}else{Stop-Service wuauserv}
        if(((Get-Service wuauserv).Status -eq "Running") -ne $state.running -or (Get-ItemProperty $svcKey).Start -ne $state.start){throw "Chưa khôi phục đủ cấu hình. Có thể nhấn Bật Update để thử lại."}
        Remove-ItemProperty $record UpdateSnapshot
        "100% • Đã khôi phục Windows Update theo cấu hình ban đầu."
    }
 }
 'notifications' { Set-Number 'HKCU:\Software\Microsoft\Windows\CurrentVersion\PushNotifications' ToastEnabled 0; 'Đã tắt thông báo dạng biểu ngữ cho người dùng hiện tại.' }
 'firewallOn' { Set-NetFirewallProfile -Profile Domain,Private,Public -Enabled True; 'Đã bật tường lửa.' }
 'rename' {
    if ($inputValue -notmatch '^(?![0-9]+$)[a-z0-9](?:[a-z0-9-]{0,13}[a-z0-9])?$') { throw 'Tên máy cần 1–15 ký tự chữ, số hoặc dấu gạch nối.' }
    Step "50% • Áp dụng tên máy"; Rename-Computer -NewName $inputValue; 'Đã đổi tên máy. Khởi động lại để có hiệu lực.'
 }
 {$_ -in 'classic','modern'} {
    $key='HKCU:\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32'
    if ($_ -eq 'classic') { (New-Item -Path $key -Force).SetValue('','') }
    elseif (Test-Path -LiteralPath $key) { Remove-Item -LiteralPath $key }
    With-ExplorerStopped {}; 'Đã áp dụng cấu hình menu cho Explorer.'
 }
 'taskmgr' { Set-Number 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Policies\System' DisableTaskMgr 0; Step "80% • Mở Task Manager"; Start-Process taskmgr.exe; 'Đã mở Task Manager.' }
 'explorer' { With-ExplorerStopped {}; 'Đã khởi động lại Explorer.' }
 'icons' {
    With-ExplorerStopped {
        Step "50% • Xoá bộ nhớ đệm biểu tượng"; $cache=Join-Path $env:LOCALAPPDATA 'Microsoft\Windows\Explorer'
        Get-ChildItem -LiteralPath $cache -Filter 'iconcache*.db' -File -Force | Remove-Item -Force
        $old=Join-Path $env:LOCALAPPDATA 'IconCache.db'
        if(Test-Path -LiteralPath $old){Remove-Item -LiteralPath $old -Force}
    }; 'Explorer đã tạo lại bộ nhớ đệm biểu tượng.'
 }
 'usb' {
    Set-Number 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Policies\Explorer' NoDriveTypeAutoRun 255
    Step "20% • Tìm ổ USB"; $volumes=@([IO.DriveInfo]::GetDrives() | Where-Object {$_.DriveType -eq 'Removable' -and $_.IsReady})
     $index=0;foreach($v in $volumes){Step ("{0}% • Quét USB {1}" -f (20+[int](80*$index/$volumes.Count)),$v.Name);$index++;Start-MpScan -ScanType CustomScan -ScanPath $v.RootDirectory.FullName}
    "Đã tắt AutoPlay. Đã quét $($volumes.Count) ổ tháo rời bằng Microsoft Defender."
 }
 'queue' {
    With-SpoolerStopped {
        Step "50% • Xoá lệnh in đang chờ"; $queue=Join-Path ([Environment]::SystemDirectory) 'spool\PRINTERS'
        Get-ChildItem -LiteralPath $queue -File -Force | Remove-Item -Force
    }; 'Đã xoá lệnh in và bật lại Print Spooler.'
 }
 'canon' {
    $root='HKLM:\SYSTEM\CurrentControlSet\Control\Print'
    $pattern='^Canon\s+(?:LBP\s*)?2900(?:\b|$)'
    Step "10% • Tìm hàng đợi Canon"; $printers=@(Get-Printer)
    $targets=@($printers | Where-Object {$_.Name -match $pattern -or $_.DriverName -match $pattern})
    if(!$targets.Count){throw 'Windows chưa tìm thấy hàng đợi Canon LBP2900.'}
    Step "30% • Gỡ hàng đợi Canon"; $ports=@($targets.PortName | Select-Object -Unique)
    foreach($t in $targets){Remove-Printer -InputObject $t}
    With-SpoolerStopped {
        Step "60% • Kiểm tra cổng USB riêng"; foreach($port in $ports){
            if($port -notmatch '^USB\d+$' -or @($printers | Where-Object {$_.PortName -eq $port -and $_ -notin $targets}).Count){continue}
            foreach($sub in 'Ports','UsbPortList'){
                $entry=Join-Path "$root\Monitors\USB Monitor\$sub" $port
                if(Test-Path -LiteralPath $entry){Remove-Item -LiteralPath $entry -Recurse -Force}
            }
        }
    }
    Step "90% • Dò lại thiết bị USB"; & pnputil.exe /scan-devices | Out-Null
    if($LASTEXITCODE){throw 'Đã gỡ hàng đợi Canon. Windows chưa quét lại thiết bị USB thành công.'}
    'Đã quét lại USB. Tắt rồi bật nguồn Canon LBP2900 và kiểm tra trong Printers.'
 }
 'lan' {
    if($inputValue -notmatch '^[A-Za-z0-9](?:[A-Za-z0-9.-]{0,251}[A-Za-z0-9])?$'){throw 'Nhập tên máy chủ hoặc địa chỉ IPv4.'}
    Step "40% • Bật dịch vụ khám phá mạng"; Set-Service FDResPub -StartupType Automatic; Start-Service FDResPub
    Step "80% • Mở máy chủ chia sẻ"; Start-Process explorer.exe -ArgumentList ("\\$inputValue")
    'Đã mở máy chủ. Nhấn đúp máy in được chia sẻ để kết nối.'
 }
 'temp' {
    Step "10% • Kiểm tra thư mục Temp"; $folder=(Get-Item -LiteralPath ([IO.Path]::GetTempPath())).FullName.TrimEnd('\')
    $expected=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Temp'
    if($folder -ne $expected -or ((Get-Item -LiteralPath $folder).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'TEMP nằm ngoài thư mục Temp chuẩn của người dùng.'}
    $count=0;$skip=0
    $dirs=New-Object 'System.Collections.Generic.Stack[string]';$dirs.Push($folder)
    $empty=New-Object 'System.Collections.Generic.Stack[string]'
    Step "30% • Dọn các tệp tạm"; while($dirs.Count){
        $dir=$dirs.Pop()
        foreach($entry in Get-ChildItem -LiteralPath $dir -Force -ErrorAction SilentlyContinue){
            if($entry.Attributes -band [IO.FileAttributes]::ReparsePoint){$skip++;continue}
            if($entry.PSIsContainer){$dirs.Push($entry.FullName);$empty.Push($entry.FullName);continue}
            try{[IO.File]::Delete($entry.FullName);$count++}catch{$skip++}
        }
    }
    Step "90% • Dọn thư mục rỗng"; while($empty.Count){try{[IO.Directory]::Delete($empty.Pop(),$false)}catch{}}
    "Đã xoá $count tệp tạm. Bỏ qua $skip mục bị khoá hoặc liên kết."
 }
 default {throw 'Tác vụ không hợp lệ.'}
 }
} catch { [Console]::WriteLine($_.Exception.Message);exit 1 }
