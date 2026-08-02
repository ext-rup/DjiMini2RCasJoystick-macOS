using System.Diagnostics;

namespace DjiRcJoystick.App;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly BridgeService bridge;
    private readonly NotifyIcon notifyIcon;
    private readonly ToolStripMenuItem statusItem;
    private bool stopping;

    public TrayApplicationContext(Options options)
    {
        statusItem = new ToolStripMenuItem("Starting...") { Enabled = false };
        var menu = new ContextMenuStrip();
        menu.Items.Add(statusItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Open Game Controllers", null, OpenGameControllers);
        menu.Items.Add("Quit DJI RC Joystick", null, Quit);

        notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "DJI RC Joystick: Starting...",
            ContextMenuStrip = menu,
            Visible = true,
        };

        var uiContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        bridge = new BridgeService(options);
        bridge.StatusChanged += status => uiContext.Post(_ => SetStatus(status), null);
        bridge.Start();
    }

    private void SetStatus(string status)
    {
        statusItem.Text = status;
        notifyIcon.Text = $"DJI RC Joystick: {status}"[..Math.Min(63, $"DJI RC Joystick: {status}".Length)];
    }

    private static void OpenGameControllers(object? sender, EventArgs eventArgs)
    {
        Process.Start(new ProcessStartInfo("control.exe", "joy.cpl") { UseShellExecute = true });
    }

    private async void Quit(object? sender, EventArgs eventArgs)
    {
        if (stopping) return;
        stopping = true;
        notifyIcon.Visible = false;
        try
        {
            await bridge.StopAsync();
        }
        finally
        {
            ExitThread();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            notifyIcon.Dispose();
            bridge.Dispose();
        }
        base.Dispose(disposing);
    }
}
