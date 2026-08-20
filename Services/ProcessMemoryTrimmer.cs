using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WinToastRelay.Services;

/// <summary>
/// Returns unused managed memory and asks Windows to trim the process working set
/// while the UI is hidden. Notification delivery remains active.
/// </summary>
internal static class ProcessMemoryTrimmer
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetProcessWorkingSetSize(nint process, nint minimumWorkingSetSize, nint maximumWorkingSetSize);

    public static void Trim()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);

        using var process = Process.GetCurrentProcess();
        _ = SetProcessWorkingSetSize(process.Handle, -1, -1);
    }
}
