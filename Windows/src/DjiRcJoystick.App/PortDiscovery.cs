using System.IO.Ports;
using System.Management;
using System.Text.RegularExpressions;

namespace DjiRcJoystick.App;

internal sealed record SerialPortInfo(string PortName, string DisplayName, string PnpDeviceId)
{
    public bool IsDji => PnpDeviceId.Contains("VID_2CA3", StringComparison.OrdinalIgnoreCase);
    public bool IsCommandInterface => PnpDeviceId.Contains("MI_03", StringComparison.OrdinalIgnoreCase);
}

internal static partial class PortDiscovery
{
    public static IReadOnlyList<SerialPortInfo> GetPorts()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, PNPDeviceID FROM Win32_PnPEntity WHERE Name LIKE '%(COM%'");
            var result = new List<SerialPortInfo>();
            foreach (ManagementObject device in searcher.Get())
            {
                var name = device["Name"]?.ToString() ?? string.Empty;
                var match = ComPortPattern().Match(name);
                if (!match.Success) continue;
                result.Add(new SerialPortInfo(
                    match.Groups[1].Value.ToUpperInvariant(),
                    name,
                    device["PNPDeviceID"]?.ToString() ?? string.Empty));
            }

            if (result.Count > 0)
            {
                return result.OrderBy(port => PortNumber(port.PortName)).ToArray();
            }
        }
        catch
        {
        }

        return SerialPort.GetPortNames()
            .OrderBy(PortNumber)
            .Select(name => new SerialPortInfo(name, name, string.Empty))
            .ToArray();
    }

    public static string? SelectPort(string? requestedPort)
    {
        if (!string.IsNullOrWhiteSpace(requestedPort))
        {
            return requestedPort.ToUpperInvariant();
        }

        var ports = GetPorts();
        var commandPorts = ports.Where(port => port.IsDji && port.IsCommandInterface).ToArray();
        if (commandPorts.Length == 1) return commandPorts[0].PortName;

        var djiPorts = ports.Where(port => port.IsDji).ToArray();
        if (djiPorts.Length == 1) return djiPorts[0].PortName;
        return ports.Count == 1 ? ports[0].PortName : null;
    }

    private static int PortNumber(string name) =>
        int.TryParse(name.AsSpan(3), out var number) ? number : int.MaxValue;

    [GeneratedRegex(@"\((COM\d+)\)", RegexOptions.IgnoreCase)]
    private static partial Regex ComPortPattern();
}
