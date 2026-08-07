using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

internal static class Program
{
    private static Mutex singleton;

    [STAThread]
    private static void Main()
    {
        singleton = new Mutex(true, "JIGGLEFORGE_WheelBridge_8B3C51B4", out bool createdNew);
        if (!createdNew)
        {
            return;
        }

        try
        {
            string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WheelBridge.txt");
            WheelBridgeConfig config = WheelBridgeConfig.Load(configPath);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new BridgeContext(config));
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                "滚轮输入桥接器启动失败。\r\nWheelBridge failed to start.\r\n\r\n" + exception.Message,
                "JiggleForge WheelBridge",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            if (singleton != null)
            {
                singleton.Dispose();
            }
        }
    }
}
