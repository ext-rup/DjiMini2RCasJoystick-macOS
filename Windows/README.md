# Windows

The Windows port is a .NET 8 notification-area application. It reads the DJI
RC over its USB serial interface and feeds a DirectInput joystick through
[vJoy](https://github.com/jshafer817/vJoy).

## One-installer test build

The `Windows installer` GitHub Actions workflow produces one versioned
`DjiRCJoystickSetup` executable. It contains the self-contained bridge and the
official vJoy 2.1.9.1 installer. A test user only needs to run that setup file,
restart if prompted, connect the RC, and launch **DJI RC Joystick** from the
Start menu. The embedded vJoy installer remains in the protected application
directory so Windows can complete vJoy setup after a required restart.

The workflow downloads vJoy from its official GitHub release and verifies the
pinned SHA-256 before packaging it. vJoy is installed with its default device
1 configuration, which already has all required axes and buttons. Uninstalling
DJI RC Joystick intentionally leaves vJoy installed because other applications
may use it.

The outer test installer is not currently code-signed, although the bundled
vJoy kernel driver is signed. Windows may therefore show an Unknown Publisher
warning for `DjiRCJoystickSetup`.

If vJoy was already installed with a custom configuration, the installer does
not overwrite it. Device 1 must still provide X, Y, Rx, Ry and six buttons.

## Manual requirements

- Windows 10 or 11, x64
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
- A current signed [vJoy release](https://github.com/jshafer817/vJoy/releases)

For a manual installation, use **Configure vJoy** to enable device 1 with these
controls:

- Axes: X, Y, Rx, Ry
- Buttons: at least 6
- POV hats: none required

Buttons 1-4 are the four decoded RC buttons. Buttons 5 and 6 represent the two
non-center positions of the three-position switch.

## Build

Open `DjiRcJoystick.Windows.sln` in Visual Studio 2022 with the .NET desktop
development workload, or run:

```powershell
dotnet restore .\DjiRcJoystick.Windows.sln
dotnet build .\DjiRcJoystick.Windows.sln -c Release
dotnet test .\DjiRcJoystick.Windows.sln -c Release
```

To produce a self-contained x64 application:

```powershell
dotnet publish .\src\DjiRcJoystick.App\DjiRcJoystick.App.csproj `
  -c Release -r win-x64 --self-contained true `
  -o .\publish
```

The vJoy driver and `vJoyInterface.dll` are not redistributed. The application
loads the interface DLL from the installed vJoy directory. This applies to the
manual publish command; the one-installer workflow separately embeds the
official vJoy installer under its MIT license.

## Run

Launch `DjiRCJoystick.exe`. Its notification-area icon reports the vJoy and
controller status, reconnects automatically, opens the Windows Game
Controllers panel, and provides a Quit command.

Diagnostics are available from PowerShell:

```powershell
.\DjiRCJoystick.exe --list
.\DjiRCJoystick.exe --monitor
.\DjiRCJoystick.exe --port COM5 --monitor
.\DjiRCJoystick.exe --port COM5 --vjoy 1
```

The RC normally exposes two DJI serial interfaces. Automatic selection prefers
the `VID_2CA3` interface whose PnP identifier contains `MI_03`, corresponding to
the command interface used by macOS. If Windows enumerates it differently, use
`--list` and pass the working port explicitly.

## Mapping

| vJoy control | DJI control |
| --- | --- |
| X | Left stick horizontal |
| Y | Left stick vertical |
| Rx | Right stick horizontal |
| Ry | Right stick vertical |
| Buttons 1-4 | RC buttons |
| Button 5 | Center switch position -1 |
| Button 6 | Center switch position +1 |

Calibrate the device with `joy.cpl`, then map and invert axes in the simulator
as needed.
