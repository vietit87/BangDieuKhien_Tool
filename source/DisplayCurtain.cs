using System;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;

internal sealed class DisplayCurtain : Form {
 [DllImport("user32.dll")] static extern IntPtr DefWindowProc(IntPtr window,int message,IntPtr command,IntPtr value);
 readonly Timer delay=new Timer{Interval=180};
 void ScheduleOff(){delay.Stop();delay.Start();}
 DisplayCurtain(){
  FormBorderStyle=FormBorderStyle.None;StartPosition=FormStartPosition.Manual;
  Bounds=SystemInformation.VirtualScreen;TopMost=true;ShowInTaskbar=false;BackColor=Color.Black;
  delay.Tick+=(s,e)=>{delay.Stop();DefWindowProc(Handle,0x112,(IntPtr)0xF170,(IntPtr)2);};
  MouseDown+=(s,e)=>ScheduleOff();MouseMove+=(s,e)=>ScheduleOff();
  Shown+=(s,e)=>{Activate();ScheduleOff();};
 }
 protected override void WndProc(ref Message m){
  if(m.Msg==0x100||m.Msg==0x104){delay.Stop();Close();return;}
  base.WndProc(ref m);
 }
 protected override void Dispose(bool disposing){if(disposing)delay.Dispose();base.Dispose(disposing);}
 internal static void ShowFor(Form owner){using(var screen=new DisplayCurtain())screen.ShowDialog(owner);}
}
