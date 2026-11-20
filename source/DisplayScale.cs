using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
// DisplayConfig DPI packets used by Windows 10/11. They are not a public API contract:
// only expose values if the driver successfully answers the query; no registry fallback.
internal static class DisplayScale {
 [StructLayout(LayoutKind.Sequential)]struct Luid {public uint low;public int high;}
 [StructLayout(LayoutKind.Sequential)]struct Source {public Luid adapter;public uint id,mode,flags;}
 [StructLayout(LayoutKind.Sequential)]struct Rational {public uint numerator,denominator;}
 [StructLayout(LayoutKind.Sequential)]struct Target {public Luid adapter;public uint id,mode,technology,rotation,scaling;public Rational rate;public uint scan;public int available;public uint flags;}
 [StructLayout(LayoutKind.Sequential)]struct Path {public Source source;public Target target;public uint flags;}
 [StructLayout(LayoutKind.Sequential)]struct Header {public int type;public uint size;public Luid adapter;public uint id;}
 [StructLayout(LayoutKind.Sequential)]struct Get {public Header header;public int min,current,max;}
 [StructLayout(LayoutKind.Sequential)]struct Set {public Header header;public int value;}
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct Name {public Header header;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)]public string name;}
 [DllImport("user32.dll")]static extern int GetDisplayConfigBufferSizes(uint flags,out uint paths,out uint modes);
 [DllImport("user32.dll")]static extern int QueryDisplayConfig(uint flags,ref uint paths,[Out] Path[] list,ref uint modes,IntPtr modeList,IntPtr topology);
 [DllImport("user32.dll",EntryPoint="DisplayConfigGetDeviceInfo")]static extern int GetDpi(ref Get d);
 [DllImport("user32.dll",EntryPoint="DisplayConfigGetDeviceInfo",CharSet=CharSet.Unicode)]static extern int GetName(ref Name d);
 [DllImport("user32.dll",EntryPoint="DisplayConfigSetDeviceInfo")]static extern int SetDpi(ref Set d);
 static int[] Values={100,125,150,175,200,225,250,300,350,400,450,500};
 static Get Read(){uint n,m;int r=GetDisplayConfigBufferSizes(2,out n,out m);if(r!=0)throw new Exception("Không đọc được cấu hình DisplayConfig: "+r);var list=new Path[n];IntPtr p=Marshal.AllocHGlobal(checked((int)m*64));try{r=QueryDisplayConfig(2,ref n,list,ref m,p,IntPtr.Zero);if(r!=0)throw new Exception("DisplayConfig thay đổi hoặc không khả dụng: "+r);for(int i=0;i<n;i++){var h=new Header{type=1,size=(uint)Marshal.SizeOf(typeof(Name)),adapter=list[i].source.adapter,id=list[i].source.id};var name=new Name{header=h};if(GetName(ref name)!=0)continue;if(name.name!=System.Windows.Forms.Screen.PrimaryScreen.DeviceName)continue;var g=new Get{header=new Header{type=-3,size=32,adapter=h.adapter,id=h.id}};r=GetDpi(ref g);if(r==0&&g.min<=0&&g.max>=g.current&&g.current>=g.min&&-g.min<Values.Length)return g;throw new Exception("Driver không hỗ trợ đổi scale trực tiếp ("+r+").");}throw new Exception("Không tìm được màn hình chính.");}finally{Marshal.FreeHGlobal(p);}}
 public static int Current(){Get g=Read();int i=-g.min+g.current;if(i<0||i>=Values.Length)throw new Exception("Scale ngoài dải hỗ trợ.");return Values[i];}
 public static List<int> Options(){Get g=Read();var r=new List<int>();int recommended=-g.min;for(int i=Math.Max(0,recommended+g.min);i<=Math.Min(Values.Length-1,recommended+g.max);i++)r.Add(Values[i]);return r;}
 public static void Apply(int percent){Get g=Read();int i=Array.IndexOf(Values,percent);int relative=i+g.min;if(i<0||relative<g.min||relative>g.max)throw new Exception("Tỷ lệ không được màn hình hỗ trợ.");Set s=new Set{header=new Header{type=-4,size=24,adapter=g.header.adapter,id=g.header.id},value=relative};int r=SetDpi(ref s);if(r!=0)throw new Exception("Windows không áp dụng được scale: "+r);}
}
