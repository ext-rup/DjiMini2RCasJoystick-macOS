import Darwin
#if canImport(DJIProtocol)
import DJIProtocol
#endif
import Foundation

struct SerialPortError: Error, CustomStringConvertible {
    let description: String
}

final class SerialPort {
    private let descriptor: Int32

    init(path: String) throws {
        descriptor = Darwin.open(path, O_RDWR | O_NOCTTY | O_NONBLOCK)
        guard descriptor >= 0 else {
            throw SerialPortError(description: "Could not open \(path): \(String(cString: strerror(errno)))")
        }

        do {
            try configure()
        } catch {
            Darwin.close(descriptor)
            throw error
        }
    }

    deinit {
        Darwin.close(descriptor)
    }

    func write(_ bytes: [UInt8]) throws {
        var offset = 0
        while offset < bytes.count {
            let written = bytes.withUnsafeBytes { buffer in
                Darwin.write(descriptor, buffer.baseAddress!.advanced(by: offset), bytes.count - offset)
            }
            guard written >= 0 else {
                if errno == EINTR { continue }
                throw SerialPortError(description: "Serial write failed: \(String(cString: strerror(errno)))")
            }
            offset += written
        }
    }

    func readByte(timeoutMilliseconds: Int32) throws -> UInt8? {
        var pollDescriptor = pollfd(fd: descriptor, events: Int16(POLLIN), revents: 0)
        let pollResult = Darwin.poll(&pollDescriptor, 1, timeoutMilliseconds)
        guard pollResult >= 0 else {
            if errno == EINTR { return nil }
            throw SerialPortError(description: "Serial poll failed: \(String(cString: strerror(errno)))")
        }
        if pollDescriptor.revents & Int16(POLLERR | POLLHUP | POLLNVAL) != 0 {
            throw SerialPortError(description: "Serial device disconnected")
        }
        guard pollResult > 0, pollDescriptor.revents & Int16(POLLIN) != 0 else { return nil }

        var byte: UInt8 = 0
        let count = Darwin.read(descriptor, &byte, 1)
        guard count >= 0 else {
            if errno == EAGAIN || errno == EINTR { return nil }
            throw SerialPortError(description: "Serial read failed: \(String(cString: strerror(errno)))")
        }
        return count == 1 ? byte : nil
    }

    func readPacket(timeoutMilliseconds: Int32 = 250) throws -> [UInt8]? {
        let deadline = Date().addingTimeInterval(Double(timeoutMilliseconds) / 1_000)
        while Date() < deadline {
            guard try readByte(timeoutMilliseconds: 20) == 0x55 else { continue }

            var result: [UInt8] = [0x55]
            while result.count < 4, Date() < deadline {
                guard let byte = try readByte(timeoutMilliseconds: 20) else { continue }
                result.append(byte)
            }
            guard result.count == 4 else { return nil }

            let packetLength = (Int(result[2]) << 8 | Int(result[1])) & 0x03ff
            guard packetLength >= 13,
                  result[2] & 0xfc == 0x04,
                  result[3] == DJIProtocol.headerChecksum(seed: 0x77, bytes: Array(result.prefix(3)))
            else {
                continue
            }

            while result.count < packetLength, Date() < deadline {
                guard let byte = try readByte(timeoutMilliseconds: 20) else { continue }
                result.append(byte)
            }
            guard result.count == packetLength else { return nil }
            if DJIProtocol.isValid(packet: result) { return result }
        }
        return nil
    }

    private func configure() throws {
        var options = termios()
        guard tcgetattr(descriptor, &options) == 0 else {
            throw SerialPortError(description: "Could not read serial settings: \(String(cString: strerror(errno)))")
        }

        cfmakeraw(&options)
        guard cfsetspeed(&options, speed_t(B115200)) == 0 else {
            throw SerialPortError(description: "Could not set serial speed: \(String(cString: strerror(errno)))")
        }
        let disabledFlags = tcflag_t(
            PARENB | CSTOPB | CSIZE | CCTS_OFLOW | CRTS_IFLOW | CDTR_IFLOW | CDSR_OFLOW | CCAR_OFLOW
        )
        options.c_cflag = (options.c_cflag & ~disabledFlags) | tcflag_t(CS8 | CLOCAL | CREAD)
        guard tcsetattr(descriptor, TCSANOW, &options) == 0 else {
            throw SerialPortError(description: "Could not configure serial port: \(String(cString: strerror(errno)))")
        }
        guard fcntl(descriptor, F_SETFL, 0) == 0 else {
            throw SerialPortError(description: "Could not enable blocking serial writes: \(String(cString: strerror(errno)))")
        }
        tcflush(descriptor, TCIOFLUSH)
    }
}
