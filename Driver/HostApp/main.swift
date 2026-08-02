import AppKit
import SystemExtensions

final class MenuBarController: NSObject, NSApplicationDelegate, OSSystemExtensionRequestDelegate {
    static let extensionIdentifier = "io.github.ext-rup.dji-mini2-joystick.driver"

    private let deactivate = CommandLine.arguments.contains("--deactivate")
    private let bridgeQueue = DispatchQueue(label: "io.github.ext-rup.dji-mini2-joystick.bridge")
    private let statusMenuItem = NSMenuItem(title: "Starting...", action: nil, keyEquivalent: "")
    private var statusItem: NSStatusItem?

    func applicationDidFinishLaunching(_ notification: Notification) {
        if !deactivate {
            configureMenuBar()
            bridgeQueue.async { [self] in runBridgeLoop() }
        }
        submitExtensionRequest()
    }

    func applicationWillTerminate(_ notification: Notification) {
        guard !deactivate else { return }
        let virtualPad = VirtualPadClient()
        try? virtualPad.connect()
        try? virtualPad.post(ControllerState())
    }

    private func configureMenuBar() {
        let statusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength)
        statusItem.button?.image = NSImage(
            systemSymbolName: "gamecontroller.fill",
            accessibilityDescription: "DJI RC Joystick"
        )
        statusItem.button?.toolTip = "DJI RC Joystick"

        let menu = NSMenu()
        statusMenuItem.isEnabled = false
        menu.addItem(statusMenuItem)
        menu.addItem(.separator())
        let quitItem = NSMenuItem(title: "Quit DJI RC Joystick", action: #selector(quit), keyEquivalent: "q")
        quitItem.target = self
        menu.addItem(quitItem)
        statusItem.menu = menu
        self.statusItem = statusItem
    }

    @objc private func quit() {
        NSApp.terminate(nil)
    }

    private func setStatus(_ status: String) {
        DispatchQueue.main.async { [weak self] in
            self?.statusMenuItem.title = status
            self?.statusItem?.button?.toolTip = "DJI RC Joystick: \(status)"
        }
    }

    private func serialPorts() -> [String] {
        let names = (try? FileManager.default.contentsOfDirectory(atPath: "/dev")) ?? []
        return names
            .filter { $0.hasPrefix("cu.usbmodem") || $0.hasPrefix("cu.usbserial") }
            .map { "/dev/\($0)" }
            .sorted()
    }

    private func selectedPort() -> String? {
        let ports = serialPorts()
        if ports.count == 1 { return ports[0] }
        let commandPorts = ports.filter { port in
            port.hasSuffix("3") && ports.contains(String(port.dropLast()) + "5")
        }
        return commandPorts.count == 1 ? commandPorts[0] : nil
    }

    private func runBridgeLoop() {
        while true {
            guard let port = selectedPort() else {
                setStatus("Waiting for DJI RC")
                Thread.sleep(forTimeInterval: 1)
                continue
            }

            do {
                setStatus("Connecting...")
                try runBridge(port: port)
            } catch {
                setStatus("Disconnected; retrying")
                Thread.sleep(forTimeInterval: 1)
            }
        }
    }

    private func runBridge(port: String) throws {
        let serial = try SerialPort(path: port)
        let virtualPad = VirtualPadClient()
        try virtualPad.connect()
        defer { try? virtualPad.post(ControllerState()) }

        var sequence: UInt16 = 0x34eb
        func send(commandID: UInt8, payload: [UInt8] = []) throws {
            try serial.write(
                DJIProtocol.packet(commandID: commandID, payload: payload, sequence: sequence)
            )
            sequence &+= 1
        }

        try send(commandID: 0x24, payload: [0x01])
        setStatus("DJI RC connected")
        var state = ControllerState()

        while true {
            try send(commandID: 0x01)
            try send(commandID: 0x27)

            var receivedInput = false
            for _ in 0..<2 {
                if let packet = try serial.readPacket() {
                    receivedInput = state.apply(packet: packet) || receivedInput
                }
            }
            if receivedInput { try virtualPad.post(state) }
        }
    }

    private func submitExtensionRequest() {
        let request: OSSystemExtensionRequest = deactivate
            ? .deactivationRequest(forExtensionWithIdentifier: Self.extensionIdentifier, queue: .main)
            : .activationRequest(forExtensionWithIdentifier: Self.extensionIdentifier, queue: .main)
        request.delegate = self
        OSSystemExtensionManager.shared.submitRequest(request)
    }

    func request(
        _ request: OSSystemExtensionRequest,
        actionForReplacingExtension existing: OSSystemExtensionProperties,
        withExtension extension: OSSystemExtensionProperties
    ) -> OSSystemExtensionRequest.ReplacementAction {
        .replace
    }

    func requestNeedsUserApproval(_ request: OSSystemExtensionRequest) {
        setStatus("Driver approval required")
    }

    func request(_ request: OSSystemExtensionRequest, didFinishWithResult result: OSSystemExtensionRequest.Result) {
        if deactivate {
            print(result == .willCompleteAfterReboot
                ? "The virtual joystick will finish deactivating after a restart."
                : "Virtual joystick deactivated.")
            NSApp.terminate(nil)
        }
    }

    func request(_ request: OSSystemExtensionRequest, didFailWithError error: Error) {
        if deactivate {
            fputs("Virtual joystick deactivation failed: \(error.localizedDescription)\n", stderr)
            exit(EXIT_FAILURE)
        }
        setStatus("Driver error: \(error.localizedDescription)")
    }
}

let application = NSApplication.shared
let delegate = MenuBarController()
application.delegate = delegate
application.setActivationPolicy(.accessory)
application.run()
