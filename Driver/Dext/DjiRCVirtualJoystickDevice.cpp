#include <DriverKit/DriverKit.h>
#include <HIDDriverKit/HIDDriverKit.h>

#include "DjiRCVirtualJoystickDevice.h"

static const uint8_t kControlReportID = 0x7f;

// Four 15-bit axes, four buttons, and a three-position switch. Report 0x7f
// is a private feature report used by the serial bridge to inject report 1.
static const uint8_t kReportDescriptor[] = {
    0x05, 0x01,             // Usage Page (Generic Desktop)
    0x09, 0x04,             // Usage (Joystick)
    0xa1, 0x01,             // Collection (Application)
    0x85, 0x01,             //   Report ID (1)
    0x09, 0x30,             //   Usage (X)
    0x09, 0x31,             //   Usage (Y)
    0x09, 0x33,             //   Usage (Rx)
    0x09, 0x34,             //   Usage (Ry)
    0x15, 0x00,             //   Logical Minimum (0)
    0x26, 0xff, 0x7f,       //   Logical Maximum (32767)
    0x75, 0x10,             //   Report Size (16)
    0x95, 0x04,             //   Report Count (4)
    0x81, 0x02,             //   Input (Data, Variable, Absolute)
    0x05, 0x09,             //   Usage Page (Button)
    0x19, 0x01,             //   Usage Minimum (1)
    0x29, 0x04,             //   Usage Maximum (4)
    0x15, 0x00,             //   Logical Minimum (0)
    0x25, 0x01,             //   Logical Maximum (1)
    0x75, 0x01,             //   Report Size (1)
    0x95, 0x04,             //   Report Count (4)
    0x81, 0x02,             //   Input (Data, Variable, Absolute)
    0x75, 0x01,             //   Report Size (1)
    0x95, 0x04,             //   Report Count (4)
    0x81, 0x03,             //   Input (Constant)
    0x05, 0x01,             //   Usage Page (Generic Desktop)
    0x09, 0x38,             //   Usage (Wheel)
    0x15, 0xff,             //   Logical Minimum (-1)
    0x25, 0x01,             //   Logical Maximum (1)
    0x75, 0x08,             //   Report Size (8)
    0x95, 0x01,             //   Report Count (1)
    0x81, 0x02,             //   Input (Data, Variable, Absolute)
    0x06, 0x00, 0xff,       //   Usage Page (Vendor Defined)
    0x09, 0x01,             //   Usage (1)
    0x85, 0x7f,             //   Report ID (127)
    0x75, 0x08,             //   Report Size (8)
    0x95, 0x0b,             //   Report Count (11)
    0xb1, 0x02,             //   Feature (Data, Variable, Absolute)
    0xc0,                   // End Collection
};

OSDictionary * DjiRCVirtualJoystickDevice::newDeviceDescription(void)
{
    auto dictionary = OSDictionary::withCapacity(8);
    if (!dictionary) {
        return nullptr;
    }

    struct NumberEntry { const char * key; uint32_t value; };
    const NumberEntry numbers[] = {
        { "VendorID", 0x2ca3 },
        { "ProductID", 0x161b },
        { "VersionNumber", 0x0100 },
        { "CountryCode", 0 },
    };
    for (auto & entry : numbers) {
        if (auto number = OSNumber::withNumber(entry.value, 32)) {
            dictionary->setObject(entry.key, number);
            number->release();
        }
    }

    struct StringEntry { const char * key; const char * value; };
    const StringEntry strings[] = {
        { "Transport", "USB" },
        { "Product", "DJI Mini 2 RC Virtual Joystick" },
        { "Manufacturer", "DJI RC Joystick" },
        { "SerialNumber", "dji-mini2-virtual" },
    };
    for (auto & entry : strings) {
        if (auto string = OSString::withCString(entry.value)) {
            dictionary->setObject(entry.key, string);
            string->release();
        }
    }

    dictionary->setObject("RegisterService", kOSBooleanTrue);
    return dictionary;
}

OSData * DjiRCVirtualJoystickDevice::newReportDescriptor(void)
{
    return OSData::withBytes(kReportDescriptor, sizeof(kReportDescriptor));
}

kern_return_t DjiRCVirtualJoystickDevice::setReport(IOMemoryDescriptor * report,
                                                     IOHIDReportType reportType,
                                                     IOOptionBits options,
                                                     uint32_t completionTimeout,
                                                     OSAction * action)
{
    kern_return_t result = kIOReturnUnsupported;
    IOMemoryMap * map = nullptr;
    if (reportType == kIOHIDReportTypeFeature && report &&
        report->CreateMapping(0, 0, 0, 0, 0, &map) == kIOReturnSuccess && map) {
        const uint64_t address = map->GetAddress();
        const uint64_t length = map->GetLength();
        const uint8_t * bytes = reinterpret_cast<const uint8_t *>(address);

        if (address && length >= 3 && bytes[0] == kControlReportID) {
            const uint64_t innerLength = length - 1;
            IOBufferMemoryDescriptor * buffer = nullptr;
            if (IOBufferMemoryDescriptor::Create(kIOMemoryDirectionInOut, innerLength, 0, &buffer)
                    == kIOReturnSuccess) {
                uint64_t bufferAddress = 0;
                uint64_t bufferLength = 0;
                buffer->Map(0, 0, 0, 0, &bufferAddress, &bufferLength);
                if (bufferAddress) {
                    memcpy(reinterpret_cast<void *>(bufferAddress), bytes + 1, innerLength);
                    result = handleReport(
                        mach_absolute_time(),
                        buffer,
                        static_cast<uint32_t>(innerLength),
                        kIOHIDReportTypeInput,
                        0
                    );
                }
                buffer->release();
            }
        }
        map->release();
    }

    if (action) {
        CompleteReport(action, result, 0);
    }
    return result;
}
