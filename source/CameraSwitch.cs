using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

internal static class CameraSwitch {
 const string cameraClass="{ca3e7ab9-b4c3-4ae6-8251-579ef933890f}";
 const uint presentClass=0x300;
 [DllImport("cfgmgr32.dll",CharSet=CharSet.Unicode)] static extern uint CM_Get_Device_ID_List_SizeW(out uint length,string filter,uint flags);
 [DllImport("cfgmgr32.dll",CharSet=CharSet.Unicode)] static extern uint CM_Get_Device_ID_ListW(string filter,[Out]char[] buffer,uint length,uint flags);
 [DllImport("cfgmgr32.dll",CharSet=CharSet.Unicode)] static extern uint CM_Locate_DevNodeW(out uint node,string id,uint flags);
 [DllImport("cfgmgr32.dll")] static extern uint CM_Get_DevNode_Status(out uint status,out uint problem,uint node,uint flags);
 [DllImport("cfgmgr32.dll")] static extern uint CM_Disable_DevNode(uint node,uint flags);
 [DllImport("cfgmgr32.dll")] static extern uint CM_Enable_DevNode(uint node,uint flags);
 static void Check(uint result){if(result!=0)throw new InvalidOperationException("Windows trả mã PnP 0x"+result.ToString("X")+".");}
 static Dictionary<uint,uint> Devices(){
  uint size;Check(CM_Get_Device_ID_List_SizeW(out size,cameraClass,presentClass));
  var buffer=new char[size];Check(CM_Get_Device_ID_ListW(cameraClass,buffer,size,presentClass));
  var list=new Dictionary<uint,uint>();
  foreach(string id in new string(buffer).Split(new[]{'\0'},StringSplitOptions.RemoveEmptyEntries)){
   uint node,status,problem;Check(CM_Locate_DevNodeW(out node,id,0));Check(CM_Get_DevNode_Status(out status,out problem,node,0));list[node]=problem;
  }
  if(list.Count==0)throw new InvalidOperationException("Windows chưa nhận camera.");
  return list;
 }
 internal static bool IsOff(){var devices=Devices();if(devices.Values.Any(p=>p!=0&&p!=22))throw new InvalidOperationException("Kiểm tra driver camera trong Device Manager.");return devices.Values.All(p=>p==22);}
 internal static bool Toggle(){
  var devices=Devices();bool disable=devices.Values.Contains(0u);
  var changed=new List<uint>();
  try{
   foreach(var d in devices.Where(x=>x.Value==(disable?0u:22u))){
    Check(disable?CM_Disable_DevNode(d.Key,8):CM_Enable_DevNode(d.Key,0));changed.Add(d.Key);
   }
   if(IsOff()!=disable)throw new InvalidOperationException("Windows chưa xác nhận thay đổi camera.");
   return disable;
  }catch{foreach(var node in changed){if(disable)CM_Enable_DevNode(node,0);else CM_Disable_DevNode(node,8);}throw;}
 }
}
