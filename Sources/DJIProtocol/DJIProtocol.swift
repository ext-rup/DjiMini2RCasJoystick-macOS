public enum DJIProtocol {
    public static func packet(
        source: UInt8 = 0x0a,
        target: UInt8 = 0x06,
        commandType: UInt8 = 0x40,
        commandSet: UInt8 = 0x06,
        commandID: UInt8,
        payload: [UInt8] = [],
        sequence: UInt16
    ) -> [UInt8] {
        let length = 13 + payload.count
        precondition(length <= 0x3ff, "DUML packet exceeds its 10-bit length field")

        var result: [UInt8] = [
            0x55,
            UInt8(length & 0xff),
            UInt8((length >> 8) | 0x04),
        ]
        result.append(headerChecksum(seed: 0x77, bytes: result))
        result += [source, target, UInt8(sequence & 0xff), UInt8(sequence >> 8)]
        result += [commandType, commandSet, commandID]
        result += payload

        let checksum = checksum16(bytes: result)
        result += [UInt8(checksum & 0xff), UInt8(checksum >> 8)]
        return result
    }

    public static func checksum16(seed: UInt16 = 0x3692, bytes: [UInt8]) -> UInt16 {
        var checksum = seed
        for byte in bytes {
            checksum ^= UInt16(byte)
            for _ in 0..<8 {
                checksum = (checksum >> 1) ^ (checksum & 1 == 1 ? 0x8408 : 0)
            }
        }
        return checksum
    }

    public static func headerChecksum(seed: UInt8, bytes: [UInt8]) -> UInt8 {
        var checksum = seed
        for byte in bytes {
            checksum ^= byte
            for _ in 0..<8 {
                checksum = (checksum >> 1) ^ (checksum & 1 == 1 ? 0x8c : 0)
            }
        }
        return checksum
    }

    public static func isValid(packet: [UInt8]) -> Bool {
        guard packet.count >= 13, packet[0] == 0x55 else { return false }
        let declaredLength = (Int(packet[2]) << 8 | Int(packet[1])) & 0x03ff
        guard declaredLength == packet.count, packet[2] & 0xfc == 0x04 else { return false }
        guard packet[3] == headerChecksum(seed: 0x77, bytes: Array(packet.prefix(3))) else {
            return false
        }

        let expectedChecksum = UInt16(packet[packet.count - 2]) | UInt16(packet[packet.count - 1]) << 8
        return expectedChecksum == checksum16(bytes: Array(packet.dropLast(2)))
    }
}
