import DJIProtocol
import Foundation

private struct Options {
    var port: String?
    var monitorOnly = false
    var listPorts = false
    var testVirtual = false

    init(arguments: [String]) throws {
        var index = 0
        while index < arguments.count {
            switch arguments[index] {
            case "-p", "--port":
                index += 1
                guard index < arguments.count else { throw UsageError("--port requires a device path") }
                port = arguments[index]
            case "--monitor":
                monitorOnly = true
            case "--list":
                listPorts = true
            case "--test-virtual":
                testVirtual = true
            case "-h", "--help":
                printUsage()
                exit(0)
            default:
                throw UsageError("Unknown argument: \(arguments[index])")
            }
            index += 1
        }
    }
}

private struct UsageError: Error, CustomStringConvertible {
    let description: String
    init(_ description: String) { self.description = description }
}

private func serialPorts() -> [String] {
    let names = (try? FileManager.default.contentsOfDirectory(atPath: "/dev")) ?? []
    return names
        .filter { $0.hasPrefix("cu.usbmodem") || $0.hasPrefix("cu.usbserial") }
        .map { "/dev/\($0)" }
        .sorted()
}

private func automaticallySelectedPort(from ports: [String]) -> String? {
    if ports.count == 1 { return ports[0] }

    // The RC exposes paired ACM interfaces 3 and 5; DUML uses interface 3.
    let commandPorts = ports.filter { port in
        port.hasSuffix("3") && ports.contains(String(port.dropLast()) + "5")
    }
    return commandPorts.count == 1 ? commandPorts[0] : nil
}

private func printUsage() {
    print("""
    Usage: dji-rc-joystick [--port /dev/cu.usbmodem...] [--monitor]

      -p, --port PATH  DJI RC serial device; auto-detected when exactly one is present
          --monitor    Decode and display controls without requiring the virtual HID driver
          --list       List likely DJI serial devices
          --test-virtual  Send one neutral report to the installed virtual joystick
      -h, --help       Show this help
    """)
}

private func runBridge(port: String, monitorOnly: Bool) throws {
    let serial = try SerialPort(path: port)
    let virtualPad = VirtualPadClient()
    if !monitorOnly { try virtualPad.connect() }
    print("Opened DJI RC on \(port). Press Control-C to stop.")
    fflush(stdout)

    var sequence: UInt16 = 0x34eb
    func send(commandID: UInt8, payload: [UInt8] = []) throws {
        try serial.write(DJIProtocol.packet(commandID: commandID, payload: payload, sequence: sequence))
        sequence &+= 1
    }

    try send(commandID: 0x24, payload: [0x01])
    var state = ControllerState()
    var lastPrinted = Date.distantPast

    while true {
        try send(commandID: 0x01)
        try send(commandID: 0x27)

        var changed = false
        for _ in 0..<2 {
            if let packet = try serial.readPacket() {
                changed = state.apply(packet: packet) || changed
            }
        }
        guard changed else { continue }

        if monitorOnly {
            if Date().timeIntervalSince(lastPrinted) >= 0.05 {
                print(
                    String(
                        format: "\rLX %5d  LY %5d  RX %5d  RY %5d  buttons %01x  switch %+d",
                        state.leftHorizontal,
                        state.leftVertical,
                        state.rightHorizontal,
                        state.rightVertical,
                        state.buttons,
                        state.centerSwitch
                    ),
                    terminator: ""
                )
                fflush(stdout)
                lastPrinted = Date()
            }
        } else {
            try virtualPad.post(state)
        }
    }
}

private func run() throws {
    let options = try Options(arguments: Array(CommandLine.arguments.dropFirst()))
    let ports = serialPorts()
    if options.listPorts {
        print(ports.isEmpty ? "No USB serial ports found." : ports.joined(separator: "\n"))
        return
    }
    if options.testVirtual {
        let virtualPad = VirtualPadClient()
        try virtualPad.connect()
        try virtualPad.post(ControllerState())
        print("Virtual joystick accepted a neutral input report.")
        return
    }
    let port: String
    if let requestedPort = options.port {
        port = requestedPort
    } else if let detectedPort = automaticallySelectedPort(from: ports) {
        port = detectedPort
    } else if ports.isEmpty {
        throw UsageError("No USB serial port found. Connect the RC or pass --port explicitly.")
    } else {
        throw UsageError("Multiple USB serial ports found; select one with --port:\n\(ports.joined(separator: "\n"))")
    }

    try runBridge(port: port, monitorOnly: options.monitorOnly)
}

do {
    try run()
} catch {
    fputs("Error: \(error)\n", stderr)
    exit(1)
}
