using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

internal static class Native {
 [DllImport("kernel32.dll")] public static extern bool GetSystemTimes(out long idle,out long kernel,out long user);
 [StructLayout(LayoutKind.Sequential)] public class Memory {public uint length=64,load;public ulong total,available,page,availPage,virtualTotal,virtualAvailable,extended;}
 [DllImport("kernel32.dll",CharSet=CharSet.Auto)] public static extern bool GlobalMemoryStatusEx([In,Out]Memory s);
 [DllImport("user32.dll")] public static extern int SetDisplayConfig(uint a,IntPtr b,uint c,IntPtr d,uint flags);
 [DllImport("powrprof.dll")] public static extern uint PowerSetActiveScheme(IntPtr root,ref Guid scheme);
 [DllImport("powrprof.dll")] public static extern uint PowerSetActiveOverlayScheme(ref Guid scheme);
 [DllImport("powrprof.dll")] public static extern uint PowerGetActualOverlayScheme(out Guid scheme);
 public static string Query(string cls,string prop){try{using(var s=new ManagementObjectSearcher("SELECT "+prop+" FROM "+cls))foreach(ManagementObject o in s.Get())return Convert.ToString(o[prop]);}catch{}return "";}
 public static void Brightness(int n){using(var s=new ManagementObjectSearcher("root\\WMI","SELECT * FROM WmiMonitorBrightnessMethods")){bool found=false;foreach(ManagementObject o in s.Get()){o.InvokeMethod("WmiSetBrightness",new object[]{(uint)1,(byte)n});found=true;}if(!found)throw new Exception("Màn hình này không hỗ trợ WMI. Màn hình ngoài cần DDC/CI hoặc nút vật lý.");}}
 public static int GetBrightness(out string hint){
  hint="WMI không thấy đèn nền. Màn rời: dùng nút trên màn. Laptop: kiểm tra driver đồ họa.";
  try{
   using(var query=new ManagementObjectSearcher(@"root\WMI","SELECT CurrentBrightness FROM WmiMonitorBrightness"))
   using(var monitors=query.Get()){
    foreach(ManagementObject monitor in monitors)using(monitor){int level=Convert.ToInt32(monitor["CurrentBrightness"]);if(level>=0 && level<=100)return level;}
   }
  }catch(UnauthorizedAccessException){hint="Windows chặn đọc độ sáng. Kiểm tra quyền WMI hoặc chỉnh trong Cài đặt > Màn hình.";}
  catch(ManagementException ex){if(ex.ErrorCode==ManagementStatus.AccessDenied)hint="WMI từ chối quyền đọc. Kiểm tra quyền WMI hoặc chỉnh trong Cài đặt > Màn hình.";else if(ex.ErrorCode!=ManagementStatus.InvalidClass && ex.ErrorCode!=ManagementStatus.NotSupported)hint="Lỗi WMI ("+ex.ErrorCode+"). Kiểm tra dịch vụ WMI và driver đồ họa.";}
  catch(Exception){hint="Không đọc được độ sáng. Kiểm tra dịch vụ WMI và driver đồ họa.";}
  return -1;
 }
 public static void Open(string target){Process.Start(new ProcessStartInfo(target){UseShellExecute=true});}
 public static string Run(string file,string args){using(var p=new Process()){p.StartInfo=new ProcessStartInfo(file,args){CreateNoWindow=true,UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8};p.Start();var output=p.StandardOutput.ReadToEndAsync();var error=p.StandardError.ReadToEndAsync();if(!p.WaitForExit(20000)){p.Kill();throw new Exception("Thao tác quá thời gian chờ.");}string r=output.Result,e=error.Result;if(p.ExitCode!=0)throw new Exception(string.IsNullOrWhiteSpace(e)?r:e);return r;}}
 public static string PS(string script){return Run("powershell.exe","-NoProfile -NonInteractive -InputFormat None -OutputFormat Text -EncodedCommand "+Convert.ToBase64String(Encoding.Unicode.GetBytes("$ErrorActionPreference='Stop'; $ProgressPreference='SilentlyContinue'; [Console]::OutputEncoding=[Text.Encoding]::UTF8; try { "+script+" } catch { [Console]::Error.WriteLine($_.Exception.Message); exit 1 }")));}
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]public struct Mode{[MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)]public string device;public ushort spec,driver,size,extra;public uint fields;public int x,y;public uint orientation,fixedOutput;public short color,duplex,yRes,tt,collate;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)]public string form;public ushort logPixels;public uint bits,width,height,flags,frequency,icmMethod,icmIntent,media,dither,res1,res2,panningW,panningH;}
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern bool EnumDisplaySettings(string device,int n,ref Mode m);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int ChangeDisplaySettingsEx(string device,ref Mode m,IntPtr hwnd,uint flags,IntPtr param);
 public static Mode CurrentMode(){Mode m=new Mode();m.size=(ushort)Marshal.SizeOf(m);if(!EnumDisplaySettings(null,-1,ref m))throw new Exception("Không đọc được màn hình chính.");return m;}
 public static List<Mode> Modes(){var r=new List<Mode>();for(int i=0;i<1000;i++){Mode m=new Mode();m.size=(ushort)Marshal.SizeOf(m);if(!EnumDisplaySettings(null,i,ref m))break;if(m.bits>=24&&!r.Any(v=>v.width==m.width&&v.height==m.height&&v.frequency==m.frequency))r.Add(m);}return r.OrderByDescending(v=>v.width).ThenByDescending(v=>v.height).ThenByDescending(v=>v.frequency).ToList();}
 public static void Resolution(Mode m){m.fields=0x40000|0x80000|0x100000|0x400000;int r=ChangeDisplaySettingsEx(null,ref m,IntPtr.Zero,0,IntPtr.Zero);if(r!=0)throw new Exception("Windows từ chối chế độ màn hình: "+r);}
}

[ComImport,Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]internal class MMEnumerator{}
[ComImport,Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]internal interface IMMEnum{[PreserveSig] int EnumAudioEndpoints(int flow,uint state,out IMMCollection collection);[PreserveSig] int GetDefaultAudioEndpoint(int flow,int role,out IMMDevice device);[PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)]string id,out IMMDevice device);[PreserveSig] int Register(IntPtr p);[PreserveSig] int Unregister(IntPtr p);}
[ComImport,Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]internal interface IMMCollection{[PreserveSig] int GetCount(out uint n);[PreserveSig] int Item(uint i,out IMMDevice d);}
[ComImport,Guid("D666063F-1587-4E43-81F1-B948E807363F"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]internal interface IMMDevice{[PreserveSig] int Activate(ref Guid id,uint ctx,IntPtr p,[MarshalAs(UnmanagedType.IUnknown)]out object o);[PreserveSig] int OpenPropertyStore(uint access,out IProps p);[PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)]out string id);[PreserveSig] int GetState(out uint state);}
[StructLayout(LayoutKind.Sequential)]internal struct PropKey{public Guid fmt;public uint pid;public PropKey(Guid g,uint n){fmt=g;pid=n;}}
[StructLayout(LayoutKind.Explicit,Size=24)]internal struct PropValue{[FieldOffset(0)]public ushort type;[FieldOffset(8)]public IntPtr ptr;}
[ComImport,Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]internal interface IProps{[PreserveSig] int Count(out uint n);[PreserveSig] int At(uint n,out PropKey key);[PreserveSig] int GetValue(ref PropKey key,out PropValue value);[PreserveSig] int SetValue(ref PropKey k,ref PropValue v);[PreserveSig] int Commit();}
[ComImport,Guid("5CDF2C82-841E-4546-9722-0CF74078229A"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]internal interface IVolume{[PreserveSig] int Register(IntPtr p);[PreserveSig] int Unregister(IntPtr p);[PreserveSig] int Channels(out uint n);[PreserveSig] int SetDb(float v,ref Guid c);[PreserveSig] int SetScalar(float v,ref Guid c);[PreserveSig] int GetDb(out float v);[PreserveSig] int GetScalar(out float v);[PreserveSig] int SetChannelDb(uint n,float v,ref Guid c);[PreserveSig] int SetChannelScalar(uint n,float v,ref Guid c);[PreserveSig] int GetChannelDb(uint n,out float v);[PreserveSig] int GetChannelScalar(uint n,out float v);[PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)]bool mute,ref Guid c);[PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)]out bool mute);}
// Windows desktop policy interface, available on Windows 10/11; errors are surfaced.
[ComImport,Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9")]internal class PolicyConfig{}
[ComImport,Guid("F8679F50-850A-41CF-9C72-430F290290C8"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]internal interface IPolicy{[PreserveSig] int GetMix([MarshalAs(UnmanagedType.LPWStr)]string s,IntPtr p);[PreserveSig] int GetFormat(string s,int b,IntPtr p);[PreserveSig] int Reset(string s);[PreserveSig] int SetFormat(string s,IntPtr p,IntPtr q);[PreserveSig] int GetPeriod(string s,int b,IntPtr p,IntPtr q);[PreserveSig] int SetPeriod(string s,IntPtr p);[PreserveSig] int GetShare(string s,IntPtr p);[PreserveSig] int SetShare(string s,IntPtr p);[PreserveSig] int GetProp(string s,IntPtr p,IntPtr q);[PreserveSig] int SetProp(string s,IntPtr p,IntPtr q);[PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)]string id,int role);[PreserveSig] int Visibility(string id,int v);}
internal class AudioDevice{public string Id,Name;public override string ToString(){return Name;}}
internal static class Audio{
 [DllImport("ole32.dll")]static extern int PropVariantClear(ref PropValue p);
 static IMMEnum E(){return (IMMEnum)new MMEnumerator();}
 static IVolume V(IMMDevice d){object o;Guid g=typeof(IVolume).GUID;Marshal.ThrowExceptionForHR(d.Activate(ref g,23,IntPtr.Zero,out o));return (IVolume)o;}
 static IMMDevice Default(int flow){var e=E();try{IMMDevice d;Marshal.ThrowExceptionForHR(e.GetDefaultAudioEndpoint(flow,0,out d));return d;}finally{Marshal.ReleaseComObject(e);}}
 public static List<AudioDevice> Devices(int flow){var r=new List<AudioDevice>();var e=E();IMMCollection c=null;try{Marshal.ThrowExceptionForHR(e.EnumAudioEndpoints(flow,1,out c));uint n;c.GetCount(out n);for(uint i=0;i<n;i++){IMMDevice d;c.Item(i,out d);IProps p=null;try{string id;d.GetId(out id);d.OpenPropertyStore(0,out p);var k=new PropKey(new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"),14);PropValue v;Marshal.ThrowExceptionForHR(p.GetValue(ref k,out v));string name=v.type==31?Marshal.PtrToStringUni(v.ptr):id;PropVariantClear(ref v);r.Add(new AudioDevice{Id=id,Name=name});}finally{if(p!=null)Marshal.ReleaseComObject(p);Marshal.ReleaseComObject(d);}}return r;}finally{if(c!=null)Marshal.ReleaseComObject(c);Marshal.ReleaseComObject(e);}}
 public static string DefaultId(){var d=Default(0);try{string id;Marshal.ThrowExceptionForHR(d.GetId(out id));return id;}finally{Marshal.ReleaseComObject(d);}}
 public static float Volume(){IMMDevice d=Default(0);IVolume v=null;try{v=V(d);float f;Marshal.ThrowExceptionForHR(v.GetScalar(out f));return f;}finally{if(v!=null)Marshal.ReleaseComObject(v);Marshal.ReleaseComObject(d);}}
 public static void SetVolume(float f){IMMDevice d=Default(0);IVolume v=null;try{v=V(d);Guid g=Guid.Empty;Marshal.ThrowExceptionForHR(v.SetScalar(f,ref g));}finally{if(v!=null)Marshal.ReleaseComObject(v);Marshal.ReleaseComObject(d);}}
 public static bool Muted(int flow){IMMDevice d=Default(flow);IVolume v=null;try{v=V(d);bool b;Marshal.ThrowExceptionForHR(v.GetMute(out b));return b;}finally{if(v!=null)Marshal.ReleaseComObject(v);Marshal.ReleaseComObject(d);}}
 public static void Mute(int flow,bool b,bool all){var e=E();try{foreach(var a in all?Devices(flow):new List<AudioDevice>{new AudioDevice{Id=null}}){IMMDevice d;if(a.Id==null)Marshal.ThrowExceptionForHR(e.GetDefaultAudioEndpoint(flow,0,out d));else Marshal.ThrowExceptionForHR(e.GetDevice(a.Id,out d));IVolume v=null;try{v=V(d);Guid g=Guid.Empty;Marshal.ThrowExceptionForHR(v.SetMute(b,ref g));}finally{if(v!=null)Marshal.ReleaseComObject(v);Marshal.ReleaseComObject(d);}}}finally{Marshal.ReleaseComObject(e);}}
 public static void Select(string id){object p=new PolicyConfig();try{var v=(IPolicy)p;for(int i=0;i<3;i++)Marshal.ThrowExceptionForHR(v.SetDefaultEndpoint(id,i));}finally{Marshal.ReleaseComObject(p);}}
 public static bool EndpointMute(string id,bool? state){var e=E();IMMDevice d=null;IVolume v=null;try{Marshal.ThrowExceptionForHR(e.GetDevice(id,out d));v=V(d);if(state.HasValue){Guid g=Guid.Empty;Marshal.ThrowExceptionForHR(v.SetMute(state.Value,ref g));}bool result;Marshal.ThrowExceptionForHR(v.GetMute(out result));return result;}finally{if(v!=null)Marshal.ReleaseComObject(v);if(d!=null)Marshal.ReleaseComObject(d);Marshal.ReleaseComObject(e);}}
}

