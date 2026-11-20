using System;
using System.Drawing;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Forms;

internal partial class Dashboard {
 Button Command(string glyph,string caption,Action run) {
  string help=RepairHint(caption);var button=Button(caption,()=>{if(busy){Status("Đang thực hiện thao tác trước…");return;}Status(caption+" • "+help);run();},155);
  button.Height=34; button.Margin=new Padding(2); button.Font=new Font("Segoe UI",8);
  var icon=new Bitmap(21,22);
  using(var g=Graphics.FromImage(icon))
  using(var font=new Font("Segoe MDL2 Assets",13))
   TextRenderer.DrawText(g,glyph,font,new Rectangle(0,0,21,22),Color.SteelBlue,TextFormatFlags.NoPadding|TextFormatFlags.VerticalCenter);
  button.Image=icon;button.TextImageRelation=TextImageRelation.ImageBeforeText;
  button.Disposed+=(s,e)=>icon.Dispose();button.AccessibleName=caption;tip.AutoPopDelay=16000;tip.SetToolTip(button,help);
  return button;
 }
 void RepairPanel(){
  if(!Expand("Cấu hình/Sửa",540))return;
  var machine=new TextBox{Width=220,Text=Environment.MachineName};
  var server=new TextBox{Width=220};
  Note("Cấu hình máy tính",21);
  Actions(Command("\uE72E","Cấu hình UAC",()=>Native.Open("UserAccountControlSettings.exe")),Job("\uEA8F","Tắt thông báo","notifications"),Command("\uE713","Bộ lọc tường lửa",()=>Native.Open("wf.msc")),Job("\uE83D","Bật tường lửa","firewallOn"));
  Actions(Command("\uE83D","Tắt tường lửa…",()=>Native.Open("firewall.cpl")),Command("\uE73D","SmartScreen…",()=>Native.Open("windowsdefender://appbrowser")),Command("\uE77B","Quản lý user",()=>Launch("netplwiz.exe","")));
  InputAction("Tên máy mới",machine,Command("\uE70F","Đổi tên máy",()=>RunRepair("rename",machine.Text.Trim())));
  Note("Tuỳ chỉnh hệ thống",21);
  Actions(Command("\uE70F","Hosts · Notepad",()=>Hosts(false)),Command("\uE70F","Hosts · Notepad++",()=>Hosts(true)),Job("\uE700","Menu cổ điển","classic"),Job("\uE700","Menu hiện đại","modern"));
  Actions(Command("\uE895","Tắt Update",()=>RunRepair("updateOff","")),Command("\uE895","Bật Update",()=>RunRepair("updateOn","")),Command("\uE713","Control Panel",()=>Native.Open("control.exe")),Command("\uE787","Date Time Format",()=>Launch("control.exe","intl.cpl")));
  Note("Fix lỗi",21);
  Actions(Job("\uE9D9","Sửa Task Manager","taskmgr"),Job("\uE91B","Sửa icon trắng","icons"),Job("\uE88E","Tắt AutoPlay/quét USB","usb"),Job("\uE749","Sửa kẹt giấy máy in","queue"));
  Actions(Job("\uE749","Sửa Canon LBP2900","canon"),Job("\uE72C","Restart Explorer","explorer"),Job("\uE74D","Xoá tập tin tạm","temp"),Command("\uE968","Ping 8.8.8.8",()=>Launch("ping.exe","-t 8.8.8.8")));
  InputAction("Tên/IP máy in LAN",server,Command("\uE749","Chia sẻ máy in LAN",()=>RunRepair("lan",server.Text.Trim())));
  Theme();
 }
 void InputAction(string caption,TextBox box,Button button){
  var row=new FlowLayoutPanel{Width=650,Height=40,WrapContents=false};
  var label=Label(caption);label.Width=125;row.Controls.Add(label);row.Controls.Add(box);row.Controls.Add(button);drawer.Controls.Add(row);PaintTheme(row);
 }
 Button Job(string glyph,string caption,string key){return Command(glyph,caption,()=>RunRepair(key,""));}
 static void Launch(string file,string args){Process.Start(new ProcessStartInfo(file,args){UseShellExecute=true});}
 void Hosts(bool plus){
  string editor=Path.Combine(Environment.SystemDirectory,"notepad.exe");
  if(plus){
   editor=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),@"Notepad++\notepad++.exe");
   if(!File.Exists(editor))editor=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),@"Notepad++\notepad++.exe");
   if(!File.Exists(editor)){using(var d=new OpenFileDialog{Filter="Notepad++|notepad++.exe"}){if(d.ShowDialog(this)!=DialogResult.OK)return;editor=d.FileName;}}
  }
  Launch(editor,"\""+Path.Combine(Environment.SystemDirectory,@"drivers\etc\hosts")+"\"");
 }
 static string RepairHint(string name){
  switch(name){
   case "Cấu hình UAC":return "Mở UAC → chọn mức nhắc → nhấn OK.";
   case "Tắt thông báo":return "Tắt biểu ngữ thông báo của tài khoản hiện tại.";
   case "Bộ lọc tường lửa":return "Mở Firewall Advanced → chọn Inbound hoặc Outbound Rules → New Rule.";
   case "Bật tường lửa":return "Bật Windows Firewall cho Domain, Private và Public.";
   case "Tắt tường lửa…":return "Mở cấu hình → chọn Turn Windows Defender Firewall on or off → chọn cấu hình mạng.";
   case "SmartScreen…":return "Mở App & browser control → chọn Reputation-based protection settings.";
   case "Quản lý user":return "Mở danh sách tài khoản → chọn người dùng để chỉnh sửa.";
   case "Đổi tên máy":return "Nhập tên máy mới → nhấn đổi tên → khởi động lại khi thuận tiện.";
   case "Hosts · Notepad":return "Mở hosts bằng Notepad quyền quản trị → sửa → Ctrl+S.";
   case "Hosts · Notepad++":return "Mở hosts bằng Notepad++ → chọn chương trình nếu chưa tìm thấy → Ctrl+S.";
   case "Menu cổ điển":return "Áp dụng menu chuột phải cổ điển → khởi động lại Explorer.";
   case "Menu hiện đại":return "Gỡ tuỳ chỉnh menu cổ điển → khởi động lại Explorer.";
   case "Tắt Update":return "Lưu cấu hình dịch vụ hiện tại → tắt Windows Update → kiểm tra.";
   case "Bật Update":return "Khôi phục kiểu khởi động và trạng thái đã lưu khi tắt Update.";
   case "Control Panel":return "Mở Control Panel → chọn mục hệ thống cần chỉnh.";
   case "Date Time Format":return "Mở Region → Formats → Additional settings để đổi định dạng ngày, giờ.";
   case "Sửa Task Manager":return "Cho phép Task Manager cho tài khoản hiện tại → mở Task Manager.";
   case "Sửa icon trắng":return "Dừng Explorer → xoá bộ nhớ đệm biểu tượng → mở lại Explorer.";
   case "Tắt AutoPlay/quét USB":return "Tắt AutoPlay → dùng Defender quét từng ổ tháo rời.";
   case "Sửa kẹt giấy máy in":return "Lấy giấy kẹt ra theo hướng dẫn máy in → xác nhận xoá hàng đợi → khởi động lại dịch vụ in.";
   case "Sửa Canon LBP2900":return "Xác nhận gỡ hàng đợi Canon → dọn cổng USB riêng → dò lại máy in.";
   case "Restart Explorer":return "Đóng Explorer của phiên hiện tại → khởi động lại thanh tác vụ.";
   case "Xoá tập tin tạm":return "Dọn Temp của tài khoản hiện tại → bỏ qua liên kết và tệp đang sử dụng.";
   case "Ping 8.8.8.8":return "Mở cửa sổ ping liên tục → nhấn Ctrl+C để dừng.";
   case "Chia sẻ máy in LAN":return "Nhập tên/IP máy chủ → mở danh sách chia sẻ → nhấn đúp máy in cần kết nối.";
   default:return name;
  }
 } async void RunRepair(string key,string value){
  if((key=="canon"||key=="queue") && MessageBox.Show(this,key=="queue"?"Xoá toàn bộ lệnh in đang chờ trên máy này?":"Gỡ hàng đợi Canon LBP2900 và dò lại máy in USB?",Text,MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
  int generation=viewGeneration;var progress=new Progress<string>(line=>{if(!closing && generation==viewGeneration && busy)Status(line);});await Work(()=>ExecuteRepair(key,value,progress));
 }
 static string ExecuteRepair(string key,string value,IProgress<string> progress){
  string script;
  using(var reader=new StreamReader(typeof(Dashboard).Assembly.GetManifestResourceStream("Repairs.ps1")))script=reader.ReadToEnd();
  var info=new ProcessStartInfo(Path.Combine(Environment.SystemDirectory,@"WindowsPowerShell\v1.0\powershell.exe"),"-NoProfile -NonInteractive -EncodedCommand "+Convert.ToBase64String(Encoding.Unicode.GetBytes(script))){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8};
  info.EnvironmentVariables["BDK_COMMAND"]=key;info.EnvironmentVariables["BDK_INPUT"]=value;
  using(var process=Process.Start(info)){
   var errors=process.StandardError.ReadToEndAsync();
   string output="",line;while((line=process.StandardOutput.ReadLine())!=null){if(line.StartsWith("@@"))progress.Report(line.Substring(2));else if(line.Trim().Length>0)output=line.Trim();}process.WaitForExit();
   if(process.ExitCode!=0)throw new Exception(output.Length>0?output:errors.Result);
   return output;
  }
 }
}
