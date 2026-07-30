using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

internal sealed class WheelBridgeConfig
{
    internal readonly HashSet<string> ProcessNames = new(StringComparer.OrdinalIgnoreCase);
    internal int PulseMilliseconds = 60;
    internal bool BlockWheelWhileDragging = true;
    internal bool RequireDragButton = true;
    internal readonly List<int> DragVirtualKeys = new() { NativeMethods.VK_LBUTTON };
    internal string SourcePath = string.Empty;
    internal DateTime SourceWriteTimeUtc;

    internal static WheelBridgeConfig Load(string path)
    {
        WheelBridgeConfig config = new();
        config.ProcessNames.Add("ZenlessZoneZero");
        config.SourcePath = path;
        if (!File.Exists(path))
        {
            return config;
        }

        config.SourceWriteTimeUtc = File.GetLastWriteTimeUtc(path);
        foreach (string sourceLine in File.ReadAllLines(path))
        {
            string line = sourceLine.Trim();
            if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";"))
            {
                continue;
            }

            int separator = line.IndexOf('=');
            if (separator < 0)
            {
                continue;
            }

            string key = line.Substring(0, separator).Trim();
            string value = line.Substring(separator + 1).Trim();
            if (key.Equals("process_names", StringComparison.OrdinalIgnoreCase))
            {
                config.ProcessNames.Clear();
                foreach (string item in value.Split(','))
                {
                    string processName = Path.GetFileNameWithoutExtension(item.Trim());
                    if (processName.Length > 0)
                    {
                        config.ProcessNames.Add(processName);
                    }
                }
            }
            else if (key.Equals("pulse_ms", StringComparison.OrdinalIgnoreCase) && int.TryParse(value, out int pulse))
            {
                config.PulseMilliseconds = Math.Max(16, Math.Min(250, pulse));
            }
            else if (key.Equals("block_wheel_while_dragging", StringComparison.OrdinalIgnoreCase) &&
                     bool.TryParse(value, out bool block))
            {
                config.BlockWheelWhileDragging = block;
            }
            else if (key.Equals("require_drag_button", StringComparison.OrdinalIgnoreCase) &&
                     bool.TryParse(value, out bool require))
            {
                config.RequireDragButton = require;
            }
            else if (key.Equals("drag_keys", StringComparison.OrdinalIgnoreCase))
            {
                List<int> virtualKeys = new();
                foreach (string token in value.Split(','))
                {
                    if (TryParseVirtualKey(token, out int virtualKey) && !virtualKeys.Contains(virtualKey))
                    {
                        virtualKeys.Add(virtualKey);
                    }
                }

                if (virtualKeys.Count > 0)
                {
                    config.DragVirtualKeys.Clear();
                    config.DragVirtualKeys.AddRange(virtualKeys);
                }
            }
            else if (key.Equals("drag_key", StringComparison.OrdinalIgnoreCase) &&
                     TryParseVirtualKey(value, out int virtualKey))
            {
                config.DragVirtualKeys.Clear();
                config.DragVirtualKeys.Add(virtualKey);
            }
        }

        return config;
    }

    internal WheelBridgeConfig ReloadIfChanged()
    {
        try
        {
            if (SourcePath.Length > 0 && File.Exists(SourcePath) &&
                File.GetLastWriteTimeUtc(SourcePath) != SourceWriteTimeUtc)
            {
                return Load(SourcePath);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return this;
    }

    internal bool AllowsForegroundProcess()
    {
        if (ProcessNames.Contains("*"))
        {
            return true;
        }

        IntPtr foregroundWindow = NativeMethods.GetForegroundWindow();
        if (foregroundWindow == IntPtr.Zero)
        {
            return false;
        }

        NativeMethods.GetWindowThreadProcessId(foregroundWindow, out uint processId);
        if (processId == 0)
        {
            return false;
        }

        try
        {
            using Process process = Process.GetProcessById((int)processId);
            return ProcessNames.Contains(process.ProcessName);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryParseVirtualKey(string token, out int virtualKey)
    {
        switch (token.Trim().ToUpperInvariant())
        {
            case "VK_LBUTTON": virtualKey = NativeMethods.VK_LBUTTON; return true;
            case "VK_RBUTTON": virtualKey = NativeMethods.VK_RBUTTON; return true;
            case "VK_MBUTTON": virtualKey = NativeMethods.VK_MBUTTON; return true;
            case "VK_XBUTTON1": virtualKey = NativeMethods.VK_XBUTTON1; return true;
            case "VK_XBUTTON2": virtualKey = NativeMethods.VK_XBUTTON2; return true;
            case "X": virtualKey = 'X'; return true;
            case "C": virtualKey = 'C'; return true;
            case "V": virtualKey = 'V'; return true;
            default: virtualKey = 0; return false;
        }
    }
}
