using System.Runtime.InteropServices;

namespace DjiRcJoystick.App;

internal static class ConsoleWindow
{
    private const uint AttachParentProcess = 0xffffffff;

    public static void Attach()
    {
        if (!AttachConsole(AttachParentProcess))
        {
            AllocConsole();
        }

        Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
        Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachConsole(uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllocConsole();
}
