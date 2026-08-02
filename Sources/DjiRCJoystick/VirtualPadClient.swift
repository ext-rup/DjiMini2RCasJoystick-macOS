#if canImport(DJIProtocol)
import DJIProtocol
#endif
import Foundation
import IOKit.hid

struct VirtualPadError: Error, CustomStringConvertible {
    let description: String
}

final class VirtualPadClient {
    private static let vendorID = 0x2ca3
    private static let productID = 0x161b
    private static let serialNumber = "dji-mini2-virtual"
    private static let controlReportID: CFIndex = 0x7f

    private var device: IOHIDDevice?
    private var manager: IOHIDManager?

    func connect() throws {
        let manager = IOHIDManagerCreate(kCFAllocatorDefault, IOOptionBits(kIOHIDOptionsTypeNone))
        IOHIDManagerSetDeviceMatching(manager, [
            kIOHIDVendorIDKey: Self.vendorID,
            kIOHIDProductIDKey: Self.productID,
            kIOHIDSerialNumberKey: Self.serialNumber,
        ] as CFDictionary)
        IOHIDManagerOpen(manager, IOOptionBits(kIOHIDOptionsTypeNone))

        guard let device = (IOHIDManagerCopyDevices(manager) as? Set<IOHIDDevice>)?.first else {
            IOHIDManagerClose(manager, IOOptionBits(kIOHIDOptionsTypeNone))
            throw VirtualPadError(description: "Virtual joystick not found. Install and activate Driver/DjiRCVirtualJoystick.app first.")
        }
        guard IOHIDDeviceOpen(device, IOOptionBits(kIOHIDOptionsTypeNone)) == kIOReturnSuccess else {
            throw VirtualPadError(description: "Could not open the DJI virtual joystick.")
        }
        self.manager = manager
        self.device = device
    }

    func post(_ state: ControllerState) throws {
        guard let device else { throw VirtualPadError(description: "Virtual joystick is not connected.") }

        var report: [UInt8] = [0x01]
        for axis in [state.leftHorizontal, state.leftVertical, state.rightHorizontal, state.rightVertical] {
            report += [UInt8(axis & 0xff), UInt8(axis >> 8)]
        }
        report += [state.buttons & 0x0f, UInt8(bitPattern: state.centerSwitch)]

        var controlReport = [UInt8](repeating: 0, count: 12)
        controlReport[0] = UInt8(Self.controlReportID)
        controlReport.replaceSubrange(1..<(1 + report.count), with: report)
        let result = IOHIDDeviceSetReport(
            device,
            kIOHIDReportTypeFeature,
            Self.controlReportID,
            controlReport,
            controlReport.count
        )
        guard result == kIOReturnSuccess else {
            throw VirtualPadError(description: String(format: "Could not send virtual joystick report (IOKit 0x%08x).", result))
        }
    }

    func disconnect() {
        if let device { IOHIDDeviceClose(device, IOOptionBits(kIOHIDOptionsTypeNone)) }
        if let manager { IOHIDManagerClose(manager, IOOptionBits(kIOHIDOptionsTypeNone)) }
        device = nil
        manager = nil
    }

    deinit {
        disconnect()
    }
}
