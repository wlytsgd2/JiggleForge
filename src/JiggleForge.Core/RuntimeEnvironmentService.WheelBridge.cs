using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace JiggleForge.Core;

public sealed partial class RuntimeEnvironmentService
{
    public void StartWheelBridge(string zzmiRoot)
    {
        string root = NormalizeRoot(zzmiRoot);
        string executable = Path.Combine(root, "Mods", RuntimeFolderName, "JiggleForge", "WheelBridge.exe");
        if (!File.Exists(executable))
        {
            throw new FileNotFoundException("WheelBridge is not installed. Install the global runtime first.", executable);
        }

        StopWheelBridge(requestElevation: true);
        try
        {
            Process.Start(new ProcessStartInfo(executable)
            {
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = Path.GetDirectoryName(executable),
            });
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == 1223)
        {
            throw new UnauthorizedAccessException("Administrator permission was cancelled; WheelBridge was not started.", exception);
        }
    }

    public void StopWheelBridge(bool requestElevation)
    {
        foreach (Process process in Process.GetProcessesByName("WheelBridge"))
        {
            using (process)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(2000);
                }
                catch (InvalidOperationException)
                {
                }
                catch (Win32Exception)
                {
                }
            }
        }

        if (!IsWheelBridgeRunning())
        {
            return;
        }
        if (!requestElevation)
        {
            throw new UnauthorizedAccessException("WheelBridge requires administrator permission to stop.");
        }

        try
        {
            using Process? elevated = Process.Start(new ProcessStartInfo("taskkill.exe", "/IM WheelBridge.exe /F")
            {
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden,
            });
            elevated?.WaitForExit();
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == 1223)
        {
            throw new UnauthorizedAccessException("Administrator permission was cancelled; WheelBridge is still running.", exception);
        }

        if (IsWheelBridgeRunning())
        {
            throw new UnauthorizedAccessException("Unable to stop WheelBridge even with administrator permission.");
        }
    }

    public static bool IsWheelBridgeRunning()
    {
        Process[] processes = Process.GetProcessesByName("WheelBridge");
        try
        {
            return processes.Length > 0;
        }
        finally
        {
            foreach (Process process in processes)
            {
                process.Dispose();
            }
        }
    }

}
