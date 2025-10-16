
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace x360ce.App.Input.Devices
{
    /// <summary>
    /// RawInput device container with both device information and the actual device handle.
    /// Contains comprehensive device metadata plus the device handle for input reading.
    /// </summary>
    /// <remarks>
    /// RawInput API Capabilities:
    /// • Provides device interface paths, VID/PID, HID usage page/usage, and basic device type
    /// • For HID devices: Parses HID Report Descriptor to extract actual button/axis/POV counts and force feedback support
    /// • For Mouse/Keyboard: Provides actual button/key counts from RawInput API
    /// • Does NOT provide friendly device names or manufacturer information (requires additional Windows APIs)
    ///
    /// Note: HID Report Descriptor parsing provides device-reported capabilities, which are accurate for most devices.
    /// </remarks>
    public class RawInputDeviceInfo : IDisposable
    {
        public Guid InstanceGuid { get; set; }
        public string InstanceName { get; set; }
        public Guid ProductGuid { get; set; }
        public string ProductName { get; set; }
        public int DeviceType { get; set; }
        public int DeviceSubtype { get; set; }
        public int Usage { get; set; }
        public int UsagePage { get; set; }
        public string InputType { get; set; }
        public int AxeCount { get; set; }
        public int SliderCount { get; set; }
        public int ButtonCount { get; set; }
        public int KeyCount { get; set; }
        public int PovCount { get; set; }
        
        // Simulation Controls (Usage Page 0x02)
        public int ThrottleCount { get; set; }
        public int BrakeCount { get; set; }
        public int SteeringCount { get; set; }
        public int AcceleratorCount { get; set; }
        public int ClutchCount { get; set; }
        
        /// <summary>
        /// Force feedback availability. Always false for RawInput devices.
        /// RawInput API does not provide force feedback capability information.
        /// Use DirectInput or XInput APIs to determine actual force feedback support.
        /// </summary>
        public bool HasForceFeedback { get; set; }
        public int DriverVersion { get; set; }
        public int HardwareRevision { get; set; }
        public int FirmwareRevision { get; set; }
        public bool IsOnline { get; set; }
        public string DeviceTypeName { get; set; }
        public string InterfacePath { get; set; }
        
        // Common identifier for grouping devices from same physical hardware
        public string CommonIdentifier { get; set; }
        
        // Additional identification properties
        public int VendorId { get; set; }
        public int ProductId { get; set; }
        public Guid ClassGuid { get; set; }
        public string HardwareIds { get; set; }
        public string DeviceId { get; set; }
        public string ParentDeviceId { get; set; }
        
        // RawInput-specific properties
        public IntPtr DeviceHandle { get; set; }
        public RawInputDeviceType RawInputDeviceType { get; set; }
        public uint RawInputFlags { get; set; }
        public string RegistryPath { get; set; }
        
        /// <summary>
        /// Indicates whether this device uses Report IDs in its HID reports.
        /// When true, the first byte of the report is the Report ID.
        /// When false, button data starts at byte 0.
        /// </summary>
        public bool UsesReportIds { get; set; }
        
        /// <summary>
        /// The byte offset where button data starts in the HID report.
        /// Typically 0 (no report ID) or 1 (with report ID).
        /// </summary>
        public int ButtonDataOffset { get; set; }
        
        /// <summary>
        /// Note: RawInput API does not provide native friendly names or manufacturer information.
        /// RawInput only provides device interface paths and basic HID information.
        /// Friendly names would require additional Windows Registry queries or device manager APIs
        /// which are not part of the core RawInput functionality.
        /// </summary>
        
        /// <summary>
        /// Display name combining device type and name for easy identification.
        /// </summary>
        public string DisplayName => $"{DeviceTypeName} - {InstanceName}";
        
        /// <summary>
        /// VID/PID string in standard format for hardware identification.
        /// </summary>
        public string VidPidString => $"VID_{VendorId:X4}&PID_{ProductId:X4}";
        
        /// <summary>
        /// Dispose the RawInput device when no longer needed.
        /// </summary>
        public void Dispose()
        {
            // RawInput devices don't need explicit disposal, but we clear the handle
            DeviceHandle = IntPtr.Zero;
        }
    }

    /// <summary>
    /// RawInput device types enumeration.
    /// </summary>
    public enum RawInputDeviceType : uint
    {
        Mouse = 0,
        Keyboard = 1,
        HID = 2
    }

    /// <summary>
    /// RawInput device enumeration and management class.
    /// Self-contained implementation with minimal external dependencies.
    /// Provides functionality to discover and list RawInput devices including gamepads, keyboards, and mice.
    /// Returns device information that can be used for input reading through RawInput API.
    /// </summary>
    internal class DevicesRawInput
    {
        #region Win32 API Structures and Constants

        /// <summary>
        /// Contains information about a raw input device.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTDEVICELIST
        {
            public IntPtr hDevice;
            public uint dwType;
        }

        /// <summary>
        /// Contains information about a raw input device.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct RID_DEVICE_INFO
        {
            public uint cbSize;
            public uint dwType;
            public RID_DEVICE_INFO_UNION union;
        }

        /// <summary>
        /// Union for different device types in RID_DEVICE_INFO.
        /// </summary>
        [StructLayout(LayoutKind.Explicit)]
        private struct RID_DEVICE_INFO_UNION
        {
            [FieldOffset(0)]
            public RID_DEVICE_INFO_MOUSE mouse;
            [FieldOffset(0)]
            public RID_DEVICE_INFO_KEYBOARD keyboard;
            [FieldOffset(0)]
            public RID_DEVICE_INFO_HID hid;
        }

        /// <summary>
        /// Mouse device information.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct RID_DEVICE_INFO_MOUSE
        {
            public uint dwId;
            public uint dwNumberOfButtons;
            public uint dwSampleRate;
            public bool fHasHorizontalWheel;
        }

        /// <summary>
        /// Keyboard device information.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct RID_DEVICE_INFO_KEYBOARD
        {
            public uint dwType;
            public uint dwSubType;
            public uint dwKeyboardMode;
            public uint dwNumberOfFunctionKeys;
            public uint dwNumberOfIndicators;
            public uint dwNumberOfKeysTotal;
        }

        /// <summary>
        /// HID device information.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct RID_DEVICE_INFO_HID
        {
            public uint dwVendorId;
            public uint dwProductId;
            public uint dwVersionNumber;
            public ushort usUsagePage;
            public ushort usUsage;
        }

        /// <summary>
        /// HID preparsed data structure (opaque handle).
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct HIDP_PREPARSED_DATA
        {
            public IntPtr Data;
        }

        /// <summary>
        /// HID device capabilities structure.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct HIDP_CAPS
        {
            public ushort Usage;
            public ushort UsagePage;
            public ushort InputReportByteLength;
            public ushort OutputReportByteLength;
            public ushort FeatureReportByteLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
            public ushort[] Reserved;
            public ushort NumberLinkCollectionNodes;
            public ushort NumberInputButtonCaps;
            public ushort NumberInputValueCaps;
            public ushort NumberInputDataIndices;
            public ushort NumberOutputButtonCaps;
            public ushort NumberOutputValueCaps;
            public ushort NumberOutputDataIndices;
            public ushort NumberFeatureButtonCaps;
            public ushort NumberFeatureValueCaps;
            public ushort NumberFeatureDataIndices;
        }

        /// <summary>
        /// HID button capabilities structure.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct HIDP_BUTTON_CAPS
        {
            public ushort UsagePage;
            public byte ReportID;
            [MarshalAs(UnmanagedType.U1)]
            public bool IsAlias;
            public ushort BitField;
            public ushort LinkCollection;
            public ushort LinkUsage;
            public ushort LinkUsagePage;
            [MarshalAs(UnmanagedType.U1)]
            public bool IsRange;
            [MarshalAs(UnmanagedType.U1)]
            public bool IsStringRange;
            [MarshalAs(UnmanagedType.U1)]
            public bool IsDesignatorRange;
            [MarshalAs(UnmanagedType.U1)]
            public bool IsAbsolute;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
            public uint[] Reserved;
            public HIDP_BUTTON_CAPS_RANGE Range;
            public HIDP_BUTTON_CAPS_NOT_RANGE NotRange;
        }

        /// <summary>
        /// HID button capabilities range union.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct HIDP_BUTTON_CAPS_RANGE
        {
            public ushort UsageMin;
            public ushort UsageMax;
            public ushort StringMin;
            public ushort StringMax;
            public ushort DesignatorMin;
            public ushort DesignatorMax;
            public ushort DataIndexMin;
            public ushort DataIndexMax;
        }

        /// <summary>
        /// HID button capabilities not range union.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct HIDP_BUTTON_CAPS_NOT_RANGE
        {
            public ushort Usage;
            public ushort Reserved1;
            public ushort StringIndex;
            public ushort Reserved2;
            public ushort DesignatorIndex;
            public ushort Reserved3;
            public ushort DataIndex;
            public ushort Reserved4;
        }

        /// <summary>
        /// HID value capabilities structure.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct HIDP_VALUE_CAPS
        {
            public ushort UsagePage;
            public byte ReportID;
            [MarshalAs(UnmanagedType.U1)]
            public bool IsAlias;
            public ushort BitField;
            public ushort LinkCollection;
            public ushort LinkUsage;
            public ushort LinkUsagePage;
            [MarshalAs(UnmanagedType.U1)]
            public bool IsRange;
            [MarshalAs(UnmanagedType.U1)]
            public bool IsStringRange;
            [MarshalAs(UnmanagedType.U1)]
            public bool IsDesignatorRange;
            [MarshalAs(UnmanagedType.U1)]
            public bool IsAbsolute;
            [MarshalAs(UnmanagedType.U1)]
            public bool HasNull;
            public byte Reserved;
            public ushort BitSize;
            public ushort ReportCount;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
            public ushort[] Reserved2;
            public uint UnitsExp;
            public uint Units;
            public int LogicalMin;
            public int LogicalMax;
            public int PhysicalMin;
            public int PhysicalMax;
            public HIDP_VALUE_CAPS_RANGE Range;
            public HIDP_VALUE_CAPS_NOT_RANGE NotRange;
        }

        /// <summary>
        /// HID value capabilities range union.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct HIDP_VALUE_CAPS_RANGE
        {
            public ushort UsageMin;
            public ushort UsageMax;
            public ushort StringMin;
            public ushort StringMax;
            public ushort DesignatorMin;
            public ushort DesignatorMax;
            public ushort DataIndexMin;
            public ushort DataIndexMax;
        }

        /// <summary>
        /// HID value capabilities not range union.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct HIDP_VALUE_CAPS_NOT_RANGE
        {
            public ushort Usage;
            public ushort Reserved1;
            public ushort StringIndex;
            public ushort Reserved2;
            public ushort DesignatorIndex;
            public ushort Reserved3;
            public ushort DataIndex;
            public ushort Reserved4;
        }

        // HID report types
        private enum HIDP_REPORT_TYPE
        {
            HidP_Input = 0,
            HidP_Output = 1,
            HidP_Feature = 2
        }

        // HID status codes
        private const int HIDP_STATUS_SUCCESS = 0x00110000;

        // RawInput API constants
        private const uint RIDI_DEVICENAME = 0x20000007;
        private const uint RIDI_DEVICEINFO = 0x2000000b;
        private const uint RIDI_PREPARSEDDATA = 0x20000005;
        // Device types
        private const uint RIM_TYPEMOUSE = 0;
        private const uint RIM_TYPEKEYBOARD = 1;
        private const uint RIM_TYPEHID = 2;

        // HID Usage Pages
        private const ushort HID_USAGE_PAGE_GENERIC = 0x01;
        private const ushort HID_USAGE_PAGE_SIMULATION = 0x02;
        private const ushort HID_USAGE_PAGE_GAME = 0x05;

        // HID Usages for Generic Desktop
        private const ushort HID_USAGE_GENERIC_JOYSTICK = 0x04;
        private const ushort HID_USAGE_GENERIC_GAMEPAD = 0x05;
        private const ushort HID_USAGE_GENERIC_MULTI_AXIS_CONTROLLER = 0x08;

        #endregion

        #region Win32 API Imports

        /// <summary>
        /// Enumerates the raw input devices attached to the system.
        /// </summary>
        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetRawInputDeviceList(
            [Out] RAWINPUTDEVICELIST[] pRawInputDeviceList,
            ref uint puiNumDevices,
            uint cbSize);

        /// <summary>
        /// Gets information about the raw input device.
        /// </summary>
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern uint GetRawInputDeviceInfo(
            IntPtr hDevice,
            uint uiCommand,
            IntPtr pData,
            ref uint pcbSize);

        /// <summary>
        /// Gets the last Win32 error code.
        /// </summary>
        [DllImport("kernel32.dll")]
        private static extern uint GetLastError();

        /// <summary>
        /// Gets the capabilities of a HID device from preparsed data.
        /// </summary>
        [DllImport("hid.dll", SetLastError = true)]
        private static extern int HidP_GetCaps(
            IntPtr PreparsedData,
            out HIDP_CAPS Capabilities);

        /// <summary>
        /// Gets button capabilities for a specific report type.
        /// </summary>
        [DllImport("hid.dll", SetLastError = true)]
        private static extern int HidP_GetButtonCaps(
            HIDP_REPORT_TYPE ReportType,
            [Out] HIDP_BUTTON_CAPS[] ButtonCaps,
            ref ushort ButtonCapsLength,
            IntPtr PreparsedData);

        /// <summary>
        /// Gets value capabilities for a specific report type.
        /// </summary>
        [DllImport("hid.dll", SetLastError = true)]
        private static extern int HidP_GetValueCaps(
            HIDP_REPORT_TYPE ReportType,
            [Out] HIDP_VALUE_CAPS[] ValueCaps,
            ref ushort ValueCapsLength,
            IntPtr PreparsedData);

        /// <summary>
        /// Gets a usage value from a HID input report.
        /// </summary>
        [DllImport("hid.dll", SetLastError = true)]
        private static extern int HidP_GetUsageValue(
            HIDP_REPORT_TYPE ReportType,
            ushort UsagePage,
            ushort LinkCollection,
            ushort Usage,
            out int UsageValue,
            IntPtr PreparsedData,
            IntPtr Report,
            uint ReportLength);

        #endregion

        /// <summary>
        /// Creates a public list of RawInput devices (gamepads, keyboards, mice) with device information and logs their properties.
        /// This method enumerates all available RawInput devices and outputs detailed information for debugging.
        /// </summary>
        /// <returns>List of RawInputDeviceInfo objects containing device information and handles</returns>
        /// <remarks>
        /// This method performs comprehensive RawInput device enumeration:
        /// • Discovers all RawInput-compatible devices (HID devices, keyboards, mice)
        /// • Creates RawInputDeviceInfo objects with device information AND device handles
        /// • Logs detailed device properties using Debug.WriteLine for diagnostics
        /// • Filters devices by type and availability
        /// • Provides device capability information where available
        /// • Keeps device handles for immediate input reading
        /// • Is self-contained with minimal external dependencies
        ///
        /// IMPORTANT: The returned RawInputDeviceInfo objects contain device handles.
        /// Call Dispose() on each RawInputDeviceInfo when no longer needed to free resources.
        /// </remarks>
        public List<RawInputDeviceInfo> GetRawInputDeviceList()
        {
            var stopwatch = Stopwatch.StartNew();
            var deviceList = new List<RawInputDeviceInfo>();
            var deviceListDebugLines = new List<string>();
            int deviceListIndex = 0;

            try
            {
                Debug.WriteLine("\n-----------------------------------------------------------------------------------------------------------------\n\n" +
                    "DevicesRawInput: Starting RawInput device enumeration...");

                // Get device count
                uint deviceCount = 0;
                uint structSize = (uint)Marshal.SizeOf<RAWINPUTDEVICELIST>();
                uint result = GetRawInputDeviceList(null, ref deviceCount, structSize);
                
                if (result == uint.MaxValue || deviceCount == 0)
                {
                    LogEmptyResult(deviceListDebugLines, result == uint.MaxValue ? GetLastError() : 0);
                    return deviceList;
                }

                Debug.WriteLine($"DevicesRawInput: Found {deviceCount} RawInput devices");

                // Enumerate devices
                var rawDevices = new RAWINPUTDEVICELIST[deviceCount];
                result = GetRawInputDeviceList(rawDevices, ref deviceCount, structSize);

                if (result == uint.MaxValue)
                {
                    LogEmptyResult(deviceListDebugLines, GetLastError());
                    return deviceList;
                }

                // Process each device with optimized early filtering
                foreach (var rawDevice in rawDevices)
                {
                    try
                    {
                        if (ShouldProcessDevice(rawDevice, out bool isInputDevice) && isInputDevice)
                        {
                            ProcessInputDevice(rawDevice, ref deviceListIndex, deviceList, deviceListDebugLines);
                        }
                    }
                    catch (Exception deviceEx)
                    {
                        Debug.WriteLine($"DevicesRawInput: Error processing device 0x{rawDevice.hDevice.ToInt64():X8}: {deviceEx.Message}");
                    }
                }

                LogSummary(deviceList, stopwatch, deviceListDebugLines);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DevicesRawInput: Fatal error during RawInput device enumeration: {ex.Message}");
                Debug.WriteLine($"DevicesRawInput: Stack trace: {ex.StackTrace}");
            }

            foreach (var debugLine in deviceListDebugLines) { Debug.WriteLine(debugLine); }

            return deviceList;
        }

        /// <summary>
        /// Disposes all RawInput devices in the provided list to free resources.
        /// Call this method when the device list is no longer needed.
        /// </summary>
        /// <param name="deviceList">List of RawInputDeviceInfo objects to dispose</param>
        public static void DisposeDeviceList(List<RawInputDeviceInfo> deviceList)
        {
            if (deviceList == null) return;

            Debug.WriteLine($"DevicesRawInput: Disposing {deviceList.Count} RawInput devices...");

            foreach (var deviceInfo in deviceList)
            {
                try
                {
                    if (deviceInfo != null)
                    {
                        Debug.WriteLine($"DevicesRawInput: Disposing device - {deviceInfo.InstanceName}");
                        deviceInfo.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"DevicesRawInput: Error disposing device {deviceInfo?.InstanceName}: {ex.Message}");
                }
            }

            Debug.WriteLine("DevicesRawInput: All RawInput devices disposed.");
        }

        #region Private Helper Methods
        /// <summary>
        /// Logs empty result when device enumeration fails or finds no devices.
        /// </summary>
        private void LogEmptyResult(List<string> debugLines, uint errorCode)
        {
        	string message = errorCode != 0
        		? $"DevicesRawInput: Failed to enumerate devices. Error: {errorCode}"
        		: "DevicesRawInput: No RawInput devices found";
        	
        	Debug.WriteLine(message);
        	debugLines.Add(message);
        	debugLines.Add("\nDevicesRawInput: RawInput devices found: 0, HID: 0, Keyboards: 0, Mice: 0, Offline/Failed: 0\n");
        	foreach (var debugLine in debugLines) { Debug.WriteLine(debugLine); }
        }

        /// <summary>
        /// Logs summary statistics for device enumeration.
        /// </summary>
        private void LogSummary(List<RawInputDeviceInfo> deviceList, Stopwatch stopwatch, List<string> debugLines)
        {
            var hidCount = deviceList.Count(d => d.RawInputDeviceType == RawInputDeviceType.HID);
            var keyboardCount = deviceList.Count(d => d.RawInputDeviceType == RawInputDeviceType.Keyboard);
            var mouseCount = deviceList.Count(d => d.RawInputDeviceType == RawInputDeviceType.Mouse);
            var gamepadCount = deviceList.Count(d => IsGamepadDevice(d.Usage, d.UsagePage));
            var offlineCount = deviceList.Count(d => !d.IsOnline);

            stopwatch.Stop();

            debugLines.Add($"\nDevicesRawInput: ({(int)Math.Round(stopwatch.Elapsed.TotalMilliseconds)} ms) " +
                $"Input Devices found: {deviceList.Count}, " +
                $"HID: {hidCount}, " +
                $"Gamepads: {gamepadCount}, " +
                $"Keyboards: {keyboardCount}, " +
                $"Mice: {mouseCount}, " +
                $"Offline/Failed: {offlineCount}\n");
        }

        /// <summary>
        /// Determines if a device should be processed based on optimized early filtering.
        /// Returns true if device should be processed, and sets isInputDevice to indicate if it's an input device.
        /// </summary>
        private bool ShouldProcessDevice(RAWINPUTDEVICELIST rawDevice, out bool isInputDevice)
        {
            isInputDevice = false;
            var deviceType = (RawInputDeviceType)rawDevice.dwType;

            // Always process Mouse and Keyboard devices (fastest path)
            if (deviceType == RawInputDeviceType.Mouse || deviceType == RawInputDeviceType.Keyboard)
            {
                isInputDevice = true;
                return true;
            }

            // Only process HID devices
            if (deviceType != RawInputDeviceType.HID)
                return false;

            // Step 1: Get device name once (used for all subsequent checks)
            string deviceName = GetDeviceName(rawDevice.hDevice);
            if (string.IsNullOrEmpty(deviceName))
                return false;

            // Step 2: Quick rejection based on device name patterns (fastest check - no Win32 calls)
            if (IsKnownNonInputDeviceByName(deviceName))
                return false;

            // Step 3: Get device info once (reuse for all checks)
            var deviceInfoStruct = GetDeviceInfo(rawDevice.hDevice);
            if (!deviceInfoStruct.HasValue || deviceInfoStruct.Value.dwType != RIM_TYPEHID)
                return false;

            var hid = deviceInfoStruct.Value.union.hid;
            int usagePage = hid.usUsagePage;
            int usage = hid.usUsage;

            // Step 4: Check if it's a known input device by HID usage (second fastest - simple comparison)
            if (IsKnownInputDeviceByUsage(usagePage, usage))
            {
                isInputDevice = true;
                return true;
            }

            // Step 5: Check if device name suggests it's an input device (pattern matching)
            if (HasInputDeviceNamePattern(deviceName, ""))
            {
                isInputDevice = true;
                return true;
            }

            // Step 6: Final check - verify actual input capabilities (slowest - requires HID parsing)
            // Only do this if previous checks didn't conclusively identify the device
            var tempDeviceInfo = CreateTempDeviceInfo(rawDevice, deviceName, deviceInfoStruct.Value);
            if (tempDeviceInfo != null && HasActualInputCapabilities(tempDeviceInfo))
            {
                isInputDevice = true;
                return true;
            }

            return false;
        }


        /// <summary>
        /// Gets the device name (interface path) for a RawInput device.
        /// </summary>
        /// <param name="hDevice">Device handle</param>
        /// <returns>Device name/interface path</returns>
        private string GetDeviceName(IntPtr hDevice)
        {
            uint size = 0;
            GetRawInputDeviceInfo(hDevice, RIDI_DEVICENAME, IntPtr.Zero, ref size);

            if (size == 0)
                return "";

            IntPtr buffer = Marshal.AllocHGlobal((int)size * Marshal.SystemDefaultCharSize); // Unicode characters
            try
            {
                uint result = GetRawInputDeviceInfo(hDevice, RIDI_DEVICENAME, buffer, ref size);
                return result == uint.MaxValue ? "" : (Marshal.PtrToStringUni(buffer) ?? "");
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        /// <summary>
        /// Gets device information structure for a RawInput device.
        /// </summary>
        /// <param name="hDevice">Device handle</param>
        /// <returns>Device information structure or null if failed</returns>
        private RID_DEVICE_INFO? GetDeviceInfo(IntPtr hDevice)
        {
            uint size = (uint)Marshal.SizeOf<RID_DEVICE_INFO>();
            IntPtr buffer = Marshal.AllocHGlobal((int)size);
            try
            {
                uint result = GetRawInputDeviceInfo(hDevice, RIDI_DEVICEINFO, buffer, ref size);
                if (result == uint.MaxValue)
                    return null;

                var deviceInfo = Marshal.PtrToStructure<RID_DEVICE_INFO>(buffer);
                deviceInfo.cbSize = size;
                return deviceInfo;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        /// <summary>
        /// Gets HID device capabilities by parsing the HID Report Descriptor.
        /// </summary>
        /// <param name="hDevice">Device handle</param>
        /// <param name="axeCount">Output: number of axes</param>
        /// <param name="buttonCount">Output: number of buttons</param>
        /// <param name="povCount">Output: number of POV hats</param>
        /// <param name="hasForceFeedback">Output: whether device supports force feedback</param>
        /// <returns>True if capabilities were successfully retrieved</returns>
        private bool GetHidCapabilities(IntPtr hDevice, out int axeCount, out int sliderCount, out int buttonCount, out int povCount,
            out int throttleCount, out int brakeCount, out int steeringCount, out int acceleratorCount, out int clutchCount,
            out bool hasForceFeedback, out bool usesReportIds, out int buttonDataOffset)
        {
            axeCount = 0;
            sliderCount = 0;
            buttonCount = 0;
            povCount = 0;
            throttleCount = 0;
            brakeCount = 0;
            steeringCount = 0;
            acceleratorCount = 0;
            clutchCount = 0;
            hasForceFeedback = false;
            usesReportIds = false;
            buttonDataOffset = 0;

            IntPtr preparsedData = IntPtr.Zero;
            
            try
            {
                // Get the size of preparsed data
                uint size = 0;
                GetRawInputDeviceInfo(hDevice, RIDI_PREPARSEDDATA, IntPtr.Zero, ref size);
                
                if (size == 0)
                {
                    Debug.WriteLine($"DevicesRawInput: No preparsed data available for device 0x{hDevice.ToInt64():X8}");
                    return false;
                }

                Debug.WriteLine($"DevicesRawInput: Preparsed data size: {size} bytes for device 0x{hDevice.ToInt64():X8}");

                // Allocate buffer and get preparsed data
                preparsedData = Marshal.AllocHGlobal((int)size);
                uint result = GetRawInputDeviceInfo(hDevice, RIDI_PREPARSEDDATA, preparsedData, ref size);
                
                if (result == uint.MaxValue || result == 0)
                {
                    Debug.WriteLine($"DevicesRawInput: Failed to get preparsed data for device 0x{hDevice.ToInt64():X8}, result: {result}");
                    return false;
                }

                Debug.WriteLine($"DevicesRawInput: Successfully retrieved preparsed data for device 0x{hDevice.ToInt64():X8}");

                // Get HID capabilities
                HIDP_CAPS caps;
                int status = HidP_GetCaps(preparsedData, out caps);
                
                if (status != HIDP_STATUS_SUCCESS)
                {
                    Debug.WriteLine($"DevicesRawInput: HidP_GetCaps failed with status 0x{status:X8} for device 0x{hDevice.ToInt64():X8}");
                    return false;
                }

                Debug.WriteLine($"DevicesRawInput: HID Caps - InputButtonCaps: {caps.NumberInputButtonCaps}, InputValueCaps: {caps.NumberInputValueCaps}");

                // Parse input button capabilities and detect Report ID usage
                if (caps.NumberInputButtonCaps > 0)
                {
                    var buttonCaps = new HIDP_BUTTON_CAPS[caps.NumberInputButtonCaps];
                    ushort buttonCapsLength = caps.NumberInputButtonCaps;
                    status = HidP_GetButtonCaps(HIDP_REPORT_TYPE.HidP_Input, buttonCaps, ref buttonCapsLength, preparsedData);
                    
                    if (status == HIDP_STATUS_SUCCESS)
                    {
                        foreach (var buttonCap in buttonCaps)
                        {
                            // Check if device uses Report IDs (non-zero ReportID indicates usage)
                            if (buttonCap.ReportID != 0)
                            {
                                usesReportIds = true;
                            }
                            
                            if (buttonCap.IsRange)
                            {
                                // Range of buttons
                                buttonCount += (buttonCap.Range.UsageMax - buttonCap.Range.UsageMin + 1);
                            }
                            else
                            {
                                // Single button
                                buttonCount++;
                            }
                        }
                    }
                }

                // Parse input value capabilities (axes and POVs) and check for Report IDs
                if (caps.NumberInputValueCaps > 0)
                {
                    var valueCaps = new HIDP_VALUE_CAPS[caps.NumberInputValueCaps];
                    ushort valueCapsLength = caps.NumberInputValueCaps;
                    status = HidP_GetValueCaps(HIDP_REPORT_TYPE.HidP_Input, valueCaps, ref valueCapsLength, preparsedData);
                    
                    if (status == HIDP_STATUS_SUCCESS)
                    {
                        // Also check value caps for Report ID usage
                        foreach (var valueCap in valueCaps)
                        {
                            if (valueCap.ReportID != 0)
                            {
                                usesReportIds = true;
                                break;
                            }
                        }
                        
                        foreach (var valueCap in valueCaps)
                        {
                            // Debug: Log ALL value capabilities regardless of usage page
                            ushort usage = valueCap.IsRange ? valueCap.Range.UsageMin : valueCap.NotRange.Usage;
                            ushort usageMax = valueCap.IsRange ? valueCap.Range.UsageMax : usage;
                            ushort linkUsage = valueCap.LinkUsage;
                            ushort linkUsagePage = valueCap.LinkUsagePage;
                            
                            if (valueCap.IsRange)
                            {
                                Debug.WriteLine($"DevicesRawInput: HID Value Range - UsagePage: 0x{valueCap.UsagePage:X2}, UsageMin: 0x{usage:X2}, UsageMax: 0x{usageMax:X2}, " +
                                    $"LinkUsage: 0x{linkUsage:X2}, LinkUsagePage: 0x{linkUsagePage:X2}, ReportCount: {valueCap.ReportCount}, " +
                                    $"BitSize: {valueCap.BitSize}, LogicalMin: {valueCap.LogicalMin}, LogicalMax: {valueCap.LogicalMax}");
                            }
                            else
                            {
                                Debug.WriteLine($"DevicesRawInput: HID Value - UsagePage: 0x{valueCap.UsagePage:X2}, Usage: 0x{usage:X2}, " +
                                    $"LinkUsage: 0x{linkUsage:X2}, LinkUsagePage: 0x{linkUsagePage:X2}, ReportCount: {valueCap.ReportCount}, " +
                                    $"BitSize: {valueCap.BitSize}, LogicalMin: {valueCap.LogicalMin}, LogicalMax: {valueCap.LogicalMax}");
                            }
                            
                            // Special handling for devices with invalid UsagePage (0x00) but valid LinkUsage
                            // Some devices (like T.16000M) report axes/sliders in LinkUsage when UsagePage is 0
                            // Note: Some devices put the usage code in BOTH LinkUsage and LinkUsagePage fields
                            if (valueCap.UsagePage == 0x00 && linkUsage >= 0x30 && linkUsage <= 0x39 &&
                                (linkUsagePage == HID_USAGE_PAGE_GENERIC || linkUsagePage == linkUsage))
                            {
                                Debug.WriteLine($"DevicesRawInput: Found control with invalid UsagePage but valid LinkUsage: 0x{linkUsage:X2}, ReportCount: {valueCap.ReportCount}");
                                
                                // Standard axes: X(0x30), Y(0x31), Z(0x32), Rx(0x33), Ry(0x34), Rz(0x35)
                                if (linkUsage >= 0x30 && linkUsage <= 0x35)
                                {
                                    axeCount++;
                                    Debug.WriteLine($"DevicesRawInput: Found 1 axis at LinkUsage 0x{linkUsage:X2}");
                                }
                                // Sliders: Slider(0x36), Dial(0x37), Wheel(0x38)
                                else if (linkUsage >= 0x36 && linkUsage <= 0x38)
                                {
                                    sliderCount++;
                                    Debug.WriteLine($"DevicesRawInput: Found 1 slider at LinkUsage 0x{linkUsage:X2}");
                                }
                                // POV Hat Switch (0x39)
                                else if (linkUsage == 0x39)
                                {
                                    povCount++;
                                    Debug.WriteLine($"DevicesRawInput: Found 1 POV at LinkUsage 0x{linkUsage:X2}");
                                }
                                continue; // Skip normal processing since we handled it
                            }
                            
                            // Check for Pointer (0x01) usage - this is a collection that may contain multiple axes
                            // Xbox One controllers report Pointer with ReportCount indicating total axis count
                            if (valueCap.UsagePage == HID_USAGE_PAGE_GENERIC && usage == 0x01)
                            {
                                // Check if this Pointer has nested axes via LinkUsage
                                if (linkUsagePage == HID_USAGE_PAGE_GENERIC && linkUsage >= 0x30 && linkUsage <= 0x39)
                                {
                                    Debug.WriteLine($"DevicesRawInput: Found nested control in Pointer collection - LinkUsage: 0x{linkUsage:X2}, ReportCount: {valueCap.ReportCount}");
                                    
                                    // Standard axes: X(0x30), Y(0x31), Z(0x32), Rx(0x33), Ry(0x34), Rz(0x35)
                                    if (linkUsage >= 0x30 && linkUsage <= 0x35)
                                    {
                                        axeCount++;
                                        Debug.WriteLine($"DevicesRawInput: Found 1 axis (nested) at LinkUsage 0x{linkUsage:X2}");
                                    }
                                    // Sliders: Slider(0x36), Dial(0x37), Wheel(0x38)
                                    else if (linkUsage >= 0x36 && linkUsage <= 0x38)
                                    {
                                        sliderCount++;
                                        Debug.WriteLine($"DevicesRawInput: Found 1 slider (nested) at LinkUsage 0x{linkUsage:X2}");
                                    }
                                    // POV Hat Switch (0x39)
                                    else if (linkUsage == 0x39)
                                    {
                                        povCount++;
                                        Debug.WriteLine($"DevicesRawInput: Found 1 POV (nested) at LinkUsage 0x{linkUsage:X2}");
                                    }
                                    continue; // Skip normal processing for Pointer collections
                                }
                                else
                                {
                                    // Pointer without specific LinkUsage - might indicate a collection of multiple axes
                                    // For Xbox One controllers, the Pointer itself might represent multiple axes
                                    // We'll handle this in the normal processing below, but log it for debugging
                                    Debug.WriteLine($"DevicesRawInput: Found Pointer collection without specific LinkUsage - ReportCount: {valueCap.ReportCount}, LinkUsage: 0x{linkUsage:X2}, LinkUsagePage: 0x{linkUsagePage:X2}");
                                }
                            }
                            
                            // Check usage page and usage to determine if it's an axis or POV
                            if (valueCap.UsagePage == HID_USAGE_PAGE_GENERIC)
                            {
                                // Standard axes: X(0x30), Y(0x31), Z(0x32), Rx(0x33), Ry(0x34), Rz(0x35)
                                if (usage >= 0x30 && usage <= 0x35)
                                {
                                    if (valueCap.IsRange)
                                    {
                                        int usageCount = (valueCap.Range.UsageMax - valueCap.Range.UsageMin + 1);
                                        // Multiply by ReportCount to get actual physical axis count
                                        // ReportCount indicates how many values are reported for each usage
                                        int physicalCount = usageCount * Math.Max(1, (int)valueCap.ReportCount);
                                        axeCount += physicalCount;
                                        Debug.WriteLine($"DevicesRawInput: Found {physicalCount} physical axes ({usageCount} usages × {valueCap.ReportCount} reports) " +
                                            $"in range 0x{usage:X2}-0x{usageMax:X2}, BitSize: {valueCap.BitSize}");
                                    }
                                    else
                                    {
                                        // ReportCount indicates how many physical values are reported for this single usage
                                        // For example, a Pointer collection might report 6 axis values with ReportCount=6
                                        int physicalCount = Math.Max(1, (int)valueCap.ReportCount);
                                        axeCount += physicalCount;
                                        Debug.WriteLine($"DevicesRawInput: Found {physicalCount} physical axis/axes at usage 0x{usage:X2} " +
                                            $"(ReportCount: {valueCap.ReportCount}), BitSize: {valueCap.BitSize}");
                                    }
                                }
                                // Sliders: Slider(0x36), Dial(0x37), Wheel(0x38)
                                else if (usage >= 0x36 && usage <= 0x38)
                                {
                                    if (valueCap.IsRange)
                                    {
                                        int usageCount = (valueCap.Range.UsageMax - valueCap.Range.UsageMin + 1);
                                        sliderCount += usageCount;
                                        Debug.WriteLine($"DevicesRawInput: Found {usageCount} slider(s) in range 0x{usage:X2}-0x{usageMax:X2} (ReportCount: {valueCap.ReportCount})");
                                    }
                                    else
                                    {
                                        sliderCount++;
                                        Debug.WriteLine($"DevicesRawInput: Found 1 slider at usage 0x{usage:X2} (ReportCount: {valueCap.ReportCount})");
                                    }
                                }
                                // POV Hat Switch (0x39)
                                else if (usage == 0x39)
                                {
                                    if (valueCap.IsRange)
                                    {
                                        int usageCount = (valueCap.Range.UsageMax - valueCap.Range.UsageMin + 1);
                                        povCount += usageCount;
                                        Debug.WriteLine($"DevicesRawInput: Found {usageCount} POV(s) in range 0x{usage:X2}-0x{usageMax:X2} (ReportCount: {valueCap.ReportCount})");
                                    }
                                    else
                                    {
                                        povCount++;
                                        Debug.WriteLine($"DevicesRawInput: Found 1 POV at usage 0x{usage:X2} (ReportCount: {valueCap.ReportCount})");
                                    }
                                }
                            }
                            // Simulation Controls Page (0x02) - Racing wheels, flight sim controls
                            else if (valueCap.UsagePage == HID_USAGE_PAGE_SIMULATION)
                            {
                                switch (usage)
                                {
                                    case 0xBA: // Throttle
                                        if (valueCap.IsRange)
                                        {
                                            int count = (valueCap.Range.UsageMax - valueCap.Range.UsageMin + 1);
                                            throttleCount += count;
                                            Debug.WriteLine($"DevicesRawInput: Found {count} throttles in range 0x{usage:X2}-0x{usageMax:X2}");
                                        }
                                        else
                                        {
                                            throttleCount++;
                                            Debug.WriteLine($"DevicesRawInput: Found 1 throttle at usage 0x{usage:X2}");
                                        }
                                        break;
                                    case 0xBB: // Accelerator
                                        if (valueCap.IsRange)
                                        {
                                            int count = (valueCap.Range.UsageMax - valueCap.Range.UsageMin + 1);
                                            acceleratorCount += count;
                                            Debug.WriteLine($"DevicesRawInput: Found {count} accelerators in range 0x{usage:X2}-0x{usageMax:X2}");
                                        }
                                        else
                                        {
                                            acceleratorCount++;
                                            Debug.WriteLine($"DevicesRawInput: Found 1 accelerator at usage 0x{usage:X2}");
                                        }
                                        break;
                                    case 0xBC: // Brake
                                        if (valueCap.IsRange)
                                        {
                                            int count = (valueCap.Range.UsageMax - valueCap.Range.UsageMin + 1);
                                            brakeCount += count;
                                            Debug.WriteLine($"DevicesRawInput: Found {count} brakes in range 0x{usage:X2}-0x{usageMax:X2}");
                                        }
                                        else
                                        {
                                            brakeCount++;
                                            Debug.WriteLine($"DevicesRawInput: Found 1 brake at usage 0x{usage:X2}");
                                        }
                                        break;
                                    case 0xBD: // Clutch
                                        if (valueCap.IsRange)
                                        {
                                            int count = (valueCap.Range.UsageMax - valueCap.Range.UsageMin + 1);
                                            clutchCount += count;
                                            Debug.WriteLine($"DevicesRawInput: Found {count} clutches in range 0x{usage:X2}-0x{usageMax:X2}");
                                        }
                                        else
                                        {
                                            clutchCount++;
                                            Debug.WriteLine($"DevicesRawInput: Found 1 clutch at usage 0x{usage:X2}");
                                        }
                                        break;
                                    case 0xB0: // Steering
                                        if (valueCap.IsRange)
                                        {
                                            int count = (valueCap.Range.UsageMax - valueCap.Range.UsageMin + 1);
                                            steeringCount += count;
                                            Debug.WriteLine($"DevicesRawInput: Found {count} steering controls in range 0x{usage:X2}-0x{usageMax:X2}");
                                        }
                                        else
                                        {
                                            steeringCount++;
                                            Debug.WriteLine($"DevicesRawInput: Found 1 steering control at usage 0x{usage:X2}");
                                        }
                                        break;
                                }
                            }
                        }
                    }
                }

                // Check for force feedback support (output reports)
                if (caps.NumberOutputButtonCaps > 0 || caps.NumberOutputValueCaps > 0 ||
                    caps.NumberFeatureButtonCaps > 0 || caps.NumberFeatureValueCaps > 0)
                {
                    // Device has output or feature reports, which may indicate force feedback
                    // Check for Physical Interface Device (PID) usage page (0x0F)
                    if (caps.NumberOutputValueCaps > 0)
                    {
                        var outputValueCaps = new HIDP_VALUE_CAPS[caps.NumberOutputValueCaps];
                        ushort outputValueCapsLength = caps.NumberOutputValueCaps;
                        status = HidP_GetValueCaps(HIDP_REPORT_TYPE.HidP_Output, outputValueCaps, ref outputValueCapsLength, preparsedData);
                        
                        if (status == HIDP_STATUS_SUCCESS)
                        {
                            foreach (var valueCap in outputValueCaps)
                            {
                                // PID usage page (0x0F) indicates force feedback
                                if (valueCap.UsagePage == 0x0F)
                                {
                                    hasForceFeedback = true;
                                    break;
                                }
                            }
                        }
                    }
                }

                // If we found sliders but no axes/POVs, and we detected a Pointer→Joystick collection,
                // the device likely has axes and POVs nested inside the Pointer that Windows HID API can't enumerate.
                // Make an intelligent estimate based on device type and what we found.
                if ((axeCount == 0 || povCount == 0) && sliderCount > 0)
                {
                    // Check if we saw a Pointer→Joystick collection in the value caps
                    bool hasPointerJoystickCollection = false;
                    if (caps.NumberInputValueCaps > 0)
                    {
                        var valueCaps = new HIDP_VALUE_CAPS[caps.NumberInputValueCaps];
                        ushort valueCapsLength = caps.NumberInputValueCaps;
                        status = HidP_GetValueCaps(HIDP_REPORT_TYPE.HidP_Input, valueCaps, ref valueCapsLength, preparsedData);
                        
                        if (status == HIDP_STATUS_SUCCESS)
                        {
                            foreach (var valueCap in valueCaps)
                            {
                                ushort usage = valueCap.IsRange ? valueCap.Range.UsageMin : valueCap.NotRange.Usage;
                                ushort linkUsage = valueCap.LinkUsage;
                                ushort linkUsagePage = valueCap.LinkUsagePage;
                                
                                // Check for Pointer (0x01) linking to Joystick (0x04)
                                if (valueCap.UsagePage == HID_USAGE_PAGE_GENERIC && usage == 0x01 &&
                                    linkUsagePage == HID_USAGE_PAGE_GENERIC && linkUsage == HID_USAGE_GENERIC_JOYSTICK)
                                {
                                    hasPointerJoystickCollection = true;
                                    break;
                                }
                            }
                        }
                    }
                    
                    if (hasPointerJoystickCollection)
                    {
                        // Flight sticks typically have 3-4 axes (X, Y, Z/twist, and sometimes RZ)
                        // Since we found a slider (likely the throttle), estimate 3 axes for the stick itself
                        if (axeCount == 0)
                        {
                            axeCount = 3;
                            Debug.WriteLine($"DevicesRawInput: Estimated {axeCount} axes for joystick with Pointer→Joystick collection and {sliderCount} slider(s)");
                        }
                        
                        // Flight sticks typically have 1 POV hat switch on top
                        if (povCount == 0)
                        {
                            povCount = 1;
                            Debug.WriteLine($"DevicesRawInput: Estimated {povCount} POV for joystick with Pointer→Joystick collection");
                        }
                    }
                }
                
                // If we got very few axes from HID parsing (less than expected for a gamepad),
                // try to analyze the actual input report structure to get accurate counts
                if (axeCount < 4 && caps.InputReportByteLength > 0)
                {
                    Debug.WriteLine($"DevicesRawInput: Low axis count ({axeCount}), attempting to analyze input report structure...");
                    
                    // Try to get more accurate counts by analyzing what values we can actually read
                    int reportAxes = 0, reportButtons = 0, reportPovs = 0;
                    if (AnalyzeInputReportStructure(preparsedData, caps, out reportAxes, out reportButtons, out reportPovs))
                    {
                        Debug.WriteLine($"DevicesRawInput: Input report analysis found - Axes: {reportAxes}, Buttons: {reportButtons}, POVs: {reportPovs}");
                        
                        // Use the higher count (report analysis is usually more accurate for complex devices)
                        if (reportAxes > axeCount)
                        {
                            Debug.WriteLine($"DevicesRawInput: Using report-analyzed axis count ({reportAxes}) instead of HID-parsed count ({axeCount})");
                            axeCount = reportAxes;
                        }
                        if (reportButtons > buttonCount)
                        {
                            Debug.WriteLine($"DevicesRawInput: Using report-analyzed button count ({reportButtons}) instead of HID-parsed count ({buttonCount})");
                            buttonCount = reportButtons;
                        }
                        if (reportPovs > povCount)
                        {
                            Debug.WriteLine($"DevicesRawInput: Using report-analyzed POV count ({reportPovs}) instead of HID-parsed count ({povCount})");
                            povCount = reportPovs;
                        }
                    }
                }
                
                // Set button data offset based on Report ID usage
                buttonDataOffset = usesReportIds ? 1 : 0;
                
                Debug.WriteLine($"DevicesRawInput: Parsed HID capabilities for device 0x{hDevice.ToInt64():X8} - " +
                    $"Axes: {axeCount}, Sliders: {sliderCount}, Buttons: {buttonCount}, POVs: {povCount}, " +
                    $"Throttles: {throttleCount}, Brakes: {brakeCount}, Steering: {steeringCount}, " +
                    $"Accelerators: {acceleratorCount}, Clutches: {clutchCount}, ForceFeedback: {hasForceFeedback}, " +
                    $"UsesReportIds: {usesReportIds}, ButtonDataOffset: {buttonDataOffset}");
                
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DevicesRawInput: Error parsing HID capabilities for device 0x{hDevice.ToInt64():X8}: {ex.Message}");
                return false;
            }
            finally
            {
                if (preparsedData != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(preparsedData);
                }
            }
        }

        /// <summary>
        /// Analyzes the input report structure to determine actual axis, button, and POV counts.
        /// This method attempts to read all possible axis and button values from the HID report
        /// to get accurate counts, especially for devices with complex reporting structures.
        /// </summary>
        /// <param name="preparsedData">HID preparsed data</param>
        /// <param name="caps">HID capabilities</param>
        /// <param name="axeCount">Output: number of axes found</param>
        /// <param name="buttonCount">Output: number of buttons found</param>
        /// <param name="povCount">Output: number of POVs found</param>
        /// <returns>True if analysis was successful</returns>
        private bool AnalyzeInputReportStructure(IntPtr preparsedData, HIDP_CAPS caps,
            out int axeCount, out int buttonCount, out int povCount)
        {
            axeCount = 0;
            buttonCount = 0;
            povCount = 0;
            
            try
            {
                // Try to read all possible axis usages (X, Y, Z, Rx, Ry, Rz, Slider, Dial, Wheel, POV)
                ushort[] axisUsages = { 0x30, 0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37, 0x38 };
                ushort povUsage = 0x39;
                
                // Create a dummy input report buffer
                byte[] reportBuffer = new byte[caps.InputReportByteLength];
                
                // Try to get value for each possible axis usage
                foreach (var usage in axisUsages)
                {
                    IntPtr reportPtr = Marshal.AllocHGlobal(reportBuffer.Length);
                    try
                    {
                        Marshal.Copy(reportBuffer, 0, reportPtr, reportBuffer.Length);
                        
                        // Try to get the usage value - if it succeeds, the axis exists
                        int value;
                        int status = HidP_GetUsageValue(
                            HIDP_REPORT_TYPE.HidP_Input,
                            HID_USAGE_PAGE_GENERIC,
                            0, // LinkCollection
                            usage,
                            out value,
                            preparsedData,
                            reportPtr,
                            (uint)reportBuffer.Length);
                        
                        if (status == HIDP_STATUS_SUCCESS)
                        {
                            axeCount++;
                            Debug.WriteLine($"DevicesRawInput: Found axis at usage 0x{usage:X2} via report analysis");
                        }
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(reportPtr);
                    }
                }
                
                // Check for POV
                IntPtr povReportPtr = Marshal.AllocHGlobal(reportBuffer.Length);
                try
                {
                    Marshal.Copy(reportBuffer, 0, povReportPtr, reportBuffer.Length);
                    
                    int povValue;
                    int status = HidP_GetUsageValue(
                        HIDP_REPORT_TYPE.HidP_Input,
                        HID_USAGE_PAGE_GENERIC,
                        0,
                        povUsage,
                        out povValue,
                        preparsedData,
                        povReportPtr,
                        (uint)reportBuffer.Length);
                    
                    if (status == HIDP_STATUS_SUCCESS)
                    {
                        povCount = 1;
                        Debug.WriteLine($"DevicesRawInput: Found POV at usage 0x{povUsage:X2} via report analysis");
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(povReportPtr);
                }
                
                // For buttons, use the button caps we already have
                // The button count from HID parsing is usually accurate
                buttonCount = 0; // Will be filled by caller if needed
                
                return axeCount > 0 || povCount > 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DevicesRawInput: Error analyzing input report structure: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Populates device information from RawInput device info structure.
        /// </summary>
        /// <param name="deviceInfo">Device info object to populate</param>
        /// <param name="ridDeviceInfo">RawInput device info structure</param>
        private void PopulateDeviceInfo(RawInputDeviceInfo deviceInfo, RID_DEVICE_INFO ridDeviceInfo)
        {
            switch (ridDeviceInfo.dwType)
            {
                case RIM_TYPEMOUSE:
                    var mouse = ridDeviceInfo.union.mouse;
                    deviceInfo.ButtonCount = (int)mouse.dwNumberOfButtons;
                    deviceInfo.ProductName = "Mouse";
                    deviceInfo.AxeCount = mouse.fHasHorizontalWheel ? 4 : 3; // X, Y, Wheel, (Horizontal Wheel)
                    break;

                case RIM_TYPEKEYBOARD:
                    var keyboard = ridDeviceInfo.union.keyboard;
                    deviceInfo.KeyCount = (int)keyboard.dwNumberOfKeysTotal;
                    deviceInfo.ButtonCount = 0; // Keyboards have keys, not buttons
                    deviceInfo.ProductName = "Keyboard";
                    deviceInfo.DeviceSubtype = (int)keyboard.dwSubType;
                    break;

                case RIM_TYPEHID:
                    var hid = ridDeviceInfo.union.hid;
                    deviceInfo.VendorId = (int)hid.dwVendorId;
                    deviceInfo.ProductId = (int)hid.dwProductId;
                    deviceInfo.Usage = hid.usUsage;
                    deviceInfo.UsagePage = hid.usUsagePage;
                    deviceInfo.HardwareRevision = (int)hid.dwVersionNumber;
                    
                    // Set product name based on device type
                    if (IsGamepadDevice(hid.usUsage, hid.usUsagePage))
                    {
                        deviceInfo.ProductName = "HID Gamepad";
                    }
                    else
                    {
                        deviceInfo.ProductName = "HID Device";
                    }
                    
                    // Initialize to 0 (will be populated if HID parsing succeeds)
                    deviceInfo.AxeCount = 0;
                    deviceInfo.SliderCount = 0;
                    deviceInfo.ButtonCount = 0;
                    deviceInfo.KeyCount = 0; // HID gamepads have no keys
                    deviceInfo.PovCount = 0;
                    deviceInfo.HasForceFeedback = false;
                    break;
            }
        }

        /// <summary>
        /// Determines if a device is a gamepad based on usage and usage page.
        /// </summary>
        /// <param name="usage">HID usage</param>
        /// <param name="usagePage">HID usage page</param>
        /// <returns>True if device is a gamepad</returns>
        private bool IsGamepadDevice(int usage, int usagePage)
        {
            return (usagePage == HID_USAGE_PAGE_GENERIC && 
                   (usage == HID_USAGE_GENERIC_JOYSTICK || 
                    usage == HID_USAGE_GENERIC_GAMEPAD ||
                    usage == HID_USAGE_GENERIC_MULTI_AXIS_CONTROLLER)) ||
                   (usagePage == HID_USAGE_PAGE_GAME);
        }

        /// <summary>
        /// Gets a human-readable device type name.
        /// </summary>
        /// <param name="deviceType">RawInput device type</param>
        /// <param name="usage">HID usage</param>
        /// <param name="usagePage">HID usage page</param>
        /// <returns>Human-readable device type name</returns>
        private string GetDeviceTypeName(RawInputDeviceType deviceType, int usage, int usagePage)
        {
            switch (deviceType)
            {
                case RawInputDeviceType.Mouse:
                    return "RawInput Mouse";
                case RawInputDeviceType.Keyboard:
                    return "RawInput Keyboard";
                case RawInputDeviceType.HID:
                    if (IsGamepadDevice(usage, usagePage))
                    {
                        if (usage == HID_USAGE_GENERIC_JOYSTICK)
                            return "RawInput Joystick";
                        else if (usage == HID_USAGE_GENERIC_GAMEPAD)
                            return "RawInput Gamepad";
                        else
                            return "RawInput Game Controller";
                    }
                    return $"RawInput HID Device (Usage: 0x{usage:X4}, Page: 0x{usagePage:X4})";
                default:
                    return $"RawInput Unknown ({(int)deviceType})";
            }
        }

        /// <summary>
        /// Extracts VID and PID from device interface path.
        /// Supports multiple VID/PID formats including standard (VID_XXXX&PID_XXXX) and alternate (VID&XXXXXXXX_PID&XXXX) formats.
        /// Falls back to VEN_/DEV_ values or composite device IDs (like INT33D2) when VID/PID are not available.
        /// </summary>
        /// <param name="interfacePath">Device interface path</param>
        /// <returns>Tuple containing VID and PID values</returns>
        private (int vid, int pid) ExtractVidPidFromPath(string interfacePath)
        {
        	if (string.IsNullOrEmpty(interfacePath))
        		return (0, 0);
      
        	try
        	{
        		Debug.WriteLine($"DevicesRawInput: ExtractVidPidFromPath called with: {interfacePath}");
        		
        		// Common patterns in interface paths:
        		// Standard format: \\?\hid#vid_045e&pid_028e#...
        		// Alternate format: {GUID}_VID&00020111_PID&1431&Col02
        		// Fallback format with VEN/DEV: \\?\HID#VEN_INT&DEV_33D2&Col01#...
        		// Composite format: \\?\HID#INT33D2&Col01#... (vendor+device without prefix)
        		var upperPath = interfacePath.ToUpperInvariant();
      
        		// Try standard format first: VID_XXXX
        		int vidIndex = upperPath.IndexOf("VID_", StringComparison.Ordinal);
        		int pidIndex = upperPath.IndexOf("PID_", StringComparison.Ordinal);
      
        		if (vidIndex >= 0 && pidIndex >= 0)
        		{
        			int vidStart = vidIndex + 4;
        			int pidStart = pidIndex + 4;
      
        			// Extract 4-character hex values
        			if (vidStart + 4 <= upperPath.Length && pidStart + 4 <= upperPath.Length)
        			{
        				var vidStr = upperPath.Substring(vidStart, 4);
        				var pidStr = upperPath.Substring(pidStart, 4);
      
        				if (int.TryParse(vidStr, System.Globalization.NumberStyles.HexNumber, null, out int vid) &&
        					int.TryParse(pidStr, System.Globalization.NumberStyles.HexNumber, null, out int pid))
        				{
        					return (vid, pid);
        				}
        			}
        		}

                // Try alternate format: VID&XXXXXXXX_PID&XXXX
                vidIndex = upperPath.IndexOf("VID&", StringComparison.Ordinal);
                pidIndex = upperPath.IndexOf("_PID&", StringComparison.Ordinal);
                
                if (vidIndex >= 0 && pidIndex >= 0)
                {
                	int vidStart = vidIndex + 4;
                	int pidStart = pidIndex + 5; // Skip "_PID&"
                	
                	// Find the end of VID value (up to underscore or ampersand)
                	int vidEnd = vidStart;
                	while (vidEnd < upperPath.Length && char.IsLetterOrDigit(upperPath[vidEnd]))
                		vidEnd++;
                	
                	// Find the end of PID value (up to ampersand, space, or other delimiter)
                	int pidEnd = pidStart;
                	while (pidEnd < upperPath.Length && char.IsLetterOrDigit(upperPath[pidEnd]))
                		pidEnd++;
                	
                	if (vidEnd > vidStart && pidEnd > pidStart)
                	{
                		var vidStr = upperPath.Substring(vidStart, vidEnd - vidStart);
                		var pidStr = upperPath.Substring(pidStart, pidEnd - pidStart);
                        
                        // Handle variable-length hex strings (take last 4 characters for standard VID/PID)
                        if (vidStr.Length > 4)
                            vidStr = vidStr.Substring(vidStr.Length - 4);
                        if (pidStr.Length > 4)
                            pidStr = pidStr.Substring(pidStr.Length - 4);

                        if (int.TryParse(vidStr, System.Globalization.NumberStyles.HexNumber, null, out int vid) &&
                            int.TryParse(pidStr, System.Globalization.NumberStyles.HexNumber, null, out int pid))
                        {
                            return (vid, pid);
                        }
                    }
                }
                
                // Fallback: Try VEN_ and DEV_ formats (for devices like HID\VEN_INT&DEV_33D2)
                int venId = ExtractHexValue(upperPath, "VEN_", 4) ?? 0;
                int devId = ExtractHexValue(upperPath, "DEV_", 4) ?? 0;
                
                if (venId != 0 || devId != 0)
                {
                    return (venId, devId);
                }
                
                // Final fallback: Try to extract composite device ID from various path formats
                // Supported patterns:
                // - HID: \\?\HID#INT33D2&Col01#... -> INT (vendor) + 33D2 (device)
                // - ACPI: \\?\ACPI#DLLK0A05#... -> DLLK (vendor) + 0A05 (device)
                
                // Try to find device ID after HID# or ACPI#
                string deviceId = null;
                int deviceIdStart = -1;
                
                // Check for HID devices: look for "HID#" pattern
                int hidIndex = upperPath.IndexOf("HID#", StringComparison.Ordinal);
                if (hidIndex >= 0)
                {
                	deviceIdStart = hidIndex + 4; // Skip "HID#"
                }
                
                // Check for ACPI devices if HID not found: look for "ACPI#" pattern
                if (deviceIdStart < 0)
                {
                	int acpiIndex = upperPath.IndexOf("ACPI#", StringComparison.Ordinal);
                	if (acpiIndex >= 0)
                	{
                		deviceIdStart = acpiIndex + 5; // Skip "ACPI#"
                	}
                }
                
                if (deviceIdStart >= 0)
                {
                    // Find the end of device ID - stop at first '&' or '#' (whichever comes first)
                    // For ACPI paths like "ACPI#DLL0A05#4&...", we want just "DLL0A05" (stop at '#')
                    // For HID paths like "HID#INT33D2&Col01#...", we want just "INT33D2" (stop at '&')
                    var ampersandPos = upperPath.IndexOf('&', deviceIdStart);
                    var hashPos = upperPath.IndexOf('#', deviceIdStart);
                    
                    // Take whichever delimiter comes first (or the one that exists if only one is found)
                    int deviceIdEnd = -1;
                    if (ampersandPos >= 0 && hashPos >= 0)
                        deviceIdEnd = Math.Min(ampersandPos, hashPos);
                    else if (ampersandPos >= 0)
                        deviceIdEnd = ampersandPos;
                    else if (hashPos >= 0)
                        deviceIdEnd = hashPos;
                    
                    if (deviceIdEnd > deviceIdStart)
                    {
                        deviceId = upperPath.Substring(deviceIdStart, deviceIdEnd - deviceIdStart);
                        Debug.WriteLine($"DevicesRawInput: Found device ID: {deviceId}");
                        
                        // Try to split into vendor (letters) and device (alphanumeric)
                        // Examples:
                        // - INT33D2 -> INT (vendor) + 33D2 (device)
                        // - DLLK0A05 -> Try DLLK (vendor) + 0A05 (device) first, then DLL (vendor) + K0A05 (device)
                        
                        // Strategy: Find the longest letter sequence at the start, then check if remainder is valid hex
                        int splitPos = 0;
                        while (splitPos < deviceId.Length && char.IsLetter(deviceId[splitPos]))
                            splitPos++;
                        
                        Debug.WriteLine($"DevicesRawInput: Initial split position: {splitPos} (letters: {deviceId.Substring(0, splitPos)})");
                        
                        // Try different split positions if the first one doesn't work
                        for (int tryPos = splitPos; tryPos > 0; tryPos--)
                        {
                            var vendorPart = deviceId.Substring(0, tryPos);
                            var devicePart = deviceId.Substring(tryPos);
                            
                            Debug.WriteLine($"DevicesRawInput: Trying split - Vendor: '{vendorPart}', Device: '{devicePart}'");
                            
                            // Device part must be valid hex and at least 2 characters
                            bool isValidHex = devicePart.Length >= 2 &&
                                devicePart.All(c => (c >= '0' && c <= '9') || (c >= 'A' && c <= 'F'));
                            
                            Debug.WriteLine($"DevicesRawInput: Device part '{devicePart}' valid hex: {isValidHex}");
                            
                            if (isValidHex)
                            {
                                // Convert vendor part using ASCII encoding, always pad to 4 bytes
                                int vendorCode = 0;
                                for (int i = 0; i < vendorPart.Length && i < 4; i++)
                                {
                                    vendorCode = (vendorCode << 8) | (byte)vendorPart[i];
                                }
                                // Pad with zeros if vendor is less than 4 characters
                                // vendorCode = vendorCode << (8 * (4 - Math.Min(vendorPart.Length, 4)));
                                
                                // Parse device part as hex
                                if (int.TryParse(devicePart, System.Globalization.NumberStyles.HexNumber, null, out int deviceCode))
                                {
                                    Debug.WriteLine($"DevicesRawInput: Extracted composite device ID: Vendor={vendorPart} (0x{vendorCode:X8}), Device={devicePart} (0x{deviceCode:X})");
                                    return (vendorCode, deviceCode);
                                }
                                else
                                {
                                    Debug.WriteLine($"DevicesRawInput: Failed to parse device part '{devicePart}' as hex");
                                }
                            }
                        }
                        
                        Debug.WriteLine($"DevicesRawInput: No valid split found for device ID: {deviceId}");
                    }
                    else
                    {
                        Debug.WriteLine($"DevicesRawInput: Could not find device ID end delimiter");
                    }
                }
                else
                {
                    Debug.WriteLine($"DevicesRawInput: Could not find HID# or ACPI# in path");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DevicesRawInput: Error extracting VID/PID from path '{interfacePath}': {ex.Message}");
                Debug.WriteLine($"DevicesRawInput: Stack trace: {ex.StackTrace}");
            }

            Debug.WriteLine($"DevicesRawInput: ExtractVidPidFromPath returning (0, 0) for: {interfacePath}");
            return (0, 0);
        }
        
        /// <summary>
        /// Extracts a hexadecimal value following a specific pattern in a string.
        /// Handles both numeric hex values (e.g., "046A") and alphanumeric vendor codes (e.g., "INT").
        /// Uses the same conversion logic as DevicesPnPInput for consistency.
        /// </summary>
        /// <param name="text">Text to search in</param>
        /// <param name="pattern">Pattern to search for (e.g., "VID_", "VEN_")</param>
        /// <param name="length">Expected length of hex value</param>
        /// <returns>Parsed integer value or null if not found</returns>
        private static int? ExtractHexValue(string text, string pattern, int length)
        {
            var index = text.IndexOf(pattern, StringComparison.Ordinal);
            if (index < 0)
                return null;

            var start = index + pattern.Length;
            if (start >= text.Length)
                return null;

            // Extract characters that could be hex digits or alphanumeric vendor codes
            var end = start;
            var maxEnd = Math.Min(start + length, text.Length);
            
            while (end < maxEnd)
            {
                var ch = text[end];
                // Accept hex digits (0-9, A-F) and letters (for vendor codes like "INT")
                if ((ch >= '0' && ch <= '9') || (ch >= 'A' && ch <= 'Z'))
                    end++;
                else
                    break;
            }

            if (end <= start)
                return null;

            var hexStr = text.Substring(start, end - start);
            
            // Try to parse as hexadecimal number
            if (int.TryParse(hexStr, System.Globalization.NumberStyles.HexNumber, null, out int value))
                return value;
            
            // If parsing fails but we have a valid string (like "INT"),
            // treat each character as a hex digit to create a unique identifier
            // This ensures vendor codes like "INT" get converted to a numeric value
            if (hexStr.Length > 0 && hexStr.All(c => (c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z')))
            {
                // For vendor codes, use ASCII-based conversion to create unique numeric ID
                // This ensures "INT" becomes a valid numeric identifier
                int vendorCode = 0;
                for (int i = 0; i < Math.Min(hexStr.Length, 4); i++)
                {
                    vendorCode = (vendorCode << 8) | (byte)hexStr[i];
                }
                return vendorCode;
            }
            
            return null;
        }

        /// <summary>
        /// Extracts device ID from interface path.
        /// </summary>
        /// <param name="interfacePath">Device interface path</param>
        /// <returns>Device ID string</returns>
        private string ExtractDeviceIdFromPath(string interfacePath)
        {
            if (string.IsNullOrEmpty(interfacePath))
                return "";

            // Extract the device ID portion from paths like:
            // \\?\hid#vid_045e&pid_028e&mi_00#7&1234abcd&0&0000#{...}
            var parts = interfacePath.Split('#');
            return parts.Length >= 2 ? parts[1] : interfacePath;
        }

        /// <summary>
        /// Extracts a friendly device name from the interface path.
        /// </summary>
        /// <param name="interfacePath">Device interface path</param>
        /// <returns>Friendly device name</returns>
        private string ExtractDeviceNameFromPath(string interfacePath)
        {
            if (string.IsNullOrEmpty(interfacePath))
                return "Unknown Device";

            // Try to extract meaningful name from path
            var parts = interfacePath.Split('#');
            if (parts.Length >= 2)
            {
                var devicePart = parts[1];
                // Remove VID/PID and return a cleaner name
                return devicePart.Replace("vid_", "VID_").Replace("pid_", "PID_").Replace("&", " ");
            }

            return "RawInput Device";
        }

        /// <summary>
        /// Generates a unique instance GUID for a device.
        /// </summary>
        /// <param name="hDevice">Device handle</param>
        /// <param name="deviceName">Device name</param>
        /// <returns>Unique instance GUID</returns>
        private Guid GenerateInstanceGuid(IntPtr hDevice, string deviceName)
        {
            // Create a deterministic GUID based on device handle and name
            var input = $"RawInput_{hDevice.ToInt64():X16}_{deviceName}";
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
                return new Guid(hash);
            }
        }

        /// <summary>
        /// Generates a product GUID based on VID and PID.
        /// </summary>
        /// <param name="vendorId">Vendor ID</param>
        /// <param name="productId">Product ID</param>
        /// <returns>Product GUID</returns>
        private Guid GenerateProductGuid(int vendorId, int productId)
        {
            // Create a deterministic GUID based on VID/PID
            var guidBytes = new byte[16];
            var vidBytes = BitConverter.GetBytes(vendorId);
            var pidBytes = BitConverter.GetBytes(productId);
            
            // Fill GUID with VID/PID pattern
            Array.Copy(vidBytes, 0, guidBytes, 0, 4);
            Array.Copy(pidBytes, 0, guidBytes, 4, 4);
            
            // Add RawInput identifier
            guidBytes[8] = 0x52; // 'R'
            guidBytes[9] = 0x41; // 'A'
            guidBytes[10] = 0x57; // 'W'
            guidBytes[11] = 0x49; // 'I'
            
            return new Guid(guidBytes);
        }
        
        /// <summary>
        /// Generates CommonIdentifier for the device by extracting VID, PID, MI, and COL values.
        /// </summary>
        /// <param name="deviceInfo">Device information to process</param>
        private void GenerateCommonIdentifier(RawInputDeviceInfo deviceInfo)
        {
            var vid = deviceInfo.VendorId > 0 ? $"{deviceInfo.VendorId:X4}" : "0000";
            var pid = deviceInfo.ProductId > 0 ? $"{deviceInfo.ProductId:X4}" : "0000";
            
            var commonId = $"VID_{vid}&PID_{pid}";
            
            // Try to extract MI and COL from InterfacePath if available
            if (!string.IsNullOrEmpty(deviceInfo.InterfacePath))
            {
                var upperPath = deviceInfo.InterfacePath.ToUpperInvariant();
                
                // Extract MI
                int miIndex = upperPath.IndexOf("&MI_", StringComparison.Ordinal);
                if (miIndex < 0) miIndex = upperPath.IndexOf("\\MI_", StringComparison.Ordinal);
                if (miIndex >= 0 && miIndex + 6 <= upperPath.Length)
                {
                	var mi = upperPath.Substring(miIndex + 4, 2);
                	if (mi != "00") commonId += $"&MI_{mi}";
                }
                
                // Extract COL
                int colIndex = upperPath.IndexOf("&COL", StringComparison.Ordinal);
                if (colIndex < 0) colIndex = upperPath.IndexOf("\\COL", StringComparison.Ordinal);
                if (colIndex >= 0)
                {
                	int colStart = colIndex + 4;
                	int colEnd = colStart;
                	while (colEnd < upperPath.Length && char.IsLetterOrDigit(upperPath[colEnd]))
                		colEnd++;
                	if (colEnd > colStart)
                	{
                		var col = upperPath.Substring(colStart, colEnd - colStart);
                		commonId += $"&COL_{col}";
                	}
                }
            }
            
            deviceInfo.CommonIdentifier = commonId;
        }

        /// <summary>
        /// Checks if device is a known input device based on HID usage page and usage.
        /// </summary>
        /// <param name="usagePage">HID usage page</param>
        /// <param name="usage">HID usage</param>
        /// <returns>True if device is a known input device type</returns>
        private bool IsKnownInputDeviceByUsage(int usagePage, int usage)
        {
            // Generic Desktop Controls (0x01)
            if (usagePage == HID_USAGE_PAGE_GENERIC)
            {
                switch (usage)
                {
                    case HID_USAGE_GENERIC_JOYSTICK:        // 0x04 - Joystick
                    case HID_USAGE_GENERIC_GAMEPAD:         // 0x05 - Gamepad
                    case HID_USAGE_GENERIC_MULTI_AXIS_CONTROLLER: // 0x08 - Multi-axis Controller
                    case 0x02: // Mouse (HID_USAGE_GENERIC_MOUSE)
                    case 0x06: // Keyboard (HID_USAGE_GENERIC_KEYBOARD)
                    case 0x07: // Keypad (HID_USAGE_GENERIC_KEYPAD)
                        return true;
                }
            }

            // Game Controls (0x05)
            if (usagePage == HID_USAGE_PAGE_GAME)
            {
                // Any device in Game Controls usage page is likely an input device
                return true;
            }

            // Consumer Controls (0x0C) - These are typically media keys, volume controls, etc.
            // Most are not gaming input devices, so we need to be more selective
            if (usagePage == 0x0C)
            {
                // Only include specific consumer control usages that are actual gaming inputs
                // Most consumer controls (like volume, media keys) should be excluded
                switch (usage)
                {
                    case 0x01: // Consumer Control (generic) - usually not a gaming device
                    case 0x02: // Numeric Key Pad - usually not a gaming device
                    case 0x03: // Programmable Buttons - usually not a gaming device
                    case 0x04: // Microphone - not a gaming input device
                    case 0x05: // Headphone - not a gaming input device
                    case 0x06: // Graphic Equalizer - not a gaming input device
                        return false; // Exclude these consumer controls
                    
                    // We could add specific gaming-related consumer controls here if needed
                    // For now, exclude all consumer controls as they're typically not gaming devices
                    default:
                        return false;
                }
            }

            // Telephony (0x0B) - Phone controls are typically not gaming input devices
            if (usagePage == 0x0B)
            {
                return false; // Exclude telephony devices
            }

            return false;
        }

        /// <summary>
        /// Checks if device name/path contains patterns typical of input devices.
        /// Optimized with comprehensive patterns and efficient checking.
        /// </summary>
        /// <param name="interfacePath">Device interface path</param>
        /// <param name="instanceName">Device instance name</param>
        /// <returns>True if device has input device name patterns</returns>
        private bool HasInputDeviceNamePattern(string interfacePath, string instanceName)
        {
            if (string.IsNullOrEmpty(interfacePath) && string.IsNullOrEmpty(instanceName))
                return false;

            var combinedText = $"{interfacePath} {instanceName}".ToLowerInvariant();

            // Gaming input devices (highest priority)
            if (combinedText.Contains("gamepad") || combinedText.Contains("joystick") ||
                combinedText.Contains("controller") || combinedText.Contains("wheel") ||
                combinedText.Contains("pedal") || combinedText.Contains("throttle") ||
                combinedText.Contains("flight") || combinedText.Contains("racing"))
                return true;

            // Standard input devices
            if (combinedText.Contains("mouse") || combinedText.Contains("keyboard") ||
                combinedText.Contains("trackpad") || combinedText.Contains("touchpad") ||
                combinedText.Contains("trackball") || combinedText.Contains("pointing"))
                return true;

            // Pen/touch input devices
            if (combinedText.Contains("tablet") || combinedText.Contains("stylus") ||
                combinedText.Contains("pen") || combinedText.Contains("digitizer") ||
                combinedText.Contains("touch"))
                return true;

            // Remote/media controls
            if (combinedText.Contains("remote") || combinedText.Contains("media") ||
                combinedText.Contains("volume") || combinedText.Contains("button") ||
                combinedText.Contains("switch"))
                return true;

            return false;
        }


        /// <summary>
        /// Processes a device that has been identified as an input device.
        /// Extracts full device information and adds it to the device list.
        /// </summary>
        /// <param name="rawDevice">Raw device information</param>
        /// <param name="deviceListIndex">Current device index (will be incremented)</param>
        /// <param name="deviceList">List to add the device to</param>
        /// <param name="deviceListDebugLines">Debug lines list</param>
        private void ProcessInputDevice(RAWINPUTDEVICELIST rawDevice, ref int deviceListIndex,
            List<RawInputDeviceInfo> deviceList, List<string> deviceListDebugLines)
        {
            try
            {
                // Create RawInputDeviceInfo object with basic information
                var deviceInfo = new RawInputDeviceInfo
                {
                    DeviceHandle = rawDevice.hDevice,
                    RawInputDeviceType = (RawInputDeviceType)rawDevice.dwType,
                    DeviceType = (int)rawDevice.dwType,
                    RawInputFlags = rawDevice.dwType,
                    IsOnline = true,
                    InputType = "RawInput"
                };

                // Get device name (interface path)
                string deviceName = GetDeviceName(rawDevice.hDevice);
                deviceInfo.InterfacePath = deviceName;
                
                // Use only what RawInput provides - extracted name from interface path
                deviceInfo.InstanceName = ExtractDeviceNameFromPath(deviceName);

                // Get device information
                var deviceInfoStruct = GetDeviceInfo(rawDevice.hDevice);
                if (deviceInfoStruct.HasValue)
                {
                    PopulateDeviceInfo(deviceInfo, deviceInfoStruct.Value);
                    
                    // For HID devices, try to parse actual capabilities from HID Report Descriptor
                    if (deviceInfo.RawInputDeviceType == RawInputDeviceType.HID)
                    {
                        int axes, sliders, buttons, povs, throttles, brakes, steering, accelerators, clutches;
                        bool forceFeedback, usesReportIds;
                        int buttonDataOffset;
                        if (GetHidCapabilities(rawDevice.hDevice, out axes, out sliders, out buttons, out povs,
                            out throttles, out brakes, out steering, out accelerators, out clutches, out forceFeedback,
                            out usesReportIds, out buttonDataOffset))
                        {
                            deviceInfo.AxeCount = axes;
                            deviceInfo.SliderCount = sliders;
                            deviceInfo.ButtonCount = buttons;
                            deviceInfo.PovCount = povs;
                            deviceInfo.ThrottleCount = throttles;
                            deviceInfo.BrakeCount = brakes;
                            deviceInfo.SteeringCount = steering;
                            deviceInfo.AcceleratorCount = accelerators;
                            deviceInfo.ClutchCount = clutches;
                            deviceInfo.HasForceFeedback = forceFeedback;
                            deviceInfo.UsesReportIds = usesReportIds;
                            deviceInfo.ButtonDataOffset = buttonDataOffset;
                        }
                    }
                }

                // Extract VID/PID from InterfacePath for all device types
                // For HID devices, this may override values from RID_DEVICE_INFO if they were 0
                // For Keyboard/Mouse devices, this is the only way to get VID/PID
                var vidPid = ExtractVidPidFromPath(deviceName);
                if (vidPid.vid != 0 || vidPid.pid != 0)
                {
                    // Only override if we found valid values
                    if (deviceInfo.VendorId == 0) deviceInfo.VendorId = vidPid.vid;
                    if (deviceInfo.ProductId == 0) deviceInfo.ProductId = vidPid.pid;
                }

                // Generate GUIDs
                deviceInfo.InstanceGuid = GenerateInstanceGuid(rawDevice.hDevice, deviceName);
                deviceInfo.ProductGuid = GenerateProductGuid(deviceInfo.VendorId, deviceInfo.ProductId);

                // Set device type name
                deviceInfo.DeviceTypeName = GetDeviceTypeName(deviceInfo.RawInputDeviceType, deviceInfo.Usage, deviceInfo.UsagePage);

                // Extract additional device identification
                deviceInfo.DeviceId = ExtractDeviceIdFromPath(deviceName);
                
                // Generate CommonIdentifier for device grouping
                GenerateCommonIdentifier(deviceInfo);
                
                // HardwareIds and ParentDeviceId are not natively provided by RawInput API
                deviceInfo.HardwareIds = "";
                deviceInfo.ParentDeviceId = "";

                // Set default values for properties not available through RawInput
                deviceInfo.DriverVersion = 1;
                deviceInfo.HardwareRevision = 1;
                deviceInfo.FirmwareRevision = 1;

                deviceListIndex++;

                // Log comprehensive device information for debugging using StringBuilder for efficiency
                var sb = new StringBuilder();
                sb.AppendLine($"\n{deviceListIndex}. DevicesRawInputInfo:");
                sb.Append($"  CommonIdentifier (generated): {deviceInfo.CommonIdentifier}, ");
                sb.Append($"DeviceHandle: 0x{deviceInfo.DeviceHandle.ToInt64():X8}, ");
                sb.Append($"Type: {deviceInfo.RawInputDeviceType}, ");
                sb.Append($"InstanceGuid (generated): {deviceInfo.InstanceGuid}");
                sb.AppendLine();
                sb.Append($"  InstanceName (generated): {deviceInfo.InstanceName}, ");
                sb.Append($"ProductName: {deviceInfo.ProductName}, ");
                sb.Append($"DeviceTypeName (generated): {deviceInfo.DeviceTypeName}");
                sb.AppendLine();
                sb.Append($"  Usage: 0x{deviceInfo.Usage:X4}, ");
                sb.Append($"UsagePage: 0x{deviceInfo.UsagePage:X4}");
                if (!string.IsNullOrEmpty(deviceInfo.InterfacePath))
                    sb.Append($", InterfacePath: {deviceInfo.InterfacePath}");
                deviceListDebugLines.Add(sb.ToString());
            
                sb.Clear();
                sb.Append($"  Identification: VID/PID (generated): {deviceInfo.VidPidString}, ");
                sb.Append($"VendorId: {deviceInfo.VendorId} (0x{deviceInfo.VendorId:X4}), ");
                sb.Append($"ProductId: {deviceInfo.ProductId} (0x{deviceInfo.ProductId:X4})");
                if (!string.IsNullOrEmpty(deviceInfo.DeviceId))
                    sb.Append($", DeviceId (generated): {deviceInfo.DeviceId}");
                deviceListDebugLines.Add(sb.ToString());

                // Add capability information with appropriate context using StringBuilder
                sb.Clear();
                if (deviceInfo.RawInputDeviceType == RawInputDeviceType.HID)
                {
                    // HID devices: Show parsed capabilities from HID Report Descriptor
                    if (deviceInfo.AxeCount > 0 || deviceInfo.SliderCount > 0 || deviceInfo.ButtonCount > 0 || deviceInfo.PovCount > 0)
                    {
                        sb.Append($"  Capabilities (HID Report Descriptor): ");
                        sb.Append($"Axes: {deviceInfo.AxeCount}, Sliders: {deviceInfo.SliderCount}, ");
                        sb.Append($"Buttons: {deviceInfo.ButtonCount}, POVs: {deviceInfo.PovCount}, ");
                        sb.Append($"ForceFeedback: {deviceInfo.HasForceFeedback}");
                        deviceListDebugLines.Add(sb.ToString());
                    }
                    else
                    {
                        deviceListDebugLines.Add($"  Note: Could not parse HID Report Descriptor - capabilities unknown");
                    }
                }
                else
                {
                    // Mouse and Keyboard have actual counts from RawInput API
                    sb.Append($"  Capabilities: ");
                    if (deviceInfo.RawInputDeviceType == RawInputDeviceType.Keyboard)
                    {
                        sb.Append($"Keys: {deviceInfo.KeyCount}, Buttons: {deviceInfo.ButtonCount}");
                    }
                    else // Mouse
                    {
                        sb.Append($"Axes: {deviceInfo.AxeCount}, Buttons: {deviceInfo.ButtonCount}");
                    }
                    deviceListDebugLines.Add(sb.ToString());
                }

                // Add device to the final list (already filtered as input device)
                deviceList.Add(deviceInfo);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DevicesRawInput: Error processing input device 0x{rawDevice.hDevice.ToInt64():X8}: {ex.Message}");
            }
        }

        /// <summary>
        /// Quick check if device name indicates it's a known non-input device.
        /// Used for early filtering to skip processing entirely.
        /// Optimized with comprehensive patterns and efficient checking.
        /// </summary>
        /// <param name="deviceName">Device name/interface path</param>
        /// <returns>True if device is definitely not an input device</returns>
        private bool IsKnownNonInputDeviceByName(string deviceName)
        {
        	if (string.IsNullOrEmpty(deviceName))
        		return false;
      
        	// Use IndexOf with StringComparison.OrdinalIgnoreCase for better performance
        	// than ToLowerInvariant() + Contains()
        	return deviceName.IndexOf("audio", StringComparison.OrdinalIgnoreCase) >= 0 ||
        		   deviceName.IndexOf("sound", StringComparison.OrdinalIgnoreCase) >= 0 ||
        		   deviceName.IndexOf("speaker", StringComparison.OrdinalIgnoreCase) >= 0 ||
        		   deviceName.IndexOf("microphone", StringComparison.OrdinalIgnoreCase) >= 0 ||
        		   deviceName.IndexOf("headphone", StringComparison.OrdinalIgnoreCase) >= 0 ||
        		   deviceName.IndexOf("headset", StringComparison.OrdinalIgnoreCase) >= 0 ||
        		   deviceName.IndexOf("storage", StringComparison.OrdinalIgnoreCase) >= 0 ||
        		   deviceName.IndexOf("disk", StringComparison.OrdinalIgnoreCase) >= 0 ||
        		   deviceName.IndexOf("drive", StringComparison.OrdinalIgnoreCase) >= 0 ||
        		   deviceName.IndexOf("mass", StringComparison.OrdinalIgnoreCase) >= 0 ||
        		   deviceName.IndexOf("flash", StringComparison.OrdinalIgnoreCase) >= 0 ||
        		   deviceName.IndexOf("card reader", StringComparison.OrdinalIgnoreCase) >= 0 ||
        		   deviceName.IndexOf("network", StringComparison.OrdinalIgnoreCase) >= 0 ||
        		   deviceName.IndexOf("ethernet", StringComparison.OrdinalIgnoreCase) >= 0 ||
        		   deviceName.IndexOf("wifi", StringComparison.OrdinalIgnoreCase) >= 0 ||
        		   deviceName.IndexOf("bluetooth\\radio", StringComparison.OrdinalIgnoreCase) >= 0 ||
        		   deviceName.IndexOf("wireless adapter", StringComparison.OrdinalIgnoreCase) >= 0 ||
        		   deviceName.IndexOf("camera", StringComparison.OrdinalIgnoreCase) >= 0 ||
        		   deviceName.IndexOf("webcam", StringComparison.OrdinalIgnoreCase) >= 0 ||
        		   deviceName.IndexOf("video", StringComparison.OrdinalIgnoreCase) >= 0 ||
        		   deviceName.IndexOf("capture", StringComparison.OrdinalIgnoreCase) >= 0 ||
        		   deviceName.IndexOf("imaging", StringComparison.OrdinalIgnoreCase) >= 0 ||
        		   deviceName.IndexOf("printer", StringComparison.OrdinalIgnoreCase) >= 0 ||
        		   deviceName.IndexOf("scanner", StringComparison.OrdinalIgnoreCase) >= 0 ||
        		   deviceName.IndexOf("fax", StringComparison.OrdinalIgnoreCase) >= 0 ||
        		   deviceName.IndexOf("modem", StringComparison.OrdinalIgnoreCase) >= 0 ||
        		   deviceName.IndexOf("serial", StringComparison.OrdinalIgnoreCase) >= 0 ||
        		   deviceName.IndexOf("parallel", StringComparison.OrdinalIgnoreCase) >= 0 ||
        		   deviceName.IndexOf("hub", StringComparison.OrdinalIgnoreCase) >= 0 ||
        		   deviceName.IndexOf("root", StringComparison.OrdinalIgnoreCase) >= 0 ||
        		   deviceName.IndexOf("composite\\interface", StringComparison.OrdinalIgnoreCase) >= 0 ||
        		   deviceName.IndexOf("system", StringComparison.OrdinalIgnoreCase) >= 0 ||
        		   deviceName.IndexOf("acpi", StringComparison.OrdinalIgnoreCase) >= 0 ||
        		   deviceName.IndexOf("pci", StringComparison.OrdinalIgnoreCase) >= 0 ||
        		   deviceName.IndexOf("processor", StringComparison.OrdinalIgnoreCase) >= 0 ||
        		   deviceName.IndexOf("chipset", StringComparison.OrdinalIgnoreCase) >= 0 ||
        		   deviceName.IndexOf("monitor", StringComparison.OrdinalIgnoreCase) >= 0 ||
        		   deviceName.IndexOf("display", StringComparison.OrdinalIgnoreCase) >= 0 ||
        		   deviceName.IndexOf("screen", StringComparison.OrdinalIgnoreCase) >= 0 ||
        		   deviceName.IndexOf("battery", StringComparison.OrdinalIgnoreCase) >= 0 ||
        		   deviceName.IndexOf("power", StringComparison.OrdinalIgnoreCase) >= 0 ||
        		   deviceName.IndexOf("ups", StringComparison.OrdinalIgnoreCase) >= 0 ||
        		   deviceName.IndexOf("sensor", StringComparison.OrdinalIgnoreCase) >= 0 ||
        		   deviceName.IndexOf("accelerometer", StringComparison.OrdinalIgnoreCase) >= 0 ||
        		   deviceName.IndexOf("gyroscope", StringComparison.OrdinalIgnoreCase) >= 0 ||
        		   deviceName.IndexOf("proximity", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Creates a temporary device info object to check capabilities without full processing.
        /// </summary>
        /// <param name="rawDevice">Raw device information</param>
        /// <param name="deviceName">Device name</param>
        /// <param name="ridDeviceInfo">RawInput device info structure</param>
        /// <returns>Temporary device info or null if creation fails</returns>
        private RawInputDeviceInfo CreateTempDeviceInfo(RAWINPUTDEVICELIST rawDevice, string deviceName, RID_DEVICE_INFO ridDeviceInfo)
        {
            try
            {
                var deviceInfo = new RawInputDeviceInfo
                {
                    DeviceHandle = rawDevice.hDevice,
                    RawInputDeviceType = (RawInputDeviceType)rawDevice.dwType,
                    DeviceType = (int)rawDevice.dwType,
                    InterfacePath = deviceName
                };

                // Populate basic device information
                PopulateDeviceInfo(deviceInfo, ridDeviceInfo);

                return deviceInfo;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Checks if a device has actual input capabilities (buttons, axes, or povs).
        /// Used to filter out devices that pass usage checks but have no real input capabilities.
        /// </summary>
        /// <param name="deviceInfo">Device info to check</param>
        /// <returns>True if device has actual input capabilities</returns>
        private bool HasActualInputCapabilities(RawInputDeviceInfo deviceInfo)
        {
            // Device must have at least one input capability to be considered an input device
            return deviceInfo.ButtonCount > 0 ||
                   deviceInfo.AxeCount > 0 ||
                   deviceInfo.PovCount > 0;
        }

        #endregion
    }
}
