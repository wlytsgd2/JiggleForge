using System.Drawing;
using System.Windows.Forms;

internal sealed class BridgeContext : ApplicationContext
{
    private readonly NotifyIcon trayIcon;
    private readonly KeyPulseQueue pulses;
    private readonly MouseWheelHook hook;

    internal BridgeContext(WheelBridgeConfig config)
    {
        pulses = new KeyPulseQueue(config.PulseMilliseconds);
        hook = new MouseWheelHook(config, pulses);
        ContextMenuStrip menu = new ContextMenuStrip();
        ToolStripMenuItem exit = new ToolStripMenuItem("Exit WheelBridge");
        exit.Click += delegate { ExitThread(); };
        menu.Items.Add(exit);
        trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "JiggleForge WheelBridge: running",
            ContextMenuStrip = menu,
            Visible = true,
        };
    }

    protected override void ExitThreadCore()
    {
        trayIcon.Visible = false;
        trayIcon.Dispose();
        hook.Dispose();
        pulses.Dispose();
        base.ExitThreadCore();
    }
}
