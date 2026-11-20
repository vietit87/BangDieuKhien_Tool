using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

internal static class CpuRanking
{
    sealed class Sample
    {
        internal string Name;
        internal long Started, CpuTicks, Timestamp;
    }

    static Dictionary<int, Sample> Snapshot()
    {
        var samples = new Dictionary<int, Sample>();
        foreach (var process in Process.GetProcesses())
        using (process)
        {
            try
            {
                if (process.Id == 0) continue;
                samples[process.Id] = new Sample {
                    Name = process.ProcessName,
                    Started = process.StartTime.ToUniversalTime().Ticks,
                    CpuTicks = process.TotalProcessorTime.Ticks,
                    Timestamp = Stopwatch.GetTimestamp()
                };
            }
            catch (InvalidOperationException) { } // Process exited during the sample.
            catch (Win32Exception) { } // Protected process.
        }
        return samples;
    }

    internal static async Task<string[]> Measure()
    {
        var first = await Task.Run(() => Snapshot()).ConfigureAwait(false);
        await Task.Delay(1000).ConfigureAwait(false);
        var totals = new Dictionary<string,double>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in Snapshot())
        {
            Sample previous;
            var now = entry.Value;
            if (!first.TryGetValue(entry.Key, out previous) || previous.Started != now.Started) continue;
            double seconds = (now.Timestamp - previous.Timestamp) / (double)Stopwatch.Frequency;
            long used = now.CpuTicks - previous.CpuTicks;
            if (seconds <= 0 || used < 0) continue;
            double share = used / (double)TimeSpan.TicksPerSecond / seconds / Environment.ProcessorCount * 100;
            double accumulated;
            totals.TryGetValue(now.Name, out accumulated);
            totals[now.Name] = accumulated + share;
        }
        return totals.OrderByDescending(item => item.Value).ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .Take(5).Select((item,index) => string.Format("{0}.  {1}    {2:0.0}%", index+1, item.Key, Math.Min(100,item.Value))).ToArray();
    }
}
