using System.Collections.Generic;

namespace x360ce.App.Input.Devices
{
    /// <summary>
    /// Combined device management class that orchestrates different input device types.
    /// Provides unified access to DirectInput, XInput, and other input methods.
    /// </summary>
    internal class DevicesCombined
    {
        /// <summary>
        /// Runs the PnPInput device enumeration method from DevicesPnPInput class.
        /// </summary>
        /// <returns>List of PnPInputDeviceInfo objects representing all PnPInput devices</returns>
        public List<PnPInputDeviceInfo> RunPnPInputEnumeration()
        {
            var pnPInputDevices = new DevicesPnPInput();
            return pnPInputDevices.GetPnPInputDeviceList();
        }


        /// <summary>
        /// Runs the DirectInput device enumeration method from DevicesDirectInput class.
        /// </summary>
        /// <returns>List of DirectInputDeviceInfo objects representing all DirectInput devices</returns>
        public List<DirectInputDeviceInfo> RunDirectInputEnumeration()
        {
            var directInputDevices = new DevicesDirectInput();
            return directInputDevices.GetDirectInputDeviceList();
        }

        /// <summary>
        /// Runs the XInput device enumeration method from DevicesXInput class.
        /// </summary>
        /// <returns>List of XInputDeviceInfo objects representing all XInput devices</returns>
        public List<XInputDeviceInfo> RunXInputEnumeration()
        {
            var xInputDevices = new DevicesXInput();
            return xInputDevices.GetXInputDeviceList();
        }

        /// <summary>
        /// Runs the RawInput device enumeration method from DevicesRawInput class.
        /// </summary>
        /// <returns>List of RawInputDeviceInfo objects representing all RawInput devices</returns>
        public List<RawInputDeviceInfo> RunRawInputEnumeration()
        {
            var rawInputDevices = new DevicesRawInput();
            return rawInputDevices.GetRawInputDeviceList();
        }

        /// <summary>
        /// Runs the GamingInput device enumeration method from DevicesGamingInput class.
        /// </summary>
        /// <returns>List of GamingInputDeviceInfo objects representing all GamingInput devices</returns>
        public List<GamingInputDeviceInfo> RunGamingInputEnumeration()
        {
            var gamingInputDevices = new DevicesGamingInput();
            return gamingInputDevices.GetGamingInputDeviceList();
        }

    }
}
