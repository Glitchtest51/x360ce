
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;
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
    public class RawInputDeviceInfo : InputDeviceInfo, IDisposable
    {
        /// <summary>
        /// Logical Maximum values for each POV, used to determine format (4-way, 8-way, etc.).
        /// </summary>
        public List<int> PovLogicalMins { get; set; } = new List<int>();

        /// <summary>
        /// Logical Minimum values for each POV, used to determine if values are 0-based or 1-based.
        /// </summary>
        public List<int> PovLogicalMaxes { get; set; } = new List<int>();

        /// <summary>
        /// Stores Logical Min/Max for each axis/slider by Usage.
        /// Key = (UsagePage << 16) | Usage
        /// </summary>
        public Dictionary<int, AxisRange> DeviceAxisProperties { get; set; } = new Dictionary<int, AxisRange>();
        
        // Simulation Controls (Usage Page 0x02)
        public int ThrottleCount { get; set; }
        public int BrakeCount { get; set; }
        public int SteeringCount { get; set; }
        public int AcceleratorCount { get; set; }
        public int ClutchCount { get; set; }
        
        /// <summary>
        /// Mouse axis sensitivity values for: X axis, Y axis, Vertical wheel axis, Horizontal wheel axis.
        /// Defaults: {20, 20, 50, 50}.
        /// Minimum is 1.
        /// </summary>
        public List<int> MouseAxisSensitivity { get; set; } = new List<int> { 20, 20, 50, 50 };

        /// <summary>
        /// Mouse axis delta accumulated positions for: X axis, Y axis, Vertical wheel axis, Horizontal wheel axis.
        /// Defaults: {32767, 32767, 0, 0}.
        /// Minimum is 0, maximum is 65535, center is 32767.
        /// </summary>
        public List<int> MouseAxisAccumulatedDelta { get; set; } = new List<int> { 32767, 32767, 0, 0 };

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
        /// Cached preparsed data pointer for HID API operations (HidP_GetUsages, etc.).
        /// This is allocated during device enumeration and must be freed on disposal.
        /// Only valid for HID devices (RawInputDeviceType.HID).
        /// </summary>
        public IntPtr PreparsedData { get; set; }
        
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
        /// Dispose the RawInput device when no longer needed.
        /// </summary>
        public void Dispose()
        {
            // Free the preparsed data buffer if it was allocated
            if (PreparsedData != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(PreparsedData);
                PreparsedData = IntPtr.Zero;
            }
            // RawInput devices don't need explicit disposal, but we clear the handle
            DeviceHandle = IntPtr.Zero;
        }
    }

    /// <summary>
    /// Logical range for an axis.
    /// </summary>
    public struct AxisRange
    {
        public int Min;
        public int Max;

        public AxisRange(int min, int max)
        {
            Min = min;
            Max = max;
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
    internal class RawInputDevice
    {
        /// <summary>
        /// Single, authoritative list of RawInput devices.
        /// This is the master list that all components reference - no duplicates.
        /// Updated by GetRawInputDeviceInfoList() during device enumeration.
        /// </summary>
        public static List<RawInputDeviceInfo> RawInputDeviceInfoList { get; private set; }
            = new List<RawInputDeviceInfo>();

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
            [MarshalAs(UnmanagedType.Bool)]
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
        [StructLayout(LayoutKind.Explicit)]
        private struct HIDP_BUTTON_CAPS
        {
            [FieldOffset(0)]
            public ushort UsagePage;
            [FieldOffset(2)]
            public byte ReportID;
            [FieldOffset(3)]
            [MarshalAs(UnmanagedType.U1)]
            public bool IsAlias;
            [FieldOffset(4)]
            public ushort BitField;
            [FieldOffset(6)]
            public ushort LinkCollection;
            [FieldOffset(8)]
            public ushort LinkUsage;
            [FieldOffset(10)]
            public ushort LinkUsagePage;
            [FieldOffset(12)]
            [MarshalAs(UnmanagedType.U1)]
            public bool IsRange;
            [FieldOffset(13)]
            [MarshalAs(UnmanagedType.U1)]
            public bool IsStringRange;
            [FieldOffset(14)]
            [MarshalAs(UnmanagedType.U1)]
            public bool IsDesignatorRange;
            [FieldOffset(15)]
            [MarshalAs(UnmanagedType.U1)]
            public bool IsAbsolute;
            // Reserved array removed to avoid marshalling issues in Explicit layout
            // [FieldOffset(16)] uint[10] Reserved (40 bytes)
            [FieldOffset(56)]
            public HIDP_BUTTON_CAPS_RANGE Range;
            [FieldOffset(56)]
            public HIDP_BUTTON_CAPS_NOT_RANGE NotRange;
        }

        /// <summary>
        /// HID button capabilities range union.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct HIDP_BUTTON_CAPS_RANGE
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
        public struct HIDP_BUTTON_CAPS_NOT_RANGE
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
        [StructLayout(LayoutKind.Explicit)]
        public struct HIDP_VALUE_CAPS
        {
            [FieldOffset(0)]
            public ushort UsagePage;
            [FieldOffset(2)]
            public byte ReportID;
            [FieldOffset(3)]
            [MarshalAs(UnmanagedType.U1)]
            public bool IsAlias;
            [FieldOffset(4)]
            public ushort BitField;
            [FieldOffset(6)]
            public ushort LinkCollection;
            [FieldOffset(8)]
            public ushort LinkUsage;
            [FieldOffset(10)]
            public ushort LinkUsagePage;
            [FieldOffset(12)]
            [MarshalAs(UnmanagedType.U1)]
            public bool IsRange;
            [FieldOffset(13)]
            [MarshalAs(UnmanagedType.U1)]
            public bool IsStringRange;
            [FieldOffset(14)]
            [MarshalAs(UnmanagedType.U1)]
            public bool IsDesignatorRange;
            [FieldOffset(15)]
            [MarshalAs(UnmanagedType.U1)]
            public bool IsAbsolute;
            [FieldOffset(16)]
            [MarshalAs(UnmanagedType.U1)]
            public bool HasNull;
            [FieldOffset(17)]
            public byte Reserved;
            [FieldOffset(18)]
            public ushort BitSize;
            [FieldOffset(20)]
            public ushort ReportCount;
            // Reserved2 array removed to avoid marshalling issues in Explicit layout
            // [FieldOffset(22)] ushort[5] Reserved2 (10 bytes)
            [FieldOffset(32)]
            public uint UnitsExp;
            [FieldOffset(36)]
            public uint Units;
            [FieldOffset(40)]
            public int LogicalMin;
            [FieldOffset(44)]
            public int LogicalMax;
            [FieldOffset(48)]
            public int PhysicalMin;
            [FieldOffset(52)]
            public int PhysicalMax;
            [FieldOffset(56)]
            public HIDP_VALUE_CAPS_RANGE Range;
            [FieldOffset(56)]
            public HIDP_VALUE_CAPS_NOT_RANGE NotRange;
        }

        /// <summary>
        /// HID value capabilities range union.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct HIDP_VALUE_CAPS_RANGE
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
        public struct HIDP_VALUE_CAPS_NOT_RANGE
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

        // HID Usage Pages - Digitizers
        private const ushort HID_USAGE_PAGE_DIGITIZER = 0x0D;

        // HID Usages for Digitizers
        private const ushort HID_USAGE_DIGITIZER_PEN = 0x02;
        private const ushort HID_USAGE_DIGITIZER_TOUCH_SCREEN = 0x04;
        private const ushort HID_USAGE_DIGITIZER_TOUCH_PAD = 0x05;

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
        /// Populates the static RawInputDeviceInfoList with all available RawInput devices (gamepads, keyboards, mice).
        /// This method enumerates all available RawInput devices and outputs detailed information for debugging.
        /// </summary>
        /// <remarks>
        /// This method performs comprehensive RawInput device enumeration:
        /// • Discovers all RawInput-compatible devices (HID devices, keyboards, mice)
        /// • Populates the static RawInputDeviceInfoList with device information AND device handles
        /// • Filters devices by type and availability
        /// • Provides device capability information where available
        /// • Keeps device handles for immediate input reading
        /// • Is self-contained with minimal external dependencies
        ///
        /// IMPORTANT: The RawInputDeviceInfo objects contain device handles.
        /// Call Dispose() on each RawInputDeviceInfo when no longer needed to free resources.
        /// </remarks>
        public void GetRawInputDeviceInfoList()
        {
            // Dispose existing devices before clearing to prevent memory leaks
            // and release unmanaged resources (PreparsedData)
            foreach (var device in RawInputDeviceInfoList)
            {
                device.Dispose();
            }

            // Clear the static list before repopulating
            RawInputDeviceInfoList.Clear();
            
            try
            {
                // Get device count
                uint deviceCount = 0;
                uint structSize = (uint)Marshal.SizeOf<RAWINPUTDEVICELIST>();
                uint result = GetRawInputDeviceList(null, ref deviceCount, structSize);
                
                if (result == uint.MaxValue || deviceCount == 0)
                {
                    return;
                }

                // Enumerate devices
                var rawDevices = new RAWINPUTDEVICELIST[deviceCount];
                result = GetRawInputDeviceList(rawDevices, ref deviceCount, structSize);

                if (result == uint.MaxValue)
                {
                    return;
                }

                // Process each device with optimized early filtering
                foreach (var rawDevice in rawDevices)
                {
                    try
                    {
                        if (ShouldProcessDevice(rawDevice, out bool isInputDevice) && isInputDevice)
                        {
                            ProcessInputDevice(rawDevice, RawInputDeviceInfoList);
                        }
                    }
                    catch { }
                }

                // Filter out MI-only devices (USB composite parent nodes) when sibling COL devices exist
                // This prevents double-counting the same physical device
                var filteredDevices = FilterMiOnlyDevices(RawInputDeviceInfoList);
                if (filteredDevices.Count != RawInputDeviceInfoList.Count)
                {
                    RawInputDeviceInfoList.Clear();
                    RawInputDeviceInfoList.AddRange(filteredDevices);
                }
            }
            catch { }
        }

        /// <summary>
        /// Disposes all RawInput devices in the provided list to free resources.
        /// Call this method when the device list is no longer needed.
        /// </summary>
        /// <param name="deviceList">List of RawInputDeviceInfo objects to dispose</param>
        public static void DisposeDeviceList(List<RawInputDeviceInfo> deviceList)
        {
            if (deviceList == null) return;

            foreach (var deviceInfo in deviceList)
            {
                try
                {
                    if (deviceInfo != null)
                    {
                        deviceInfo.Dispose();
                    }
                }
                catch { }
            }
        }

        #region Private Helper Methods
        /// <summary>
        /// Determines if a device should be processed based on optimized early filtering.
        /// Returns true if device should be processed, and sets isInputDevice to indicate if it's an input device.
        /// </summary>
        private bool ShouldProcessDevice(RAWINPUTDEVICELIST rawDevice, out bool isInputDevice)
        {
            isInputDevice = false;

            // Step 1: Get device name first to allow filtering by name for ALL device types
            // This is necessary because some system devices (like Intel HID Event Filter)
            // report themselves as Keyboards but should be excluded.
            string deviceName = GetDeviceName(rawDevice.hDevice);
            if (string.IsNullOrEmpty(deviceName))
                return false;

            var deviceType = (RawInputDeviceType)rawDevice.dwType;

            // Step 2: Quick rejection based on device name patterns
            if (IsKnownNonInputDeviceByName(deviceName, deviceType))
                return false;

            // Step 3: Always process Mouse and Keyboard devices
            // Safe to do here because we've already filtered out unwanted system "keyboards"
            if (deviceType == RawInputDeviceType.Mouse || deviceType == RawInputDeviceType.Keyboard)
            {
                isInputDevice = true;
                return true;
            }

            // Only process HID devices from here on
            if (deviceType != RawInputDeviceType.HID)
                return false;

            // Step 4: Get device info once (reuse for all checks)
            var deviceInfoStruct = GetDeviceInfo(rawDevice.hDevice);
            if (!deviceInfoStruct.HasValue || deviceInfoStruct.Value.dwType != RIM_TYPEHID)
                return false;

            var hid = deviceInfoStruct.Value.union.hid;
            int usagePage = hid.usUsagePage;
            int usage = hid.usUsage;

            // Step 5: Check if it's an explicitly excluded device by usage
            if (IsExcludedDeviceByUsage(usagePage, usage))
                return false;

            // Step 6: Check if it's a known input device by HID usage
            if (IsKnownInputDeviceByUsage(usagePage, usage))
            {
                isInputDevice = true;
                return true;
            }

            // Step 7: Check if device name suggests it's an input device (pattern matching)
            if (HasInputDeviceNamePattern(deviceName, ""))
            {
                isInputDevice = true;
                return true;
            }

            // Step 8: Final check - verify actual input capabilities (slowest - requires HID parsing)
            // Only do this if previous checks didn't conclusively identify the device
            if (deviceType == RawInputDeviceType.HID)
            {
                // Attempt to parse HID capabilities to check for buttons/axes
                if (GetHidCapabilities(rawDevice.hDevice, out HidCapabilitiesResult caps))
                {
                    // Free the preparsed data as we only needed it for the check
                    // (It will be re-acquired in ProcessInputDevice if accepted)
                    if (caps.PreparsedData != IntPtr.Zero)
                        Marshal.FreeHGlobal(caps.PreparsedData);

                    // Check if it has any relevant input controls
                    if (caps.ButtonCount > 0 || caps.AxeCount > 0 || caps.PovCount > 0 || caps.SliderCount > 0)
                    {
                        isInputDevice = true;
                        return true;
                    }
                }
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
        /// Holds the results of HID capability parsing.
        /// </summary>
        private class HidCapabilitiesResult
        {
            public int AxeCount;
            public int SliderCount;
            public int ButtonCount;
            public int PovCount;
            public int ThrottleCount;
            public int BrakeCount;
            public int SteeringCount;
            public int AcceleratorCount;
            public int ClutchCount;
            public bool HasForceFeedback;
            public bool UsesReportIds;
            public int ButtonDataOffset;
            public IntPtr PreparsedData = IntPtr.Zero;
            public List<int> PovLogicalMins = new List<int>();
            public List<int> PovLogicalMaxes = new List<int>();
            public Dictionary<int, AxisRange> AxisProperties = new Dictionary<int, AxisRange>();
        }

        /// <summary>
        /// Gets HID device capabilities by parsing the HID Report Descriptor.
        /// IMPORTANT: This method allocates preparsed data that must be stored in the device info
        /// and freed when the device is disposed.
        /// </summary>
        private bool GetHidCapabilities(IntPtr hDevice, out HidCapabilitiesResult result)
        {
            result = new HidCapabilitiesResult();
            IntPtr preparsedData = IntPtr.Zero;

            try
            {
                // Get the size of preparsed data
                uint size = 0;
                GetRawInputDeviceInfo(hDevice, RIDI_PREPARSEDDATA, IntPtr.Zero, ref size);

                if (size == 0) return false;

                // Allocate buffer and get preparsed data
                preparsedData = Marshal.AllocHGlobal((int)size);
                if (GetRawInputDeviceInfo(hDevice, RIDI_PREPARSEDDATA, preparsedData, ref size) == uint.MaxValue)
                    return false;

                // Get HID capabilities
                HIDP_CAPS caps;
                if (HidP_GetCaps(preparsedData, out caps) != HIDP_STATUS_SUCCESS)
                    return false;

                // Track unique usages
                var foundButtons = new HashSet<int>();
                var foundAxes = new HashSet<int>();
                var foundSliders = new HashSet<int>();
                var foundPovs = new HashSet<int>();

                // 1. Parse Input Button Capabilities
                ParseButtonCaps(preparsedData, caps, result, foundButtons);

                // 2. Parse Input Value Capabilities
                ParseValueCaps(preparsedData, caps, result, foundAxes, foundSliders, foundPovs);

                // 3. Check for Force Feedback
                CheckForceFeedback(preparsedData, caps, result);

                // 4. Heuristics for special devices (Flight Sticks etc.)
                ApplyHeuristics(preparsedData, caps, result);

                // 5. Analyze Report Structure for complex devices
                if (result.AxeCount < 4 && caps.InputReportByteLength > 0)
                {
                    if (AnalyzeInputReportStructure(preparsedData, caps, out int ra, out int rs, out int rb, out int rp))
                    {
                        result.AxeCount = Math.Max(result.AxeCount, ra);
                        result.SliderCount = Math.Max(result.SliderCount, rs);
                        result.ButtonCount = Math.Max(result.ButtonCount, rb);
                        result.PovCount = Math.Max(result.PovCount, rp);
                    }
                }

                // 6. Calculate Button Data Offset
                result.ButtonDataOffset = CalculateButtonDataOffset(caps, result.UsesReportIds, preparsedData);

                // Success
                result.PreparsedData = preparsedData;
                preparsedData = IntPtr.Zero; // Prevent cleanup
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (preparsedData != IntPtr.Zero)
                    Marshal.FreeHGlobal(preparsedData);
            }
        }

        private void ParseButtonCaps(IntPtr preparsedData, HIDP_CAPS caps, HidCapabilitiesResult result, HashSet<int> foundButtons)
        {
            if (caps.NumberInputButtonCaps == 0) return;

            var buttonCaps = new HIDP_BUTTON_CAPS[caps.NumberInputButtonCaps];
            ushort length = caps.NumberInputButtonCaps;

            if (HidP_GetButtonCaps(HIDP_REPORT_TYPE.HidP_Input, buttonCaps, ref length, preparsedData) != HIDP_STATUS_SUCCESS)
                return;

            for (int i = 0; i < length; i++)
            {
                var cap = buttonCaps[i];
                if (cap.ReportID != 0) result.UsesReportIds = true;
                if (!cap.IsRange && cap.IsAlias) continue;

                if (cap.IsRange)
                {
                    result.ButtonCount += (cap.Range.UsageMax - cap.Range.UsageMin + 1);
                }
                else
                {
                    if (foundButtons.Add((cap.UsagePage << 16) | cap.NotRange.Usage))
                        result.ButtonCount++;
                }
            }
        }

        private void ParseValueCaps(IntPtr preparsedData, HIDP_CAPS caps, HidCapabilitiesResult result,
            HashSet<int> foundAxes, HashSet<int> foundSliders, HashSet<int> foundPovs)
        {
            if (caps.NumberInputValueCaps == 0) return;

            var valueCaps = new HIDP_VALUE_CAPS[caps.NumberInputValueCaps];
            ushort length = caps.NumberInputValueCaps;

            if (HidP_GetValueCaps(HIDP_REPORT_TYPE.HidP_Input, valueCaps, ref length, preparsedData) != HIDP_STATUS_SUCCESS)
                return;

            // Check Report IDs first
            for (int i = 0; i < length; i++)
            {
                if (valueCaps[i].ReportID != 0)
                {
                    result.UsesReportIds = true;
                    break;
                }
            }

            for (int k = 0; k < length; k++)
            {
                var cap = valueCaps[k];
                if (cap.IsAlias || cap.BitSize < 2) continue;

                int reportCount = Math.Max(1, (int)cap.ReportCount);

                // Handle special devices with UsagePage 0x00
                if (cap.UsagePage == 0x00 && cap.LinkUsage >= 0x30 && cap.LinkUsage <= 0x39 &&
                    (cap.LinkUsagePage == HID_USAGE_PAGE_GENERIC || cap.LinkUsagePage == cap.LinkUsage))
                {
                    if (cap.LinkUsage >= 0x30 && cap.LinkUsage <= 0x35) result.AxeCount += reportCount;
                    else if (cap.LinkUsage >= 0x36 && cap.LinkUsage <= 0x38) result.SliderCount += reportCount;
                    else if (cap.LinkUsage == 0x39) result.PovCount += reportCount;
                    continue;
                }

                // Handle Pointer collections
                ushort usage = cap.IsRange ? cap.Range.UsageMin : cap.NotRange.Usage;
                if (cap.UsagePage == HID_USAGE_PAGE_GENERIC && usage == 0x01 &&
                    cap.LinkUsagePage == HID_USAGE_PAGE_GENERIC && cap.LinkUsage >= 0x30 && cap.LinkUsage <= 0x39)
                {
                    if (cap.LinkUsage >= 0x30 && cap.LinkUsage <= 0x35) result.AxeCount += reportCount;
                    else if (cap.LinkUsage >= 0x36 && cap.LinkUsage <= 0x38) result.SliderCount += reportCount;
                    else if (cap.LinkUsage == 0x39) result.PovCount += reportCount;
                    continue;
                }

                // General Processing
                int rangeMin = cap.IsRange ? cap.Range.UsageMin : cap.NotRange.Usage;
                int rangeSize = (cap.IsRange ? cap.Range.UsageMax : cap.NotRange.Usage) - rangeMin + 1;

                int axesFound = 0, slidersFound = 0, povsFound = 0;
                int throttleFound = 0, brakeFound = 0, steeringFound = 0, accelFound = 0, clutchFound = 0;

                for (int i = 0; i < reportCount; i++)
                {
                    int u = (i < rangeSize) ? (rangeMin + i) : (cap.IsRange ? cap.Range.UsageMax : cap.NotRange.Usage);
                    int key = (cap.UsagePage << 16) | u;

                    if (!result.AxisProperties.ContainsKey(key) && cap.LogicalMax > cap.LogicalMin)
                    {
                        result.AxisProperties[key] = new AxisRange(cap.LogicalMin, cap.LogicalMax);
                    }

                    if (cap.UsagePage == HID_USAGE_PAGE_GENERIC)
                    {
                        if (u >= 0x30 && u <= 0x35) { if (foundAxes.Add((HID_USAGE_PAGE_GENERIC << 16) | u)) axesFound++; }
                        else if (u >= 0x36 && u <= 0x38) { if (foundSliders.Add((HID_USAGE_PAGE_GENERIC << 16) | u)) slidersFound++; }
                        else if (u == 0x39 && foundPovs.Add((HID_USAGE_PAGE_GENERIC << 16) | u))
                        {
                            povsFound++;
                            result.PovLogicalMins.Add(cap.LogicalMin);
                            result.PovLogicalMaxes.Add(cap.LogicalMax);
                        }
                    }
                    else if (cap.UsagePage == HID_USAGE_PAGE_DIGITIZER)
                    {
                        if ((u == 0x30 || u == 0x31 || u == 0x3D || u == 0x3E) && foundAxes.Add((HID_USAGE_PAGE_DIGITIZER << 16) | u))
                            axesFound++;
                    }
                    else if (cap.UsagePage == HID_USAGE_PAGE_SIMULATION)
                    {
                        if (u == 0xBA) throttleFound++;
                        else if (u == 0xBB) accelFound++;
                        else if (u == 0xBC) brakeFound++;
                        else if (u == 0xBD) clutchFound++;
                        else if (u == 0xB0) steeringFound++;
                    }
                }

                result.AxeCount += axesFound;
                result.SliderCount += slidersFound;
                result.PovCount += povsFound;
                result.ThrottleCount += throttleFound;
                result.AcceleratorCount += accelFound;
                result.BrakeCount += brakeFound;
                result.ClutchCount += clutchFound;
                result.SteeringCount += steeringFound;
            }
        }

        private void CheckForceFeedback(IntPtr preparsedData, HIDP_CAPS caps, HidCapabilitiesResult result)
        {
            if (caps.NumberOutputValueCaps > 0)
            {
                var capsList = new HIDP_VALUE_CAPS[caps.NumberOutputValueCaps];
                ushort len = caps.NumberOutputValueCaps;
                if (HidP_GetValueCaps(HIDP_REPORT_TYPE.HidP_Output, capsList, ref len, preparsedData) == HIDP_STATUS_SUCCESS)
                {
                    for (int i = 0; i < len; i++)
                    {
                        if (capsList[i].UsagePage == 0x0F) // PID usage page
                        {
                            result.HasForceFeedback = true;
                            break;
                        }
                    }
                }
            }
        }

        private void ApplyHeuristics(IntPtr preparsedData, HIDP_CAPS caps, HidCapabilitiesResult result)
        {
            if ((result.AxeCount == 0 || result.PovCount == 0) && result.SliderCount > 0 && caps.NumberInputValueCaps > 0)
            {
                var valueCaps = new HIDP_VALUE_CAPS[caps.NumberInputValueCaps];
                ushort len = caps.NumberInputValueCaps;
                if (HidP_GetValueCaps(HIDP_REPORT_TYPE.HidP_Input, valueCaps, ref len, preparsedData) == HIDP_STATUS_SUCCESS)
                {
                    for (int i = 0; i < len; i++)
                    {
                        var cap = valueCaps[i];
                        if (cap.IsAlias) continue;
                        ushort usage = cap.IsRange ? cap.Range.UsageMin : cap.NotRange.Usage;
                        if (cap.UsagePage == HID_USAGE_PAGE_GENERIC && usage == 0x01 &&
                            cap.LinkUsagePage == HID_USAGE_PAGE_GENERIC && cap.LinkUsage == HID_USAGE_GENERIC_JOYSTICK)
                        {
                            if (result.AxeCount == 0) result.AxeCount = 3;
                            if (result.PovCount == 0) result.PovCount = 1;
                            break;
                        }
                    }
                }
            }
        }

 
  /// <summary>
  /// Calculates the actual byte offset where button data starts in the HID report.
  /// CRITICAL: In HID reports, buttons ALWAYS come after axis/value data.
  /// Structure: [Report ID] + [Axis/Value Data] + [Button Data] + [Padding]
  /// Uses both HID descriptor analysis AND heuristic fallback for reliability.
  /// </summary>
  /// <param name="caps">HID capabilities structure</param>
  /// <param name="usesReportIds">Whether device uses Report IDs</param>
  /// <param name="preparsedData">HID preparsed data for detailed analysis</param>
  /// <returns>Byte offset where button data starts in the report</returns>
  private int CalculateButtonDataOffset(HIDP_CAPS caps, bool usesReportIds, IntPtr preparsedData)
  {
   try
   {
   	// Start with Report ID byte if present
   	int baseOffset = usesReportIds ? 1 : 0;
   	
   	// If no value caps, buttons start right after Report ID
   	if (caps.NumberInputValueCaps == 0)
   	{
   		return baseOffset;
   	}
   	
   	// Get all value capabilities (axes, POVs, sliders)
   	var valueCaps = new HIDP_VALUE_CAPS[caps.NumberInputValueCaps];
   	ushort valueCapsLength = caps.NumberInputValueCaps;
   	int status = HidP_GetValueCaps(HIDP_REPORT_TYPE.HidP_Input, valueCaps, ref valueCapsLength, preparsedData);
   	
   	if (status != HIDP_STATUS_SUCCESS)
   	{
   		return baseOffset;
   	}
   	
   	// Calculate total bits used by ALL values (axes/POVs/sliders)
   	// CRITICAL: Must account for EVERY value capability
   	int totalValueBits = 0;
   	int valueCapsProcessed = 0;
   	
   	for (int i = 0; i < valueCapsLength; i++)
   	{
   	       var valueCap = valueCaps[i];

   	       // Skip aliases to avoid double-counting bits
   	       if (valueCap.IsAlias) continue;

   		// ReportCount indicates how many fields are in this capability
   		int reportCount = Math.Max(1, (int)valueCap.ReportCount);
   		
   		// Total bits for this capability
   	       // Note: In HIDP_VALUE_CAPS, ReportCount is the total number of fields for this structure
   		int bitsForThisCap = valueCap.BitSize * reportCount;
   		totalValueBits += bitsForThisCap;
   		valueCapsProcessed++;
   		
   	}
   	
   	// Convert total value bits to bytes (round up to next byte boundary)
   	int valueBytes = (totalValueBits + 7) / 8;
   	
   	// Button offset = Report ID + All axis/value bytes
   	// This is ALWAYS correct because buttons come after axes in HID reports
   	int buttonByteOffset = baseOffset + valueBytes;
   	
   	// Sanity check: offset must be within report bounds
   	if (buttonByteOffset < baseOffset || buttonByteOffset >= caps.InputReportByteLength)
   	{
   		return baseOffset;
   	}
   	
   	return buttonByteOffset;
   }
   catch
   {
    return usesReportIds ? 1 : 0;
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
            out int axeCount, out int sliderCount, out int buttonCount, out int povCount)
        {
            axeCount = 0;
            sliderCount = 0;
            buttonCount = 0;
            povCount = 0;
            
            // Single allocation for report buffer
            IntPtr reportPtr = Marshal.AllocHGlobal(caps.InputReportByteLength);
            // Zero out buffer
            byte[] zeroBuffer = new byte[caps.InputReportByteLength];
            Marshal.Copy(zeroBuffer, 0, reportPtr, zeroBuffer.Length);

            try
            {
                ushort[] checkUsages = { 0x30, 0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37, 0x38 };
                
                foreach (var usage in checkUsages)
                {
                    int value;
                    if (HidP_GetUsageValue(HIDP_REPORT_TYPE.HidP_Input, HID_USAGE_PAGE_GENERIC, 0, usage,
                        out value, preparsedData, reportPtr, (uint)zeroBuffer.Length) == HIDP_STATUS_SUCCESS)
                    {
                        if (usage <= 0x35) axeCount++; else sliderCount++;
                    }
                }
                
                // Check for POV
                int povValue;
                if (HidP_GetUsageValue(HIDP_REPORT_TYPE.HidP_Input, HID_USAGE_PAGE_GENERIC, 0, 0x39,
                    out povValue, preparsedData, reportPtr, (uint)zeroBuffer.Length) == HIDP_STATUS_SUCCESS)
                {
                    povCount = 1;
                }
                
                return axeCount > 0 || sliderCount > 0 || povCount > 0;
            }
            catch
            {
                return false;
            }
            finally
            {
                Marshal.FreeHGlobal(reportPtr);
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
                    // Enforce minimum 5 buttons for mouse (Left, Right, Middle, X1, X2) to ensure list capacity
                    // Some trackpads report 0 or 2 buttons but still emit events for others
                    deviceInfo.ButtonCount = Math.Max(5, (int)mouse.dwNumberOfButtons);
                    deviceInfo.ProductName = "Mouse";
                    
                    // ENHANCEMENT: Set to 4 axes if horizontal wheel is supported.
                    // Most modern mice support horizontal wheels even if not reported by Windows.
                    deviceInfo.AxeCount = mouse.fHasHorizontalWheel ? 4 : 3; // X, Y, Z (vertical wheel), [W (horizontal wheel)]
                    break;

                case RIM_TYPEKEYBOARD:
                    var keyboard = ridDeviceInfo.union.keyboard;
                    // dwNumberOfKeysTotal often returns a driver-defined maximum (e.g., 264) rather than the actual physical key count.
                    // This is expected behavior for the RawInput API and not a bug in the detection code.
                    // The value 264 usually corresponds to the maximum number of scan codes supported by the driver/hardware
                    // and typically includes all standard keys plus potential media/macro keys.
                    // IMPORTANT: Ensure we have at least 256 buttons to cover all Virtual Keys (0-255)
                    // Integrated keyboards often report exact physical key counts (e.g. 101), causing crashes when VKey > 101
                    deviceInfo.ButtonCount = Math.Max(256, (int)keyboard.dwNumberOfKeysTotal); // Keyboards report keys as buttons
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
                var upperPath = interfacePath.ToUpperInvariant();

                // 1. Standard VID_XXXX and PID_XXXX
                var vid = InputDeviceInfo.ExtractHexValue(upperPath, "VID_", 4) ??
                          InputDeviceInfo.ExtractHexValue(upperPath, "VID&", 4) ?? // Alternate VID&...
                          InputDeviceInfo.ExtractHexValue(upperPath, "VEN_", 4);   // Fallback VEN_

                var pid = InputDeviceInfo.ExtractHexValue(upperPath, "PID_", 4) ??
                          InputDeviceInfo.ExtractHexValue(upperPath, "PID&", 4) ?? // Alternate PID&...
                          InputDeviceInfo.ExtractHexValue(upperPath, "DEV_", 4);   // Fallback DEV_

                if (vid.HasValue && pid.HasValue)
                    return (vid.Value, pid.Value);

                // 2. Composite Device ID (e.g. HID#INT33D2...)
                var match = Regex.Match(upperPath, @"(HID|ACPI)[#\\]([A-Z]+)([0-9A-F]{2,})[#&]");
                if (match.Success)
                {
                    string vendorPart = match.Groups[2].Value;
                    string devicePart = match.Groups[3].Value;

                    // Convert vendor part (ASCII bytes)
                    int vendorCode = 0;
                    for (int i = 0; i < vendorPart.Length && i < 4; i++)
                    {
                        vendorCode = (vendorCode << 8) | (byte)vendorPart[i];
                    }

                    // Parse device part (Hex)
                    if (int.TryParse(devicePart, System.Globalization.NumberStyles.HexNumber, null, out int deviceCode))
                    {
                        return (vendorCode, deviceCode);
                    }
                }
            }
            catch { }

            return (0, 0);
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

            // Digitizers (0x0D) - Pens, TouchScreens and TouchPads are valid input devices
            if (usagePage == HID_USAGE_PAGE_DIGITIZER)
            {
                switch (usage)
                {
                    case HID_USAGE_DIGITIZER_PEN:
                    case HID_USAGE_DIGITIZER_TOUCH_SCREEN:
                    case HID_USAGE_DIGITIZER_TOUCH_PAD:
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if device should be explicitly excluded based on usage.
        /// Used to filter out devices that might have buttons but are not game controllers (e.g. Media Keys).
        /// </summary>
        private bool IsExcludedDeviceByUsage(int usagePage, int usage)
        {
            // Consumer Controls (0x0C) - Media keys, volume controls, etc.
            // These often report support for the entire usage range as "buttons" (e.g. 767 buttons)
            // which clutters the device list.
            if (usagePage == 0x0C)
                return true;

            // Telephony (0x0B) - Phone controls
            if (usagePage == 0x0B)
                return true;

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
        /// <param name="deviceList">List to add the device to</param>
        private void ProcessInputDevice(RAWINPUTDEVICELIST rawDevice, List<RawInputDeviceInfo> deviceList)
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
                    InputType = "RawInput",
                    // Initial application profile state
                    IsEnabled = false,
                    AssignedToPad1 = false,
                    AssignedToPad2 = false,
                    AssignedToPad3 = false,
                    AssignedToPad4 = false
                };

                // Get device name (interface path)
                string deviceName = GetDeviceName(rawDevice.hDevice);
                deviceInfo.InterfacePath = deviceName;
                
                // Filter out virtual/converted devices
                if (deviceInfo.IsVirtualConvertedDevice())
                {
                    return;
                }
                
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
                        if (GetHidCapabilities(rawDevice.hDevice, out HidCapabilitiesResult caps))
                        {
                            deviceInfo.AxeCount = caps.AxeCount;
                            deviceInfo.SliderCount = caps.SliderCount;
                            deviceInfo.ButtonCount = caps.ButtonCount;
                            deviceInfo.PovCount = caps.PovCount;
                            deviceInfo.ThrottleCount = caps.ThrottleCount;
                            deviceInfo.BrakeCount = caps.BrakeCount;
                            deviceInfo.SteeringCount = caps.SteeringCount;
                            deviceInfo.AcceleratorCount = caps.AcceleratorCount;
                            deviceInfo.ClutchCount = caps.ClutchCount;
                            deviceInfo.HasForceFeedback = caps.HasForceFeedback;
                            deviceInfo.UsesReportIds = caps.UsesReportIds;
                            deviceInfo.ButtonDataOffset = caps.ButtonDataOffset;
                            deviceInfo.PovLogicalMins = caps.PovLogicalMins;
                            deviceInfo.PovLogicalMaxes = caps.PovLogicalMaxes;
                            deviceInfo.DeviceAxisProperties = caps.AxisProperties;
                            // CRITICAL: Store preparsed data for later use by HidP_GetUsages
                            // This will be freed when the device is disposed
                            deviceInfo.PreparsedData = caps.PreparsedData;
    
                            // ENHANCEMENT: For Digitizers (TouchScreens, TouchPads, Pens), ensure we have enough button slots
                            // to map standard digitizer flags (Tip, Barrel, Eraser, InRange, etc.) even if the device
                            // reports fewer physical buttons (e.g. only Tip Switch).
                            // This allows us to map usages like Tip Switch (0x42) to Button 0, Barrel (0x44) to Button 1, etc.
                            if (deviceInfo.UsagePage == HID_USAGE_PAGE_DIGITIZER)
                            {
                                deviceInfo.ButtonCount = Math.Max(deviceInfo.ButtonCount, 5);
                            }
                        }
                    }
                }
    
                // Attempt to get a better product name from the Registry (e.g. "Logitech G903...")
                // instead of generic "Keyboard" or "Mouse"
                var registryName = GetDeviceProductFromRegistry(deviceInfo.InterfacePath);
                if (!string.IsNullOrEmpty(registryName))
                {
                    deviceInfo.ProductName = registryName;
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

                // Filter out devices with no physical inputs (HID only)
                // This ensures we don't add devices that claimed to be inputs but have no actual controls
                if (deviceInfo.RawInputDeviceType == RawInputDeviceType.HID &&
                    deviceInfo.AxeCount == 0 && deviceInfo.ButtonCount == 0 &&
                    deviceInfo.SliderCount == 0 && deviceInfo.PovCount == 0 &&
                    deviceInfo.ThrottleCount == 0 && deviceInfo.BrakeCount == 0 &&
                    deviceInfo.SteeringCount == 0 && deviceInfo.AcceleratorCount == 0 &&
                    deviceInfo.ClutchCount == 0)
                {
                    deviceInfo.Dispose();
                    return;
                }

                // Add device to the final list (already filtered as input device)
                deviceList.Add(deviceInfo);
            }
            catch { }
        }

        private static readonly string[] NonInputKeywords = {
            "audio", "sound", "speaker", "microphone", "headphone", "headset",
            "storage", "disk", "drive", "mass", "flash", "card reader",
            "network", "ethernet", "wifi", "bluetooth\\radio", "wireless adapter",
            "camera", "webcam", "video", "capture", "imaging",
            "printer", "scanner", "fax",
            "modem", "serial", "parallel", "hub", "root",
            "composite\\interface", "system", "acpi", "pci", "processor", "chipset",
            "monitor", "display", "screen",
            "battery", "power", "ups",
            "sensor", "accelerometer", "gyroscope", "proximity",
            "input_config", "inputconfig", "input configuration",
            "portable_device", "portabledevice", "portable device control"
        };

        /// <summary>
        /// Quick check if device name indicates it's a known non-input device.
        /// Used for early filtering to skip processing entirely.
        /// Optimized with comprehensive patterns and efficient checking.
        /// </summary>
        /// <param name="deviceName">Device name/interface path</param>
        /// <param name="deviceType">Optional device type to allow context-aware filtering</param>
        /// <returns>True if device is definitely not an input device</returns>
        private bool IsKnownNonInputDeviceByName(string deviceName, RawInputDeviceType? deviceType = null)
        {
            if (string.IsNullOrEmpty(deviceName))
                return false;

            // Early reject: Intel platform endpoints (HID Event Filter) - platform hotkey controllers
            // Examples: VID_494E54&PID_33D2 (INT33D2), VID_8087&PID_0000 (INTC816)
            var upperName = deviceName.ToUpperInvariant();
            if (upperName.Contains("HID#INT33D2") || upperName.Contains("HID#INTC816") ||
                upperName.Contains("HID\\INT33D2") || upperName.Contains("HID\\INTC816") ||
                (upperName.Contains("VID_494E54") && upperName.Contains("&COL")) ||
                (upperName.Contains("VID_8087") && upperName.Contains("&COL")))
            {
                return true;
            }

            // Early reject: Vendor-defined usage page 0x01FF (configuration/feature collections)
            if (upperName.Contains("UP:01FF") || upperName.Contains("&UP:01FF"))
            {
                return true;
            }

            // Check against known non-input keywords
            foreach (var keyword in NonInputKeywords)
            {
                // SPECIAL CASE: Ignore "acpi" keyword completely.
                // Many valid internal input devices (Keyboards, Mice, Touchpads) are connected via
                // PS/2 or I2C and enumerated under the ACPI bus (e.g. ACPI\VEN_... or ACPI#...).
                // Blocking "acpi" hides these devices. We rely on Usage Page/Usage and Capability
                // checks (later steps) to filter out actual non-input system devices.
                if (keyword == "acpi")
                    continue;

                if (deviceName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }


        /// <summary>
        /// Filters out HID-type MI-only devices (USB composite parent nodes) IF a sibling device with COL exists.
        /// This prevents double-counting the same physical device and removes ambiguous transport nodes,
        /// while preserving MI-only devices that are actually valid (no COL siblings).
        /// </summary>
        /// <param name="deviceList">List of devices to filter</param>
        /// <returns>Filtered list</returns>
        private List<RawInputDeviceInfo> FilterMiOnlyDevices(List<RawInputDeviceInfo> deviceList)
        {
            // First, find all HID devices that have both MI and COL in their path
            // and capture their "Base MI Path" identifier (VID+PID+MI)
            var devicesWithCol = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var device in deviceList)
            {
                if (device.RawInputDeviceType != RawInputDeviceType.HID)
                    continue;

                var path = device.InterfacePath ?? "";

                // Check if device has COL
                if (path.IndexOf("&COL", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    path.IndexOf("\\COL", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    // Extract MI part to form the ID
                    string miPart = GetMiPart(path);
                    if (!string.IsNullOrEmpty(miPart))
                    {
                        // ID = VID_PID_MI
                        string id = $"{device.VendorId:X4}_{device.ProductId:X4}_{miPart}";
                        devicesWithCol.Add(id);
                    }
                }
            }

            var filteredList = new List<RawInputDeviceInfo>();

            foreach (var device in deviceList)
            {
                bool keepDevice = true;

                // Only check HID devices for filtering
                if (device.RawInputDeviceType == RawInputDeviceType.HID)
                {
                    var path = device.InterfacePath ?? "";

                    // Check if it is an MI-only device (Has MI, No COL)
                    string miPart = GetMiPart(path);
                    bool hasMi = !string.IsNullOrEmpty(miPart);
                    bool hasCol = path.IndexOf("&COL", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  path.IndexOf("\\COL", StringComparison.OrdinalIgnoreCase) >= 0;

                    if (hasMi && !hasCol)
                    {
                        // This is an MI-only device.
                        // Check if we have a corresponding COL device (sibling)
                        string id = $"{device.VendorId:X4}_{device.ProductId:X4}_{miPart}";

                        if (devicesWithCol.Contains(id))
                        {
                            // Sibling COL device exists, so this is likely a parent container/duplicate.
                            // Filter it out.
                            keepDevice = false;
                        }
                    }
                }

                if (keepDevice)
                {
                    filteredList.Add(device);
                }
            }

            return filteredList;
        }

        /// <summary>
        /// Extracts the MI part (e.g. "MI_00") from the interface path.
        /// </summary>
        private string GetMiPart(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            var upperPath = path.ToUpperInvariant();

            int miIndex = upperPath.IndexOf("&MI_", StringComparison.Ordinal);
            if (miIndex < 0) miIndex = upperPath.IndexOf("\\MI_", StringComparison.Ordinal);

            if (miIndex >= 0 && miIndex + 6 <= upperPath.Length)
            {
                // Return "MI_XX"
                return upperPath.Substring(miIndex + 1, 5);
            }

            return null;
        }

        /// <summary>
        /// Gets the friendly device name from the Windows Registry.
        /// Resolves generic names like "Keyboard" or "Mouse" to actual hardware names.
        /// </summary>
        private string GetDeviceProductFromRegistry(string interfacePath)
        {
            if (string.IsNullOrEmpty(interfacePath)) return null;

            try
            {
                // Convert Interface Path to Registry Path
                // Format: \\?\HID#VID_XXXX&PID_XXXX...#{GUID}
                // Target: SYSTEM\CurrentControlSet\Enum\HID\VID_XXXX&PID_XXXX...\InstanceId

                // 1. Remove \\?\ prefix
                var path = interfacePath;
                if (path.StartsWith(@"\\?\")) path = path.Substring(4);

                // 2. Remove {GUID} suffix
                int lastHash = path.LastIndexOf('#');
                if (lastHash > 0) path = path.Substring(0, lastHash);

                // 3. Replace remaining # with \ to form the Enum path
                path = path.Replace('#', '\\');

                // 4. Construct full registry key
                string keyPath = $@"SYSTEM\CurrentControlSet\Enum\{path}";

                using (var key = Registry.LocalMachine.OpenSubKey(keyPath))
                {
                    if (key != null)
                    {
                        // Try "FriendlyName" first (most descriptive)
                        var friendlyName = key.GetValue("FriendlyName") as string;
                        if (!string.IsNullOrEmpty(friendlyName)) return friendlyName;

                        // Fallback to "DeviceDesc"
                        var deviceDesc = key.GetValue("DeviceDesc") as string;
                        if (!string.IsNullOrEmpty(deviceDesc))
                        {
                            // DeviceDesc often looks like "@oemXX.inf,%DeviceName%;Actual Name"
                            // We want the last part
                            int semiColon = deviceDesc.LastIndexOf(';');
                            if (semiColon >= 0 && semiColon < deviceDesc.Length - 1)
                            {
                                return deviceDesc.Substring(semiColon + 1);
                            }
                            return deviceDesc;
                        }
                    }
                }
            }
            catch { }

            return null;
        }

        #endregion
    }
}
