# DJI Mini 2 RC as a Joystick

Use a DJI Mini 2 remote controller as an analog joystick for flight simulators.

Supported controller names include RC-N1, RCS231, WM161b-RC-N1, and RCN1. Four
buttons and the center three-position switch are also exposed.

## macOS

The macOS port consists of:

- `dji-rc-joystick`, a native Swift command-line bridge that reads the RC over
  USB serial and enables DJI simulator mode.
- `DjiRCVirtualJoystick.app`, a minimal HID DriverKit system extension that
  exposes four analog axes and four buttons to games such as Liftoff.

macOS does not provide an unrestricted equivalent of Linux `uinput`. Building
the virtual joystick therefore requires a paid Apple Developer Program account
and development signing. Local development does not require notarization or a
Developer ID distribution certificate.

### 1. Verify controller input

Connect the RC using its bottom USB-C connector, then build and monitor it:

```sh
swift build -c release
.build/release/dji-rc-joystick --list
.build/release/dji-rc-joystick --monitor
```

If auto-detection does not select the controller, pass the listed path:

```sh
.build/release/dji-rc-joystick --port /dev/cu.usbmodemXXXX --monitor
```

Move every stick and press Control-C after confirming that all four values
change. This step does not require DriverKit or signing.

### 2. Prepare development signing

1. Open Xcode, then choose **Xcode > Settings > Accounts** and add the Apple ID
   enrolled in the paid Developer Program.
2. Select the team, open **Manage Certificates**, and create an
   **Apple Development** certificate.
3. Change the three `io.github.ext-rup...` bundle identifiers in
   `Driver/project.yml`, `Driver/Dext/Info.plist`, and
   `Driver/HostApp/main.swift` if those identifiers are not available to your
   team. The driver identifier must remain identical in all three files.
4. Generate and open the Xcode project:

```sh
brew install xcodegen
cd Driver
xcodegen generate
open DjiRCVirtualJoystick.xcodeproj
```

5. In **Signing & Capabilities**, select your team for both targets and leave
   automatic signing enabled. Xcode should create Mac App Development and
   DriverKit Development profiles.

The driver profile needs these capabilities:

- DriverKit
- DriverKit HID Device
- DriverKit HID Event Service

The restricted `com.apple.developer.hid.virtual.device` entitlement is needed
by the driverless userspace API, but not by this DriverKit HID-family design.
An ad-hoc or independently self-signed certificate still cannot grant the
DriverKit entitlements used here.

### 3. Build and activate the virtual joystick

After selecting the team in Xcode, build the `DjiRCVirtualJoystick` scheme. For
a command-line build using the signing settings saved by Xcode:

```sh
cd Driver
xcodebuild \
  -project DjiRCVirtualJoystick.xcodeproj \
  -scheme DjiRCVirtualJoystick \
  -configuration Debug \
  -derivedDataPath build \
  -allowProvisioningUpdates \
  build
```

Copy `Driver/build/Build/Products/Debug/DjiRCVirtualJoystick.app` to
`/Applications`, open it, and approve the system extension when macOS prompts
in **System Settings > General > Login Items & Extensions > Driver
Extensions**. This is a one-time installation.

Verify that the active driver accepts input reports:

```sh
.build/release/dji-rc-joystick --test-virtual
```

### 4. Run the menu-bar utility

Open the installed utility before starting the simulator:

```sh
open /Applications/DjiRCVirtualJoystick.app
```

The game-controller icon in the menu bar reports whether the utility is waiting,
connecting, or forwarding input. It detects the RC when connected and returns
to waiting after disconnection. Choose **Quit DJI RC Joystick** from the menu
to stop it. The utility does not start at login or install a background helper.

The command-line bridge remains available for diagnostics:

```sh
.build/release/dji-rc-joystick
```

In Liftoff or The Zone, run controller calibration and map the four detected
axes to roll, pitch, yaw, and throttle. Axis order from the virtual joystick is:

| HID axis | DJI control |
| --- | --- |
| X | Left stick horizontal |
| Y | Left stick vertical |
| Rx | Right stick horizontal |
| Ry | Right stick vertical |

Invert axes in the simulator as needed for the selected transmitter mode. If
Steam captures the controller instead of the simulator, disable Steam Input
for that game.

### macOS development checks

```sh
swift test
cd Driver
xcodegen generate
xcodebuild \
  -project DjiRCVirtualJoystick.xcodeproj \
  -scheme DjiRCVirtualJoystick \
  -configuration Debug \
  -derivedDataPath build \
  CODE_SIGNING_ALLOWED=NO \
  build
```

The unsigned command checks compilation only; macOS will not activate that
build as a system extension.

To deactivate and remove the development driver:

```sh
/Applications/DjiRCVirtualJoystick.app/Contents/MacOS/DjiRCVirtualJoystick --deactivate
rm -rf /Applications/DjiRCVirtualJoystick.app
```

The deactivation may require a restart if a game still has the joystick open.

## Linux

The original Python `uinput` implementation remains available as `main.py`.

```sh
python3 -m venv .venv
. .venv/bin/activate
pip install -r requirements.txt
sudo modprobe uinput
sudo python3 main.py -p /dev/ttyACM0
```

The RC is placed in simulator mode and appears as `/dev/js0`. If several ACM
devices are connected, inspect `/dev/ttyACM*` and pass the appropriate path.

## Credits

The controller protocol was inspired by
[justin97530/miniDjiController](https://github.com/justin97530/miniDjiController).
The macOS DriverKit architecture was adapted from
[caqlayan/procon2-mac](https://github.com/caqlayan/procon2-mac); see `NOTICE`.
