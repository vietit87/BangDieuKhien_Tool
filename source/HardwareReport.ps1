[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false)
$ErrorActionPreference='SilentlyContinue'
$unknown='Không xác định'
function row($label,$value){if(!("$value").Trim()){$value=$unknown};"$label : $value"}
function cim($class){Get-CimInstance -ClassName $class -OperationTimeoutSec 5}
# Windows caches the firmware tables even when CIM access is restricted.
$raw=(Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Services\mssmbios\Data').SMBiosData
$dimmMiB=@()
$cores=0
$threads=0
for($i=8;$i+4 -le $raw.Length;){
 $type=$raw[$i];$length=$raw[$i+1]
 if($type -eq 127 -or $length -lt 4 -or $i+$length -gt $raw.Length){break}
 if($type -eq 17 -and $length -ge 14){
  $size=[BitConverter]::ToUInt16($raw,$i+12)
  if($size -eq 32767 -and $length -ge 32){$size=[BitConverter]::ToUInt32($raw,$i+28)}
  elseif($size -eq 65535){$size=0}
  elseif($size -band 32768){$size=($size -band 32767)/1024}
  if($size -gt 0){$dimmMiB+=$size}
 }
 if($type -eq 4 -and $length -ge 38){
  $c=$raw[$i+35];$t=$raw[$i+37]
  if($c -eq 255 -and $length -ge 44){$c=[BitConverter]::ToUInt16($raw,$i+42)}
  if($t -eq 255 -and $length -ge 48){$t=[BitConverter]::ToUInt16($raw,$i+46)}
  $cores+=$c;$threads+=$t
 }
 $stringsAt=$i+$length
 $serialIndex=if($type -eq 1 -and $length -gt 7){$raw[$i+7]}else{0}
 $i+=$length
 while($i+1 -lt $raw.Length -and ($raw[$i] -ne 0 -or $raw[$i+1] -ne 0)){$i++}
 if($serialIndex -gt 0 -and $i -ge $stringsAt){$parts=[Text.Encoding]::ASCII.GetString($raw,$stringsAt,$i-$stringsAt).Split([char]0);if($parts.Count -ge $serialIndex){$systemSerial=$parts[$serialIndex-1]}}
 $i+=2
}
$pc=cim Win32_ComputerSystem
$board=cim Win32_BaseBoard|Select-Object -First 1
$bios=cim Win32_BIOS|Select-Object -First 1
row 'Tên máy' ([Environment]::MachineName)
row 'Ngày' (Get-Date -Format 'dd/MM/yyyy HH:mm')
$reg=Get-ItemProperty 'HKLM:\HARDWARE\DESCRIPTION\System\BIOS'
row 'Model' $(if($pc.Model){$pc.Model}else{$reg.SystemProductName})
$cpuText=((cim Win32_Processor|ForEach-Object {"$($_.Name) ($($_.NumberOfCores) nhân, $($_.NumberOfLogicalProcessors) luồng)"}) -join ' + ')
if(!$cpuText){$cpuText=(Get-ItemProperty 'HKLM:\HARDWARE\DESCRIPTION\System\CentralProcessor\0').ProcessorNameString+' ('+$(if($cores){[string]$cores+' nhân'}else{'chưa xác định số nhân'})+', '+[Environment]::ProcessorCount+' luồng)'}
row 'CPU' $cpuText
$ram=@(cim Win32_PhysicalMemory)
if($ram.Count){$mem='{0:0.##} GiB ({1} thanh)' -f (($ram|Measure-Object Capacity -Sum).Sum/1GB),$ram.Count}
if(!$mem -and $dimmMiB.Count){$mem=("{0:0.##} GiB ({1} thanh)" -f (($dimmMiB|Measure-Object -Sum).Sum/1024),$dimmMiB.Count)}
if(!$mem){
 Add-Type -AssemblyName Microsoft.VisualBasic
 $bytes=(New-Object Microsoft.VisualBasic.Devices.ComputerInfo).TotalPhysicalMemory
 if($bytes){$mem=('{0:0.##} GiB khả dụng cho Windows (chưa xác định số thanh)' -f ($bytes/1GB))}
}
row 'RAM' $mem
$disks=@(Get-PhysicalDisk|Where-Object BusType -ne USB|ForEach-Object {'{0} {1:0.##} GiB ({2:0} GB danh định) - {3}' -f $_.MediaType,($_.Size/1GB),($_.Size/1e9),$_.FriendlyName})
if(!$disks){$disks=@(cim Win32_DiskDrive|Where-Object InterfaceType -ne USB|ForEach-Object {'{0} - {1:0.##} GiB' -f $_.Model,($_.Size/1GB)})}
if($disks.Count){$storage=('{0:00} ổ - ' -f $disks.Count)+($disks -join ' + ')}
row 'Lưu trữ' $storage
$gpuNames=@(cim Win32_VideoController|ForEach-Object Name)
if(!$gpuNames){$gpuNames=@(Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\0*'|ForEach-Object DriverDesc|Where-Object {$_}|Sort-Object -Unique)}
row 'Card đồ họa' (($gpuNames|ForEach-Object {
 $kind=switch -Regex ($_){'Intel.*(UHD|HD Graphics|Iris Xe(?! MAX))|Radeon.*(780M|680M|760M|890M)'{'onboard';break};'GeForce|Quadro|Radeon RX'{'rời';break};default{'chưa xác định onboard/rời'}}
 "$_ ($kind)"
}) -join ' + ')
$base=("$($board.Manufacturer) $($board.Product)").Trim()
if(!$base){$base=("$($reg.BaseBoardManufacturer) $($reg.BaseBoardProduct)").Trim()}
row 'Mainboard' $base
$driver=switch -Regex ("$($reg.SystemManufacturer) $($pc.Manufacturer) $($board.Manufacturer)"){
 'Dell'{if(!$systemSerial){$systemSerial=$bios.SerialNumber};if($systemSerial){"https://www.dell.com/support/home/product-support/servicetag/$([uri]::EscapeDataString($systemSerial.Trim()))/drivers"}else{'https://www.dell.com/support/home'};break}
 'Lenovo'{'https://pcsupport.lenovo.com/';break}
 'HP|Hewlett'{'https://support.hp.com/drivers';break}
 'ASUS'{'https://www.asus.com/support/download-center/';break}
 'Gigabyte'{'https://www.gigabyte.com/Support';break}
 'Micro-Star|MSI'{'https://www.msi.com/support/download';break}
 default{if($base){"https://www.bing.com/search?q=$([uri]::EscapeDataString($base+' official drivers')) (tra cứu theo mainboard)"}}
}
row 'Trang tải driver' $driver
if($board.InstallDate){$boardDate=$board.InstallDate.ToString('dd/MM/yyyy')+' (ngày cài đặt, chưa xác định ngày xuất xưởng)'}
if($reg.BIOSReleaseDate){$biosDate=$reg.BIOSReleaseDate}
if($bios.ReleaseDate){$biosDate=$bios.ReleaseDate.ToString('dd/MM/yyyy')}
row 'Năm sản xuất' "Chưa xác định ngày xuất xưởng mainboard$(if($biosDate){'. Ngày phát hành BIOS: '+$biosDate})"
$os=cim Win32_OperatingSystem
$ver=Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion'
$osName=$os.Caption
if(!$osName){$osName=$ver.ProductName;if([int]$ver.CurrentBuild -ge 22000){$osName=$osName -replace 'Windows 10','Windows 11'}}
$bits=if([Environment]::Is64BitOperatingSystem){64}else{32}
row 'Hệ điều hành' "$osName $($ver.DisplayVersion) - $bits bit - build $($ver.CurrentBuild).$($ver.UBR)"
$licenses=@(Get-CimInstance SoftwareLicensingProduct -Filter 'PartialProductKey IS NOT NULL' -OperationTimeoutSec 5)
function activation($items){if($items){($items|ForEach-Object {$_.Name+' - '+@('Chưa kích hoạt','Đã kích hoạt','Gia hạn ban đầu','Gia hạn bổ sung','Gia hạn không chính hãng','Chế độ thông báo','Gia hạn mở rộng')[[int]$_.LicenseStatus]}) -join ' | '}else{'Không xác định'}}
row 'Kích hoạt Windows' (activation ($licenses|Where-Object ApplicationID -eq '55c92734-d682-4d71-983e-d6ec3f16059f'))
$apps=@(Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*','HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*','HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*'|Where-Object {$_.DisplayName -and !$_.SystemComponent})
$office=$apps|Where-Object {$_.DisplayName -match '^Microsoft (365|Office)\b' -and $_.DisplayName -notmatch 'Update|Language Pack|Proofing|Add-in|Click-to-Run|Component|Runtime'}|Sort-Object @{Expression={try{[version]$_.DisplayVersion}catch{[version]'0.0'}};Descending=$true}|Select-Object -First 1
row 'Office' ("$($office.DisplayName) $($office.DisplayVersion)".Trim())
$officeLic=@($licenses|Where-Object {$_.Name -match 'Office|Microsoft 365'})
if(!$officeLic){$officeLic=@(Get-CimInstance OfficeSoftwareProtectionProduct -Filter 'PartialProductKey IS NOT NULL' -OperationTimeoutSec 5)}
row 'Kích hoạt Office' (activation $officeLic)
$av=@(Get-CimInstance -Namespace root/SecurityCenter2 -ClassName AntiVirusProduct|ForEach-Object displayName)
if(Get-Service WinDefend){$av+=@("Microsoft Defender Antivirus")}
$av+=@($apps|Where-Object {$_.DisplayName -match 'Antivirus|Anti-virus|Internet Security|Endpoint Security|Total Security|Malwarebytes|Bitdefender|Kaspersky|ESET NOD32'}|ForEach-Object DisplayName)
row 'Phần mềm bảo mật' (($av|Sort-Object -Unique) -join ' | ')
$net=@(foreach($nic in [Net.NetworkInformation.NetworkInterface]::GetAllNetworkInterfaces()){
 if($nic.OperationalStatus -ne 'Up' -or $nic.NetworkInterfaceType -eq 'Loopback'){continue}
 $ips=($nic.GetIPProperties().UnicastAddresses|ForEach-Object {$_.Address.ToString()}) -join ', '
 "$($nic.Description) - $($nic.GetPhysicalAddress()) - $ips"
})
row 'MAC / IP' ($net -join ' | ')
$bat=@(cim Win32_Battery)
Add-Type -AssemblyName System.Windows.Forms
$charge=[Windows.Forms.SystemInformation]::PowerStatus.BatteryChargeStatus
$hasBattery=$null -ne $charge -and [int]$charge -ne 255 -and (([int]$charge -band 128) -eq 0)
$desktop=$env:BDK_REPORT_DESKTOP
$html=Join-Path $desktop 'battery-report.html'
$tmp=Join-Path $desktop ('battery-'+[guid]::NewGuid()+'.html')
try{
 & "$env:windir\System32\powercfg.exe" /batteryreport /output $tmp 2>$null|Out-Null
 if($LASTEXITCODE -eq 0 -and (Test-Path -LiteralPath $tmp)){
  $body=Get-Content -LiteralPath $tmp -Raw -Encoding utf8
  if(Test-Path -LiteralPath $html){Move-Item -LiteralPath $tmp -Destination $html -Force -ErrorAction Stop}else{[IO.File]::Move($tmp,$html)}
 }
}finally{if(Test-Path -LiteralPath $tmp){Remove-Item -LiteralPath $tmp}}
function capacities($pattern){
 foreach($tr in [regex]::Matches($body,'(?is)<tr\b[^>]*>(.*?)</tr>')){
  $cells=[regex]::Matches($tr.Groups[1].Value,'(?is)<t[dh]\b[^>]*>(.*?)</t[dh]>')
  if($cells.Count -lt 2){continue}
  if(($cells[0].Groups[1].Value -replace '<[^>]*>','') -notmatch $pattern){continue}
  for($i=1;$i -lt $cells.Count;$i++){
   $val=[Net.WebUtility]::HtmlDecode(($cells[$i].Groups[1].Value -replace '<[^>]*>',''))
   if($val -match '([\d\s.,]+)\s*mWh'){[double]($matches[1] -replace '\D','')}else{0}
  };break
 }
}
$design=@(capacities 'DESIGN CAPACITY|DUNG LƯỢNG THIẾT KẾ')
$full=@(capacities 'FULL CHARGE CAPACITY|DUNG LƯỢNG SẠC ĐẦY')
if(!$bat.Count -and !$design.Count -and !$hasBattery){
 if($pc.PCSystemType -eq 1){row 'PIN' 'Máy bàn - không phát hiện pin'}else{row 'PIN' 'Không phát hiện pin hoặc Windows chưa cung cấp dữ liệu'}
}else{
 for($i=0;$i -lt [Math]::Max(1,[Math]::Max($bat.Count,$design.Count));$i++){
 $info='Số cell: không xác định'
 if($design[$i] -gt 0){$info+=('; thiết kế {0:0.##} Wh' -f ($design[$i]/1000))}
 if($full[$i] -gt 0){$info+=('; sạc đầy {0:0.##} Wh' -f ($full[$i]/1000))}
 if($design[$i] -gt 0 -and $full[$i] -gt 0){$info+=('; sức khỏe {0:0.#}%' -f (100*$full[$i]/$design[$i]))}else{$info+='; sức khỏe chưa xác định'}
 row ('PIN '+($i+1)) $info
 }
}
