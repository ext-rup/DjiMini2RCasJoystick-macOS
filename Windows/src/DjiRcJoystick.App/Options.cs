namespace DjiRcJoystick.App;

internal sealed record Options(string? Port, uint VJoyDeviceId, bool ListPorts, bool Monitor, bool ShowHelp)
{
    public static Options Parse(string[] arguments)
    {
        string? port = null;
        uint vjoyDeviceId = 1;
        var listPorts = false;
        var monitor = false;
        var showHelp = false;

        for (var index = 0; index < arguments.Length; index++)
        {
            switch (arguments[index])
            {
                case "-p":
                case "--port":
                    if (++index >= arguments.Length) throw new ArgumentException("--port requires a COM port");
                    port = arguments[index];
                    break;
                case "--vjoy":
                    if (++index >= arguments.Length || !uint.TryParse(arguments[index], out vjoyDeviceId) || vjoyDeviceId is < 1 or > 16)
                    {
                        throw new ArgumentException("--vjoy requires a device ID from 1 through 16");
                    }
                    break;
                case "--list":
                    listPorts = true;
                    break;
                case "--monitor":
                    monitor = true;
                    break;
                case "-h":
                case "--help":
                    showHelp = true;
                    break;
                default:
                    throw new ArgumentException($"Unknown argument: {arguments[index]}");
            }
        }

        return new Options(port, vjoyDeviceId, listPorts, monitor, showHelp);
    }

    public static string Usage => """
        DJI RC Joystick for Windows

        Usage: DjiRCJoystick.exe [options]

          -p, --port COMn   Use a specific DJI RC serial port
              --vjoy ID     Use vJoy device 1-16 (default: 1)
              --list        List detected serial ports
              --monitor     Print decoded controls without vJoy
          -h, --help        Show this help

        With no diagnostic option, the application runs in the notification area.
        """;
}
