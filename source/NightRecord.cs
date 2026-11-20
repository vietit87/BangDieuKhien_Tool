using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;

internal sealed class NightRecord
{
    const string Root = @"Software\Microsoft\Windows\CurrentVersion\CloudStore\Store\DefaultAccount\Current\";
    static readonly byte[] Magic = {67,66,1,0};
    readonly SortedDictionary<int,byte[]> fields = new SortedDictionary<int,byte[]>();
    readonly byte[] original;
    readonly ulong timestamp;
    readonly bool state;
    static void Require(bool valid)
    {
        if (!valid) throw new InvalidDataException("Windows dùng định dạng Ánh sáng đêm chưa được hỗ trợ.");
    }
    static ulong Number(BinaryReader reader)
    {
        ulong result = 0;
        for (int shift=0; shift<64; shift+=7) {
            byte b=reader.ReadByte();
            Require(shift<63 || b<=1);
            result |= (ulong)(b & 127)<<shift;
            if (b<128) return result;
        }
        throw new InvalidDataException();
    }
    static byte[] Encode(ulong value)
    {
        var bytes=new List<byte>();
        while(value>=128){bytes.Add((byte)((value & 127)|128));value>>=7;}
        bytes.Add((byte)value);
        return bytes.ToArray();
    }
    static void Expect(BinaryReader r, params byte[] pattern)
    {
        Require(r.ReadBytes(pattern.Length).SequenceEqual(pattern));
    }
    internal NightRecord(byte[] data,bool isState)
    {
        original=data;state=isState;
        using(var r=new BinaryReader(new MemoryStream(data))) {
            Expect(r,67,66,1,0,10,2,1,0,42,6);
            timestamp=Number(r);
            Expect(r,42,43,14);
            int length=checked((int)Number(r));
            Require(length>=5 && length==data.Length-r.BaseStream.Position-3);
            long end=r.BaseStream.Position+length;
            Expect(r,Magic);
            while(r.BaseStream.Position<end){
                int start=(int)r.BaseStream.Position;
                byte header=r.ReadByte();
                if(header==0)break;
                int id=header>>5;
                if(id==6)id=r.ReadByte();
                else Require(id<6);
                int type=header&31;
                if(type==2){Require(r.ReadByte()<=1);}
                else if(type==6||type==15||type==16){Number(r);}
                else if(type==10){
                    byte field;
                    while((field=r.ReadByte())!=0){Require(field==14||field==46);r.ReadByte();}
                }else Require(false);
                Require(!fields.ContainsKey(id));
                fields.Add(id,data.Skip(start).Take((int)r.BaseStream.Position-start).ToArray());
            }
            Require(r.BaseStream.Position==end && data[end-1]==0);
            Expect(r,0,0,0);
        }
    }
    static string PathFor(bool state)
    {
        string name="windows.data.bluelightreduction."+(state?"bluelightreductionstate":"settings");
        return Root+"default$"+name+"\\"+name;
    }
    public static NightRecord Read(bool state)
    {
        using(var key=Registry.CurrentUser.OpenSubKey(PathFor(state))){
            var data=key==null?null:key.GetValue("Data") as byte[];
            if(data==null)throw new IOException("Windows chưa có cấu hình Ánh sáng đêm.");
            return new NightRecord(data,state);
        }
    }
    public bool Enabled {
        get {
            byte[] v;
            if(fields.TryGetValue(30,out v)&&v.SequenceEqual(new byte[]{194,30,0}))
                throw new InvalidOperationException("Màn hình hoặc driver chưa hỗ trợ Ánh sáng đêm.");
            if(!fields.TryGetValue(0,out v))return false;
            Require(v.SequenceEqual(new byte[]{16,0}));return true;
        }
    }
    public void SetEnabled(bool on)
    {
        if(on)fields[0]=new byte[]{16,0};else fields.Remove(0);
        fields[10]=new byte[]{208,10,2};
        fields[20]=new byte[]{198,20}.Concat(Encode((ulong)DateTime.UtcNow.ToFileTimeUtc())).ToArray();
    }
    public int Strength {
        get {
            byte[] field;
            if(!fields.TryGetValue(40,out field))return 0;
            using(var r=new BinaryReader(new MemoryStream(field))){
                Expect(r,207,40);ulong raw=Number(r);
                Require(raw>=2400 && raw<=13000 && raw%2==0);
                return (int)Math.Round((6500-(int)(raw/2))/53.0);
            }
        }
        set {
            Require(value>=0 && value<=100);
            fields[40]=new byte[]{207,40}.Concat(Encode((ulong)((6500-53*value)*2))).ToArray();
        }
    }
    internal byte[] Serialize()
    {
        var body=new List<byte>(Magic);
        foreach(byte[] field in fields.Values)body.AddRange(field);
        body.Add(0);
        var outer=new List<byte>(original.Take(10));
        ulong now=(ulong)(DateTime.UtcNow.ToFileTimeUtc()/10000000-11644473600L);
        outer.AddRange(Encode(Math.Max(now,checked(timestamp+2))));
        outer.AddRange(new byte[]{42,43,14});
        outer.AddRange(Encode((ulong)body.Count));outer.AddRange(body);
        outer.AddRange(new byte[3]);return outer.ToArray();
    }
    public void Save()
    {
        using(var key=Registry.CurrentUser.OpenSubKey(PathFor(state),true)){
            var current=key==null?null:key.GetValue("Data") as byte[];
            if(current==null||!current.SequenceEqual(original))throw new IOException("Cấu hình vừa thay đổi. Hãy thử lại.");
            key.SetValue("Data",Serialize(),RegistryValueKind.Binary);
        }
    }
}
