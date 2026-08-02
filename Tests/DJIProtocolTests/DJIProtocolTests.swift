import XCTest
@testable import DJIProtocol

final class DJIProtocolTests: XCTestCase {
    private func response(length: Int, commandID: UInt8, mutate: (inout [UInt8]) -> Void) -> [UInt8] {
        var packet = DJIProtocol.packet(
            commandID: commandID,
            payload: [UInt8](repeating: 0, count: length - 13),
            sequence: 0x34eb
        )
        mutate(&packet)
        let checksum = DJIProtocol.checksum16(bytes: Array(packet.dropLast(2)))
        packet[packet.count - 2] = UInt8(checksum & 0xff)
        packet[packet.count - 1] = UInt8(checksum >> 8)
        return packet
    }

    func testStickScaling() {
        XCTAssertEqual(ControllerState.stickValue(0x6c, 0x01), 0)
        XCTAssertEqual(ControllerState.stickValue(0x00, 0x04), 16_384)
        XCTAssertEqual(ControllerState.stickValue(0x94, 0x06), 32_767)
    }

    func testStickPacketMapping() {
        let packet = response(length: 38, commandID: 0x01) {
            $0[13] = 0x00; $0[14] = 0x04
            $0[16] = 0x6c; $0[17] = 0x01
            $0[19] = 0x94; $0[20] = 0x06
            $0[22] = 0x00; $0[23] = 0x04
        }

        var state = ControllerState()
        XCTAssertTrue(state.apply(packet: packet))
        XCTAssertEqual(state.rightHorizontal, 16_384)
        XCTAssertEqual(state.rightVertical, 0)
        XCTAssertEqual(state.leftVertical, 32_767)
        XCTAssertEqual(state.leftHorizontal, 16_384)
    }

    func testButtonsPacketMapping() {
        let packet = response(length: 58, commandID: 0x27) {
            $0[28] = 0x10
            $0[29] = 0x66
        }

        var state = ControllerState()
        XCTAssertTrue(state.apply(packet: packet))
        XCTAssertEqual(state.buttons, 0b1101)
        XCTAssertEqual(state.centerSwitch, 0)
    }

    func testDumlPacketLayoutAndChecksums() {
        let packet = DJIProtocol.packet(commandID: 0x24, payload: [0x01], sequence: 0x34eb)
        XCTAssertEqual(packet, [0x55, 0x0e, 0x04, 0x66, 0x0a, 0x06, 0xeb, 0x34, 0x40, 0x06, 0x24, 0x01, 0xd9, 0xec])
        XCTAssertEqual(packet.count, 14)
        XCTAssertEqual(Array(packet.prefix(3)), [0x55, 0x0e, 0x04])
        XCTAssertEqual(packet[4...10], [0x0a, 0x06, 0xeb, 0x34, 0x40, 0x06, 0x24])
        XCTAssertEqual(packet[11], 0x01)
        XCTAssertEqual(packet[3], DJIProtocol.headerChecksum(seed: 0x77, bytes: Array(packet.prefix(3))))

        let expectedChecksum = DJIProtocol.checksum16(bytes: Array(packet.dropLast(2)))
        XCTAssertEqual(packet[12], UInt8(expectedChecksum & 0xff))
        XCTAssertEqual(packet[13], UInt8(expectedChecksum >> 8))
        XCTAssertTrue(DJIProtocol.isValid(packet: packet))
    }

    func testRejectsCorruptAndUnexpectedPackets() {
        var corrupt = response(length: 38, commandID: 0x01) { _ in }
        corrupt[13] ^= 0xff

        var state = ControllerState()
        XCTAssertFalse(state.apply(packet: corrupt))
        XCTAssertFalse(state.apply(packet: response(length: 38, commandID: 0x27) { _ in }))
    }
}
