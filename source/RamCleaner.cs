using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

internal sealed class HardwareIcon : Control
{
    readonly bool memory;

    public HardwareIcon(bool memory)
    {
        this.memory = memory;
        AccessibleName = memory ? "RAM – nhấn đúp để dọn bộ nhớ" : "CPU";
        SetStyle(ControlStyles.StandardClick | ControlStyles.StandardDoubleClick |
                 ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        if (memory) Cursor = Cursors.Hand;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.ScaleTransform(Width / 24f, Height / 28f);
        using (var pen = new Pen(ForeColor, 1.25f))
        {
            if (memory)
            {
                g.DrawRectangle(pen, 2, 8, 20, 11);
                for (int x = 5; x < 20; x += 5) g.DrawRectangle(pen, x, 11, 3, 4);
                for (int x = 4; x < 21; x += 3) g.DrawLine(pen, x, 19, x, 22);
            }
            else
            {
                g.DrawRectangle(pen, 5, 7, 14, 14);
                g.DrawRectangle(pen, 8, 10, 8, 8);
                for (int x = 8; x < 18; x += 4)
                {
                    g.DrawLine(pen, x, 4, x, 7);
                    g.DrawLine(pen, x, 21, x, 24);
                    g.DrawLine(pen, 2, x+2, 5, x+2);
                    g.DrawLine(pen, 19, x+2, 22, x+2);
                }
            }
        }
    }
}

internal static class RamCleaner
{
    [DllImport("ntdll.dll")]
    static extern int NtSetSystemInformation(int informationClass, ref int command, int length);
    [DllImport("advapi32.dll", SetLastError=true)]
    static extern bool ImpersonateSelf(int level);
    [DllImport("advapi32.dll", SetLastError=true)]
    static extern bool RevertToSelf();
    [DllImport("ntdll.dll")]
    static extern int RtlAdjustPrivilege(uint privilege, byte enable, byte thread, out byte previous);
    [DllImport("kernel32.dll", SetLastError=true)]
    static extern bool SetSystemFileCacheSize(UIntPtr minimum, UIntPtr maximum, uint flags);

    static void TrimList(int command, string name, List<string> errors)
    {
        int status = NtSetSystemInformation(80, ref command, sizeof(int));
        if (status < 0) errors.Add(name + " (0x" + status.ToString("X8") + ")");
    }

    internal static void Worker()
    {
        try
        {
            string result = Run();
            using (var writer = new System.IO.StreamWriter(Console.OpenStandardOutput(), new System.Text.UTF8Encoding(false)))
                writer.WriteLine(result);
        }
        catch (Exception) { Environment.ExitCode = 1; }
    }

    internal static string RunIsolated()
    {
        var start = new System.Diagnostics.ProcessStartInfo(Application.ExecutablePath, "--clean-ram-worker");
        start.UseShellExecute = false;
        start.RedirectStandardOutput = true;
        start.StandardOutputEncoding = System.Text.Encoding.UTF8;
        start.CreateNoWindow = true;
        using (var worker = System.Diagnostics.Process.Start(start))
        {
            if (!worker.WaitForExit(60000))
            {
                if (!worker.HasExited) worker.Kill();
                throw new TimeoutException("Dọn RAM quá thời gian chờ.");
            }
            if (worker.ExitCode != 0)
                throw new InvalidOperationException("Dọn RAM bị gián đoạn (0x" + worker.ExitCode.ToString("X8") + ").");
            return worker.StandardOutput.ReadToEnd().Trim();
        }
    }
    public static string Run()
    {
        var before = new Native.Memory();
        bool measured = Native.GlobalMemoryStatusEx(before);
        var errors = new List<string>();
        if (!ImpersonateSelf(2)) throw new Win32Exception(Marshal.GetLastWin32Error());
        try
        {
            byte previous;
            if (RtlAdjustPrivilege(13, 1, 1, out previous) >= 0)
            {
                var trim = new UIntPtr(ulong.MaxValue);
                if (!SetSystemFileCacheSize(trim, trim, 0))
                    errors.Add("cache hệ thống (" + Marshal.GetLastWin32Error() + ")");
            }
            else errors.Add("cache hệ thống (thiếu quyền)");
            if (RtlAdjustPrivilege(20, 1, 1, out previous) >= 0)
            {
                TrimList(2, "working set", errors);
                TrimList(5, "standby ưu tiên thấp", errors);
            }
            else errors.Add("working set và standby (thiếu quyền)");
        }
        finally
        {
            if (!RevertToSelf()) throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        var after = new Native.Memory();
        string summary = errors.Count==0 ? "Đã dọn RAM." : "Mục chưa dọn được: " + string.Join(", ", errors) + ".";
        if (measured && Native.GlobalMemoryStatusEx(after))
            summary += string.Format(" RAM khả dụng: {0:N0} → {1:N0} MB.", before.available/1048576.0, after.available/1048576.0);
        return summary;
    }
}
