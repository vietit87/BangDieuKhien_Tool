using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
internal static class Checks {
 static StringBuilder log=new StringBuilder();static int failed;
 static void Check(string name,Action a){File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"check-progress.txt"),"START "+name+Environment.NewLine);try{a();log.AppendLine("PASS "+name);}catch(Exception ex){failed++;log.AppendLine("FAIL "+name+": "+ex.GetBaseException().Message);}File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-results.txt"),log.ToString(),Encoding.UTF8);}
 static void LunarCheck(int y,int m,int d,int ld,int lm,int ly,int leap){int[] a=Lunar.Date(new DateTime(y,m,d));if(a[0]!=ld||a[1]!=lm||a[2]!=ly||a[3]!=leap)throw new Exception(string.Join("/",a));}
 internal static void Run(){
  Check("Night Light toggle and strength preserve schedule",()=>{
   Func<string,byte[]> payload=hex=>hex.Split(' ').Select(v=>Convert.ToByte(v,16)).ToArray();
   Func<byte[],byte[]> envelope=body=>payload("43 42 01 00 0A 02 01 00 2A 06 01 2A 2B 0E").Concat(new[]{(byte)body.Length}).Concat(body).Concat(new byte[3]).ToArray();
   var state=new NightRecord(envelope(payload("43 42 01 00 D0 0A 02 C6 14 01 00")),true);
   if(state.Enabled)throw new Exception("Off expected");state.SetEnabled(true);
   var on=new NightRecord(state.Serialize(),true);if(!on.Enabled)throw new Exception("On expected");on.SetEnabled(false);
   if(new NightRecord(on.Serialize(),true).Enabled)throw new Exception("Off expected after second click");
   byte[] schedule=payload("02 01 C2 0A 00 CA 14 0E 01 2E 0F 00 CA 1E 00");
   var config=new NightRecord(envelope(payload("43 42 01 00").Concat(schedule).Concat(payload("CF 28 CC 2B CA 32 00 CA 3C 00 00")).ToArray()),false);
   foreach(int level in new[]{0,20,50,100}){
    config.Strength=level;var encoded=config.Serialize();var roundtrip=new NightRecord(encoded,false);
    if(roundtrip.Strength!=level||!BitConverter.ToString(encoded).Contains(BitConverter.ToString(schedule)))throw new Exception("Strength or schedule changed unexpectedly");
   }
  });
  Check("Tet 2024",()=>LunarCheck(2024,2,10,1,1,2024,0));Check("Tet 2025",()=>LunarCheck(2025,1,29,1,1,2025,0));Check("Tet 2026",()=>LunarCheck(2026,2,17,1,1,2026,0));Check("Tet 2027",()=>LunarCheck(2027,2,6,1,1,2027,0));Check("Leap month 2023",()=>LunarCheck(2023,3,22,1,2,2023,1));Check("Leap month 2025",()=>LunarCheck(2025,7,25,1,6,2025,1));Check("User date 17 Nov 2026",()=>LunarCheck(2026,11,17,9,10,2026,0));
  Check("Lunar range 1900-2100",()=>{for(var d=new DateTime(1900,1,1);d<new DateTime(2101,1,1);d=d.AddDays(1)){var a=Lunar.Date(d);if(a[0]<1||a[0]>30||a[1]<1||a[1]>12)throw new Exception(d.ToString());}});
  Check("Memory + CPU native API",()=>{var m=new Native.Memory();if(!Native.GlobalMemoryStatusEx(m)||m.total==0)throw new Exception("Memory");long i,k,u;if(!Native.GetSystemTimes(out i,out k,out u))throw new Exception("CPU");});
  Check("Display modes read",()=>{if(Native.Modes().Count==0)throw new Exception("No mode");var m=Native.CurrentMode();log.AppendLine("Display "+m.width+"x"+m.height);});
  Check("Audio endpoints read",()=>{log.AppendLine("Audio endpoints: "+Audio.Devices(0).Count+"; Volume "+Audio.Volume());});
  Check("Repeated CPU popup and RAM double-click",()=>{
   using(var form=new Dashboard()){
    form.Show();Application.DoEvents();
    var flags=BindingFlags.NonPublic|BindingFlags.Instance;
    var header=(Control)typeof(Dashboard).GetField("header",flags).GetValue(form);
    var icons=header.Controls.OfType<HardwareIcon>().ToArray();
    var menu=(ContextMenuStrip)typeof(Dashboard).GetField("cpuMenu",flags).GetValue(form);
    var status=(Label)typeof(Dashboard).GetField("status",flags).GetValue(form);
    Action<Func<bool>> wait=ready=>{
     var watch=System.Diagnostics.Stopwatch.StartNew();
     while(!ready() && watch.ElapsedMilliseconds<15000){Application.DoEvents();System.Threading.Thread.Sleep(10);}
     if(!ready())throw new Exception("Timeout: "+status.Text);
    };
    for(int pass=0;pass<2;pass++){
     typeof(Control).GetMethod("OnClick",flags).Invoke(icons[0],new object[]{EventArgs.Empty});
     wait(()=>menu.Visible && menu.Items[2].Text.StartsWith("1."));
     menu.Close();Application.DoEvents();
     typeof(Control).GetMethod("OnMouseDoubleClick",flags).Invoke(icons[1],new object[]{new MouseEventArgs(MouseButtons.Left,2,5,5,0)});
     wait(()=>status.Text.Contains("RAM khả dụng"));
     if(!form.Visible || form.IsDisposed)throw new Exception("Dashboard closed");
    }
    form.Close();
   }
  });
  Check("Form render and inline calendar",()=>{using(var f=new Dashboard()){f.Show();Application.DoEvents();f.SavePreview(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"preview.png"));var method=typeof(Dashboard).GetMethod("Calendar",BindingFlags.NonPublic|BindingFlags.Instance);method.Invoke(f,null);Application.DoEvents();f.SavePreview(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"preview-calendar.png"));log.AppendLine("Working set (diagnostic, includes rendering): "+Math.Round(System.Diagnostics.Process.GetCurrentProcess().WorkingSet64/1048576.0)+" MB");f.Close();}});
  Check("Lid power configuration read only",()=>{log.AppendLine("Lid AC="+LidPower.Read(true)+" DC="+LidPower.Read(false));});
  try{var list=MixerSession.List();try{foreach(var x in list)log.AppendLine("Session: "+x.Name+" level="+x.Level+" mute="+x.Muted);log.AppendLine("PASS Mixer session enumeration read only");}finally{foreach(var x in list)x.Dispose();}}catch(Exception ex){if(ex.HResult==unchecked((int)0x80070005)||ex.Message.Contains("E_ACCESSDENIED"))log.AppendLine("SKIP Mixer session read: access denied in restricted environment");else{failed++;log.AppendLine("FAIL Mixer: "+ex);}}
  Check("v1.0.2 panels and power confirmation",()=>{using(var f=new Dashboard()){f.Show();Application.DoEvents();var flags=BindingFlags.NonPublic|BindingFlags.Instance;Action<string> open=n=>{typeof(Dashboard).GetMethod(n,flags).Invoke(f,null);Application.DoEvents();};var brightness=(Control)typeof(Dashboard).GetField("brightness",flags).GetValue(f);var volume=(Control)typeof(Dashboard).GetField("volume",flags).GetValue(f);if(brightness.Top!=volume.Top||brightness.Width!=volume.Width||brightness.Bounds.IntersectsWith(volume.Bounds))throw new Exception("Slider layout");open("Power");f.SavePreview(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"preview-power.png"));open("Outputs");f.SavePreview(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"preview-mixer.png"));open("Resolutions");f.SavePreview(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"preview-resolution.png"));typeof(Dashboard).GetMethod("ConfirmPower",flags).Invoke(f,new object[]{"Tắt máy",(Action)(()=>{throw new Exception("Power action should not execute");})});Application.DoEvents();f.SavePreview(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"preview-confirm.png"));open("Collapse");var header=(Control)typeof(Dashboard).GetField("header",flags).GetValue(f);var pin=header.Controls.OfType<CheckBox>().Single();pin.Checked=true;if(!f.TopMost)throw new Exception("Pin on top failed");pin.Checked=false;if(f.TopMost)throw new Exception("Unpin failed");open("Theme");f.Close();}});
  Check("Async scan result cannot overwrite another panel",()=>{using(var f=new Dashboard())using(var gate=new System.Threading.ManualResetEventSlim(false)){f.Show();Application.DoEvents();var flags=BindingFlags.NonPublic|BindingFlags.Instance;var task=(System.Threading.Tasks.Task)typeof(Dashboard).GetMethod("Work",flags).Invoke(f,new object[]{(Func<string>)(()=>{gate.Wait();return "Tìm thấy mạng cũ";}),null});typeof(Dashboard).GetMethod("Calendar",flags).Invoke(f,null);gate.Set();var watch=System.Diagnostics.Stopwatch.StartNew();while(!task.IsCompleted&&watch.ElapsedMilliseconds<3000){Application.DoEvents();System.Threading.Thread.Sleep(5);}if(!task.IsCompleted)throw new Exception("Async result timed out");var status=(Label)typeof(Dashboard).GetField("status",flags).GetValue(f);if(status.Text.Contains("mạng cũ"))throw new Exception("Stale scan message");typeof(Dashboard).GetField("dark",flags).SetValue(f,false);typeof(Dashboard).GetMethod("Theme",flags).Invoke(f,null);f.SavePreview(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"preview-light.png"));f.Close();}});
  Check("v1.0.2 compact drawer below fixed footer",()=>{using(var f=new Dashboard()){f.Show();Application.DoEvents();var flags=BindingFlags.NonPublic|BindingFlags.Instance;var footer=(Control)typeof(Dashboard).GetField("footer",flags).GetValue(f);var status=(Control)typeof(Dashboard).GetField("status",flags).GetValue(f);var tiles=(Control)typeof(Dashboard).GetField("tiles",flags).GetValue(f);var drawer=(Control)typeof(Dashboard).GetField("drawer",flags).GetValue(f);var footerBounds=footer.Bounds;var statusBounds=status.Bounds;var tileBounds=tiles.Bounds;var originalSize=f.ClientSize;foreach(string panel in new[]{"Calendar","Power","Outputs","Resolutions","Display","Extras","RepairPanel","Scale"}){typeof(Dashboard).GetMethod(panel,flags,null,Type.EmptyTypes,null).Invoke(f,null);Application.DoEvents();if(footer.Bounds!=footerBounds||status.Bounds!=statusBounds||tiles.Bounds!=tileBounds)throw new Exception("Main rows moved: "+panel);if(drawer.Top<footer.Bottom||drawer.Right>f.ClientSize.Width||drawer.Bottom>f.ClientSize.Height)throw new Exception("Drawer clipping: "+panel);if(f.ClientSize.Width!=originalSize.Width)throw new Exception("Side expansion: "+panel);if(panel=="Calendar")f.SavePreview(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"preview-calendar-bottom.png"));typeof(Dashboard).GetMethod("Collapse",flags).Invoke(f,null);Application.DoEvents();if(f.ClientSize!=originalSize)throw new Exception("Collapse did not restore compact size");}if(status.Top<tiles.Bottom||footer.Top<status.Bottom)throw new Exception("Base row overlap");log.AppendLine("Compact client size at current DPI: "+originalSize.Width+"x"+originalSize.Height);f.Close();}});
  try{log.AppendLine("INFO Airplane read only: "+Airplane.State(null));}catch(Exception ex){log.AppendLine("INFO Airplane unavailable: "+ex.Message);}
  try{log.AppendLine("INFO Scale read: "+DisplayScale.Current()+"%; "+string.Join(",",DisplayScale.Options()));}catch(Exception ex){log.AppendLine("INFO Scale unavailable: "+ex.Message);}
  try{log.AppendLine("INFO Wi-Fi scan count: "+Wifi.Scan().Count);}catch(Exception ex){log.AppendLine("INFO Wi-Fi unavailable: "+ex.Message);}
  try{log.AppendLine("INFO Hotspot read: "+WinRT.Hotspot(null,null,null).Trim());}catch(Exception ex){log.AppendLine("INFO Hotspot unavailable: "+ex.Message);}
  Check("Bluetooth watcher reopen and stop",()=>{
   using(var form=new Dashboard()){
    form.Show();Application.DoEvents();
    var flags=BindingFlags.Instance|BindingFlags.NonPublic;
    for(int i=0;i<2;i++){
     typeof(Dashboard).GetMethod("Bluetooth",flags).Invoke(form,null);
     Application.DoEvents();
     var watchers=(System.Collections.IList)typeof(Dashboard).GetField("btWatchers",flags).GetValue(form);
     var drawer=(Control)typeof(Dashboard).GetField("drawer",flags).GetValue(form);
     var list=drawer.Controls.OfType<ListBox>().Single();
     list.ClearSelected();
     var pairButton=drawer.Controls.OfType<FlowLayoutPanel>().SelectMany(row=>row.Controls.OfType<Button>()).Single(button=>button.Text=="Ghép đôi");
     pairButton.PerformClick();Application.DoEvents();
     if(!((Label)typeof(Dashboard).GetField("status",flags).GetValue(form)).Text.Contains("Chọn thiết bị"))throw new Exception("Pair selection feedback missing.");
     var clock=System.Diagnostics.Stopwatch.StartNew();
     while(clock.ElapsedMilliseconds<3500){Application.DoEvents();System.Threading.Thread.Sleep(20);}
     var state=(System.Threading.Tasks.Task)typeof(Dashboard).GetMethod("ShowBluetoothState",flags).Invoke(form,new object[]{list});
     while(!state.IsCompleted && clock.ElapsedMilliseconds<18000){Application.DoEvents();System.Threading.Thread.Sleep(20);}
     var status=(Label)typeof(Dashboard).GetField("status",flags).GetValue(form);
     log.AppendLine("INFO Bluetooth: "+status.Text+" | observed="+list.Items.Count);
     if(!state.IsCompleted)throw new Exception("Status did not return");
     if(Environment.GetEnvironmentVariable("BDK_TEST_J229AI")=="1"){
      Action<int> pump=ms=>{var until=System.Diagnostics.Stopwatch.StartNew();while(until.ElapsedMilliseconds<ms){Application.DoEvents();System.Threading.Thread.Sleep(20);}};
      Func<string,Button> btn=name=>drawer.Controls.OfType<FlowLayoutPanel>().SelectMany(row=>row.Controls.OfType<Button>()).FirstOrDefault(b=>b.Text==name);
      Func<object> findTarget=()=>list.Items.Cast<object>().FirstOrDefault(x=>x.ToString().StartsWith("J229AI •",StringComparison.OrdinalIgnoreCase));
      Action<string> click=name=>{
       btn(name).PerformClick();var watch=System.Diagnostics.Stopwatch.StartNew();
       do{pump(100);var confirm=btn("Xác nhận");
        if(confirm!=null && confirm.Visible && confirm.Parent.Controls.Cast<Control>().Any(c=>c.Text=="Cho phép ghép đôi với J229AI?"))confirm.PerformClick();
       }while((typeof(Dashboard).GetField("activePair",flags).GetValue(form)!=null || (bool)typeof(Dashboard).GetField("switchingBluetooth",flags).GetValue(form)) && watch.ElapsedMilliseconds<100000);
       log.AppendLine("LIVE "+name+" => "+status.Text);
      };
      click("Tắt Bluetooth");click("Bật Bluetooth");click("Quét lại");
      var search=System.Diagnostics.Stopwatch.StartNew();while(findTarget()==null && search.ElapsedMilliseconds<25000)pump(100);
      log.AppendLine("LIVE J229AI: "+(findTarget()??"không tìm thấy"));
      if(findTarget()!=null){
       list.SelectedItem=findTarget();click("Ghép đôi");pump(1500);
       if(findTarget()!=null && (findTarget().ToString().Contains("Đã ghép đôi") || findTarget().ToString().Contains("Đã kết nối") || status.Text.StartsWith("Đã ghép đôi với"))){list.SelectedItem=findTarget();click("Hủy ghép đôi");}
      }
     }
     if(watchers.Count!=2)throw new Exception("Bluetooth discovery did not start.");
     typeof(Dashboard).GetMethod("Collapse",flags).Invoke(form,null);
     if(watchers.Count!=0)throw new Exception("Bluetooth discovery still running.");
    }
    form.Close();
   }
  });
  log.AppendLine("Failures: "+failed);File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-results.txt"),log.ToString(),Encoding.UTF8);Environment.ExitCode=failed==0?0:1;
 }
}
