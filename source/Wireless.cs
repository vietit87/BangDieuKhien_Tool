using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Security;
using System.Web.Script.Serialization;
internal class WifiItem{public Guid Adapter;public string Name,Profile;public uint Signal;public bool Secure,Connected;public override string ToString(){return (Connected?"● ":"")+Name+"    "+Signal+"%"+(Secure?"  • Bảo mật":"  • Mở");}}
internal static class Wifi {
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct Interface{public Guid id;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=256)]public string description;public uint state;}
 [StructLayout(LayoutKind.Sequential)]struct Ssid{public uint len;[MarshalAs(UnmanagedType.ByValArray,SizeConst=32)]public byte[] bytes;}
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct Network{[MarshalAs(UnmanagedType.ByValTStr,SizeConst=256)]public string profile;public Ssid ssid;public uint bss,number;[MarshalAs(UnmanagedType.Bool)]public bool connectable;public uint reason,phyCount;[MarshalAs(UnmanagedType.ByValArray,SizeConst=8)]public uint[] phy;[MarshalAs(UnmanagedType.Bool)]public bool more;public uint quality;[MarshalAs(UnmanagedType.Bool)]public bool secure;public uint auth,cipher,flags,reserved;}
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct Parameters{public int mode;[MarshalAs(UnmanagedType.LPWStr)]public string profile;public IntPtr ssid,bssid;public int bss;public uint flags;}
 [DllImport("wlanapi.dll")]static extern uint WlanOpenHandle(uint v,IntPtr p,out uint negotiated,out IntPtr h);
 [DllImport("wlanapi.dll")]static extern uint WlanCloseHandle(IntPtr h,IntPtr p);
 [DllImport("wlanapi.dll")]static extern uint WlanEnumInterfaces(IntPtr h,IntPtr p,out IntPtr list);
 [DllImport("wlanapi.dll")]static extern uint WlanGetAvailableNetworkList(IntPtr h,ref Guid id,uint flags,IntPtr p,out IntPtr list);
 [DllImport("wlanapi.dll")]static extern uint WlanScan(IntPtr h,ref Guid id,IntPtr ssid,IntPtr ie,IntPtr p);
 [DllImport("wlanapi.dll",CharSet=CharSet.Unicode)]static extern uint WlanSetProfile(IntPtr h,ref Guid id,uint flags,string xml,string security,[MarshalAs(UnmanagedType.Bool)]bool overwrite,IntPtr p,out uint reason);
 [DllImport("wlanapi.dll")]static extern uint WlanConnect(IntPtr h,ref Guid id,ref Parameters args,IntPtr p);
 [DllImport("wlanapi.dll")]static extern uint WlanDisconnect(IntPtr h,ref Guid id,IntPtr p);
 [DllImport("wlanapi.dll")]static extern void WlanFreeMemory(IntPtr p);
 static void Check(uint r){if(r!=0)throw new Exception(r==5?"Windows yêu cầu bật quyền Vị trí để quét Wi-Fi. Vào Cài đặt > Quyền riêng tư > Vị trí.":new System.ComponentModel.Win32Exception((int)r).Message);}
 static IntPtr Handle(){uint v;IntPtr h;Check(WlanOpenHandle(2,IntPtr.Zero,out v,out h));return h;}
 public static List<WifiItem> Scan(){var r=new List<WifiItem>();IntPtr h=Handle(),p=IntPtr.Zero;try{Check(WlanEnumInterfaces(h,IntPtr.Zero,out p));int count=Marshal.ReadInt32(p);for(int i=0;i<count;i++){var a=(Interface)Marshal.PtrToStructure(IntPtr.Add(p,8+i*Marshal.SizeOf(typeof(Interface))),typeof(Interface));WlanScan(h,ref a.id,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero);IntPtr q=IntPtr.Zero;try{Check(WlanGetAvailableNetworkList(h,ref a.id,0,IntPtr.Zero,out q));int n=Marshal.ReadInt32(q);for(int j=0;j<n;j++){var x=(Network)Marshal.PtrToStructure(IntPtr.Add(q,8+j*Marshal.SizeOf(typeof(Network))),typeof(Network));string name=Encoding.UTF8.GetString(x.ssid.bytes,0,(int)x.ssid.len);if(name.Length>0)r.Add(new WifiItem{Adapter=a.id,Name=name,Profile=x.profile,Signal=x.quality,Secure=x.secure,Connected=(x.flags&1)!=0});}}finally{if(q!=IntPtr.Zero)WlanFreeMemory(q);}}return r;}finally{if(p!=IntPtr.Zero)WlanFreeMemory(p);WlanCloseHandle(h,IntPtr.Zero);}}
 public static void Disconnect(WifiItem w){IntPtr h=Handle();try{Check(WlanDisconnect(h,ref w.Adapter,IntPtr.Zero));}finally{WlanCloseHandle(h,IntPtr.Zero);}}
 public static void Connect(WifiItem w,string password){IntPtr h=Handle();try{string profile=w.Profile;if(string.IsNullOrEmpty(profile)){profile=w.Name;string name=SecurityElement.Escape(w.Name),hex=BitConverter.ToString(Encoding.UTF8.GetBytes(w.Name)).Replace("-","");string auth=w.Secure?"<authentication>WPA2PSK</authentication><encryption>AES</encryption>":"<authentication>open</authentication><encryption>none</encryption>";string key=w.Secure?"<sharedKey><keyType>passPhrase</keyType><protected>false</protected><keyMaterial>"+SecurityElement.Escape(password)+"</keyMaterial></sharedKey>":"";string xml="<?xml version=\"1.0\"?><WLANProfile xmlns=\"http://www.microsoft.com/networking/WLAN/profile/v1\"><name>"+name+"</name><SSIDConfig><SSID><hex>"+hex+"</hex><name>"+name+"</name></SSID></SSIDConfig><connectionType>ESS</connectionType><connectionMode>manual</connectionMode><MSM><security><authEncryption>"+auth+"<useOneX>false</useOneX></authEncryption>"+key+"</security></MSM></WLANProfile>";uint reason;Check(WlanSetProfile(h,ref w.Adapter,2,xml,null,true,IntPtr.Zero,out reason));}Parameters args=new Parameters{mode=0,profile=profile,bss=1};Check(WlanConnect(h,ref w.Adapter,ref args,IntPtr.Zero));}finally{WlanCloseHandle(h,IntPtr.Zero);}}
}
internal static class WinRT {
 const string Prelude=@"
Add-Type -AssemblyName System.Runtime.WindowsRuntime;
function Await($op, $type) {
 $m = [System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object { $_.Name -eq 'AsTask' -and $_.IsGenericMethod -and $_.GetGenericArguments().Count -eq 1 -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' } | Select-Object -First 1;
 $t=$m.MakeGenericMethod($type).Invoke($null,@($op)); if(-not $t.Wait(15000)){throw 'Quá thời gian chờ WinRT'}; return $t.Result;
}
";
 static string Quote(string s){return "'"+s.Replace("'","''")+"'";}
 public static string RadioPair(bool toggle){return Native.PS(Prelude+@"
[Windows.Devices.Radios.Radio,Windows.System.Devices,ContentType=WindowsRuntime] | Out-Null;
$radios=Await ([Windows.Devices.Radios.Radio]::GetRadiosAsync()) ([System.Collections.Generic.IReadOnlyList[Windows.Devices.Radios.Radio]]);
$r=@($radios | Where-Object { $_.Kind -eq 'WiFi' -or $_.Kind -eq 'Bluetooth' });
if($r.Count -eq 0){throw 'Không tìm thấy Wi-Fi hoặc Bluetooth'};
"+(toggle?@"
$access=Await ([Windows.Devices.Radios.Radio]::RequestAccessAsync()) ([Windows.Devices.Radios.RadioAccessStatus]);
if($access -ne 'Allowed'){throw ('Quyền Radio: '+$access)};
$target=if(@($r | Where-Object State -eq 'On').Count -gt 0){[Windows.Devices.Radios.RadioState]::Off}else{[Windows.Devices.Radios.RadioState]::On};
$changed=@();
try {
 foreach($x in $r){$before=$x.State;$s=Await ($x.SetStateAsync($target)) ([Windows.Devices.Radios.RadioAccessStatus]);if($s -ne 'Allowed'){throw ('Windows từ chối: '+$x.Kind+' '+$s)};$changed+=@{Radio=$x;State=$before};if($x.State -ne $target){throw ('Chưa áp dụng trạng thái '+$x.Kind)}}
} catch {
 $failure=$_.Exception.Message;$restoreFailed=$false;
 foreach($entry in $changed){try{$null=Await ($entry.Radio.SetStateAsync($entry.State)) ([Windows.Devices.Radios.RadioAccessStatus])}catch{$restoreFailed=$true}}
 throw ($failure+$(if($restoreFailed){'. Không khôi phục được toàn bộ radio; kiểm tra Wi-Fi/Bluetooth.'}else{'. Đã khôi phục các radio đã đổi.'}))
}
":"")+"$r | ForEach-Object { $_.Kind.ToString()+': '+$_.State }");}
 public static string Radio(string kind,bool? enabled){return Native.PS(Prelude+@"
[Windows.Devices.Radios.Radio,Windows.System.Devices,ContentType=WindowsRuntime] | Out-Null;
$access=Await ([Windows.Devices.Radios.Radio]::RequestAccessAsync()) ([Windows.Devices.Radios.RadioAccessStatus]);
if($access -ne 'Allowed'){throw ('Quyền Radio: '+$access)};
$radios=Await ([Windows.Devices.Radios.Radio]::GetRadiosAsync()) ([System.Collections.Generic.IReadOnlyList[Windows.Devices.Radios.Radio]]);
$r=@($radios | Where-Object { $_.Kind -eq "+Quote(kind)+@" }); if($r.Count -eq 0){throw 'Không tìm thấy bộ phát tương ứng'};
"+(enabled.HasValue?"foreach($x in $r){$s=Await ($x.SetStateAsync([Windows.Devices.Radios.RadioState]::"+(enabled.Value?"On":"Off")+")) ([Windows.Devices.Radios.RadioAccessStatus]);if($s -ne 'Allowed'){throw ('Windows từ chối: '+$s)}};":"")+"$r | ForEach-Object { $_.Name+': '+$_.State }");}
 const string Hot=@"
[Windows.Networking.Connectivity.NetworkInformation,Windows,ContentType=WindowsRuntime] | Out-Null;
[Windows.Networking.NetworkOperators.NetworkOperatorTetheringManager,Windows.Networking.NetworkOperators,ContentType=WindowsRuntime] | Out-Null;
$p=[Windows.Networking.Connectivity.NetworkInformation]::GetInternetConnectionProfile(); if(-not $p){throw 'Cần kết nối Internet trước khi phát Hotspot'};
$m=[Windows.Networking.NetworkOperators.NetworkOperatorTetheringManager]::CreateFromConnectionProfile($p);
";
 const string HotspotOperation=@"
function Wait-Stable {
 $watch=[Diagnostics.Stopwatch]::StartNew()
 while($m.TetheringOperationalState.ToString() -eq 'InTransition'){
  if($watch.ElapsedMilliseconds -gt 8000){throw 'Windows đang chuyển trạng thái Hotspot. Hãy thử lại.'}
  Start-Sleep -Milliseconds 100
 }
 return $m.TetheringOperationalState.ToString()
}
$state=Wait-Stable
if($null -ne $ssid){
 $cfg=$m.GetCurrentAccessPointConfiguration()
 if($cfg.Ssid -cne $ssid -or $cfg.Passphrase -cne $password){
  if($state -eq 'On'){throw 'Tắt phát trước khi đổi tên hoặc mật khẩu Hotspot.'}
  $cfg.Ssid=$ssid
  $cfg.Passphrase=$password
  $task=[System.WindowsRuntimeSystemExtensions]::AsTask($m.ConfigureAccessPointAsync($cfg))
  if(-not $task.Wait(10000)){throw 'Windows chưa hoàn tất lưu cấu hình Hotspot.'}
 }
}
if($target -and $target -ne $state){
 if($target -eq 'On'){$op=$m.StartTetheringAsync()}else{$op=$m.StopTetheringAsync()}
 $result=Await $op ([Windows.Networking.NetworkOperators.NetworkOperatorTetheringOperationResult,Windows.Networking.NetworkOperators,ContentType=WindowsRuntime])
 if($result.Status.ToString() -ne 'Success'){throw ($result.Status.ToString()+' '+$result.AdditionalErrorMessage)}
 $state=Wait-Stable
 if($state -ne $target){throw ('Windows báo trạng thái Hotspot: '+$state)}
}
$state+' | '+$m.GetCurrentAccessPointConfiguration().Ssid";
 public static string Hotspot(bool? on,string ssid,string password){
  string target=on.HasValue?(on.Value?"On":"Off"):"";
  string parameters="$target="+Quote(target)+";$ssid="+(ssid==null?"$null":Quote(ssid))+
   ";$password="+(password==null?"$null":Quote(password))+";";
  return Native.PS(Prelude+Hot+parameters+HotspotOperation);
 }
 public static string Bluetooth(){return Native.PS(Prelude+@"
[Windows.Devices.Enumeration.DeviceInformation,Windows.Devices.Enumeration,ContentType=WindowsRuntime] | Out-Null;
$a='System.Devices.Aep.ProtocolId:=""{e0cbf06c-cd8b-4647-bb8a-263b43f0f974}"" OR System.Devices.Aep.ProtocolId:=""{bb7bb05e-5972-42b5-94fc-76eaa7084d49}""';
$props=[string[]]@('System.Devices.Aep.IsConnected','System.Devices.Aep.DeviceAddress');
$ds=Await ([Windows.Devices.Enumeration.DeviceInformation]::FindAllAsync($a,$props,[Windows.Devices.Enumeration.DeviceInformationKind]::AssociationEndpoint)) ([Windows.Devices.Enumeration.DeviceInformationCollection]);
$batteries=@{};
Get-PnpDevice -Class Bluetooth -PresentOnly -ErrorAction SilentlyContinue | ForEach-Object {
 $dev=$_; $b=Get-PnpDeviceProperty -InstanceId $dev.InstanceId -KeyName '{104ea319-6ee2-4701-bd47-8ddbf425bbe5} 2' -ErrorAction SilentlyContinue;
 if($b -and $b.Data -ge 0 -and $b.Data -le 100 -and $dev.InstanceId -match 'DEV_([0-9A-Fa-f]{12})'){$batteries[$matches[1].ToUpperInvariant()]=$b.Data}
};
@($ds | Where-Object {$_.Name} | ForEach-Object { $address=([string]$_.Properties['System.Devices.Aep.DeviceAddress'] -replace '[:-]','').ToUpperInvariant(); @{Id=$_.Id;Name=$_.Name;Paired=$_.Pairing.IsPaired;Connected=$_.Properties['System.Devices.Aep.IsConnected'];Battery=$batteries[$address]} }) | ConvertTo-Json -Compress -Depth 4
");}
 public static string Pair(string id,bool unpair){return Native.PS(Prelude+@"
[Windows.Devices.Enumeration.DeviceInformation,Windows.Devices.Enumeration,ContentType=WindowsRuntime] | Out-Null;
$d=Await ([Windows.Devices.Enumeration.DeviceInformation]::CreateFromIdAsync("+Quote(id)+@",$null,[Windows.Devices.Enumeration.DeviceInformationKind]::AssociationEndpoint)) ([Windows.Devices.Enumeration.DeviceInformation]);
"+(unpair?"$r=Await ($d.Pairing.UnpairAsync()) ([Windows.Devices.Enumeration.DeviceUnpairingResult]);":"$r=Await ($d.Pairing.PairAsync()) ([Windows.Devices.Enumeration.DevicePairingResult]);")+"$r.Status.ToString()");}
}
