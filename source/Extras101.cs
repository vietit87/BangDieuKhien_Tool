using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Diagnostics;

internal static class LidPower {
 static Guid subgroup=new Guid("4f971e89-eebd-4455-a8de-9e59040e7347"),setting=new Guid("5ca83367-6e45-459f-a27b-476b1d01c936");
 [DllImport("powrprof.dll")]static extern uint PowerGetActiveScheme(IntPtr root,out IntPtr scheme);
 [DllImport("kernel32.dll")]static extern IntPtr LocalFree(IntPtr p);
 [DllImport("powrprof.dll")]static extern uint PowerReadACValueIndex(IntPtr root,ref Guid scheme,ref Guid sub,ref Guid key,out uint value);
 [DllImport("powrprof.dll")]static extern uint PowerReadDCValueIndex(IntPtr root,ref Guid scheme,ref Guid sub,ref Guid key,out uint value);
 [DllImport("powrprof.dll")]static extern uint PowerWriteACValueIndex(IntPtr root,ref Guid scheme,ref Guid sub,ref Guid key,uint value);
 [DllImport("powrprof.dll")]static extern uint PowerWriteDCValueIndex(IntPtr root,ref Guid scheme,ref Guid sub,ref Guid key,uint value);
 static void Check(uint r){if(r!=0)throw new System.ComponentModel.Win32Exception((int)r);}
 static Guid Scheme(){IntPtr p;Check(PowerGetActiveScheme(IntPtr.Zero,out p));try{return (Guid)Marshal.PtrToStructure(p,typeof(Guid));}finally{LocalFree(p);}}
 public static uint Read(bool ac){Guid g=Scheme();uint v;Check(ac?PowerReadACValueIndex(IntPtr.Zero,ref g,ref subgroup,ref setting,out v):PowerReadDCValueIndex(IntPtr.Zero,ref g,ref subgroup,ref setting,out v));return v;}
 public static void Write(bool ac,uint value){if(value>1)throw new ArgumentOutOfRangeException();Guid g=Scheme();Check(ac?PowerWriteACValueIndex(IntPtr.Zero,ref g,ref subgroup,ref setting,value):PowerWriteDCValueIndex(IntPtr.Zero,ref g,ref subgroup,ref setting,value));Check(Native.PowerSetActiveScheme(IntPtr.Zero,ref g));if(Read(ac)!=value)throw new Exception("Windows chưa áp dụng cấu hình gập nắp.");}
}

// Desktop radio manager. Read back state and surface failures; never fake a toggle.
[ComImport,Guid("581333F6-28DB-41BE-BC7A-FF201F12F3F6")]internal class RadioManagerClass{}
[ComImport,Guid("DB3AFBFB-08E6-46C6-AA70-BF9A34C30AB7"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]internal interface IRadioManager {
 [PreserveSig]int IsSupported(out int value);[PreserveSig]int Instances(out IntPtr p);[PreserveSig]int GetState(out int state,out int ui,out int reason);[PreserveSig]int SetState(int state);}
internal static class Airplane {
 public static bool State(bool? on){object o=new RadioManagerClass();try{var r=(IRadioManager)o;int state,ui,reason;Marshal.ThrowExceptionForHR(r.GetState(out state,out ui,out reason));if(on.HasValue){Marshal.ThrowExceptionForHR(r.SetState(on.Value?0:1));for(int i=0;i<10;i++){Marshal.ThrowExceptionForHR(r.GetState(out state,out ui,out reason));if((state==0)==on.Value)break;System.Threading.Thread.Sleep(100);}if((state==0)!=on.Value)throw new Exception("Windows chưa xác nhận trạng thái máy bay mới.");}return state==0;}finally{Marshal.ReleaseComObject(o);}}
}

[ComImport,Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]internal interface ISessionManager {
 [PreserveSig]int Control(IntPtr id,uint flags,out IntPtr c);[PreserveSig]int Volume(IntPtr id,uint flags,out IntPtr v);[PreserveSig]int Enumerate(out ISessions e);}
[ComImport,Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]internal interface ISessions {
 [PreserveSig]int Count(out int count);[PreserveSig]int Get(int index,out ISession c);}
[ComImport,Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]internal interface ISession {
 [PreserveSig]int State(out int state);[PreserveSig]int Name([MarshalAs(UnmanagedType.LPWStr)]out string name);
 [PreserveSig]int SetName(IntPtr name,ref Guid context);[PreserveSig]int Icon(out IntPtr path);[PreserveSig]int SetIcon(IntPtr path,ref Guid context);
 [PreserveSig]int Group(out Guid group);[PreserveSig]int SetGroup(ref Guid group,ref Guid context);[PreserveSig]int Register(IntPtr events);[PreserveSig]int Unregister(IntPtr events);
 [PreserveSig]int Identifier([MarshalAs(UnmanagedType.LPWStr)]out string id);[PreserveSig]int Instance([MarshalAs(UnmanagedType.LPWStr)]out string id);[PreserveSig]int ProcessId(out uint pid);[PreserveSig]int SystemSounds();[PreserveSig]int Duck([MarshalAs(UnmanagedType.Bool)]bool optOut);}
[ComImport,Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]internal interface ISessionVolume {
 [PreserveSig]int Set(float value,ref Guid context);[PreserveSig]int Get(out float value);[PreserveSig]int Mute([MarshalAs(UnmanagedType.Bool)]bool value,ref Guid context);[PreserveSig]int Muted([MarshalAs(UnmanagedType.Bool)]out bool value);}
internal sealed class MixerSession:IDisposable {
 internal ISession control;internal ISessionVolume volume;public string Name;public float Level{get{float v;Marshal.ThrowExceptionForHR(volume.Get(out v));return v;}set{Guid g=Guid.Empty;Marshal.ThrowExceptionForHR(volume.Set(value,ref g));}}
 public bool Muted{get{bool v;Marshal.ThrowExceptionForHR(volume.Muted(out v));return v;}set{Guid g=Guid.Empty;Marshal.ThrowExceptionForHR(volume.Mute(value,ref g));}}
 public void Dispose(){volume=null;if(control!=null){Marshal.ReleaseComObject(control);control=null;}}
 public static List<MixerSession> List(){var result=new List<MixerSession>();IMMEnum e=(IMMEnum)new MMEnumerator();IMMCollection devices=null;try{Marshal.ThrowExceptionForHR(e.EnumAudioEndpoints(0,1,out devices));uint count;Marshal.ThrowExceptionForHR(devices.GetCount(out count));for(uint j=0;j<count;j++){IMMDevice d=null;object manager=null;ISessions sessions=null;try{Marshal.ThrowExceptionForHR(devices.Item(j,out d));Guid g=typeof(ISessionManager).GUID;Marshal.ThrowExceptionForHR(d.Activate(ref g,23,IntPtr.Zero,out manager));Marshal.ThrowExceptionForHR(((ISessionManager)manager).Enumerate(out sessions));int n;Marshal.ThrowExceptionForHR(sessions.Count(out n));for(int i=0;i<n;i++){ISession c;Marshal.ThrowExceptionForHR(sessions.Get(i,out c));bool keep=false;try{int state;Marshal.ThrowExceptionForHR(c.State(out state));if(state==2)continue;uint pid;Marshal.ThrowExceptionForHR(c.ProcessId(out pid));string name=null;if(c.SystemSounds()==0)name="Âm thanh hệ thống";else{try{using(var p=Process.GetProcessById((int)pid)){name=p.ProcessName;}}catch{c.Name(out name);}}if(string.IsNullOrWhiteSpace(name)||name.StartsWith("@"))name="Ứng dụng "+pid;var item=new MixerSession{control=c,volume=(ISessionVolume)c,Name=name};float level=item.Level;result.Add(item);keep=true;}finally{if(!keep)Marshal.ReleaseComObject(c);}}}finally{if(sessions!=null)Marshal.ReleaseComObject(sessions);if(manager!=null)Marshal.ReleaseComObject(manager);if(d!=null)Marshal.ReleaseComObject(d);}}return result;}catch{foreach(var s in result)s.Dispose();throw;}finally{if(devices!=null)Marshal.ReleaseComObject(devices);Marshal.ReleaseComObject(e);}}
}
