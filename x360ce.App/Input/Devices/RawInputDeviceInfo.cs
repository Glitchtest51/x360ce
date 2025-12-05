
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
        /// • Logs detailed device properties using Debug.WriteLine for diagnostics
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
            var stopwatch = Stopwatch.StartNew();

            // Dispose existing devices before clearing to prevent memory leaks
            // and release unmanaged resources (PreparsedData)
            foreach (var device in RawInputDeviceInfoList)
            {
                device.Dispose();
            }

            // Clear the static list before repopulating
            RawInputDeviceInfoList.Clear();
            
            var deviceListDebugLines = new List<string>();
            int deviceListIndex = 0;

            try
            {
                Debug.WriteLine("\n-----------------------------------------------------------------------------------------------------------------\n\n" +
                    "RawInputDevice: Starting RawInput device enumeration...");

                // Get device count
                uint deviceCount = 0;
                uint structSize = (uint)Marshal.SizeOf<RAWINPUTDEVICELIST>();
                uint result = GetRawInputDeviceList(null, ref deviceCount, structSize);
                
                if (result == uint.MaxValue || deviceCount == 0)
                {
                    LogEmptyResult(deviceListDebugLines, result == uint.MaxValue ? GetLastError() : 0);
                    return;
                }

                Debug.WriteLine($"RawInputDevice: Found {deviceCount} RawInput devices");

                // Enumerate devices
                var rawDevices = new RAWINPUTDEVICELIST[deviceCount];
                result = GetRawInputDeviceList(rawDevices, ref deviceCount, structSize);

                if (result == uint.MaxValue)
                {
                    LogEmptyResult(deviceListDebugLines, GetLastError());
                    return;
                }

                // Process each device with optimized early filtering
                foreach (var rawDevice in rawDevices)
                {
                    try
                    {
                        if (ShouldProcessDevice(rawDevice, out bool isInputDevice) && isInputDevice)
                        {
                            ProcessInputDevice(rawDevice, ref deviceListIndex, RawInputDeviceInfoList, deviceListDebugLines);
                        }
                    }
                    catch (Exception deviceEx)
                    {
                        Debug.WriteLine($"RawInputDevice: Error processing device 0x{rawDevice.hDevice.ToInt64():X8}: {deviceEx.Message}");
                    }
                }

                // Filter out MI-only devices (USB composite parent nodes) when sibling COL devices exist
                // This prevents double-counting the same physical device
                var filteredDevices = FilterMiOnlyDevices(RawInputDeviceInfoList);
                if (filteredDevices.Count != RawInputDeviceInfoList.Count)
                {
                    Debug.WriteLine($"RawInputDevice: Filtered out {RawInputDeviceInfoList.Count - filteredDevices.Count} MI-only transport nodes");
                    RawInputDeviceInfoList.Clear();
                    RawInputDeviceInfoList.AddRange(filteredDevices);
                }

                LogSummary(RawInputDeviceInfoList, stopwatch, deviceListDebugLines);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"RawInputDevice: Fatal error during RawInput device enumeration: {ex.Message}");
                Debug.WriteLine($"RawInputDevice: Stack trace: {ex.StackTrace}");
            }

            foreach (var debugLine in deviceListDebugLines) { Debug.WriteLine(debugLine); }
        }

        /// <summary>
        /// Disposes all RawInput devices in the provided list to free resources.
        /// Call this method when the device list is no longer needed.
        /// </summary>
        /// <param name="deviceList">List of RawInputDeviceInfo objects to dispose</param>
        public static void DisposeDeviceList(List<RawInputDeviceInfo> deviceList)
        {
            if (deviceList == null) return;

            Debug.WriteLine($"RawInputDevice: Disposing {deviceList.Count} RawInput devices...");

            foreach (var deviceInfo in deviceList)
            {
                try
                {
                    if (deviceInfo != null)
                    {
                        Debug.WriteLine($"RawInputDevice: Disposing device - {deviceInfo.InstanceName}");
                        deviceInfo.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"RawInputDevice: Error disposing device {deviceInfo?.InstanceName}: {ex.Message}");
                }
            }

            Debug.WriteLine("RawInputDevice: All RawInput devices disposed.");
        }

        #region Private Helper Methods
        /// <summary>
        /// Logs empty result when device enumeration fails or finds no devices.
        /// </summary>
        private void LogEmptyResult(List<string> debugLines, uint errorCode)
        {
        	string message = errorCode != 0
        		? $"RawInputDevice: Failed to enumerate devices. Error: {errorCode}"
        		: "RawInputDevice: No RawInput devices found";
        	
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
                if (GetHidCapabilities(rawDevice.hDevice, out int axes, out int sliders, out int buttons, out int povs,
                    out _, out _, out _, out _, out _, out _,
                    out _, out _, out IntPtr preparsedData, out _, out _, out _))
                {
                    // Free the preparsed data as we only needed it for the check
                    // (It will be re-acquired in ProcessInputDevice if accepted)
                    if (preparsedData != IntPtr.Zero)
                        Marshal.FreeHGlobal(preparsedData);

                    // Check if it has any relevant input controls
                    if (buttons > 0 || axes > 0 || povs > 0 || sliders > 0)
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
        /// Gets HID device capabilities by parsing the HID Report Descriptor.
        /// IMPORTANT: This method allocates preparsed data that must be stored in the device info
        /// and freed when the device is disposed.
        /// </summary>
        /// <param name="hDevice">Device handle</param>
        /// <param name="axeCount">Output: number of axes</param>
        /// <param name="buttonCount">Output: number of buttons</param>
        /// <param name="povCount">Output: number of POV hats</param>
        /// <param name="hasForceFeedback">Output: whether device supports force feedback</param>
        /// <param name="preparsedDataOut">Output: preparsed data pointer (must be freed by caller)</param>
        /// <returns>True if capabilities were successfully retrieved</returns>
        private bool GetHidCapabilities(IntPtr hDevice, out int axeCount, out int sliderCount, out int buttonCount, out int povCount,
            out int throttleCount, out int brakeCount, out int steeringCount, out int acceleratorCount, out int clutchCount,
            out bool hasForceFeedback, out bool usesReportIds, out int buttonDataOffset, out IntPtr preparsedDataOut,
            out List<int> povLogicalMins, out List<int> povLogicalMaxes, out Dictionary<int, AxisRange> axisProperties)
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
            preparsedDataOut = IntPtr.Zero;
            povLogicalMins = new List<int>();
            povLogicalMaxes = new List<int>();
            axisProperties = new Dictionary<int, AxisRange>();

            // Track unique usages to avoid double-counting (e.g. multi-touch devices reporting 10 X-axes)
            // RawInputState currently only supports reading unique usages
            var foundAxes = new HashSet<int>();
            var foundSliders = new HashSet<int>();
            var foundButtons = new HashSet<int>();
            var foundPovs = new HashSet<int>();

            IntPtr preparsedData = IntPtr.Zero;
            
            try
            {
                // Get the size of preparsed data
                uint size = 0;
                GetRawInputDeviceInfo(hDevice, RIDI_PREPARSEDDATA, IntPtr.Zero, ref size);
                
                if (size == 0)
                {
                    Debug.WriteLine($"RawInputDevice: No preparsed data available for device 0x{hDevice.ToInt64():X8}");
                    return false;
                }

                Debug.WriteLine($"RawInputDevice: Preparsed data size: {size} bytes for device 0x{hDevice.ToInt64():X8}");

                // Allocate buffer and get preparsed data
                preparsedData = Marshal.AllocHGlobal((int)size);
                uint result = GetRawInputDeviceInfo(hDevice, RIDI_PREPARSEDDATA, preparsedData, ref size);
                
                if (result == uint.MaxValue || result == 0)
                {
                    Debug.WriteLine($"RawInputDevice: Failed to get preparsed data for device 0x{hDevice.ToInt64():X8}, result: {result}");
                    return false;
                }

                Debug.WriteLine($"RawInputDevice: Successfully retrieved preparsed data for device 0x{hDevice.ToInt64():X8}");

                // Get HID capabilities
                HIDP_CAPS caps;
                int status = HidP_GetCaps(preparsedData, out caps);
                
                if (status != HIDP_STATUS_SUCCESS)
                {
                    Debug.WriteLine($"RawInputDevice: HidP_GetCaps failed with status 0x{status:X8} for device 0x{hDevice.ToInt64():X8}");
                    return false;
                }

                Debug.WriteLine($"RawInputDevice: HID Caps - InputButtonCaps: {caps.NumberInputButtonCaps}, InputValueCaps: {caps.NumberInputValueCaps}");

                // Parse input button capabilities and detect Report ID usage
                if (caps.NumberInputButtonCaps > 0)
                {
                    var buttonCaps = new HIDP_BUTTON_CAPS[caps.NumberInputButtonCaps];
                    ushort buttonCapsLength = caps.NumberInputButtonCaps;
                    status = HidP_GetButtonCaps(HIDP_REPORT_TYPE.HidP_Input, buttonCaps, ref buttonCapsLength, preparsedData);
                    
                    if (status == HIDP_STATUS_SUCCESS)
                    {
                        for (int i = 0; i < buttonCapsLength; i++)
                        {
                            var buttonCap = buttonCaps[i];
                            // Check if device uses Report IDs (non-zero ReportID indicates usage)
                            if (buttonCap.ReportID != 0)
                            {
                                usesReportIds = true;
                            }
                            
                            // Skip aliased buttons to avoid overcounting (IsAlias is only valid for NotRange)
                            if (!buttonCap.IsRange && buttonCap.IsAlias)
                            {
                                continue;
                            }

                            if (buttonCap.IsRange)
                            {
                                // Range of buttons
                                buttonCount += (buttonCap.Range.UsageMax - buttonCap.Range.UsageMin + 1);
                            }
                            else
                            {
                                // Single button
                                int key = (buttonCap.UsagePage << 16) | buttonCap.NotRange.Usage;
                                if (foundButtons.Add(key)) buttonCount++;
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
                        for (int i = 0; i < valueCapsLength; i++)
                        {
                            if (valueCaps[i].ReportID != 0)
                            {
                                usesReportIds = true;
                                break;
                            }
                        }
                        
                        for (int k = 0; k < valueCapsLength; k++)
                        {
                            var valueCap = valueCaps[k];

                            // Skip aliases
                            if (valueCap.IsAlias) continue;

                            // Skip 1-bit values (likely buttons defined as values)
                            // Axes, Sliders and POVs are multi-bit values
                            if (valueCap.BitSize < 2) continue;

                            // Debug: Log ALL value capabilities regardless of usage page
                            ushort usage = valueCap.IsRange ? valueCap.Range.UsageMin : valueCap.NotRange.Usage;
                            ushort usageMax = valueCap.IsRange ? valueCap.Range.UsageMax : usage;

                            // CRITICAL FIX: Capture LogicalMin/Max for axis scaling
                            // x360ce needs to know the raw range to correctly scale to 0-65535
                            // Store this in a temporary map or directly in the device info if possible
                            // For now, we'll log it and rely on RawInputState to handle the scaling if it gets the raw values
                            
                            if (valueCap.LogicalMax > 0)
                            {
                                Debug.WriteLine($"RawInputDevice: Value Range: {valueCap.LogicalMin} to {valueCap.LogicalMax}");
                            }
                            ushort linkUsage = valueCap.LinkUsage;
                            ushort linkUsagePage = valueCap.LinkUsagePage;
                            
                            if (valueCap.IsRange)
                            {
                                Debug.WriteLine($"RawInputDevice: HID Value Range - UsagePage: 0x{valueCap.UsagePage:X2}, UsageMin: 0x{usage:X2}, UsageMax: 0x{usageMax:X2}, " +
                                    $"LinkUsage: 0x{linkUsage:X2}, LinkUsagePage: 0x{linkUsagePage:X2}, ReportCount: {valueCap.ReportCount}, " +
                                    $"BitSize: {valueCap.BitSize}, LogicalMin: {valueCap.LogicalMin}, LogicalMax: {valueCap.LogicalMax}");
                            }
                            else
                            {
                                Debug.WriteLine($"RawInputDevice: HID Value - UsagePage: 0x{valueCap.UsagePage:X2}, Usage: 0x{usage:X2}, " +
                                    $"LinkUsage: 0x{linkUsage:X2}, LinkUsagePage: 0x{linkUsagePage:X2}, ReportCount: {valueCap.ReportCount}, " +
                                    $"BitSize: {valueCap.BitSize}, LogicalMin: {valueCap.LogicalMin}, LogicalMax: {valueCap.LogicalMax}");
                            }
                            
                            // Special handling for devices with invalid UsagePage (0x00) but valid LinkUsage
                            // Some devices (like T.16000M) report axes/sliders in LinkUsage when UsagePage is 0
                            // Note: Some devices put the usage code in BOTH LinkUsage and LinkUsagePage fields
                            if (valueCap.UsagePage == 0x00 && linkUsage >= 0x30 && linkUsage <= 0x39 &&
                                (linkUsagePage == HID_USAGE_PAGE_GENERIC || linkUsagePage == linkUsage))
                            {
                                int reportCount = Math.Max(1, (int)valueCap.ReportCount);
                                Debug.WriteLine($"RawInputDevice: Found control with invalid UsagePage but valid LinkUsage: 0x{linkUsage:X2}, ReportCount: {valueCap.ReportCount}");
                                
                                // Standard axes: X(0x30), Y(0x31), Z(0x32), Rx(0x33), Ry(0x34), Rz(0x35)
                                if (linkUsage >= 0x30 && linkUsage <= 0x35)
                                {
                                    axeCount += reportCount;
                                    Debug.WriteLine($"RawInputDevice: Found {reportCount} axis/axes at LinkUsage 0x{linkUsage:X2}");
                                }
                                // Sliders: Slider(0x36), Dial(0x37), Wheel(0x38)
                                else if (linkUsage >= 0x36 && linkUsage <= 0x38)
                                {
                                    sliderCount += reportCount;
                                    Debug.WriteLine($"RawInputDevice: Found {reportCount} slider(s) at LinkUsage 0x{linkUsage:X2}");
                                }
                                // POV Hat Switch (0x39)
                                else if (linkUsage == 0x39)
                                {
                                    povCount += reportCount;
                                    Debug.WriteLine($"RawInputDevice: Found {reportCount} POV(s) at LinkUsage 0x{linkUsage:X2}");
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
                                    int reportCount = Math.Max(1, (int)valueCap.ReportCount);
                                    Debug.WriteLine($"RawInputDevice: Found nested control in Pointer collection - LinkUsage: 0x{linkUsage:X2}, ReportCount: {valueCap.ReportCount}");
                                    
                                    // Standard axes: X(0x30), Y(0x31), Z(0x32), Rx(0x33), Ry(0x34), Rz(0x35)
                                    if (linkUsage >= 0x30 && linkUsage <= 0x35)
                                    {
                                        axeCount += reportCount;
                                        Debug.WriteLine($"RawInputDevice: Found {reportCount} axis/axes (nested) at LinkUsage 0x{linkUsage:X2}");
                                    }
                                    // Sliders: Slider(0x36), Dial(0x37), Wheel(0x38)
                                    else if (linkUsage >= 0x36 && linkUsage <= 0x38)
                                    {
                                        sliderCount += reportCount;
                                        Debug.WriteLine($"RawInputDevice: Found {reportCount} slider(s) (nested) at LinkUsage 0x{linkUsage:X2}");
                                    }
                                    // POV Hat Switch (0x39)
                                    else if (linkUsage == 0x39)
                                    {
                                        povCount += reportCount;
                                        Debug.WriteLine($"RawInputDevice: Found {reportCount} POV(s) (nested) at LinkUsage 0x{linkUsage:X2}");
                                    }
                                    continue; // Skip normal processing for Pointer collections
                                }
                                else
                                {
                                    // Pointer without specific LinkUsage - might indicate a collection of multiple axes
                                    // For Xbox One controllers, the Pointer itself might represent multiple axes
                                    // We'll handle this in the normal processing below, but log it for debugging
                                    Debug.WriteLine($"RawInputDevice: Found Pointer collection without specific LinkUsage - ReportCount: {valueCap.ReportCount}, LinkUsage: 0x{linkUsage:X2}, LinkUsagePage: 0x{linkUsagePage:X2}");
                                }
                            }

                            // General Processing for Axes, Sliders, POVs, and Simulation Controls
                            // Correctly handles Arrays, Ranges, and Multi-Report items
                            int itemCount = Math.Max(1, (int)valueCap.ReportCount);
                            int rangeMin = valueCap.IsRange ? valueCap.Range.UsageMin : valueCap.NotRange.Usage;
                            int rangeMax = valueCap.IsRange ? valueCap.Range.UsageMax : valueCap.NotRange.Usage;
                            int rangeSize = rangeMax - rangeMin + 1;

                            // Capture LogicalMin/Max for axis scaling
                            // x360ce needs to know the raw range to correctly scale to 0-65535
                            // Store this in the axis properties map
                            // Loop below handles storing it for each specific usage

                            int axesFound = 0;
                            int slidersFound = 0;
                            int povsFound = 0;
                            int throttleFound = 0;
                            int brakeFound = 0;
                            int steeringFound = 0;
                            int accelFound = 0;
                            int clutchFound = 0;

                            for (int i = 0; i < itemCount; i++)
                            {
                                // Determine usage for this item
                                // If rangeSize < itemCount, usually the last usage repeats (common for arrays or multi-axis inputs)
                                int u = (i < rangeSize) ? (rangeMin + i) : rangeMax;

                                // Store axis properties (Min/Max) for this usage
                                int key = (valueCap.UsagePage << 16) | u;
                                if (!axisProperties.ContainsKey(key))
                                {
                                    // Use PhysicalMin/Max if available and different from LogicalMin/Max
                                    // Some devices report 0-255 Logical but represent -32768 to 32767 Physical
                                    // However, RawInput usually returns normalized logical values?
                                    // Actually, RawInput returns the raw value from the report.
                                    // If the Logical Min/Max are small (e.g. 0-255) but Physical are large,
                                    // we should trust Logical Min/Max for the raw data range.
                                    
                                    // Issue: If Min == Max, range is 0, which causes divide by zero in conversion.
                                    // Ensure valid range.
                                    if (valueCap.LogicalMax > valueCap.LogicalMin)
                                    {
                                        axisProperties[key] = new AxisRange(valueCap.LogicalMin, valueCap.LogicalMax);
                                    }
                                }

                                if (valueCap.UsagePage == HID_USAGE_PAGE_GENERIC)
                                {
                                    // Standard axes: X(0x30)-Rz(0x35)
                                    if (u >= 0x30 && u <= 0x35)
                                    {
                                        int itemKey = (HID_USAGE_PAGE_GENERIC << 16) | u;
                                        if (foundAxes.Add(itemKey)) axesFound++;
                                    }
                                    // Sliders: Slider(0x36)-Wheel(0x38)
                                    else if (u >= 0x36 && u <= 0x38)
                                    {
                                        int itemKey = (HID_USAGE_PAGE_GENERIC << 16) | u;
                                        if (foundSliders.Add(itemKey)) slidersFound++;
                                    }
                                    // POV Hat Switch (0x39)
                                    else if (u == 0x39)
                                    {
                                        int itemKey = (HID_USAGE_PAGE_GENERIC << 16) | u;
                                        if (foundPovs.Add(itemKey))
                                        {
                                            povsFound++;
                                            povLogicalMins.Add(valueCap.LogicalMin);
                                            povLogicalMaxes.Add(valueCap.LogicalMax);
                                        }
                                    }
                                }
                                else if (valueCap.UsagePage == HID_USAGE_PAGE_DIGITIZER)
                                {
                                    // Digitizer Controls
                                    // Tip Pressure (0x30), Barrel Pressure (0x31)
                                    // Tilt X (0x3D), Tilt Y (0x3E)
                                    if (u == 0x30 || u == 0x31 || u == 0x3D || u == 0x3E)
                                    {
                                        int itemKey = (HID_USAGE_PAGE_DIGITIZER << 16) | u;
                                        if (foundAxes.Add(itemKey)) axesFound++;
                                    }
                                }
                                else if (valueCap.UsagePage == HID_USAGE_PAGE_SIMULATION)
                                {
                                    // Note: These usages may be specific to certain legacy controllers or custom mappings.
                                    // Standard HID Usage Page 0x02 (Simulation Controls) defines:
                                    // 0xB0: Steering, 0xBA: Rudder, 0xBB: Throttle
                                    // 0xC4: Accelerator, 0xC5: Brake, 0xC6: Clutch
                                    if (u == 0xBA) throttleFound++;
                                    else if (u == 0xBB) accelFound++;
                                    else if (u == 0xBC) brakeFound++;
                                    else if (u == 0xBD) clutchFound++;
                                    else if (u == 0xB0) steeringFound++;
                                }
                            }

                            if (axesFound > 0)
                            {
                                axeCount += axesFound;
                                Debug.WriteLine($"RawInputDevice: Found {axesFound} axes in capability (ReportCount: {valueCap.ReportCount})");
                            }
                            if (slidersFound > 0)
                            {
                                sliderCount += slidersFound;
                                Debug.WriteLine($"RawInputDevice: Found {slidersFound} sliders in capability (ReportCount: {valueCap.ReportCount})");
                            }
                            if (povsFound > 0)
                            {
                                povCount += povsFound;
                                Debug.WriteLine($"RawInputDevice: Found {povsFound} POVs in capability (ReportCount: {valueCap.ReportCount})");
                            }
                            
                            // Simulation controls
                            if (throttleFound > 0) throttleCount += throttleFound;
                            if (accelFound > 0) acceleratorCount += accelFound;
                            if (brakeFound > 0) brakeCount += brakeFound;
                            if (clutchFound > 0) clutchCount += clutchFound;
                            if (steeringFound > 0) steeringCount += steeringFound;

                            if (throttleFound > 0 || accelFound > 0 || brakeFound > 0 || clutchFound > 0 || steeringFound > 0)
                            {
                                Debug.WriteLine($"RawInputDevice: Found Sim Controls - Throttle: {throttleFound}, Accel: {accelFound}, Brake: {brakeFound}, Clutch: {clutchFound}, Steering: {steeringFound}");
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
                            for (int i = 0; i < outputValueCapsLength; i++)
                            {
                                var valueCap = outputValueCaps[i];
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
                            for (int i = 0; i < valueCapsLength; i++)
                            {
                                var valueCap = valueCaps[i];

                                // Skip aliases
                                if (valueCap.IsAlias) continue;

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
                            Debug.WriteLine($"RawInputDevice: Estimated {axeCount} axes for joystick with Pointer→Joystick collection and {sliderCount} slider(s)");
                        }
                        
                        // Flight sticks typically have 1 POV hat switch on top
                        if (povCount == 0)
                        {
                            povCount = 1;
                            Debug.WriteLine($"RawInputDevice: Estimated {povCount} POV for joystick with Pointer→Joystick collection");
                        }
                    }
                }
                
                // If we got very few axes from HID parsing (less than expected for a gamepad),
                // try to analyze the actual input report structure to get accurate counts
                if (axeCount < 4 && caps.InputReportByteLength > 0)
                {
                    Debug.WriteLine($"RawInputDevice: Low axis count ({axeCount}), attempting to analyze input report structure...");
                    
                    // Try to get more accurate counts by analyzing what values we can actually read
                    int reportAxes = 0, reportSliders = 0, reportButtons = 0, reportPovs = 0;
                    if (AnalyzeInputReportStructure(preparsedData, caps, out reportAxes, out reportSliders, out reportButtons, out reportPovs))
                    {
                        Debug.WriteLine($"RawInputDevice: Input report analysis found - Axes: {reportAxes}, Sliders: {reportSliders}, Buttons: {reportButtons}, POVs: {reportPovs}");
                        
                        // Use the higher count (report analysis is usually more accurate for complex devices)
                        if (reportAxes > axeCount)
                        {
                            Debug.WriteLine($"RawInputDevice: Using report-analyzed axis count ({reportAxes}) instead of HID-parsed count ({axeCount})");
                            axeCount = reportAxes;
                        }
                        if (reportSliders > sliderCount)
                        {
                            Debug.WriteLine($"RawInputDevice: Using report-analyzed slider count ({reportSliders}) instead of HID-parsed count ({sliderCount})");
                            sliderCount = reportSliders;
                        }
                        if (reportButtons > buttonCount)
                        {
                            Debug.WriteLine($"RawInputDevice: Using report-analyzed button count ({reportButtons}) instead of HID-parsed count ({buttonCount})");
                            buttonCount = reportButtons;
                        }
                        if (reportPovs > povCount)
                        {
                            Debug.WriteLine($"RawInputDevice: Using report-analyzed POV count ({reportPovs}) instead of HID-parsed count ({povCount})");
                            povCount = reportPovs;
                        }
                    }
                }
                
                // Calculate button data offset by analyzing the HID Report Descriptor structure
                // Button data comes AFTER: Report ID (if present) + all axis/value data
                buttonDataOffset = CalculateButtonDataOffset(caps, usesReportIds, preparsedData);
                
                Debug.WriteLine($"RawInputDevice: Parsed HID capabilities for device 0x{hDevice.ToInt64():X8} - " +
                    $"Axes: {axeCount}, Sliders: {sliderCount}, Buttons: {buttonCount}, POVs: {povCount}, " +
                    $"Throttles: {throttleCount}, Brakes: {brakeCount}, Steering: {steeringCount}, " +
                    $"Accelerators: {acceleratorCount}, Clutches: {clutchCount}, ForceFeedback: {hasForceFeedback}, " +
                    $"UsesReportIds: {usesReportIds}, ButtonDataOffset: {buttonDataOffset}");
                
                // IMPORTANT: Return preparsed data to caller instead of freeing it
                // The caller must store it in RawInputDeviceInfo.PreparsedData and free it on disposal
                preparsedDataOut = preparsedData;
                preparsedData = IntPtr.Zero; // Prevent cleanup in finally block
                
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"RawInputDevice: Error parsing HID capabilities for device 0x{hDevice.ToInt64():X8}: {ex.Message}");
                return false;
            }
            finally
            {
                // Only free if we didn't return it to caller (error case)
                if (preparsedData != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(preparsedData);
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
   		Debug.WriteLine($"RawInputDevice: No value caps, button offset = {baseOffset}");
   		return baseOffset;
   	}
   	
   	// Get all value capabilities (axes, POVs, sliders)
   	var valueCaps = new HIDP_VALUE_CAPS[caps.NumberInputValueCaps];
   	ushort valueCapsLength = caps.NumberInputValueCaps;
   	int status = HidP_GetValueCaps(HIDP_REPORT_TYPE.HidP_Input, valueCaps, ref valueCapsLength, preparsedData);
   	
   	if (status != HIDP_STATUS_SUCCESS)
   	{
   		Debug.WriteLine($"RawInputDevice: HidP_GetValueCaps failed with status 0x{status:X8}");
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
   		
   		Debug.WriteLine($"RawInputDevice: Value cap #{valueCapsProcessed} - " +
   			$"Usage: 0x{(valueCap.IsRange ? valueCap.Range.UsageMin : valueCap.NotRange.Usage):X2}, " +
   			$"UsagePage: 0x{valueCap.UsagePage:X2}, BitSize: {valueCap.BitSize}, " +
   			$"ReportCount: {reportCount}, " +
   			$"BitsForThis: {bitsForThisCap}, TotalBits: {totalValueBits}");
   	}
   	
   	// Convert total value bits to bytes (round up to next byte boundary)
   	int valueBytes = (totalValueBits + 7) / 8;
   	
   	// Button offset = Report ID + All axis/value bytes
   	// This is ALWAYS correct because buttons come after axes in HID reports
   	int buttonByteOffset = baseOffset + valueBytes;
   	
   	Debug.WriteLine($"RawInputDevice: FINAL CALCULATION - " +
   		$"ReportID: {baseOffset} byte(s), " +
   		$"ValueCaps: {valueCapsProcessed}, " +
   		$"TotalValueBits: {totalValueBits}, " +
   		$"ValueBytes: {valueBytes}, " +
   		$"ButtonOffset: {buttonByteOffset}, " +
   		$"ReportLength: {caps.InputReportByteLength}");
   	
   	// Sanity check: offset must be within report bounds
   	if (buttonByteOffset < baseOffset || buttonByteOffset >= caps.InputReportByteLength)
   	{
   		Debug.WriteLine($"RawInputDevice: ERROR - Calculated offset {buttonByteOffset} out of bounds [{ baseOffset}..{caps.InputReportByteLength}]");
   		return baseOffset;
   	}
   	
   	return buttonByteOffset;
   }
   catch (Exception ex)
   {
    Debug.WriteLine($"RawInputDevice: Error calculating button offset: {ex.Message}");
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
            
            try
            {
                // Try to read all possible axis usages (X, Y, Z, Rx, Ry, Rz, Slider, Dial, Wheel, POV)
                ushort[] checkUsages = { 0x30, 0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37, 0x38 };
                ushort povUsage = 0x39;
                
                // Create a dummy input report buffer
                byte[] reportBuffer = new byte[caps.InputReportByteLength];
                
                // Try to get value for each possible usage
                foreach (var usage in checkUsages)
                {
                    IntPtr reportPtr = Marshal.AllocHGlobal(reportBuffer.Length);
                    try
                    {
                        Marshal.Copy(reportBuffer, 0, reportPtr, reportBuffer.Length);
                        
                        // Try to get the usage value - if it succeeds, the control exists
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
                            if (usage >= 0x30 && usage <= 0x35)
                            {
                                axeCount++;
                                Debug.WriteLine($"RawInputDevice: Found axis at usage 0x{usage:X2} via report analysis");
                            }
                            else if (usage >= 0x36 && usage <= 0x38)
                            {
                                sliderCount++;
                                Debug.WriteLine($"RawInputDevice: Found slider at usage 0x{usage:X2} via report analysis");
                            }
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
                        Debug.WriteLine($"RawInputDevice: Found POV at usage 0x{povUsage:X2} via report analysis");
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(povReportPtr);
                }
                
                // For buttons, use the button caps we already have
                // The button count from HID parsing is usually accurate
                buttonCount = 0; // Will be filled by caller if needed
                
                return axeCount > 0 || sliderCount > 0 || povCount > 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"RawInputDevice: Error analyzing input report structure: {ex.Message}");
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
                    // Enforce minimum 5 buttons for mouse (Left, Right, Middle, X1, X2) to ensure list capacity
                    // Some trackpads report 0 or 2 buttons but still emit events for others
                    deviceInfo.ButtonCount = Math.Max(5, (int)mouse.dwNumberOfButtons);
                    deviceInfo.ProductName = "Mouse";
                    
                    // Debug: Log horizontal wheel detection details
                    Debug.WriteLine($"RawInputDevice: Mouse device 0x{deviceInfo.DeviceHandle.ToInt64():X8} - " +
                        $"Buttons: {mouse.dwNumberOfButtons}, " +
                        $"SampleRate: {mouse.dwSampleRate}, " +
                        $"HasHorizontalWheel: {mouse.fHasHorizontalWheel}");
                    
                    // ENHANCEMENT: Set to 4 axes if horizontal wheel is supported.
                    // Most modern mice support horizontal wheels even if not reported by Windows.
                    deviceInfo.AxeCount = mouse.fHasHorizontalWheel ? 4 : 3; // X, Y, Z (vertical wheel), [W (horizontal wheel)]

                    Debug.WriteLine($"RawInputDevice: Mouse axes set to {deviceInfo.AxeCount} (HorizontalWheel: {mouse.fHasHorizontalWheel})");
                    Debug.WriteLine($"RawInputDevice: Mouse enumeration complete - Handle: 0x{deviceInfo.DeviceHandle.ToInt64():X8}, " +
                        $"Path: {deviceInfo.InterfacePath}, Buttons: {deviceInfo.ButtonCount}, Axes: {deviceInfo.AxeCount}");
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
            catch (Exception ex)
            {
                Debug.WriteLine($"RawInputDevice: Error extracting VID/PID from path '{interfacePath}': {ex.Message}");
            }

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
                    Debug.WriteLine($"RawInputDevice: Skipping virtual/converted device: {deviceName}");
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
                        int axes, sliders, buttons, povs, throttles, brakes, steering, accelerators, clutches;
                        bool forceFeedback, usesReportIds;
                        int buttonDataOffset;
                        IntPtr preparsedData;
                        List<int> povMins;
                        List<int> povMaxes;
                        Dictionary<int, AxisRange> axisProperties;
                        if (GetHidCapabilities(rawDevice.hDevice, out axes, out sliders, out buttons, out povs,
                            out throttles, out brakes, out steering, out accelerators, out clutches, out forceFeedback,
                            out usesReportIds, out buttonDataOffset, out preparsedData, out povMins, out povMaxes, out axisProperties))
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
                            deviceInfo.PovLogicalMins = povMins;
                            deviceInfo.PovLogicalMaxes = povMaxes;
                            deviceInfo.DeviceAxisProperties = axisProperties;
                            // CRITICAL: Store preparsed data for later use by HidP_GetUsages
                            // This will be freed when the device is disposed
                            deviceInfo.PreparsedData = preparsedData;
    
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
                        sb.Append($"Buttons (Keys): {deviceInfo.ButtonCount}");
                    }
                    else // Mouse
                    {
                        sb.Append($"Axes: {deviceInfo.AxeCount}, Buttons: {deviceInfo.ButtonCount}");
                    }
                    deviceListDebugLines.Add(sb.ToString());
                }

                // Filter out devices with no physical inputs (HID only)
                // This ensures we don't add devices that claimed to be inputs but have no actual controls
                if (deviceInfo.RawInputDeviceType == RawInputDeviceType.HID &&
                    deviceInfo.AxeCount == 0 && deviceInfo.ButtonCount == 0 &&
                    deviceInfo.SliderCount == 0 && deviceInfo.PovCount == 0 &&
                    deviceInfo.ThrottleCount == 0 && deviceInfo.BrakeCount == 0 &&
                    deviceInfo.SteeringCount == 0 && deviceInfo.AcceleratorCount == 0 &&
                    deviceInfo.ClutchCount == 0)
                {
                    Debug.WriteLine($"RawInputDevice: Skipping HID device with no detected physical inputs: {deviceName}");
                    deviceInfo.Dispose();
                    return;
                }

                // Add device to the final list (already filtered as input device)
                deviceList.Add(deviceInfo);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"RawInputDevice: Error processing input device 0x{rawDevice.hDevice.ToInt64():X8}: {ex.Message}");
            }
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
                            Debug.WriteLine($"RawInputDevice: Filtering out HID-type MI-only transport node (sibling COL exists): {path}");
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
            catch (Exception ex)
            {
                Debug.WriteLine($"RawInputDevice: Error getting registry name for {interfacePath}: {ex.Message}");
            }

            return null;
        }

        #endregion
    }
}
