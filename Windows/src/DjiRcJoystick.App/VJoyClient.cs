using System.Reflection;
using System.Runtime.InteropServices;
using DjiRcJoystick.Protocol;

namespace DjiRcJoystick.App;

internal sealed class VJoyException(string message) : Exception(message);

internal sealed class VJoyClient : IDisposable
{
    private const uint AxisX = 0x30;
    private const uint AxisY = 0x31;
    private const uint AxisRx = 0x33;
    private const uint AxisRy = 0x34;

    private readonly uint deviceId;
    private readonly Dictionary<uint, (int Minimum, int Maximum)> axisRanges = [];
    private bool acquired;

    public VJoyClient(uint deviceId)
    {
        this.deviceId = deviceId;
        VJoyNative.EnsureLoaded();
        if (!VJoyNative.VJoyEnabled()) throw new VJoyException("vJoy is not installed or enabled");
        if (!VJoyNative.DriverMatch(out var dllVersion, out var driverVersion))
        {
            throw new VJoyException($"vJoy DLL {dllVersion:x} does not match driver {driverVersion:x}");
        }

        var status = (VJoyDeviceStatus)VJoyNative.GetVjdStatus(deviceId);
        if (status == VJoyDeviceStatus.Busy) throw new VJoyException($"vJoy device {deviceId} is owned by another application");
        if (status is VJoyDeviceStatus.Missing or VJoyDeviceStatus.Unknown)
        {
            throw new VJoyException($"vJoy device {deviceId} is missing or disabled");
        }

        foreach (var axis in new[] { AxisX, AxisY, AxisRx, AxisRy })
        {
            if (!VJoyNative.GetVjdAxisExist(deviceId, axis) ||
                !VJoyNative.GetVjdAxisMin(deviceId, axis, out var minimum) ||
                !VJoyNative.GetVjdAxisMax(deviceId, axis, out var maximum))
            {
                throw new VJoyException($"vJoy device {deviceId} must provide X, Y, Rx, and Ry axes");
            }
            axisRanges[axis] = (minimum, maximum);
        }

        if (VJoyNative.GetVjdButtonNumber(deviceId) < 6)
        {
            throw new VJoyException($"vJoy device {deviceId} must provide at least six buttons");
        }

        acquired = status == VJoyDeviceStatus.Own || VJoyNative.AcquireVjd(deviceId);
        if (!acquired) throw new VJoyException($"Could not acquire vJoy device {deviceId}");
        try
        {
            if (!VJoyNative.ResetVjd(deviceId)) throw new VJoyException($"Could not reset vJoy device {deviceId}");
            Post(new ControllerState());
        }
        catch
        {
            VJoyNative.ResetVjd(deviceId);
            VJoyNative.RelinquishVjd(deviceId);
            acquired = false;
            throw;
        }
    }

    public void Post(ControllerState state)
    {
        var success =
            SetAxis(state.LeftHorizontal, AxisX) &
            SetAxis(state.LeftVertical, AxisY) &
            SetAxis(state.RightHorizontal, AxisRx) &
            SetAxis(state.RightVertical, AxisRy);

        for (byte button = 1; button <= 4; button++)
        {
            success &= VJoyNative.SetButton((state.Buttons & (1 << (button - 1))) != 0, deviceId, button);
        }
        success &= VJoyNative.SetButton(state.CenterSwitch < 0, deviceId, 5);
        success &= VJoyNative.SetButton(state.CenterSwitch > 0, deviceId, 6);

        if (!success) throw new VJoyException($"Failed to update vJoy device {deviceId}");
    }

    private bool SetAxis(ushort value, uint axis)
    {
        var range = axisRanges[axis];
        var scaled = range.Minimum + (int)Math.Round(value / 32_767.0 * (range.Maximum - range.Minimum));
        return VJoyNative.SetAxis(scaled, deviceId, axis);
    }

    public void Dispose()
    {
        if (!acquired) return;
        VJoyNative.ResetVjd(deviceId);
        VJoyNative.RelinquishVjd(deviceId);
        acquired = false;
    }
}

internal enum VJoyDeviceStatus
{
    Own,
    Free,
    Busy,
    Missing,
    Unknown,
}

internal static class VJoyNative
{
    private const string LibraryName = "vJoyInterface.dll";

    static VJoyNative()
    {
        NativeLibrary.SetDllImportResolver(typeof(VJoyNative).Assembly, ResolveLibrary);
    }

    public static void EnsureLoaded() { }

    private static IntPtr ResolveLibrary(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (!libraryName.Equals(LibraryName, StringComparison.OrdinalIgnoreCase)) return IntPtr.Zero;

        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, LibraryName),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "vJoy", "x64", LibraryName),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "vJoy", "x64", LibraryName),
        };
        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate) && NativeLibrary.TryLoad(candidate, out var handle)) return handle;
        }

        return IntPtr.Zero;
    }

    [DllImport(LibraryName, EntryPoint = "vJoyEnabled", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool VJoyEnabled();

    [DllImport(LibraryName, EntryPoint = "DriverMatch", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DriverMatch(out ushort dllVersion, out ushort driverVersion);

    [DllImport(LibraryName, EntryPoint = "GetVJDStatus", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int GetVjdStatus(uint deviceId);

    [DllImport(LibraryName, EntryPoint = "GetVJDAxisExist", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetVjdAxisExist(uint deviceId, uint axis);

    [DllImport(LibraryName, EntryPoint = "GetVJDAxisMin", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetVjdAxisMin(uint deviceId, uint axis, out int minimum);

    [DllImport(LibraryName, EntryPoint = "GetVJDAxisMax", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetVjdAxisMax(uint deviceId, uint axis, out int maximum);

    [DllImport(LibraryName, EntryPoint = "GetVJDButtonNumber", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int GetVjdButtonNumber(uint deviceId);

    [DllImport(LibraryName, EntryPoint = "AcquireVJD", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool AcquireVjd(uint deviceId);

    [DllImport(LibraryName, EntryPoint = "RelinquishVJD", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void RelinquishVjd(uint deviceId);

    [DllImport(LibraryName, EntryPoint = "ResetVJD", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ResetVjd(uint deviceId);

    [DllImport(LibraryName, EntryPoint = "SetAxis", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetAxis(int value, uint deviceId, uint axis);

    [DllImport(LibraryName, EntryPoint = "SetBtn", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetButton(
        [MarshalAs(UnmanagedType.Bool)] bool pressed,
        uint deviceId,
        byte buttonNumber);
}
