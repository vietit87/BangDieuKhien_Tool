using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

[assembly:System.Reflection.AssemblyTitle("BẢNG ĐIỀU KHIỂN")]
[assembly:System.Reflection.AssemblyProduct("BẢNG ĐIỀU KHIỂN")]
[assembly:System.Reflection.AssemblyCompany("Lê Quốc Việt")]
[assembly:System.Reflection.AssemblyVersion("1.0.16.0")]
internal static class Program {
 [STAThread]static void Main(string[] args){if(!args.Contains("--self-test") && !new System.Security.Principal.WindowsPrincipal(System.Security.Principal.WindowsIdentity.GetCurrent()).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator)){
  if(args.Contains("--clock-tray") && !ClockIntegration.Registered)return;
  try{Process.Start(new ProcessStartInfo(Application.ExecutablePath,args.Contains("--clock-tray")?"--clock-tray":args.Contains("--clean-ram-worker")?"--clean-ram-worker":""){UseShellExecute=true,Verb="runas"});}catch(System.ComponentModel.Win32Exception ex){if(ex.NativeErrorCode!=1223)MessageBox.Show(ex.Message);}
  return;
 }if(args.Length==1 && args[0]=="--clean-ram-worker"){RamCleaner.Worker();return;}Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);if(args.Contains("--self-test")){Checks.Run();return;}bool first;using(var gate=new Mutex(true,"Local\\LeQuocViet.BangDieuKhienNhanh",out first)){if(!first)return;Application.Run(new Dashboard());}}
}
internal class Tile:Button {
 public bool Active; public Color Accent=Color.FromArgb(104,112,249); public string Glyph="";public Tile(){FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;Cursor=Cursors.Hand;Font=new Font("Segoe UI",9);Size=new Size(162,52);Margin=new Padding(4);SetStyle(ControlStyles.OptimizedDoubleBuffer|ControlStyles.UserPaint,true);}
 protected override void OnPaint(PaintEventArgs e){e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;Rectangle r=new Rectangle(0,0,Width-1,Height-1);using(var path=Round(r,12))using(var b=new SolidBrush(Active?Accent:BackColor))e.Graphics.FillPath(b,path);Color fg=Enabled?(Active?Color.White:ForeColor):SystemColors.GrayText;using(var f=new Font("Segoe MDL2 Assets",17))TextRenderer.DrawText(e.Graphics,Glyph,f,new Rectangle(Text.Length==0?0:12,Text.Length==0?0:8,Text.Length==0?Width:32,Text.Length==0?Height:Height-16),fg,TextFormatFlags.VerticalCenter|TextFormatFlags.HorizontalCenter);TextRenderer.DrawText(e.Graphics,Text,Font,new Rectangle(48,8,Width-54,Height-16),fg,TextFormatFlags.VerticalCenter|TextFormatFlags.WordBreak);if(Focused)ControlPaint.DrawFocusRectangle(e.Graphics,new Rectangle(4,4,Width-8,Height-8),fg,BackColor);}
 internal static GraphicsPath Round(Rectangle r,int n){var p=new GraphicsPath();p.AddArc(r.Left,r.Top,n,n,180,90);p.AddArc(r.Right-n,r.Top,n,n,270,90);p.AddArc(r.Right-n,r.Bottom-n,n,n,0,90);p.AddArc(r.Left,r.Bottom-n,n,n,90,90);p.CloseFigure();return p;}
}
internal class BatteryButton:Button {
 protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(BackColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;var body=new Rectangle(5,8,Width-15,Height-17);using(var pen=new Pen(ForeColor,1.5f))e.Graphics.DrawRectangle(pen,body);using(var brush=new SolidBrush(ForeColor))e.Graphics.FillRectangle(brush,Width-9,Height/2-4,4,8);TextRenderer.DrawText(e.Graphics,Text.Replace("▰","").Trim(),Font,body,ForeColor,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);if(Focused)ControlPaint.DrawFocusRectangle(e.Graphics,ClientRectangle);}
}
internal partial class Dashboard:Form {
 Color bg,card,fg,muted;bool dark=true,busy=false,micMute=false,closing=false;string opened="";
 Label cpu,ram,heading,status,hardware,version,volumeValue; Panel footer; List<MixerSession> mixerSessions=new List<MixerSession>(); int viewGeneration;int drawerRequestedHeight; float uiScale=1; HashSet<Control> scaledControls=new HashSet<Control>();Button net,battery,clock;FlowLayoutPanel drawer,tiles;Panel header;TrackBar brightness,volume;Tile muteTile,micTile,cameraTile;ToolTip tip=new ToolTip();NotifyIcon tray;System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer(),revertTimer;Native.Mode oldMode;Native.Mode? startingMode;int revertSeconds;
 long oldIdle,oldKernel,oldUser;Dictionary<string,long[]> netBytes=new Dictionary<string,long[]>();DateTime lastNetwork=DateTime.UtcNow;DateTime month=DateTime.Today;Label dayDetail;List<WifiItem> wifi=new List<WifiItem>();ListBox wifiList;TextBox wifiPassword;int ticks;string hardwareText="Đang đọc cấu hình máy…";
 public Dashboard(){Text="BẢNG ĐIỀU KHIỂN";Font=new Font("Segoe UI",9);ClientSize=new Size(720,466);FormBorderStyle=FormBorderStyle.FixedSingle;MaximizeBox=false;StartPosition=FormStartPosition.CenterScreen;AutoScaleMode=AutoScaleMode.None;using(var graphics=CreateGraphics())uiScale=graphics.DpiX/96f;DoubleBuffered=true;Icon=LoadIcon();try{startingMode=Native.CurrentMode();}catch{}Build();Scale(new SizeF(uiScale,uiScale));MarkScaled(this);Relayout();Theme();timer.Interval=1000;timer.Tick+=(s,e)=>Tick();timer.Start();VisibleChanged+=(s,e)=>SyncTraffic();Resize+=(s,e)=>SyncTraffic();Shown+=async(s,e)=>{if(ClockIntegration.Registered){Guard(()=>SetClockIntegration(true));if(integration!=null && Environment.GetCommandLineArgs().Contains("--clock-tray"))Hide();}Tick();RefreshAudio();try{ShowCameraState(CameraSwitch.IsOff());}catch(Exception ex){tip.SetToolTip(cameraTile,ex.Message);}await LoadHardware();};FormClosing+=(s,e)=>{if(revertTimer!=null&&revertTimer.Enabled){Native.Resolution(oldMode);revertTimer.Stop();}if(e.CloseReason==CloseReason.UserClosing && integration!=null && !exitTray){e.Cancel=true;Hide();return;}closing=true;if(integration!=null)integration.Dispose();RemoveTraffic();StopBluetoothScan();ReleaseMixer();timer.Stop();tray.Dispose();};}
 Icon LoadIcon(){try{using(var s=typeof(Program).Assembly.GetManifestResourceStream("app.ico"))return new Icon(s);}catch{return SystemIcons.Application;}}
 Label Label(string text,int size=9){return new Label{Text=text,AutoSize=false,Font=new Font("Segoe UI",size),Height=25,TextAlign=ContentAlignment.MiddleLeft};}
 Button Button(string text,Action action,int width=140){var b=new Button{Text=text,Width=width,Height=35,FlatStyle=FlatStyle.Flat,Margin=new Padding(4),Cursor=Cursors.Hand};b.FlatAppearance.BorderSize=0;b.Click+=(s,e)=>Guard(action);return b;}
 Tile AddTile(string text,string icon,Action action){var b=new Tile{Text=text,Glyph=icon};b.Click+=(s,e)=>Guard(action);tiles.Controls.Add(b);return b;}
 readonly ContextMenuStrip cpuMenu=new ContextMenuStrip { ShowImageMargin=false };
 int cpuRequest;
 async void ShowCpuPopup(Control anchor)
 {
  if(closing || cpuMenu.Visible)return;
  int request=++cpuRequest;
  cpuMenu.BackColor=card;
  cpuMenu.ForeColor=fg;
  cpuMenu.Items[2].Text="Đang đo trong 1 giây…";
  for(int i=3;i<cpuMenu.Items.Count;i++)cpuMenu.Items[i].Available=false;
  cpuMenu.Show(anchor,new Point(0,anchor.Height));
  string[] rows;
  try { rows=await CpuRanking.Measure(); }
  catch(Exception ex){rows=new[]{"Không đọc được CPU: "+ex.Message};}
  if(closing || cpuMenu.IsDisposed || request!=cpuRequest || !cpuMenu.Visible)return;
  if(rows.Length==0)rows=new[]{"Chưa đọc được số liệu CPU."};
  for(int i=0;i<rows.Length;i++){
   cpuMenu.Items[i+2].Text=rows[i];
   cpuMenu.Items[i+2].Available=true;
  }
 }
 void Build(){
  cpuMenu.Items.Add("Top 5 ứng dụng dùng CPU");
  cpuMenu.Items.Add(new ToolStripSeparator());
  for(int i=0;i<5;i++)cpuMenu.Items.Add(" ");
  cpuMenu.Closed+=(s,e)=>cpuRequest++;
  Disposed+=(s,e)=>cpuMenu.Dispose();
  header=new Panel{Location=new Point(18,10),Size=new Size(684,76)};Controls.Add(header);var cpuIcon=new HardwareIcon(false);cpuIcon.SetBounds(0,4,20,24);header.Controls.Add(cpuIcon);
  cpu=Label("CPU —%",10);cpu.SetBounds(22,0,78,32);header.Controls.Add(cpu);
  var ramIcon=new HardwareIcon(true);ramIcon.SetBounds(101,4,22,24);header.Controls.Add(ramIcon);
  ram=Label("RAM —%",10);ram.SetBounds(126,0,92,32);header.Controls.Add(ram);
  cpuIcon.Cursor=cpu.Cursor=Cursors.Hand;
  cpuIcon.Click+=(s,e)=>ShowCpuPopup(cpuIcon);
  cpu.Click+=(s,e)=>ShowCpuPopup(cpuIcon);
  tip.SetToolTip(cpuIcon,"Nhấn để xem 5 ứng dụng dùng CPU nhiều nhất.");tip.SetToolTip(cpu,"Nhấn để xem 5 ứng dụng dùng CPU nhiều nhất.");tip.SetToolTip(ramIcon,"Nhấn đúp chuột trái để dọn RAM cơ bản.");tip.SetToolTip(ram,"Nhấn đúp chuột trái để dọn RAM cơ bản.");
  MouseEventHandler clean=async(sender,args)=>{if(args.Button==MouseButtons.Left && !busy)await Work(RamCleaner.RunIsolated);};
  ramIcon.MouseDoubleClick+=clean;ram.MouseDoubleClick+=clean;

  var theme=Button("◐",()=>{dark=!dark;Theme();},38);theme.SetBounds(0,40,38,32);tip.SetToolTip(theme,"Đổi giao diện sáng / tối");header.Controls.Add(theme);heading=Label("BẢNG ĐIỀU KHIỂN",12);heading.Font=new Font("Segoe UI Semibold",12);heading.SetBounds(142,39,400,36);heading.TextAlign=ContentAlignment.MiddleCenter;header.Controls.Add(heading);
  net=Button("Wi-Fi / LAN\n↓ —    ↑ —",()=>ShowWifi(true),170);net.SetBounds(226,0,158,38);net.Font=new Font("Segoe UI",8);header.Controls.Add(net);battery=new BatteryButton{Text="—%",FlatStyle=FlatStyle.Flat,Cursor=Cursors.Hand};battery.Click+=(s,e)=>Guard(()=>Power());battery.SetBounds(392,0,66,38);header.Controls.Add(battery);clock=Button("",()=>Calendar(),202);clock.SetBounds(466,0,218,38);clock.Font=new Font("Segoe UI",8);header.Controls.Add(clock);
  Button pin=null; Action refreshPin=()=>{pin.Text=TopMost?"\uE77A":"\uE718";pin.AccessibleName=TopMost?"Bỏ ghim":"Ghim lên trên";tip.SetToolTip(pin,pin.AccessibleName);};pin=IconButton("","Ghim lên trên",()=>{TopMost=!TopMost;refreshPin();});pin.SetBounds(44,40,38,32);refreshPin();header.Controls.Add(pin);
  var brightLabel=Label("\uE706",16);brightLabel.Font=new Font("Segoe MDL2 Assets",16);brightLabel.SetBounds(18,98,28,30);tip.SetToolTip(brightLabel,"Độ sáng màn hình");Controls.Add(brightLabel);
  brightness=new TrackBar{Minimum=0,Maximum=100,TickStyle=TickStyle.None,AutoSize=false};brightness.SetBounds(52,98,218,30);Controls.Add(brightness);var brightValue=Label("—",9);brightValue.SetBounds(274,98,48,30);Controls.Add(brightValue);string brightnessHint;int n=Native.GetBrightness(out brightnessHint);brightness.Enabled=n>=0;brightness.Value=Math.Max(0,n);brightValue.Text=n<0?"N/A":n+"%";brightness.Scroll+=(s,e)=>brightValue.Text=brightness.Value+"%";brightness.MouseUp+=(s,e)=>{if(brightness.Enabled)Guard(()=>Native.Brightness(brightness.Value));};brightness.KeyUp+=(s,e)=>{if(brightness.Enabled)Guard(()=>Native.Brightness(brightness.Value));};tip.SetToolTip(brightness,"Độ sáng đèn nền màn hình tích hợp");
  muteTile=new Tile{Text="",Glyph="\uE767"};muteTile.SetBounds(342,98,28,30);muteTile.Click+=(s,e)=>Guard(()=>{Audio.Mute(0,!Audio.Muted(0),false);RefreshAudio();});Controls.Add(muteTile);tip.SetToolTip(muteTile,"Tắt / bật âm thanh");volume=new TrackBar{Minimum=0,Maximum=100,TickStyle=TickStyle.None,AutoSize=false};volume.SetBounds(376,98,218,30);volume.Scroll+=(s,e)=>Guard(()=>{Audio.SetVolume(volume.Value/100f);volumeValue.Text=volume.Value+"%";});Controls.Add(volume);volumeValue=Label("—%",9);volumeValue.SetBounds(598,98,48,30);Controls.Add(volumeValue);var soundOut=IconButton("\uE9E9","Đầu ra âm thanh / Volume Mixer",()=>Outputs());soundOut.SetBounds(646,98,34,30);Controls.Add(soundOut);
  tiles=new FlowLayoutPanel{Location=new Point(18,164),Size=new Size(684,180),WrapContents=true};Controls.Add(tiles);
  AddTile("Wi-Fi", "\uE701",()=>ShowWifi());AddTile("Bluetooth", "\uE702",()=>Bluetooth());AddTile("Hotspot", "\uEC15",()=>Hotspot());AddTile("Trình chiếu", "\uE7F4",()=>Display());
  AddTile("Tỷ lệ hiển thị", "\uE740",()=>Scale());AddTile("Độ phân giải", "\uE7F8",()=>Resolutions());cameraTile=AddTile("Camera", "\uE714",()=>Camera());micTile=AddTile("Micro", "\uE720",()=>{micMute=!Audio.Muted(1);Audio.Mute(1,micMute,true);RefreshAudio();});
  AddTile("Ánh sáng đêm", "\uE708",()=>NightLight());AddTile("Máy bay / Radio", "\uE709",()=>Radios());AddTile("Tiện ích Windows", "\uE713",()=>Extras());AddTile("Cấu hình/Sửa", "\uE90F",()=>RepairPanel());
  drawer=new FlowLayoutPanel{Location=new Point(12,466),Size=new Size(696,0),FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Visible=false,Padding=new Padding(4)};Controls.Add(drawer);
  status=Label("Sẵn sàng • Bấm một mục để mở rộng ngay tại đây.",9);status.SetBounds(18,350,684,28);Controls.Add(status);
  footer=new Panel{Location=new Point(18,382),Size=new Size(684,72)};Controls.Add(footer);
  var minimize=IconButton("\uE921","Thu gọn vào khay",()=>Hide());minimize.SetBounds(0,8,34,34);footer.Controls.Add(minimize);
  var shut=IconButton("\uE7E8","Shut down — Tắt máy",()=>ConfirmPower("Tắt máy",()=>Native.Run("shutdown.exe","/s /t 0")));shut.SetBounds(40,8,34,34);footer.Controls.Add(shut);
  var restart=IconButton("\uE777","Restart — Khởi động lại",()=>ConfirmPower("Khởi động lại",()=>Native.Run("shutdown.exe","/r /t 0")));restart.SetBounds(80,8,34,34);footer.Controls.Add(restart);
  var sleep=IconButton("\uE708","Sleep — Ngủ",()=>ConfirmPower("Ngủ",()=>Application.SetSuspendState(PowerState.Suspend,false,false)));sleep.SetBounds(120,8,34,34);footer.Controls.Add(sleep);
  var manager=IconButton("\uE9D9","Mở Task Manager",()=>Native.Open("taskmgr.exe"));manager.SetBounds(160,8,34,34);footer.Controls.Add(manager);var screenOff=IconButton("\uE7F4","Tắt màn hình • nhấn phím để mở",()=>DisplayCurtain.ShowFor(this));screenOff.SetBounds(200,8,34,34);footer.Controls.Add(screenOff);clockOption=IconButton("\uE823","Menu chuột phải taskbar và khởi động cùng Windows",()=>SetClockIntegration(integration==null));clockOption.SetBounds(240,8,34,34);footer.Controls.Add(clockOption);hardware=Label(hardwareText,8);hardware.SetBounds(280,0,368,44);hardware.TextAlign=ContentAlignment.MiddleRight;footer.Controls.Add(hardware);var export=IconButton("\uE74E","Xuất cấu hình máy ra Desktop",async()=>await Work(HardwareExport.Save));export.SetBounds(654,8,30,30);footer.Controls.Add(export);version=Label("Phiên bản 1.0.16  •  Phát triển bởi: Lê Quốc Việt",8);version.SetBounds(198,44,486,24);version.TextAlign=ContentAlignment.MiddleRight;footer.Controls.Add(version);
  if(n<0){var hint=Label(brightnessHint,8);hint.AutoEllipsis=true;hint.SetBounds(0,40,392,32);version.SetBounds(396,44,288,24);footer.Controls.Add(hint);tip.SetToolTip(hint,brightnessHint);}
  tray=new NotifyIcon{Icon=Icon,Visible=true,Text="BẢNG ĐIỀU KHIỂN"};tray.MouseClick+=(s,e)=>{if(e.Button==MouseButtons.Left){Show();WindowState=FormWindowState.Normal;Activate();}};var menu=new ContextMenuStrip();menu.Items.Add("Mở bảng điều khiển",null,(s,e)=>{Show();Activate();});menu.Items.Add("Thoát",null,(s,e)=>{exitTray=true;Close();});tray.ContextMenuStrip=menu;
 }
 Button IconButton(string glyph,string text,Action action){var b=Button(glyph,action,34);b.Font=new Font("Segoe MDL2 Assets",14);tip.SetToolTip(b,text);b.AccessibleName=text;return b;}
 void ReleaseMixer(){foreach(var m in mixerSessions)m.Dispose();mixerSessions.Clear();}
 void Theme(){bg=dark?Color.FromArgb(21,24,35):Color.FromArgb(242,244,249);card=dark?Color.FromArgb(35,39,55):Color.White;fg=dark?Color.FromArgb(232,235,248):Color.FromArgb(30,37,55);muted=dark?Color.FromArgb(160,170,193):Color.FromArgb(85,97,120);PaintTheme(this);heading.ForeColor=dark?Color.Salmon:Color.Firebrick;version.ForeColor=dark?Color.FromArgb(255,123,132):Color.FromArgb(193,35,54);Invalidate(true);}
 int D(int value){return (int)Math.Round(value*uiScale);}
 void MarkScaled(Control c){scaledControls.Add(c);foreach(Control child in c.Controls)MarkScaled(child);c.Disposed+=(s,e)=>scaledControls.Remove(c);}
 void PaintTheme(Control c){if(!scaledControls.Contains(c)&&c.Parent!=null&&scaledControls.Contains(c.Parent)){c.Scale(new SizeF(uiScale,uiScale));MarkScaled(c);}c.BackColor=c is Button||c is ListBox||c is TextBox||c is ComboBox?card:bg;c.ForeColor=fg;foreach(Control x in c.Controls)PaintTheme(x);}
 void Guard(Action action){try{action();}catch(Exception ex){Status(ex.Message,true);}}
 async Task Work(Func<string> action,Action<string> done=null){if(busy){Status("Đang thực hiện thao tác trước…");return;}busy=true;int generation=viewGeneration;Status("Đang thực hiện…");try{string r=await Task.Run(action);if(!closing && generation==viewGeneration){Status(string.IsNullOrWhiteSpace(r)?"Hoàn tất.":r.Trim());if(done!=null)done(r);}}catch(Exception ex){if(!closing && generation==viewGeneration)Status(ex.GetBaseException().Message,true);}finally{busy=false;}}
 void Status(string s,bool error=false){status.Text=s.Replace("\r\n"," ");tip.SetToolTip(status,s);status.ForeColor=error?Color.FromArgb(239,111,117):muted;}
 bool Expand(string name,int height=235){if(opened==name){Collapse();return false;}if(revertTimer!=null&&revertTimer.Enabled){Status("Hãy giữ hoặc hoàn tác độ phân giải trước.",true);return false;}RemoveTraffic();StopBluetoothScan();viewGeneration++;Status("");ReleaseMixer();opened=name;while(drawer.Controls.Count>0){var old=drawer.Controls[0];drawer.Controls.Remove(old);old.Dispose();}drawer.Visible=true;drawerRequestedHeight=height;var row=new FlowLayoutPanel{Width=668,Height=40};var title=Label(name,11);title.Width=565;title.Font=new Font("Segoe UI Semibold",11);row.Controls.Add(title);row.Controls.Add(Button("✕",()=>Collapse(),45));drawer.Controls.Add(row);Relayout();Theme();return true;}
 void Collapse(){if(revertTimer!=null&&revertTimer.Enabled){Status("Hãy giữ hoặc hoàn tác độ phân giải trước.",true);return;}RemoveTraffic();StopBluetoothScan();viewGeneration++;Status("");ReleaseMixer();opened="";drawer.Visible=false;drawer.Height=0;Relayout();}
 void Relayout(){
  // All main rows remain fixed; only the drawer below the footer expands.
  var area=Screen.FromControl(this).WorkingArea;int borderWidth=Width-ClientSize.Width,borderHeight=Height-ClientSize.Height;
  int baseHeight=D(466),limit=Math.Max(baseHeight,area.Height-borderHeight);
  int desired=baseHeight+(drawer.Visible?D(drawerRequestedHeight+8):0),clientHeight=Math.Min(desired,limit);
  SuspendLayout();AutoScroll=false;MinimumSize=new Size(D(720)+borderWidth,baseHeight+borderHeight);MaximumSize=new Size(D(720)+borderWidth,Math.Max(baseHeight+borderHeight,area.Height));
  status.SetBounds(D(18),D(350),D(684),D(28));footer.SetBounds(D(18),D(382),D(684),D(72));
  drawer.SetBounds(D(12),baseHeight,D(696),drawer.Visible?Math.Max(0,clientHeight-baseHeight-D(8)):0);
  ClientSize=new Size(D(720),clientHeight);ResumeLayout(true);
  if(Bottom>area.Bottom)Top=Math.Max(area.Top,area.Bottom-Height);if(Right>area.Right)Left=Math.Max(area.Left,area.Right-Width);
 }
 void Note(string s,int h=48){var l=Label(s);l.Width=655;l.Height=h;drawer.Controls.Add(l);PaintTheme(l);}
 void Actions(params Button[] buttons){var p=new FlowLayoutPanel{Width=664,Height=44,WrapContents=false};p.Controls.AddRange(buttons);drawer.Controls.Add(p);PaintTheme(p);}
 void Settings(string name,string uri,string reason){if(!Expand(name,165))return;Note(reason+"\nMở trang Windows để cấu hình; mục này không điều khiển trực tiếp trong form.",58);Actions(Button("Mở Cài đặt Windows ↗",()=>Native.Open(uri),220));}
 void Tick(){UpdateTraffic();try{long i,k,u;if(Native.GetSystemTimes(out i,out k,out u)){long total=k-oldKernel+u-oldUser,idle=i-oldIdle;var m=new Native.Memory();Native.GlobalMemoryStatusEx(m);cpu.Text="CPU "+(oldKernel==0||total<=0?"—":Math.Round(100.0*(total-idle)/total).ToString())+"%";ram.Text="RAM "+m.load+"%";oldIdle=i;oldKernel=k;oldUser=u;}
  var now=DateTime.UtcNow.AddHours(7);int[] a=Lunar.Date(now);string day=now.DayOfWeek==DayOfWeek.Sunday?"CN":"T"+((int)now.DayOfWeek+1);clock.Text=now.ToString("HH:mm")+" | "+day+". "+now.ToString("dd/MM")+" | "+a[0].ToString("00")+"/"+a[1].ToString("00")+"\n"+Lunar.Year(a[2])+(a[3]==1?" • nhuận":"");
  var ps=SystemInformation.PowerStatus;bool has=(ps.BatteryChargeStatus&BatteryChargeStatus.NoSystemBattery)==0&&ps.BatteryChargeStatus!=BatteryChargeStatus.Unknown;battery.Text=has?"▰ "+Math.Round(ps.BatteryLifePercent*100)+"%":"AC";tip.SetToolTip(battery,has?(ps.PowerLineStatus==PowerLineStatus.Online?"Đang cắm sạc":"Đang dùng pin"):"Máy không có pin / Không đọc được pin");
  NetworkTick();ticks++;if(ticks%3==0)RefreshAudio();
 }catch(Exception ex){Status(ex.Message,true);}}
 Button clockOption;ClockIntegration integration;bool exitTray;ListBox trafficList;ProcessNetwork traffic;
 void SetClockIntegration(bool on){
  if(on){
   var active=new ClockIntegration(this,()=>{Show();WindowState=FormWindowState.Normal;Activate();},()=>RunRepair("explorer",""));
   try{ClockIntegration.Register(true);integration=active;}catch{active.Dispose();throw;}
  }else{ClockIntegration.Register(false);integration.Dispose();integration=null;}
  clockOption.Text=on?"\uE73E":"\uE823";tip.SetToolTip(clockOption,on?"Tắt menu chuột phải taskbar và huỷ khởi động cùng Windows":"Bật menu chuột phải taskbar và khởi động cùng Windows");Status(on?"Đã tích hợp menu taskbar và khởi động cùng Windows.":"Đã huỷ tích hợp menu taskbar.");
 }
 void AddTraffic(){
  Note("Tiến trình dùng mạng • xếp theo tổng tải xuống + tải lên",24);
  trafficList=new ListBox{Width=653,Height=125,IntegralHeight=false,HorizontalScrollbar=true,Font=new Font("Consolas",9)};
  drawer.Controls.Add(trafficList);SyncTraffic();
 }
 void SyncTraffic(){
  if(trafficList==null)return;
  if(Visible && WindowState!=FormWindowState.Minimized && !closing){
   if(traffic!=null)return;
   try{traffic=new ProcessNetwork();trafficList.Items.Clear();trafficList.Items.Add("Đang lấy mẫu…");}
   catch(Exception ex){trafficList.Items.Clear();trafficList.Items.Add("Chưa thể theo dõi mạng: "+ex.Message);}
  }else if(traffic!=null){traffic.Dispose();traffic=null;}
 }
 void UpdateTraffic(){
  if(traffic==null)return;
  var lines=traffic.Sample();trafficList.BeginUpdate();
  try{trafficList.Items.Clear();if(lines.Length==0)trafficList.Items.Add("Chưa có lưu lượng trong giây vừa qua.");else trafficList.Items.AddRange(lines);}
  finally{trafficList.EndUpdate();}
 }
 void RemoveTraffic(){trafficList=null;if(traffic!=null){traffic.Dispose();traffic=null;}}
 void NetworkTick(){var list=NetworkInterface.GetAllNetworkInterfaces().Where(n=>n.OperationalStatus==OperationalStatus.Up&&n.NetworkInterfaceType!=NetworkInterfaceType.Loopback&&n.NetworkInterfaceType!=NetworkInterfaceType.Tunnel).ToArray();long rx=0,tx=0;double seconds=Math.Max(.1,(DateTime.UtcNow-lastNetwork).TotalSeconds);string details="";foreach(var n in list){var s=n.GetIPv4Statistics();long[] prev;if(netBytes.TryGetValue(n.Id,out prev)){rx+=Math.Max(0,s.BytesReceived-prev[0]);tx+=Math.Max(0,s.BytesSent-prev[1]);}netBytes[n.Id]=new[]{s.BytesReceived,s.BytesSent};details+=n.Name+"\nIP: "+string.Join(", ",n.GetIPProperties().UnicastAddresses.Where(x=>x.Address.AddressFamily==System.Net.Sockets.AddressFamily.InterNetwork).Select(x=>x.Address.ToString()))+"\nMAC: "+BitConverter.ToString(n.GetPhysicalAddress().GetAddressBytes())+"\n";}lastNetwork=DateTime.UtcNow;net.Text=(list.Any(n=>n.NetworkInterfaceType==NetworkInterfaceType.Wireless80211)?"Wi-Fi":list.Length>0?"LAN":"Ngoại tuyến")+"  ▾\n↓ "+Rate(rx/seconds)+"   ↑ "+Rate(tx/seconds);tip.SetToolTip(net,details.Length==0?"Chưa có kết nối mạng":details);}
 string Rate(double n){return n>=1048576?(n/1048576).ToString("0.0")+" MB/s":(n/1024).ToString("0")+" KB/s";}
 void RefreshAudio(){try{if(!volume.Capture)volume.Value=Math.Max(0,Math.Min(100,(int)(Audio.Volume()*100)));volumeValue.Text=volume.Value+"%";bool m=Audio.Muted(0);muteTile.Glyph=m?"\uE74F":"\uE767";muteTile.Active=m;muteTile.Invalidate();micMute=Audio.Muted(1);micTile.Active=micMute;micTile.Text=micMute?"Micro\nĐã tắt":"Micro";micTile.Glyph=micMute?"\uE198":"\uE720";micTile.Invalidate();}catch{}}
 async Task LoadHardware(){string r=await Task.Run(()=>{var mem=new Native.Memory();Native.GlobalMemoryStatusEx(mem);string model=Native.Query("Win32_ComputerSystem","Model"),cpuName=Native.Query("Win32_Processor","Name").Replace("(R)","").Replace("(TM)","").Replace(" CPU","");string disk="";try{disk=Native.PS("$d=Get-PhysicalDisk | Where-Object BusType -ne 'USB'; ($d | ForEach-Object { $_.MediaType.ToString()+' '+[math]::Round($_.Size/1e9)+'GB' }) -join ' + '").Trim();}catch{disk=Native.Query("Win32_DiskDrive","Model");}return Environment.MachineName+"  •  "+model+"\n"+cpuName+"  •  "+Math.Round(mem.total/1073741824.0)+"GB  •  "+disk;});if(!closing){hardware.Text=r;tip.SetToolTip(hardware,r);}}
 void Outputs(){if(!Expand("Đầu ra • Volume Mixer",360))return;var devices=Audio.Devices(0);var box=new ComboBox{Width=650,DropDownStyle=ComboBoxStyle.DropDownList};box.Items.AddRange(devices.ToArray());string defaultId=Audio.DefaultId();for(int i=0;i<devices.Count;i++)if(devices[i].Id==defaultId)box.SelectedIndex=i;drawer.Controls.Add(box);Actions(Button("Chọn đầu ra",()=>{var d=box.SelectedItem as AudioDevice;if(d!=null){Audio.Select(d.Id);RefreshAudio();opened="";Outputs();}},200),Button("Làm mới mixer",()=>{opened="";Outputs();},200));Note("Volume Mixer • Âm lượng từng ứng dụng",28);try{mixerSessions=MixerSession.List();}catch(Exception ex){Note("Không đọc được phiên âm thanh: "+ex.Message,62);Theme();return;}if(mixerSessions.Count==0)Note("Chưa có ứng dụng tạo phiên âm thanh. Phát âm thanh rồi bấm Làm mới mixer.",45);foreach(var item in mixerSessions){var session=item;var row=new Panel{Width=650,Height=57};var name=Label(session.Name);name.SetBounds(0,0,650,23);row.Controls.Add(name);var mute=IconButton(session.Muted?"\uE74F":"\uE767","Tắt / bật âm ứng dụng",()=>{});mute.SetBounds(0,23,32,30);mute.Click+=(s,e)=>Guard(()=>{session.Muted=!session.Muted;mute.Text=session.Muted?"\uE74F":"\uE767";});row.Controls.Add(mute);var slider=new TrackBar{Minimum=0,Maximum=100,Value=Math.Max(0,Math.Min(100,(int)Math.Round(session.Level*100))),AutoSize=false,TickStyle=TickStyle.None};slider.SetBounds(40,23,545,30);var percent=Label(slider.Value+"%");percent.SetBounds(590,23,55,30);slider.Scroll+=(s,e)=>Guard(()=>{session.Level=slider.Value/100f;percent.Text=slider.Value+"%";});row.Controls.Add(slider);row.Controls.Add(percent);drawer.Controls.Add(row);}Theme();}
 async void ShowWifi(bool monitor=false){if(!Expand("Wi-Fi • Kết nối mạng",monitor?465:284))return;Actions(Button("Bật Wi-Fi",async()=>await Work(()=>WinRT.Radio("WiFi",true))),Button("Tắt Wi-Fi",async()=>await Work(()=>WinRT.Radio("WiFi",false))),Button("Quét lại",()=>RefreshWifi()),Button("Quyền Vị trí ↗",()=>Native.Open("ms-settings:privacy-location"),150));wifiList=new ListBox{Width=653,Height=102,IntegralHeight=false,Font=new Font("Segoe UI",10)};drawer.Controls.Add(wifiList);var row=new FlowLayoutPanel{Width=655,Height=41};wifiPassword=new TextBox{Width=230,UseSystemPasswordChar=true,Margin=new Padding(4,7,4,4)};tip.SetToolTip(wifiPassword,"Mật khẩu cho mạng WPA2 mới; mạng đã lưu không cần nhập lại.");row.Controls.Add(wifiPassword);row.Controls.Add(Button("Kết nối",()=>ConnectWifi(),120));row.Controls.Add(Button("Ngắt kết nối",()=>{var w=wifiList.SelectedItem as WifiItem;if(w!=null){Wifi.Disconnect(w);Status("Đã yêu cầu ngắt kết nối.");}},140));drawer.Controls.Add(row);Note("Mật khẩu: chỉ nhập cho mạng mới WPA2-Personal. Mạng WPA3/Enterprise dùng cấu hình Windows đã lưu.",42);if(monitor)AddTraffic();Theme();await RefreshWifiAsync();}
 async Task RefreshWifiAsync(){await Work(()=>{wifi=Wifi.Scan().GroupBy(w=>w.Name+"|"+w.Adapter).Select(g=>g.OrderByDescending(w=>w.Connected).ThenByDescending(w=>w.Profile.Length).First()).OrderByDescending(w=>w.Connected).ThenByDescending(w=>w.Signal).ToList();return "Tìm thấy "+wifi.Count+" mạng. Quét lại sau vài giây nếu chưa đủ danh sách.";},r=>{if(opened=="Wi-Fi • Kết nối mạng"){wifiList.Items.Clear();wifiList.Items.AddRange(wifi.ToArray());}});}
 async void RefreshWifi(){await RefreshWifiAsync();}
 async void ConnectWifi(){var w=wifiList.SelectedItem as WifiItem;if(w==null){Status("Chọn mạng Wi-Fi trước.");return;}string pw=wifiPassword.Text;if(string.IsNullOrEmpty(w.Profile)&&w.Secure&&(pw.Length<8||pw.Length>63)){Status("Nhập mật khẩu WPA2 dài 8–63 ký tự.",true);return;}await Work(()=>{Wifi.Connect(w,pw);return "Đã gửi yêu cầu kết nối "+w.Name+". Quét lại để xác nhận trạng thái.";},r=>wifiPassword.Clear());}
 void Power(){if(!Expand("Pin • Chế độ hiệu suất hệ thống",330))return;Actions(Button("Tiết kiệm pin",()=>SetPower(0),198),Button("Cân bằng",()=>SetPower(1),198),Button("Tốc độ",()=>SetPower(2),198));Note("Áp dụng chế độ hiệu suất Windows. Energy Saver là tính năng riêng trong Cài đặt pin.",45);Actions(Button("Pin / Energy Saver ↗",()=>Native.Open("ms-settings:powersleep"),235));Note("Gập nắp màn hình (Lid) • áp dụng ngay khi chọn Cắm sạc hoặc Dùng pin:",30);LidRow(true);LidRow(false);}
 void LidRow(bool ac){var row=new FlowLayoutPanel{Width=650,Height=43};var icon=IconButton(ac?"\uE945":"\uE850",ac?"Khi cắm sạc (Plugged in)":"Khi dùng pin",()=>{});row.Controls.Add(icon);var label=Label(ac?"Cắm sạc":"Dùng pin");label.Width=105;row.Controls.Add(label);var box=new ComboBox{Width=250,DropDownStyle=ComboBoxStyle.DropDownList};box.Items.AddRange(new object[]{"Nothing — Không làm gì","Sleep — Ngủ"});uint current=LidPower.Read(ac);box.SelectedIndex=current<=1?(int)current:-1;if(current>1){tip.SetToolTip(box,"Cấu hình hiện tại: "+(current==2?"Hibernate":"Shut down")+". Chỉ thay đổi khi anh chọn.");}bool restoring=false;box.SelectedIndexChanged+=(s,e)=>Guard(()=>{if(restoring||box.SelectedIndex<0)return;try{LidPower.Write(ac,(uint)box.SelectedIndex);}catch{restoring=true;try{uint value=LidPower.Read(ac);box.SelectedIndex=value<=1?(int)value:-1;}finally{restoring=false;}throw;}Status("Đã áp dụng gập nắp khi "+(ac?"cắm sạc":"dùng pin")+": "+box.SelectedItem);});row.Controls.Add(box);drawer.Controls.Add(row);PaintTheme(row);}
 void SetPower(int n){string[] overlay={"961cc777-2547-4f9d-8174-7d86181b8a7a","00000000-0000-0000-0000-000000000000","ded574b5-45a0-4f42-8737-46345c09c238"};Guid g=new Guid(overlay[n]);uint r=Native.PowerSetActiveOverlayScheme(ref g);if(r!=0){string[] plans={"a1841308-3541-4fab-bc81-f71556f20b4a","381b4222-f694-41f0-9685-ff5bb260df2e","8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c"};g=new Guid(plans[n]);r=Native.PowerSetActiveScheme(IntPtr.Zero,ref g);}if(r!=0)throw new Exception("Máy không hỗ trợ chế độ này. Mã Windows: "+r);Status("Đã áp dụng chế độ: "+new[]{"Tiết kiệm pin","Cân bằng","Tốc độ"}[n]);}
 void PowerMenu(){if(!Expand("Nguồn",169))return;Note("Chọn thao tác, sau đó xác nhận tại đây để tránh bấm nhầm.",33);Actions(Button("Shut down",()=>ConfirmPower("Tắt máy",()=>Native.Run("shutdown.exe","/s /t 0")),190),Button("Restart",()=>ConfirmPower("Khởi động lại",()=>Native.Run("shutdown.exe","/r /t 0")),190),Button("Sleep",()=>ConfirmPower("Ngủ",()=>Application.SetSuspendState(PowerState.Suspend,false,false)),190));Theme();}
 void ConfirmPower(string name,Action action){if(!Expand("Xác nhận: "+name,155))return;Note("Lưu công việc đang mở trước khi tiếp tục. Thao tác: "+name,40);Actions(Button("Xác nhận "+name,()=>Guard(action),230),Button("Hủy",()=>Collapse(),130));}
 void Display(){if(!Expand("Xuất màn hình • Project",175))return;Actions(Button("Chỉ màn hình PC",()=>Project(0x1),152),Button("Nhân đôi",()=>Project(0x2),152),Button("Mở rộng",()=>Project(0x4),152),Button("Chỉ màn hình ngoài",()=>Project(0x8),166));Actions(Button("Màn hình không dây ↗",()=>{Native.Open("ms-settings-connectabledevices:devicediscovery");},240),Button("Cài đặt màn hình ↗",()=>Native.Open("ms-settings:display"),220));}
 void Project(uint flag){int r=Native.SetDisplayConfig(0,IntPtr.Zero,0,IntPtr.Zero,0x80|flag);if(r!=0)throw new Exception(new System.ComponentModel.Win32Exception(r).Message);Status("Đã gửi cấu hình trình chiếu.");}
 void Scale(){if(!Expand("Tỷ lệ hiển thị • Màn hình chính",210))return;try{int current=DisplayScale.Current();Note("Tỷ lệ hiện tại: "+current+"%. Chỉ hiện các mức màn hình cho phép.",34);var row=new FlowLayoutPanel{Width=650,Height=82};foreach(int p in DisplayScale.Options()){int n=p;row.Controls.Add(Button(p+"%",()=>{DisplayScale.Apply(n);Status("Đã gửi yêu cầu đổi scale "+n+"%.");},92));}drawer.Controls.Add(row);PaintTheme(row);}catch(Exception ex){Note(ex.Message+"\nDùng Cài đặt Windows khi driver không hỗ trợ.",70);}Actions(Button("Cài đặt màn hình ↗",()=>Native.Open("ms-settings:display"),260));}
 void Resolutions(){if(!Expand("Độ phân giải • Màn hình chính",191))return;var box=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=645,Height=32};var modes=Native.Modes();var current=Native.CurrentMode();var initial=startingMode??current;int selected=0;for(int i=0;i<modes.Count;i++){var m=modes[i];bool active=m.width==current.width&&m.height==current.height&&m.frequency==current.frequency;if(active)selected=i;box.Items.Add(m.width+" × "+m.height+"  •  "+m.frequency+" Hz"+((m.width==initial.width&&m.height==initial.height&&m.frequency==initial.frequency)?" — Mặc định":"")+(active?" (đang sử dụng)":""));}if(box.Items.Count>0)box.SelectedIndex=selected;drawer.Controls.Add(box);Actions(Button("Áp dụng (hoàn tác sau 15 giây)",()=>{if(box.SelectedIndex<0)return;oldMode=Native.CurrentMode();Native.Resolution(modes[box.SelectedIndex]);ConfirmResolution();},360));Note("Chế độ mới chỉ được giữ khi anh xác nhận. Nếu không, tự trở về chế độ trước.",40);Theme();}
 void ConfirmResolution(){Expand("Giữ độ phân giải mới?",160);revertSeconds=15;Note("Màn hình có hiển thị đúng không?",38);Actions(Button("Giữ thay đổi",()=>{revertTimer.Stop();Status("Đã giữ độ phân giải mới.");Collapse();},210),Button("Hoàn tác ngay",()=>Revert(),210));revertTimer=new System.Windows.Forms.Timer{Interval=1000};revertTimer.Tick+=(s,e)=>{revertSeconds--;Status("Tự hoàn tác độ phân giải sau "+revertSeconds+" giây.");if(revertSeconds<=0)Revert();};revertTimer.Start();}
 void Revert(){revertTimer.Stop();Guard(()=>Native.Resolution(oldMode));Status("Đã hoàn tác độ phân giải.");Collapse();}

 class BtItem{public string Id,Name,Info;public bool Connected,Paired;public override string ToString(){return Name+Info;}}
 string hotspotName=Environment.MachineName, hotspotPassword="11112222";
 async Task ConfigureHotspot(TextBox name,TextBox password,bool start){
  string ssid=name.Text,pass=password.Text;
  if(string.IsNullOrWhiteSpace(ssid)||System.Text.Encoding.UTF8.GetByteCount(ssid)>32||pass.Length<8||pass.Length>63){Status("Tên Wi-Fi cần 1–32 byte, mật khẩu cần 8–63 ký tự.",true);return;}
  await Work(()=>WinRT.Hotspot(start?(bool?)true:null,ssid,pass),r=>{hotspotName=ssid;hotspotPassword=pass;});
 }
 void Hotspot(){if(!Expand("Mobile Hotspot • Trạm phát Wi-Fi",268))return;var name=new TextBox{Width=640,Text=hotspotName};var password=new TextBox{Width=640,UseSystemPasswordChar=true,Text=hotspotPassword};tip.SetToolTip(name,"Tên Wi-Fi mới (SSID)");tip.SetToolTip(password,"Mật khẩu mới tối thiểu 8 ký tự");Note("Tên Wi-Fi / Mật khẩu (8–63 ký tự). Mật khẩu mặc định là 11112222.",28);drawer.Controls.Add(name);drawer.Controls.Add(password);Actions(Button("Bật phát",async()=>await ConfigureHotspot(name,password,true),135),Button("Tắt phát",async()=>await Work(()=>WinRT.Hotspot(false,null,null)),135),Button("Lưu cấu hình",async()=>await ConfigureHotspot(name,password,false),150),Button("Xem trạng thái",async()=>await Work(()=>WinRT.Hotspot(null,null,null)),170));Note("Cần Internet và card Wi-Fi hỗ trợ. Cấu hình chỉ áp dụng nếu Windows cho phép; mật khẩu không được ghi vào tệp của tool.",42);Theme();}
 void ShowCameraState(bool off){
  cameraTile.Active=off;cameraTile.Text=off?"Camera\nĐã tắt":"Camera";
  cameraTile.Glyph=off?"\uE8D8":"\uE714";cameraTile.Invalidate();
 }
 async void Camera(){
  cameraTile.Enabled=false;Status("Đang đổi trạng thái camera…");
  try{bool off=await Task.Run(()=>CameraSwitch.Toggle());if(!closing){ShowCameraState(off);Status(off?"Đã tắt camera.":"Đã bật camera.");}}
  catch(Exception ex){if(!closing)Status(ex.Message,true);}
  finally{if(!closing)cameraTile.Enabled=true;}
 } async void Radios(){if(!Expand("Radio • Wi-Fi và Bluetooth",210))return;Note("Hai công tắc độc lập. Trạng thái được đọc lại từ Windows sau mỗi thao tác.",38);var radios=Button("Đang đọc Wi-Fi + Bluetooth…",()=>{},310);var airplane=Button("Đang đọc chế độ máy bay…",()=>{},310);Actions(radios,airplane);Theme();radios.Enabled=airplane.Enabled=false;await Work(()=>WinRT.RadioPair(false),r=>{radios.Enabled=true;radios.Text=r.Contains(": On")?"Tắt Wi-Fi + Bluetooth":"Bật Wi-Fi + Bluetooth";});if(opened!="Radio • Wi-Fi và Bluetooth")return;if(!radios.Enabled){radios.Enabled=true;radios.Text="Thử bật/tắt Wi-Fi + Bluetooth";}try{bool on=Airplane.State(null);airplane.Enabled=true;airplane.Text=on?"Tắt chế độ máy bay Windows":"Bật chế độ máy bay Windows";}catch(Exception ex){airplane.Text="Máy bay không khả dụng";tip.SetToolTip(airplane,ex.Message);}radios.Click+=async(s,e)=>{if(busy)return;await Work(()=>WinRT.RadioPair(true),r=>radios.Text=r.Contains(": On")?"Tắt Wi-Fi + Bluetooth":"Bật Wi-Fi + Bluetooth");};airplane.Click+=async(s,e)=>{if(busy)return;await Work(()=>Airplane.State(!Airplane.State(null))?"Máy bay: Bật":"Máy bay: Tắt",r=>airplane.Text=r.Contains("Bật")?"Tắt chế độ máy bay Windows":"Bật chế độ máy bay Windows");};}
 void NightLight(){
  if(!Expand("Ánh sáng đêm",210))return;
  Button toggle=null;var value=Label("",9);
  var strength=new TrackBar{Width=490,Minimum=0,Maximum=100,TickStyle=TickStyle.None,AutoSize=false,Height=32,AccessibleName="Cường độ Ánh sáng đêm"};
  Action refresh=()=>{
   bool on=NightRecord.Read(true).Enabled;
   strength.Value=NightRecord.Read(false).Strength;
   value.Text=strength.Value+"%";
   toggle.Text=on?"Tắt Ánh sáng đêm":"Mở Ánh sáng đêm";
  };
  toggle=Button("",()=>{var record=NightRecord.Read(true);record.SetEnabled(!record.Enabled);record.Save();refresh();},230);
  Actions(toggle,Button("Cài đặt Ánh sáng đêm ↗",()=>Native.Open("ms-settings:nightlight"),240));
  Note("Cường độ (Strength)",26);
  var row=new FlowLayoutPanel{Width=650,Height=40};row.Controls.Add(strength);row.Controls.Add(value);drawer.Controls.Add(row);Theme();
  strength.ValueChanged+=(s,e)=>value.Text=strength.Value+"%";
  Action apply=()=>Guard(()=>{try{var record=NightRecord.Read(false);if(record.Strength!=strength.Value){record.Strength=strength.Value;record.Save();}}finally{refresh();}});
  strength.MouseUp+=(s,e)=>apply();strength.KeyUp+=(s,e)=>apply();
  try{refresh();}catch(Exception ex){toggle.Text="Ánh sáng đêm chưa khả dụng";toggle.Enabled=strength.Enabled=false;value.Text="—";Status(ex.Message,true);}
 }
 static void OpenShellFolder(string id){Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"explorer.exe"),"shell:::"+id){UseShellExecute=true});}
 void Extras(){if(!Expand("Tiện ích • Mobility Center / Quick Settings",279))return;Actions(Button("Mobility Center ↗",()=>Native.Open("mblctr.exe"),200),Button("God Mode ↗",()=>OpenShellFolder("{ED7BA470-8E54-465E-825C-99712043E01C}"),200),Button("Chế độ thuyết trình ↗",()=>Native.Open("PresentationSettings.exe"),218));Actions(Button("Devices and Printers ↗",()=>OpenShellFolder("{2227A280-3AEA-1069-A2DE-08002B30309D}"),200),Button("Chia sẻ lân cận ↗",()=>Native.Open("ms-settings:crossdevice"),200),Button("Thông báo / Tập trung ↗",()=>Native.Open("ms-settings:notifications"),218));Actions(Button("Device Manager ↗",()=>Native.Open(Path.Combine(Environment.SystemDirectory,"devmgmt.msc")),200),Button("Quyền camera ↗",()=>Native.Open("ms-settings:privacy-webcam"),200),Button("Quyền micro ↗",()=>Native.Open("ms-settings:privacy-microphone"),218));Note("↗ mở thành phần gốc của Windows. Các phần mở rộng OEM của Mobility Center phụ thuộc nhà sản xuất; không thể nhúng mọi trang hệ thống vào form.",54);}
 void Accessibility(){if(!Expand("Trợ năng",190))return;Actions(Button("Phụ đề trực tiếp ↗",()=>Hotkey(0x5B,0x11,0x4C),210),Button("Kính lúp ↗",()=>Native.Open("magnify.exe"),190),Button("Trợ năng Windows ↗",()=>Native.Open("ms-settings:easeofaccess"),218));Note("Phụ đề trực tiếp dùng phím Win + Ctrl + L; khả dụng theo phiên bản Windows. Kính lúp và các tiện ích gốc có giao diện riêng của Windows.",58);}
 [DllImport("user32.dll")]static extern void keybd_event(byte key,byte scan,uint flags,UIntPtr extra);
 void Hotkey(params byte[] keys){foreach(byte k in keys)keybd_event(k,0,0,UIntPtr.Zero);foreach(byte k in keys.Reverse())keybd_event(k,0,2,UIntPtr.Zero);}
 void Calendar(){if(!Expand("Lịch âm dương • Việt Nam UTC+7",352))return;month=new DateTime(DateTime.UtcNow.AddHours(7).Year,DateTime.UtcNow.AddHours(7).Month,1);RenderCalendar();}
 void RenderCalendar(){while(drawer.Controls.Count>1){Control c=drawer.Controls[1];drawer.Controls.Remove(c);c.Dispose();}var nav=new FlowLayoutPanel{Width=650,Height=34};nav.Controls.Add(Button("‹",()=>{month=month.AddMonths(-1);RenderCalendar();},48));var title=Label("Tháng "+month.Month+" / "+month.Year,11);title.Width=300;title.TextAlign=ContentAlignment.MiddleCenter;nav.Controls.Add(title);nav.Controls.Add(Button("›",()=>{month=month.AddMonths(1);RenderCalendar();},48));nav.Controls.Add(Button("Hôm nay",()=>{month=new DateTime(DateTime.UtcNow.AddHours(7).Year,DateTime.UtcNow.AddHours(7).Month,1);RenderCalendar();},110));drawer.Controls.Add(nav);
  var grid=new TableLayoutPanel{Width=650,Height=197,ColumnCount=7,RowCount=7,Margin=new Padding(0)};for(int i=0;i<7;i++){grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100f/7));grid.RowStyles.Add(new RowStyle(SizeType.Percent,100f/7));}string[] weekdays={"T2","T3","T4","T5","T6","T7","CN"};for(int i=0;i<7;i++){var l=Label(weekdays[i]);l.Dock=DockStyle.Fill;l.TextAlign=ContentAlignment.MiddleCenter;grid.Controls.Add(l,i,0);}int offset=((int)month.DayOfWeek+6)%7;for(int i=0;i<42;i++){DateTime day=month.AddDays(i-offset);int[] a=Lunar.Date(day);var b=new Button{Text=day.Day+" / "+a[0]+(a[0]==1?"."+a[1]:""),Dock=DockStyle.Fill,FlatStyle=FlatStyle.Flat,Font=new Font("Segoe UI",8),Margin=new Padding(1),TabStop=true};b.FlatAppearance.BorderSize=0;b.Tag=day;b.Click+=(s,e)=>Day((DateTime)((Control)s).Tag);tip.SetToolTip(b,day.ToString("dd/MM/yyyy")+" • Âm "+a[0]+"/"+a[1]+(a[3]==1?" nhuận":"")+" • "+Lunar.Holiday(day));grid.Controls.Add(b,i%7,i/7+1);}drawer.Controls.Add(grid);dayDetail=Label("",8);dayDetail.Width=650;dayDetail.Height=59;drawer.Controls.Add(dayDetail);Theme();foreach(Control c in grid.Controls){if(c.Tag is DateTime){var d=(DateTime)c.Tag;int[] a=Lunar.Date(d);if(d.Month!=month.Month)c.ForeColor=muted;else if(d.DayOfWeek==DayOfWeek.Sunday||a[0]==1||a[0]==15||Lunar.Holiday(d)!="")c.ForeColor=Color.FromArgb(237,115,129);if(d==DateTime.UtcNow.AddHours(7).Date){c.BackColor=Color.FromArgb(94,99,224);c.ForeColor=Color.White;}}}Day(DateTime.UtcNow.AddHours(7).Date);}
 void Day(DateTime d){int[] a=Lunar.Date(d);dayDetail.Text=d.ToString("dd/MM/yyyy")+"  •  Âm lịch "+a[0]+"/"+a[1]+(a[3]==1?" nhuận":"")+" • "+Lunar.Year(a[2])+"  •  Ngày "+Lunar.Day(d)+"  "+Lunar.Holiday(d)+"\nGiờ hoàng đạo (dân gian): "+Lunar.Hours(d);}
 internal void SavePreview(string path){Tick();using(var b=new Bitmap(Width,Height)){DrawToBitmap(b,new Rectangle(0,0,Width,Height));b.Save(path);}}
}
