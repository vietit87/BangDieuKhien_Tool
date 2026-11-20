using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Windows.Devices.Enumeration;
using Windows.Devices.Bluetooth;
using Windows.Devices.Radios;

internal partial class Dashboard
{
    readonly List<DeviceWatcher> btWatchers = new List<DeviceWatcher>();
    int scanVersion;
    bool switchingBluetooth;

    void StopBluetoothScan()
    {
        scanVersion++;
        if(activePair!=null)activePair.Cancel();
        foreach (var watcher in btWatchers)
        {
            var state = watcher.Status;
            if (state == DeviceWatcherStatus.Started || state == DeviceWatcherStatus.EnumerationCompleted)
                watcher.Stop();
        }
        btWatchers.Clear();
    }

    void ScanBluetooth(ListBox list)
    {
        StopBluetoothScan();
        list.Items.Clear();
        int version = scanVersion;
        var context = new WindowsFormsSynchronizationContext();
        var devices = new Dictionary<string,DeviceInformation>();
        Action<Action> post = update => context.Post(unused => {
            if (!closing && version == scanVersion && !list.IsDisposed) update();
        }, null);
        Action<string> refresh = id => {
            var old = list.Items.Cast<BtItem>().FirstOrDefault(x => x.Id == id);
            DeviceInformation device;
            if (!devices.TryGetValue(id, out device) || string.IsNullOrWhiteSpace(device.Name)) {
                if (old != null) list.Items.Remove(old);
                return;
            }
            object connected, battery;
            device.Properties.TryGetValue("System.Devices.Aep.IsConnected",out connected);
            device.Properties.TryGetValue("System.Devices.BatteryLife",out battery);
            string info = true.Equals(connected) ? " • Đã kết nối" : device.Pairing.IsPaired ? " • Đã ghép đôi" : " • Chưa ghép đôi";
            int percent;
            if (int.TryParse(Convert.ToString(battery),out percent) && percent>=0 && percent<=100) info+=" • Pin "+percent+"%";
            var item = new BtItem {Id=id,Name=device.Name,Info=info,Connected=true.Equals(connected),Paired=device.Pairing.IsPaired};
            if (old == null) list.Items.Add(item);
            else list.Items[list.Items.IndexOf(old)] = item;
        };
        Status("Đang tìm thiết bị Bluetooth…");
        int completed=0;
        try {
            string[] queries={
                "("+BluetoothDevice.GetDeviceSelectorFromPairingState(true)+") OR ("+BluetoothDevice.GetDeviceSelectorFromPairingState(false)+")",
                "("+BluetoothLEDevice.GetDeviceSelectorFromPairingState(true)+") OR ("+BluetoothLEDevice.GetDeviceSelectorFromPairingState(false)+")"
            };
            foreach(string query in queries){
                var watcher=DeviceInformation.CreateWatcher(query,new[]{"System.Devices.Aep.IsConnected","System.Devices.BatteryLife"},DeviceInformationKind.AssociationEndpoint);
                watcher.Added+=(w,d)=>post(()=>{devices[d.Id]=d;refresh(d.Id);});
                watcher.Updated+=(w,u)=>post(()=>{DeviceInformation d;if(devices.TryGetValue(u.Id,out d)){d.Update(u);refresh(u.Id);}});
                watcher.Removed+=(w,u)=>post(()=>{devices.Remove(u.Id);refresh(u.Id);});
                watcher.EnumerationCompleted+=(w,e)=>post(()=>{if(++completed==2)Status("Đang theo dõi Bluetooth. Đưa thiết bị mới vào chế độ ghép đôi.");});
                watcher.Stopped+=(w,e)=>post(()=>{if(w.Status==DeviceWatcherStatus.Aborted)Status("Windows đã dừng quét. Kiểm tra Bluetooth rồi nhấn Quét lại.",true);});
                btWatchers.Add(watcher);
                watcher.Start();
            }
        } catch(Exception ex){StopBluetoothScan();Status(ex.GetBaseException().Message,true);}
    }

    static async Task<T> CompleteRadio<T>(Windows.Foundation.IAsyncOperation<T> operation,int timeout=12000)
    {
        var result=new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        operation.Completed=(op,state)=>{
            try { result.TrySetResult(op.GetResults()); }
            catch(Exception ex){result.TrySetException(ex);}
        };
        if(await Task.WhenAny(result.Task,Task.Delay(timeout))!=result.Task){
            operation.Cancel();
            throw new TimeoutException("Windows chưa phản hồi yêu cầu Bluetooth.");
        }
        return await result.Task;
    }
    async Task SetBluetooth(ListBox list,bool enabled)
    {
        if(switchingBluetooth || activePair!=null){Status("Đang xử lý Bluetooth. Vui lòng chờ.");return;}
        switchingBluetooth=true;
        int version=scanVersion;
        try {
            Status(enabled?"Đang bật Bluetooth…":"Đang tắt Bluetooth…");
            var access=await CompleteRadio(Radio.RequestAccessAsync());
            if(access!=RadioAccessStatus.Allowed)throw new Exception("Windows từ chối điều khiển Bluetooth: "+access);
            var radios=(await CompleteRadio(Radio.GetRadiosAsync())).Where(r=>r.Kind==RadioKind.Bluetooth).ToArray();
            if(radios.Length==0)throw new Exception("Windows không tìm thấy bộ điều hợp Bluetooth.");
            if(version!=scanVersion || closing)return;
            StopBluetoothScan();
            version=scanVersion;
            var target=enabled?RadioState.On:RadioState.Off;
            foreach(var radio in radios){
                if(radio.State!=target && await CompleteRadio(radio.SetStateAsync(target))!=RadioAccessStatus.Allowed)
                    throw new Exception("Windows từ chối đổi trạng thái Bluetooth.");
            }
            for(int i=0;i<30 && radios.Any(r=>r.State!=target);i++)await Task.Delay(100);
            if(radios.Any(r=>r.State!=target))throw new Exception("Windows chưa xác nhận trạng thái Bluetooth.");
            if(version!=scanVersion || closing)return;
            if(enabled)ScanBluetooth(list);
            else {list.Items.Clear();Status("Đã tắt Bluetooth.");}
        } catch(Exception ex){if(version==scanVersion && !closing)Status(ex.GetBaseException().Message,true);}
        finally{switchingBluetooth=false;}
    }

    async Task ShowBluetoothState(ListBox list)
    {
        int version=scanVersion;
        try {
            var radios=(await CompleteRadio(Radio.GetRadiosAsync())).Where(r=>r.Kind==RadioKind.Bluetooth);
            var states=radios.Select(r=>r.Name+" • "+(r.State==RadioState.On?"Đang bật":r.State==RadioState.Off?"Đã tắt":r.State.ToString())).ToArray();
            var names=new List<string>();
            foreach(var query in new[]{BluetoothDevice.GetDeviceSelectorFromPairingState(true),BluetoothLEDevice.GetDeviceSelectorFromPairingState(true)}){
                var found=await CompleteRadio(DeviceInformation.FindAllAsync(query,null,DeviceInformationKind.AssociationEndpoint));
                names.AddRange(found.Where(d=>d.Pairing.IsPaired).Select(d=>string.IsNullOrWhiteSpace(d.Name)?d.Id:d.Name));
            }
            if(version!=scanVersion || closing || list.IsDisposed)return;
            var visible=list.Items.Cast<BtItem>().ToArray();
            var connectedNames=visible.Where(x=>x.Connected).Select(x=>x.Name).Distinct(StringComparer.CurrentCultureIgnoreCase).ToArray();
            names.AddRange(visible.Where(x=>x.Paired).Select(x=>x.Name));
            string paired=names.Count==0?"Chưa có thiết bị ghép đôi.":"Đã ghép đôi: "+string.Join(", ",names.Distinct(StringComparer.CurrentCultureIgnoreCase));
            if(connectedNames.Length>0)paired="Đang kết nối: "+string.Join(", ",connectedNames)+(names.Count==0?"":" | "+paired);
            if(version==scanVersion && !closing)Status(states.Length==0?"Windows không tìm thấy bộ điều hợp Bluetooth.":string.Join(", ",states)+" | "+paired);
        } catch(Exception ex){if(version==scanVersion && !closing)Status(ex.GetBaseException().Message,true);}
    }
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit,Size=560)]
    struct ClassicDeviceInfo{
        [System.Runtime.InteropServices.FieldOffset(0)]public uint Size;
        [System.Runtime.InteropServices.FieldOffset(8)]public ulong Address;
    }
    [System.Runtime.InteropServices.DllImport("bthprops.cpl",ExactSpelling=true)]
    static extern uint BluetoothRemoveDevice(ref ulong address);
    [System.Runtime.InteropServices.DllImport("bthprops.cpl",ExactSpelling=true)]
    static extern uint BluetoothAuthenticateDeviceEx(IntPtr owner,IntPtr radio,ref ClassicDeviceInfo device,IntPtr oob,int requirements);
    static string PairClassic(IntPtr owner,ulong address){
        var device=new ClassicDeviceInfo{Size=560,Address=address};
        uint code=BluetoothAuthenticateDeviceEx(owner,IntPtr.Zero,ref device,IntPtr.Zero,4);
        if(code==0 || code==1247)return "Paired";
        if(code==170 || code==997)return "OperationAlreadyInProgress";
        if(code==259)return "ClassicAuthenticated";
        throw new System.ComponentModel.Win32Exception((int)code);
    }
    CancellationTokenSource activePair;
    async Task PairBluetooth(ListBox list, FlowLayoutPanel prompt, Label question, TextBox pin, Button confirm, Button cancel,bool unpair=false)
    {
        if(activePair!=null || switchingBluetooth){Status("Đang xử lý Bluetooth. Vui lòng chờ.");return;}
        var selected=list.SelectedItem as BtItem;
        if(selected==null){Status("Chọn thiết bị cần ghép đôi.",true);return;}
        int version=scanVersion;
        var context=new WindowsFormsSynchronizationContext();
        var stop=new CancellationTokenSource();
        activePair=stop;
        EventHandler disposed=(s,e)=>stop.Cancel();
        list.Disposed+=disposed;
        EventHandler cancelClick=(s,e)=>stop.Cancel();
        cancel.Click+=cancelClick;
        Windows.Foundation.IAsyncOperation<DevicePairingResult> operation=null;
        DeviceInformationCustomPairing custom=null;
        Windows.Foundation.TypedEventHandler<DeviceInformationCustomPairing,DevicePairingRequestedEventArgs> handler=null;
        try {
            Status((unpair?"Đang hủy ghép đôi với ":"Đang ghép đôi với ")+selected.Name+"…");
            var device=await CompleteRadio(DeviceInformation.CreateFromIdAsync(selected.Id,new[]{"System.Devices.Aep.ProtocolId","System.Devices.Aep.DeviceAddress"},DeviceInformationKind.AssociationEndpoint));
            if(stop.IsCancellationRequested || version!=scanVersion)return;
            if(device==null)throw new Exception("Thiết bị đã rời khỏi phạm vi. Hãy quét lại.");
            object protocol,address;
            ulong classicAddress=0;
            bool classic=device.Properties.TryGetValue("System.Devices.Aep.ProtocolId",out protocol) &&
                string.Equals(Convert.ToString(protocol).Trim('{','}'),"e0cbf06c-cd8b-4647-bb8a-263b43f0f974",StringComparison.OrdinalIgnoreCase) &&
                device.Properties.TryGetValue("System.Devices.Aep.DeviceAddress",out address) &&
                ulong.TryParse(Convert.ToString(address).Replace(":","").Replace("-",""),System.Globalization.NumberStyles.HexNumber,System.Globalization.CultureInfo.InvariantCulture,out classicAddress) && classicAddress!=0;
            custom=device.Pairing.Custom;
            handler=(sender,args)=>{
                var deferral=args.GetDeferral();
                context.Post(async unused=>{
                    EventHandler accept=null;
                    try {
                        if(stop.IsCancellationRequested || list.IsDisposed || version!=scanVersion)return;
                        var answer=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                        var kind=args.PairingKind;
                        pin.Visible=kind==DevicePairingKinds.ProvidePin;
                        pin.Clear();
                        question.Text=kind==DevicePairingKinds.ProvidePin?"Nhập PIN của "+selected.Name:
                            kind==DevicePairingKinds.ConfirmPinMatch?"Hai thiết bị cùng hiện mã "+args.Pin+"?":
                            kind==DevicePairingKinds.DisplayPin?"Nhập "+args.Pin+" trên "+selected.Name+" rồi xác nhận.":"Cho phép ghép đôi với "+selected.Name+"?";
                        confirm.Text=kind==DevicePairingKinds.DisplayPin?"Đã nhập":"Xác nhận";
                        accept=(s,e)=>{if(!pin.Visible || pin.Text.Length>0)answer.TrySetResult(true);};
                        confirm.Click+=accept;
                        prompt.Visible=true;
                        drawer.ScrollControlIntoView(prompt);
                        if(pin.Visible)pin.Focus();
                        using(stop.Token.Register(()=>answer.TrySetResult(false))){
                            if(await answer.Task && !list.IsDisposed && version==scanVersion){
                                if(kind==DevicePairingKinds.ProvidePin)args.Accept(pin.Text);
                                else args.Accept();
                            }
                        }
                    } catch(Exception ex){if(!list.IsDisposed && version==scanVersion)Status(ex.Message,true);}
                    finally{if(accept!=null)confirm.Click-=accept;if(!prompt.IsDisposed)prompt.Visible=false;deferral.Complete();}
                },null);
            };
            if(!classic && !unpair)custom.PairingRequested+=handler;else handler=null;
            var current=device;
            if(unpair && classic && !current.Pairing.IsPaired){
                uint removed=await Task.Run(()=>BluetoothRemoveDevice(ref classicAddress));
                if(removed!=0 && removed!=1168)throw new System.ComponentModel.Win32Exception((int)removed);
            }
            if(current.Pairing.IsPaired==unpair){
                stop.Token.ThrowIfCancellationRequested();
                string outcome;
                if(unpair){
                    var removal=current.Pairing.UnpairAsync();
                    using(stop.Token.Register(()=>removal.Cancel()))
                        outcome=(await CompleteRadio(removal,30000)).Status.ToString();
                }else if(classic){
                    var owner=Handle;
                    Status("Xác nhận ghép đôi "+selected.Name+" trong cửa sổ Windows nếu được hỏi.");
                    outcome=await Task.Run(()=>PairClassic(owner,classicAddress));
                    if(outcome=="ClassicAuthenticated")throw new Exception("Windows đã lưu xác thực "+selected.Name+". Chọn Hủy ghép đôi để xóa bản ghi cũ, rồi ghép lại.");
                }else{
                    operation=custom.PairAsync(DevicePairingKinds.ConfirmOnly|DevicePairingKinds.ProvidePin|DevicePairingKinds.DisplayPin|DevicePairingKinds.ConfirmPinMatch);
                    using(stop.Token.Register(()=>operation.Cancel()))
                        outcome=(await CompleteRadio(operation,90000)).Status.ToString();
                }
                bool pending=outcome=="OperationAlreadyInProgress";
                bool accepted=unpair?(outcome=="Unpaired" || outcome=="AlreadyUnpaired"):(outcome=="Paired" || outcome=="AlreadyPaired");
                if(!pending && !accepted)throw new Exception(selected.Name+" • Windows: "+outcome);
                bool confirmed=false;
                for(int poll=0;poll<(pending?80:20);poll++){
                    await Task.Delay(300,stop.Token);
                    current=await CompleteRadio(DeviceInformation.CreateFromIdAsync(selected.Id,null,DeviceInformationKind.AssociationEndpoint));
                    confirmed=current==null?unpair:current.Pairing.IsPaired!=unpair;
                    if(confirmed)break;
                }
                if(!confirmed)throw new Exception(selected.Name+" • Windows chưa xác nhận trạng thái ("+outcome+").");
            }
            if(!list.IsDisposed && version==scanVersion){
                activePair=null;ScanBluetooth(list);Status((unpair?"Đã hủy ghép đôi với ":"Đã ghép đôi với ")+selected.Name+".");
            }
        } catch(Exception ex){if(!list.IsDisposed && version==scanVersion)Status(stop.IsCancellationRequested?"Đã hủy ghép đôi.":ex.GetBaseException().Message,true);}
        finally{
            if(custom!=null && handler!=null)custom.PairingRequested-=handler;
            stop.Cancel();
            list.Disposed-=disposed;cancel.Click-=cancelClick;
            stop.Dispose();
            if(!prompt.IsDisposed)prompt.Visible=false;
            activePair=null;
        }
    }
    void Bluetooth()
    {
        if(!Expand("Bluetooth • Thiết bị",280))return;
        var list=new ListBox{Width=650,Height=100,IntegralHeight=false};
        Actions(Button("Bật Bluetooth",async()=>await SetBluetooth(list,true),150),
                Button("Tắt Bluetooth",async()=>await SetBluetooth(list,false),150),
                Button("Quét lại",()=>{if(!switchingBluetooth && activePair==null)ScanBluetooth(list);},125),Button("Xem trạng thái",async()=>await ShowBluetoothState(list),150));
        drawer.Controls.Add(list);
        var prompt=new FlowLayoutPanel{Width=650,Height=72,Visible=false};
        var question=Label("");question.Width=630;
        var pin=new TextBox{Width=130};
        var confirm=Button("Xác nhận",()=>{},105);
        var cancel=Button("Hủy",()=>{},70);
        prompt.Controls.AddRange(new Control[]{question,pin,confirm,cancel});
        Actions(Button("Ghép đôi",async()=>await PairBluetooth(list,prompt,question,pin,confirm,cancel),170),
                Button("Hủy ghép đôi",async()=>await PairBluetooth(list,prompt,question,pin,confirm,cancel,true),170),
                Button("Cài đặt / Kết nối ↗",()=>Native.Open("ms-settings:bluetooth"),215));

        drawer.Controls.Add(prompt);
        Note("Pin chỉ hiện khi thiết bị/driver công bố. Kết nối âm thanh dùng mục Đầu ra; khi ghép đôi, làm theo hướng dẫn hiện trong bảng.",42);
        Theme();
        ScanBluetooth(list);
    }
}
