namespace DjiRcJoystick.App;

internal static class DiagnosticCommands
{
    public static int ListPorts()
    {
        var ports = PortDiscovery.GetPorts();
        if (ports.Count == 0)
        {
            Console.WriteLine("No serial ports found.");
            return 0;
        }

        foreach (var port in ports)
        {
            var marker = port.IsDji ? port.IsCommandInterface ? " [DJI command]" : " [DJI]" : string.Empty;
            Console.WriteLine($"{port.PortName}: {port.DisplayName}{marker}");
        }
        return 0;
    }

    public static int Monitor(Options options)
    {
        var port = PortDiscovery.SelectPort(options.Port) ??
            throw new InvalidOperationException("Could not select the DJI RC port. Use --list and --port COMn.");
        Console.WriteLine($"Opened DJI RC on {port}. Press Control-C to stop.");

        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };

        using var controller = new ControllerSession(port);
        var lastPrinted = DateTime.MinValue;
        while (!cancellation.IsCancellationRequested)
        {
            if (!controller.Poll(cancellation.Token) || DateTime.UtcNow - lastPrinted < TimeSpan.FromMilliseconds(50)) continue;
            var state = controller.State;
            Console.Write(
                $"\rLX {state.LeftHorizontal,5}  LY {state.LeftVertical,5}  " +
                $"RX {state.RightHorizontal,5}  RY {state.RightVertical,5}  " +
                $"buttons {state.Buttons:x1}  switch {state.CenterSwitch:+0;-0; 0}");
            lastPrinted = DateTime.UtcNow;
        }

        Console.WriteLine();
        return 0;
    }
}
