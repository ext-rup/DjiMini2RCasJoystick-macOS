#include <DriverKit/DriverKit.h>
#include <os/log.h>

#include "DjiRCVirtualJoystickRoot.h"

#define LOG(fmt, ...) os_log(OS_LOG_DEFAULT, "DjiRCVirtualJoystick: " fmt, ##__VA_ARGS__)

struct DjiRCVirtualJoystickRoot_IVars {
    IOService * device = nullptr;
};

kern_return_t IMPL(DjiRCVirtualJoystickRoot, Start)
{
    kern_return_t result = Start(provider, SUPERDISPATCH);
    if (result != kIOReturnSuccess) {
        return result;
    }

    ivars = IONewZero(DjiRCVirtualJoystickRoot_IVars, 1);
    if (!ivars) {
        Stop(provider, SUPERDISPATCH);
        return kIOReturnNoMemory;
    }

    result = Create(this, "VirtualDeviceProperties", &ivars->device);
    if (result != kIOReturnSuccess) {
        LOG("failed to create virtual joystick: 0x%x", result);
        OSSafeReleaseNULL(ivars->device);
        IOSafeDeleteNULL(ivars, DjiRCVirtualJoystickRoot_IVars, 1);
        Stop(provider, SUPERDISPATCH);
        return result;
    }

    RegisterService();
    LOG("virtual joystick created");
    return kIOReturnSuccess;
}

kern_return_t IMPL(DjiRCVirtualJoystickRoot, Stop)
{
    if (ivars && ivars->device) {
        ivars->device->Terminate(0);
        OSSafeReleaseNULL(ivars->device);
    }
    IOSafeDeleteNULL(ivars, DjiRCVirtualJoystickRoot_IVars, 1);
    return Stop(provider, SUPERDISPATCH);
}
