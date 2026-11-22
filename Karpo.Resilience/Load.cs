// A machine that is slow: every core busy, or the memory nearly gone so what Karpo holds is paged out.
// Each runs in a child process of its own (`burn`, `hog`) so a scenario can end it at once, and it
// ends by itself after its time whatever happens to the harness.

using System.Diagnostics;
using System.Runtime.InteropServices;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Resilience;

internal static unsafe class Load
{
    /// <summary>The child's side of <c>burn &lt;seconds&gt;</c>: two busy threads a core, at normal priority, as a build or a game would.</summary>
    public static int Burn(int seconds)
    {
        var end = Environment.TickCount64 + seconds * 1000L;
        var threads = Enumerable.Range(0, Environment.ProcessorCount * 2).Select(_ => new Thread(() =>
        {
            var x = 1.0;
            while (Environment.TickCount64 < end)
            {
                for (var i = 0; i < 100_000; i++)
                {
                    x = Math.Sqrt(x + i);
                }
            }
            GC.KeepAlive(x);
        }) { IsBackground = true }).ToList();
        threads.ForEach(thread => thread.Start());
        threads.ForEach(thread => thread.Join());
        return 0;
    }

    /// <summary>The child's side of <c>hog &lt;MB&gt; &lt;seconds&gt;</c>: that much memory taken and touched again and again, so others' is paged out.</summary>
    public static int Hog(int megabytes, int seconds)
    {
        var end = Environment.TickCount64 + seconds * 1000L;
        var blocks = new List<byte[]>();
        for (var made = 0; made < megabytes && Environment.TickCount64 < end; made += 64)
        {
            blocks.Add(GC.AllocateUninitializedArray<byte>(64 << 20));
        }
        while (Environment.TickCount64 < end)
        {
            foreach (var block in blocks)
            {
                for (var i = 0; i < block.Length; i += 4096)
                {
                    block[i]++;
                }
            }
        }
        return 0;
    }

    /// <summary>Megabytes of physical memory free now.</summary>
    public static long FreeMegabytes()
    {
        MEMORYSTATUSEX status;
        status.dwLength = (uint)sizeof(MEMORYSTATUSEX);
        return GlobalMemoryStatusEx(&status) ? (long)(status.ullAvailPhys >> 20) : 0;
    }

    /// <summary>Starts the harness again as <paramref name="args"/>: a child to end when the scenario is done.</summary>
    public static Process Child(params string[] args)
    {
        var info = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false };
        foreach (var arg in args)
        {
            info.ArgumentList.Add(arg);
        }
        return Process.Start(info)!;
    }

    /// <summary>Ends a child, whatever it is doing.</summary>
    public static void End(Process child)
    {
        try
        {
            if (!child.HasExited)
            {
                child.Kill(entireProcessTree: true);
                child.WaitForExit(5000);
            }
        }
        catch (InvalidOperationException)
        {
        }
        child.Dispose();
    }
}
