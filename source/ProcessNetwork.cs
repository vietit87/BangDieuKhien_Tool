using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

internal sealed class ProcessNetwork : IDisposable {
 // Layouts of EVENT_TRACE_PROPERTIES, EVENT_TRACE_LOGFILEW and EVENT_RECORD in the x64 SDK.
 const int PropsSize=1024, LogSize=448;
 static Guid provider=new Guid("7dd42a49-5329-4832-8dfd-43d979153a88");
 delegate void Record(IntPtr data);
 [DllImport("advapi32",CharSet=CharSet.Unicode)]static extern uint StartTraceW(out ulong handle,string name,IntPtr properties);
 [DllImport("advapi32",CharSet=CharSet.Unicode)]static extern uint ControlTraceW(ulong handle,string name,IntPtr properties,uint control);
 [DllImport("advapi32")]static extern uint EnableTraceEx2(ulong handle,ref Guid provider,uint control,byte level,ulong any,ulong all,uint timeout,IntPtr parameters);
 [DllImport("advapi32",CharSet=CharSet.Unicode,SetLastError=true)]static extern ulong OpenTraceW(IntPtr log);
 [DllImport("advapi32")]static extern uint ProcessTrace(ulong[] traces,uint count,IntPtr start,IntPtr end);
 [DllImport("advapi32")]static extern uint CloseTrace(ulong trace);
 readonly Dictionary<int,long[]> bytes=new Dictionary<int,long[]>();
 readonly Stopwatch elapsed=new Stopwatch();
 readonly Record record;
 IntPtr props,log,namePtr;ulong session,reader=ulong.MaxValue;Thread worker;
 static IntPtr Allocate(int size){var p=Marshal.AllocHGlobal(size);Marshal.Copy(new byte[size],0,p,size);return p;}
 static void Check(uint error){if(error!=0)throw new Win32Exception((int)error);}
 public ProcessNetwork(){
  record=OnRecord;
  try{
   string name="BangDieuKhien.Network."+Process.GetCurrentProcess().SessionId;
   props=Allocate(PropsSize);Marshal.WriteInt32(props,PropsSize);Marshal.WriteInt32(props,44,0x20000);Marshal.WriteInt32(props,64,0x100);Marshal.WriteInt32(props,68,1);Marshal.WriteInt32(props,116,120);
   ulong handle;uint result=StartTraceW(out handle,name,props);
   if(result==183){Check(ControlTraceW(0,name,props,1));result=StartTraceW(out handle,name,props);}
   Check(result);session=handle;
   log=Allocate(LogSize);namePtr=Marshal.StringToHGlobalUni(name);Marshal.WriteIntPtr(log,8,namePtr);Marshal.WriteInt32(log,28,0x10000100);Marshal.WriteIntPtr(log,424,Marshal.GetFunctionPointerForDelegate(record));
   reader=OpenTraceW(log);if(reader==ulong.MaxValue)throw new Win32Exception(Marshal.GetLastWin32Error());
   Check(EnableTraceEx2(session,ref provider,1,5,0,0,0,IntPtr.Zero));
   elapsed.Start();worker=new Thread(()=>ProcessTrace(new[]{reader},1,IntPtr.Zero,IntPtr.Zero)){IsBackground=true};worker.Start();
  }catch{Dispose();throw;}
 }
 void OnRecord(IntPtr data){
  int id=(ushort)Marshal.ReadInt16(data,40);
  bool send=id==10||id==26||id==42||id==58;
  if(!send&&id!=11&&id!=27&&id!=43&&id!=59)return;
  if((ushort)Marshal.ReadInt16(data,86)<8)return;
  var payload=Marshal.ReadIntPtr(data,96);int pid=Marshal.ReadInt32(payload);
  lock(bytes){long[] pair;if(!bytes.TryGetValue(pid,out pair))bytes[pid]=pair=new long[2];pair[send?1:0]+=(uint)Marshal.ReadInt32(payload,4);}
 }
 public string[] Sample(){
  KeyValuePair<int,long[]>[] rows;double sec;
  lock(bytes){rows=bytes.ToArray();bytes.Clear();sec=Math.Max(.001,elapsed.Elapsed.TotalSeconds);elapsed.Restart();}
  return rows.OrderByDescending(r=>r.Value[0]+r.Value[1]).ThenBy(r=>r.Key).Select(r=>{
   string name="PID "+r.Key;try{using(var p=Process.GetProcessById(r.Key))name=p.ProcessName+" ("+r.Key+")";}catch(ArgumentException){}catch(InvalidOperationException){}
   return string.Format("{0,-30}  ↓ {1,10:N1} KB/s    ↑ {2,10:N1} KB/s",name,r.Value[0]/1024/sec,r.Value[1]/1024/sec);
  }).ToArray();
 }
 public void Dispose(){
  if(session!=0){ControlTraceW(session,null,props,1);session=0;}
  if(reader!=ulong.MaxValue){CloseTrace(reader);if(worker!=null)worker.Join();reader=ulong.MaxValue;}
  foreach(var p in new[]{props,log,namePtr})if(p!=IntPtr.Zero)Marshal.FreeHGlobal(p);props=log=namePtr=IntPtr.Zero;
 }
}
