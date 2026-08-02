// swift-tools-version: 5.9
import PackageDescription

let package = Package(
    name: "DjiRCJoystick",
    platforms: [.macOS(.v13)],
    products: [
        .executable(name: "dji-rc-joystick", targets: ["DjiRCJoystick"]),
    ],
    targets: [
        .target(name: "DJIProtocol"),
        .executableTarget(
            name: "DjiRCJoystick",
            dependencies: ["DJIProtocol"],
            linkerSettings: [.linkedFramework("IOKit")]
        ),
        .testTarget(name: "DJIProtocolTests", dependencies: ["DJIProtocol"]),
    ]
)
