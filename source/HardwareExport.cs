using System;
using System.Diagnostics;
using System.IO;
using System.Text;

internal static class HardwareExport
{
    public static string Save()
    {
        return SaveIn(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
    }

    internal static string SaveIn(string desktop)
    {
        if(!Directory.Exists(desktop))throw new DirectoryNotFoundException("Không tìm thấy thư mục Desktop.");
        string script;
        using(var reader=new StreamReader(typeof(HardwareExport).Assembly.GetManifestResourceStream("HardwareReport.ps1"),Encoding.UTF8))
            script=reader.ReadToEnd();
        string file=Path.Combine(desktop,Environment.MachineName+"_"+DateTime.Now.ToString("yyyy-MM-dd")+".txt");
        var start=new ProcessStartInfo(Path.Combine(Environment.SystemDirectory,@"WindowsPowerShell\v1.0\powershell.exe"),
            "-NoProfile -NonInteractive -EncodedCommand "+Convert.ToBase64String(Encoding.Unicode.GetBytes(script)));
        start.CreateNoWindow=true;start.UseShellExecute=false;
        start.RedirectStandardOutput=true;start.RedirectStandardError=true;
        start.StandardOutputEncoding=Encoding.UTF8;
        start.EnvironmentVariables["BDK_REPORT_DESKTOP"]=desktop;
        using(var process=Process.Start(start)){
            var output=process.StandardOutput.ReadToEndAsync();
            var errors=process.StandardError.ReadToEndAsync();
            if(!process.WaitForExit(120000)){process.Kill();throw new TimeoutException("Quá thời gian đọc cấu hình máy.");}
            if(process.ExitCode!=0 || string.IsNullOrWhiteSpace(output.Result))throw new IOException("Xuất cấu hình thất bại. "+errors.Result);
            string temp=Path.Combine(desktop,Guid.NewGuid().ToString("N")+".tmp");
            try{
                File.WriteAllText(temp,output.Result,new UTF8Encoding(true));
                if(File.Exists(file))File.Replace(temp,file,null);else File.Move(temp,file);
            }finally{if(File.Exists(temp))File.Delete(temp);}
        }
        return "Đã lưu cấu hình trên Desktop: "+file;
    }
}
