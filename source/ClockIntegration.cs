using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Automation;
using System.Windows.Forms;
using Microsoft.Win32;

internal sealed class ClockIntegration : IDisposable {
 static string Link {get{return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup),"Bảng điều khiển.lnk");}}
 static string LegacyLink {get{return System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Link),"BangDieuKhien - Dong ho.lnk");}}
 const string runKey=@"Software\Microsoft\Windows\CurrentVersion\Run";
 const string appName="Bảng điều khiển";
 internal static bool Registered{get{using(var key=Registry.CurrentUser.OpenSubKey(runKey))return (key!=null&&key.GetValue(appName)!=null)||System.IO.File.Exists(Link)||System.IO.File.Exists(LegacyLink);}}
 internal static void SetRunEntry(RegistryKey key,string path,bool on){
  if(on)key.SetValue(appName,"\""+path+"\" --clock-tray",RegistryValueKind.String);
  else key.DeleteValue(appName,false);
 }
 internal static void Register(bool on){
  using(var key=Registry.CurrentUser.CreateSubKey(runKey))SetRunEntry(key,Application.ExecutablePath,on);
  System.IO.File.Delete(Link);System.IO.File.Delete(LegacyLink);
 } delegate IntPtr MouseProc(int code,IntPtr msg,IntPtr data);
 [DllImport("user32",SetLastError=true)]static extern IntPtr SetWindowsHookEx(int id,MouseProc callback,IntPtr module,uint thread);
 [DllImport("kernel32",CharSet=CharSet.Unicode)]static extern IntPtr GetModuleHandle(string name);
 [DllImport("user32")]static extern bool UnhookWindowsHookEx(IntPtr h);
 [DllImport("user32")]static extern IntPtr CallNextHookEx(IntPtr h,int code,IntPtr msg,IntPtr data);
 [DllImport("user32")]static extern IntPtr WindowFromPoint(Point point);
 [DllImport("user32")]static extern IntPtr GetAncestor(IntPtr window,uint flags);
 [DllImport("user32",CharSet=CharSet.Unicode)]static extern int GetClassName(IntPtr window,StringBuilder name,int size);
 static string Class(IntPtr h){var text=new StringBuilder(128);GetClassName(h,text,text.Capacity);return text.ToString();}
 readonly Form owner;readonly ContextMenuStrip menu=new ContextMenuStrip(); readonly MouseProc proc;

 bool disposed; IntPtr hook;bool pressed;
 internal ClockIntegration(Form owner,Action show,Action restartExplorer){
  this.owner=owner;proc=Mouse;
  menu.Items.Add("Tắt màn hình",null,(s,e)=>DisplayCurtain.ShowFor(owner));
  menu.Items.Add("Restart Explorer",null,(s,e)=>restartExplorer());
  menu.Items.Add("Task Manager",null,(s,e)=>Native.Open("taskmgr.exe"));
  menu.Items.Add("Taskbar settings",null,(s,e)=>Native.Open("ms-settings:taskbar"));
  menu.Items.Add(new ToolStripSeparator());
  menu.Items.Add("Bảng điều khiển",owner.Icon.ToBitmap(),(s,e)=>show());  hook=SetWindowsHookEx(14,proc,GetModuleHandle(null),0);
  if(hook==IntPtr.Zero)throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
 }

 static bool OnTaskbar(Point p){
  IntPtr h=WindowFromPoint(p);
  string root=Class(GetAncestor(h,2));
  return root=="Shell_TrayWnd"||root=="Shell_SecondaryTrayWnd";
 }
 // Resolve the accessible element outside the low-level mouse callback.
 internal static bool Background(AutomationElement hit){
  var walker=TreeWalker.RawViewWalker;
  for(var e=hit;e!=null;e=walker.GetParent(e)){
   var c=e.Current;string name=c.ClassName??"";
   if(name=="Shell_TrayWnd"||name=="Shell_SecondaryTrayWnd")return true;
   if(c.ControlType==ControlType.Button||c.ControlType==ControlType.MenuItem||c.ControlType==ControlType.ListItem||c.ControlType==ControlType.CheckBox||name.StartsWith("SystemTray.")||name.IndexOf("TaskListButton",StringComparison.OrdinalIgnoreCase)>=0)return false;
  }
  return false;
 }
 [DllImport("user32")]static extern void mouse_event(uint flags,uint dx,uint dy,uint data,UIntPtr extra);
 static readonly UIntPtr replay=new UIntPtr(0x42444B16);
 void HandleRelease(Point p){
  if(disposed||!OnTaskbar(p)||Cursor.Position!=p)return;
  bool background=false;
  try{background=Background(AutomationElement.FromPoint(new System.Windows.Point(p.X,p.Y)));}catch(ElementNotAvailableException){}catch(COMException){}catch(InvalidOperationException){}
  if(disposed||Cursor.Position!=p||!OnTaskbar(p))return;
  if(background){menu.Show(p);return;}
  mouse_event(8,0,0,0,replay);mouse_event(16,0,0,0,replay);
 } IntPtr Mouse(int code,IntPtr msg,IntPtr data){
  if(!disposed && code>=0 && Marshal.ReadIntPtr(data,24).ToInt64()!=(long)replay.ToUInt64() && (msg.ToInt32()==0x204||msg.ToInt32()==0x205)){
   Point p=new Point(Marshal.ReadInt32(data),Marshal.ReadInt32(data,4));
   if(msg.ToInt32()==0x204){pressed=OnTaskbar(p);if(pressed)return (IntPtr)1;}
   else if(pressed){pressed=false;owner.BeginInvoke((Action)(()=>HandleRelease(p)));return (IntPtr)1;}
  }  return CallNextHookEx(hook,code,msg,data);
 }
 public void Dispose(){disposed=true;UnhookWindowsHookEx(hook);menu.Dispose();}
}
