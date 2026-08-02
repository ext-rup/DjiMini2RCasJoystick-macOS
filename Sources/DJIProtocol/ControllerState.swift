public struct ControllerState: Equatable, Sendable {
    public var leftHorizontal: UInt16 = 16_384
    public var leftVertical: UInt16 = 16_384
    public var rightHorizontal: UInt16 = 16_384
    public var rightVertical: UInt16 = 16_384
    public var buttons: UInt8 = 0
    public var centerSwitch: Int8 = 0

    public init() {}

    @discardableResult
    public mutating func apply(packet: [UInt8]) -> Bool {
        guard DJIProtocol.isValid(packet: packet), packet[9] == 0x06 else { return false }

        switch (packet.count, packet[10]) {
        case (38, 0x01):
            rightHorizontal = Self.stickValue(packet[13], packet[14])
            rightVertical = Self.stickValue(packet[16], packet[17])
            leftVertical = Self.stickValue(packet[19], packet[20])
            leftHorizontal = Self.stickValue(packet[22], packet[23])
            return true
        case (58, 0x27):
            let buttonBits = UInt16(packet[28]) << 8 | UInt16(packet[29])
            buttons = 0
            if buttonBits & 0x1060 == 0x1060 { buttons |= 1 << 0 }
            if buttonBits & 0x1080 == 0x1080 { buttons |= 1 << 1 }
            if buttonBits & 0x1004 == 0x1004 { buttons |= 1 << 2 }
            if buttonBits & 0x1002 == 0x1002 { buttons |= 1 << 3 }

            let switchBits = UInt16(packet[27]) << 8 | UInt16(packet[28])
            centerSwitch = switchBits == 0 ? 1 : (switchBits & 0x20 == 0x20 ? -1 : 0)
            return true
        default:
            return false
        }
    }

    public static func stickValue(_ lowByte: UInt8, _ highByte: UInt8) -> UInt16 {
        let raw = Int(lowByte) | Int(highByte) << 8
        let scaled = (raw - 364) * 4_096 / 165
        return UInt16(clamping: min(max(scaled, 0), 32_767))
    }
}
