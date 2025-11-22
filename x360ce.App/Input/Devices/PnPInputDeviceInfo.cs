
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace x360ce.App.Input.Devices
{
    /// <summary>
    /// Physical input device container with comprehensive device information from Windows Plug and Play Manager.
    /// Contains detailed device metadata from the Windows Device Manager and Setup API.
    /// </summary>
    public class PnPInputDeviceInfo : IDisposable
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
        public int PovCount { get; set; }
        public bool HasForceFeedback { get; set; }
        public int DriverVersion { get; set; }
        public int HardwareRevision { get; set; }
        public int FirmwareRevision { get; set; }
        public bool IsOnline { get; set; }
        public bool IsEnabled { get; set; }
        public bool AssignedToPad1 { get; set; }
        public bool AssignedToPad2 { get; set; }
        public bool AssignedToPad3 { get; set; }
        public bool AssignedToPad4 { get; set; }
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
        
        // Physical device-specific properties
        public string DeviceInstanceId { get; set; }
        public string FriendlyName { get; set; }
        public string Manufacturer { get; set; }
        public string DeviceDescription { get; set; }
        public string DriverProvider { get; set; }
        public string DriverDate { get; set; }
        public string DriverVersionString { get; set; }
        public string LocationInformation { get; set; }
        public string PhysicalDeviceObjectName { get; set; }
        public uint DeviceStatus { get; set; }
        public uint ProblemCode { get; set; }
        public bool IsPresent { get; set; }
        public bool IsStarted { get; set; }
        
        // Sorting and indentation properties
        public string SortingString { get; set; }
        public string MiValue { get; set; }
        public string ColValue { get; set; }
        
        /// <summary>
        /// Display name combining friendly name and device ID for easy identification.
        /// </summary>
        public string DisplayName => !string.IsNullOrEmpty(FriendlyName) ? FriendlyName : InstanceName;
        
        /// <summary>
        /// VID/PID string in standard format for hardware identification.
        /// </summary>
        public string VidPidString => $"VID_{VendorId:X4}&PID_{ProductId:X4}";
        
        /// <summary>
        /// Device status description for troubleshooting.
        /// </summary>
        public string StatusDescription => GetDeviceStatusDescription(DeviceStatus, ProblemCode);
        
        /// <summary>
        /// Dispose the physical device info when no longer needed.
        /// </summary>
        public void Dispose()
        {
            // Physical device info doesn't need explicit disposal
        }
        
        /// <summary>
        /// Gets a human-readable description of the device status and any problems.
        /// </summary>
        /// <param name="status">Device status flags</param>
        /// <param name="problemCode">Problem code if device has issues</param>
        /// <returns>Human-readable status description</returns>
        private string GetDeviceStatusDescription(uint status, uint problemCode)
        {
            if (status == 0x00000000) // DN_ROOT_ENUMERATED | DN_DRIVER_LOADED | DN_ENUM_LOADED | DN_STARTED
                return "Working normally";
            
            if ((status & 0x00000400) != 0) // DN_HAS_PROBLEM
            {
                switch (problemCode)
                {
                    case 0x00000001: return "Problem: Device not configured correctly";
                    case 0x00000003: return "Problem: Driver corrupted";
                    case 0x0000000A: return "Problem: Device cannot start";
                    case 0x0000000C: return "Problem: Device cannot find enough free resources";
                    case 0x00000012: return "Problem: Device disabled";
                    case 0x00000016: return "Problem: Device not present";
                    case 0x00000018: return "Problem: Device needs to be reinstalled";
                    case 0x0000001C: return "Problem: Device drivers not installed";
                    default: return $"Problem: Unknown issue (Code: {problemCode:X8})";
                }
            }
            
            if ((status & 0x00000002) != 0) // DN_DRIVER_LOADED
                return "Driver loaded";
            if ((status & 0x00000008) != 0) // DN_STARTED
                return "Started";
            if ((status & 0x00000200) != 0) // DN_DISABLEABLE
                return "Can be disabled";
            
            return $"Status: {status:X8}";
        }
    }

    /// <summary>
    /// Physical input device enumeration and management class.
    /// Self-contained implementation using Windows Setup API and Device Manager.
    /// Provides functionality to discover and list physical plug and play input devices.
    /// Enumerates devices through Windows Device Manager for comprehensive hardware information.
    /// </summary>
    internal class PnPInputDevice
    {
        #region Win32 API Constants and Structures

        // Device registry property constants
        private const uint SPDRP_DEVICEDESC = 0x00000000;
        private const uint SPDRP_HARDWAREID = 0x00000001;
        private const uint SPDRP_MFG = 0x0000000B;
        private const uint SPDRP_FRIENDLYNAME = 0x0000000C;
        private const uint SPDRP_LOCATION_INFORMATION = 0x0000000D;
        private const uint SPDRP_PHYSICAL_DEVICE_OBJECT_NAME = 0x0000000E;

        // Device information flags
        private const uint DIGCF_PRESENT = 0x00000002;

        // Device status flags
        private const uint DN_STARTED = 0x00000008;
        private const uint DN_HAS_PROBLEM = 0x00000400;

        // Error codes
        private const uint ERROR_NO_MORE_ITEMS = 259;

        // Known input device class GUIDs
        private static readonly Guid GUID_DEVCLASS_HIDCLASS = new Guid("745a17a0-74d3-11d0-b6fe-00a0c90f57da");
        private static readonly Guid GUID_DEVCLASS_KEYBOARD = new Guid("4d36e96b-e325-11ce-bfc1-08002be10318");
        private static readonly Guid GUID_DEVCLASS_MOUSE = new Guid("4d36e96f-e325-11ce-bfc1-08002be10318");

        /// <summary>
        /// Device information structure.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct SP_DEVINFO_DATA
        {
            public uint cbSize;
            public Guid ClassGuid;
            public uint DevInst;
            public IntPtr Reserved;
        }

        /// <summary>
        /// Device interface data structure.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct SP_DEVICE_INTERFACE_DATA
        {
            public uint cbSize;
            public Guid InterfaceClassGuid;
            public uint Flags;
            public IntPtr Reserved;
        }

        #endregion

        #region Win32 API Imports

        /// <summary>
        /// Creates a device information set.
        /// </summary>
        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr SetupDiGetClassDevs(
            ref Guid ClassGuid,
            string Enumerator,
            IntPtr hwndParent,
            uint Flags);

        /// <summary>
        /// Creates a device information set for all device classes.
        /// </summary>
        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr SetupDiGetClassDevs(
            IntPtr ClassGuid,
            string Enumerator,
            IntPtr hwndParent,
            uint Flags);

        /// <summary>
        /// Enumerates device information.
        /// </summary>
        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiEnumDeviceInfo(
            IntPtr DeviceInfoSet,
            uint MemberIndex,
            ref SP_DEVINFO_DATA DeviceInfoData);

        /// <summary>
        /// Gets device registry property.
        /// </summary>
        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool SetupDiGetDeviceRegistryProperty(
            IntPtr DeviceInfoSet,
            ref SP_DEVINFO_DATA DeviceInfoData,
            uint Property,
            out uint PropertyRegDataType,
            byte[] PropertyBuffer,
            uint PropertyBufferSize,
            out uint RequiredSize);

        /// <summary>
        /// Gets device instance ID.
        /// </summary>
        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool SetupDiGetDeviceInstanceId(
            IntPtr DeviceInfoSet,
            ref SP_DEVINFO_DATA DeviceInfoData,
            StringBuilder DeviceInstanceId,
            uint DeviceInstanceIdSize,
            out uint RequiredSize);

        /// <summary>
        /// Destroys device information set.
        /// </summary>
        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiDestroyDeviceInfoList(IntPtr DeviceInfoSet);

        /// <summary>
        /// Gets device status and problem code.
        /// </summary>
        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool CM_Get_DevNode_Status(
            out uint Status,
            out uint ProblemNumber,
            uint DevInst,
            uint Flags);

        /// <summary>
        /// Gets the last Win32 error code.
        /// </summary>
        [DllImport("kernel32.dll")]
        private static extern uint GetLastError();

        #endregion

        /// <summary>
        /// Creates a public list of PnP input devices with comprehensive device information and logs their properties.
        /// This method enumerates all physical plug and play input devices through Windows Device Manager.
        /// </summary>
        /// <returns>List of PnPInputDeviceInfo objects containing comprehensive device information</returns>
        /// <remarks>
        /// This method performs comprehensive PnP input device enumeration:
        /// • Discovers all physical plug and play input devices through Windows Setup API
        /// • Creates PnPInputDeviceInfo objects with comprehensive device information
        /// • Logs detailed device properties using Debug.WriteLine for diagnostics
        /// • Filters devices to only include input devices (HID, keyboards, mice, game controllers)
        /// • Provides device status, driver information, and hardware identification
        /// • Excludes non-input devices like sound cards, network adapters, storage devices
        /// • Is self-contained with minimal external dependencies
        ///
        /// IMPORTANT: The returned PnPInputDeviceInfo objects contain Windows device information.
        /// Call Dispose() on each PnPInputDeviceInfo when no longer needed to free resources.
        /// </remarks>
        public List<PnPInputDeviceInfo> GetPnPInputDeviceInfoList()
        {
            var stopwatch = Stopwatch.StartNew();
            var deviceList = new List<PnPInputDeviceInfo>();
            var deviceListDebugLines = new List<string>();
            int deviceListIndex = 0;

            try
            {
                Debug.WriteLine("\n-----------------------------------------------------------------------------------------------------------------\n\n" +
                    "PnPInputDevice: Starting PnP input device enumeration...");

                // Only enumerate specific input device classes - be very restrictive
                var inputClassGuids = new[]
                {
                    GUID_DEVCLASS_KEYBOARD,  // Keyboards only
                    GUID_DEVCLASS_MOUSE,     // Mice and pointing devices only
                    GUID_DEVCLASS_HIDCLASS   // HID devices (but will be heavily filtered)
                };

                foreach (var classGuid in inputClassGuids)
                {
                    Debug.WriteLine($"PnPInputDevice: Enumerating devices for class {classGuid}");
                    EnumerateDeviceClass(classGuid, ref deviceListIndex, deviceList, deviceListDebugLines);
                }

                // Do NOT enumerate all present devices - too many false positives
                // Only rely on specific input device classes with strict filtering

                // Remove duplicates based on DeviceInstanceId
                var uniqueDevices = deviceList
                    .GroupBy(d => d.DeviceInstanceId)
                    .Select(g => g.First())
                    .ToList();

                if (uniqueDevices.Count != deviceList.Count)
                {
                    Debug.WriteLine($"PnPInputDevice: Removed {deviceList.Count - uniqueDevices.Count} duplicate devices");
                    deviceList = uniqueDevices;
                }

                // Filter out MI-only devices (USB composite parent nodes) when sibling COL devices exist
                // This prevents double-counting the same physical device
                var filteredDevices = FilterMiOnlyDevices(deviceList);
                if (filteredDevices.Count != deviceList.Count)
                {
                    Debug.WriteLine($"PnPInputDevice: Filtered out {deviceList.Count - filteredDevices.Count} MI-only transport nodes");
                    deviceList = filteredDevices;
                }

                // Generate SortingString for each device
                foreach (var device in deviceList)
                {
                    GenerateSortingString(device);
                }

                // Order devices by SortingString (VID_PID_MI_COL format)
                deviceList = deviceList
                    .OrderBy(d => d.SortingString)                            // Primary sort by VID_PID_MI_COL
                    .ThenBy(d => d.FriendlyName ?? d.DeviceDescription ?? "") // Then by name for consistent ordering
                    .ToList();

                // Log devices with hierarchical display
                LogHierarchicalDeviceList(deviceList, deviceListDebugLines);

                // Generate summary statistics for device enumeration results
                var hidCount = deviceList.Count(d => d.ClassGuid == GUID_DEVCLASS_HIDCLASS);
                var keyboardCount = deviceList.Count(d => d.ClassGuid == GUID_DEVCLASS_KEYBOARD);
                var mouseCount = deviceList.Count(d => d.ClassGuid == GUID_DEVCLASS_MOUSE);
                var gamepadCount = deviceList.Count(d => IsGamepadDevice(d.HardwareIds, d.DeviceDescription));
                var presentCount = deviceList.Count(d => d.IsPresent);
                var enabledCount = deviceList.Count(d => d.IsStarted);
                var problemCount = deviceList.Count(d => d.ProblemCode != 0);

                stopwatch.Stop();

                deviceListDebugLines.Add($"\nDevicesPnPInput: ({(int)Math.Round(stopwatch.Elapsed.TotalMilliseconds)} ms) " +
                    $"PnP Input Devices found: {deviceList.Count}, " +
                    $"HID: {hidCount}, " +
                    $"Gamepads: {gamepadCount}, " +
                    $"Keyboards: {keyboardCount}, " +
                    $"Mice: {mouseCount}, " +
                    $"Present: {presentCount}, " +
                    $"Enabled: {enabledCount}, " +
                    $"With Problems: {problemCount}\n");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PnPInputDevice: Fatal error during PnP input device enumeration: {ex.Message}");
                Debug.WriteLine($"PnPInputDevice: Stack trace: {ex.StackTrace}");
            }

            foreach (var debugLine in deviceListDebugLines) { Debug.WriteLine(debugLine); }

            return deviceList;
        }

        /// <summary>
        /// Disposes all PnP input devices in the provided list to free resources.
        /// Call this method when the device list is no longer needed.
        /// </summary>
        /// <param name="deviceList">List of PnPInputDeviceInfo objects to dispose</param>
        public static void DisposeDeviceList(List<PnPInputDeviceInfo> deviceList)
        {
            if (deviceList == null) return;

            Debug.WriteLine($"PnPInputDevice: Disposing {deviceList.Count} PnP input devices...");

            foreach (var deviceInfo in deviceList)
            {
                try
                {
                    if (deviceInfo != null)
                    {
                        Debug.WriteLine($"PnPInputDevice: Disposing device - {deviceInfo.DisplayName}");
                        deviceInfo.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"PnPInputDevice: Error disposing device {deviceInfo?.DisplayName}: {ex.Message}");
                }
            }

            Debug.WriteLine("PnPInputDevice: All PnP input devices disposed.");
        }

        #region Private Helper Methods

        /// <summary>
        /// Enumerates devices for a specific device class.
        /// </summary>
        /// <param name="classGuid">Device class GUID</param>
        /// <param name="deviceListIndex">Current device index (will be incremented)</param>
        /// <param name="deviceList">List to add devices to</param>
        /// <param name="deviceListDebugLines">Debug lines list</param>
        private void EnumerateDeviceClass(Guid classGuid, ref int deviceListIndex,
            List<PnPInputDeviceInfo> deviceList, List<string> deviceListDebugLines)
        {
            IntPtr deviceInfoSet = IntPtr.Zero;

            try
            {
                // Get device information set for the specified class
                deviceInfoSet = SetupDiGetClassDevs(ref classGuid, null, IntPtr.Zero, DIGCF_PRESENT);

                if (deviceInfoSet == IntPtr.Zero || deviceInfoSet.ToInt64() == -1)
                {
                    uint error = GetLastError();
                    Debug.WriteLine($"PnPInputDevice: Failed to get device info set for class {classGuid}. Error: {error}");
                    return;
                }

                uint deviceIndex = 0;
                var deviceInfoData = new SP_DEVINFO_DATA();
                deviceInfoData.cbSize = (uint)Marshal.SizeOf(deviceInfoData);

                // Enumerate all devices in this class
                while (SetupDiEnumDeviceInfo(deviceInfoSet, deviceIndex, ref deviceInfoData))
                {
                    try
                    {
                        var deviceInfo = CreateDeviceInfo(deviceInfoSet, deviceInfoData);
                        if (deviceInfo != null && IsInputDevice(deviceInfo) && !IsVirtualConvertedDevice(deviceInfo))
                        {
                            deviceListIndex++;
                            deviceList.Add(deviceInfo);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"PnPInputDevice: Error processing device {deviceIndex} in class {classGuid}: {ex.Message}");
                    }

                    deviceIndex++;
                    deviceInfoData.cbSize = (uint)Marshal.SizeOf(deviceInfoData);
                }

                uint lastError = GetLastError();
                if (lastError != ERROR_NO_MORE_ITEMS)
                {
                    Debug.WriteLine($"PnPInputDevice: Enumeration ended with error {lastError} for class {classGuid}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PnPInputDevice: Error enumerating device class {classGuid}: {ex.Message}");
            }
            finally
            {
                if (deviceInfoSet != IntPtr.Zero && deviceInfoSet.ToInt64() != -1)
                {
                    SetupDiDestroyDeviceInfoList(deviceInfoSet);
                }
            }
        }


        /// <summary>
        /// Creates a PnPInputDeviceInfo object from Windows device information.
        /// </summary>
        /// <param name="deviceInfoSet">Device information set handle</param>
        /// <param name="deviceInfoData">Device information data</param>
        /// <returns>PnPInputDeviceInfo object or null if creation fails</returns>
        private PnPInputDeviceInfo CreateDeviceInfo(IntPtr deviceInfoSet, SP_DEVINFO_DATA deviceInfoData)
        {
            try
            {
                var deviceInfo = new PnPInputDeviceInfo
                {
                    ClassGuid = deviceInfoData.ClassGuid,
                    IsOnline = true,
                    InputType = "PnPInput"
                };

                // Initial application profile state
                deviceInfo.IsEnabled = false;
                deviceInfo.AssignedToPad1 = false;
                deviceInfo.AssignedToPad2 = false;
                deviceInfo.AssignedToPad3 = false;
                deviceInfo.AssignedToPad4 = false;

                // Get device instance ID
                var instanceIdBuffer = new StringBuilder(256);
                if (SetupDiGetDeviceInstanceId(deviceInfoSet, ref deviceInfoData, instanceIdBuffer, 256, out uint requiredSize))
                {
                    deviceInfo.DeviceInstanceId = instanceIdBuffer.ToString();
                    deviceInfo.DeviceId = deviceInfo.DeviceInstanceId;
                }

                // Get device properties
                deviceInfo.FriendlyName = GetDeviceProperty(deviceInfoSet, deviceInfoData, SPDRP_FRIENDLYNAME);
                deviceInfo.DeviceDescription = GetDeviceProperty(deviceInfoSet, deviceInfoData, SPDRP_DEVICEDESC);
                deviceInfo.Manufacturer = GetDeviceProperty(deviceInfoSet, deviceInfoData, SPDRP_MFG);
                deviceInfo.HardwareIds = GetDeviceProperty(deviceInfoSet, deviceInfoData, SPDRP_HARDWAREID);
                deviceInfo.LocationInformation = GetDeviceProperty(deviceInfoSet, deviceInfoData, SPDRP_LOCATION_INFORMATION);
                deviceInfo.PhysicalDeviceObjectName = GetDeviceProperty(deviceInfoSet, deviceInfoData, SPDRP_PHYSICAL_DEVICE_OBJECT_NAME);

                // Only use native properties that Windows actually provides - do not create synthetic names
                // InstanceName: Use FriendlyName if available, otherwise leave empty
                deviceInfo.InstanceName = deviceInfo.FriendlyName ?? "";
                
                // ProductName: Use DeviceDescription if available, otherwise leave empty
                deviceInfo.ProductName = deviceInfo.DeviceDescription ?? "";

                // Extract VID/PID from all available device properties
                // Search in order of likelihood: HardwareIds, DeviceInstanceId, LocationInformation, PhysicalDeviceObjectName
                var vidPid = ExtractVidPidFromAllProperties(
                    deviceInfo.HardwareIds,
                    deviceInfo.DeviceInstanceId,
                    deviceInfo.LocationInformation,
                    deviceInfo.PhysicalDeviceObjectName);
                deviceInfo.VendorId = vidPid.vid;
                deviceInfo.ProductId = vidPid.pid;

                // Generate GUIDs
                deviceInfo.InstanceGuid = GenerateInstanceGuid(deviceInfo.DeviceInstanceId);
                deviceInfo.ProductGuid = GenerateProductGuid(deviceInfo.VendorId, deviceInfo.ProductId);

                // Set device type information
                deviceInfo.DeviceTypeName = GetDeviceTypeName(deviceInfo.ClassGuid, deviceInfo.HardwareIds);
                deviceInfo.DeviceType = GetDeviceTypeFromClass(deviceInfo.ClassGuid);

                // Get device status
                if (CM_Get_DevNode_Status(out uint status, out uint problemCode, deviceInfoData.DevInst, 0))
                {
                    deviceInfo.DeviceStatus = status;
                    deviceInfo.ProblemCode = problemCode;
                    deviceInfo.IsPresent = (status & DN_HAS_PROBLEM) == 0 || problemCode == 0;
                    deviceInfo.IsStarted = (status & DN_STARTED) != 0;
                }
                else
                {
                    deviceInfo.IsPresent = true; // Assume present if we can't get status
                    deviceInfo.IsStarted = true;
                }

                // Set capability values to indicate unknown (PnP doesn't provide this information)
                SetUnknownCapabilities(deviceInfo);

                // InterfacePath is not natively provided by PnP - leave empty
                deviceInfo.InterfacePath = "";

                return deviceInfo;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PnPInputDevice: Error creating device info: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets a device property as a string.
        /// </summary>
        /// <param name="deviceInfoSet">Device information set handle</param>
        /// <param name="deviceInfoData">Device information data</param>
        /// <param name="property">Property to retrieve</param>
        /// <returns>Property value as string or empty string if not available</returns>
        private string GetDeviceProperty(IntPtr deviceInfoSet, SP_DEVINFO_DATA deviceInfoData, uint property)
        {
            try
            {
                // First call to get required buffer size
                SetupDiGetDeviceRegistryProperty(deviceInfoSet, ref deviceInfoData, property,
                    out uint propertyType, null, 0, out uint requiredSize);

                if (requiredSize == 0)
                    return "";

                // Second call to get actual data
                var buffer = new byte[requiredSize];
                if (SetupDiGetDeviceRegistryProperty(deviceInfoSet, ref deviceInfoData, property,
                    out propertyType, buffer, requiredSize, out uint actualSize))
                {
                    // Handle different property types
                    switch (propertyType)
                    {
                        case 1: // REG_SZ (string)
                            return Encoding.Unicode.GetString(buffer, 0, (int)actualSize - 2); // -2 to remove null terminator
                        case 7: // REG_MULTI_SZ (multi-string)
                            var multiString = Encoding.Unicode.GetString(buffer, 0, (int)actualSize - 2);
                            return multiString.Replace('\0', ';'); // Convert to semicolon-separated
                        default:
                            return Encoding.Unicode.GetString(buffer, 0, (int)actualSize);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PnPInputDevice: Error getting device property {property}: {ex.Message}");
            }

            return "";
        }

        /// <summary>
        /// Determines if a device is an input device based on its properties.
        /// Uses early-exit optimization and consolidated pattern matching for better performance.
        /// </summary>
        /// <param name="deviceInfo">Device information</param>
        /// <returns>True if device is an input device</returns>
        private bool IsInputDevice(PnPInputDeviceInfo deviceInfo)
        {
            // Get hardware IDs early for all device classes
            var hardwareIds = deviceInfo.HardwareIds ?? "";
            
            // Early reject: No identification information at all
            if (string.IsNullOrEmpty(hardwareIds) && string.IsNullOrEmpty(deviceInfo.DeviceDescription))
            {
                return false;
            }

            // Cache case conversion once for performance
            var upperHardwareIds = hardwareIds.ToUpperInvariant();
            
            // Early reject: Intel platform endpoints (HID Event Filter) - applies to ALL device classes
            // These are platform hotkey controllers (volume/brightness/etc.), not standalone user peripherals
            // Must check BEFORE accepting Keyboard/Mouse classes to filter out Intel platform endpoints
            if (ContainsIntelPlatformPattern(upperHardwareIds))
            {
                return false;
            }
            
            // Early accept: Keyboards and Mice from their specific classes are input devices
            // (after filtering out Intel platform endpoints above)
            if (deviceInfo.ClassGuid == GUID_DEVCLASS_KEYBOARD ||
                deviceInfo.ClassGuid == GUID_DEVCLASS_MOUSE)
            {
                return true;
            }

            // Early reject: Only process HID devices beyond this point
            if (deviceInfo.ClassGuid != GUID_DEVCLASS_HIDCLASS)
            {
                return false;
            }

            // For HID devices, apply strict filtering with early exits
            var description = deviceInfo.DeviceDescription ?? "";

            // Early reject: Vendor-defined usage pages (FF00-FFFF and 01FF) - fastest check first
            if (ContainsVendorDefinedPattern(upperHardwareIds))
            {
                return false;
            }

            var lowerHardwareIds = hardwareIds.ToLowerInvariant();
            var lowerDescription = description.ToLowerInvariant();

            // Early reject: Known non-input device patterns in hardware IDs
            if (ContainsAny(lowerHardwareIds, _excludeHardwarePatterns))
            {
                return false;
            }

            // Early reject: Known non-input device patterns in description
            if (ContainsAny(lowerDescription, _excludeDescriptionPatterns))
            {
                return false;
            }

            // Final check: Must have explicit input device indicators
            // Combine strings only if we passed all rejection filters
            var friendlyName = deviceInfo.FriendlyName ?? "";
            var combinedText = $"{lowerHardwareIds} {lowerDescription} {friendlyName.ToLowerInvariant()}";
            return ContainsAny(combinedText, _acceptInputPatterns);
        }

        /// <summary>
        /// Efficiently checks if text contains vendor-defined HID usage page patterns.
        /// Optimized for the most common case (UP:FF prefix).
        /// Includes custom/vendor page 0x01FF and standard vendor-defined pages (0xF8-0xFF).
        /// </summary>
        /// <param name="upperText">Text to search in (must be uppercase)</param>
        /// <returns>True if vendor-defined pattern is found</returns>
        private static bool ContainsVendorDefinedPattern(string upperText)
        {
            if (string.IsNullOrEmpty(upperText))
                return false;

            // Check custom/vendor page 0x01FF (configuration/feature collections, not direct input)
            if (upperText.Contains("UP:01FF") || upperText.Contains("&UP:01FF"))
                return true;

            // Check most common vendor-defined patterns (0xF8-0xFF) for early exit
            return upperText.Contains("UP:FF") || upperText.Contains("&UP:FF") ||
                   upperText.Contains("UP:FE") || upperText.Contains("&UP:FE") ||
                   upperText.Contains("UP:FD") || upperText.Contains("&UP:FD") ||
                   upperText.Contains("UP:FC") || upperText.Contains("&UP:FC") ||
                   upperText.Contains("UP:FB") || upperText.Contains("&UP:FB") ||
                   upperText.Contains("UP:FA") || upperText.Contains("&UP:FA") ||
                   upperText.Contains("UP:F9") || upperText.Contains("&UP:F9") ||
                   upperText.Contains("UP:F8") || upperText.Contains("&UP:F8");
        }

        /// <summary>
        /// Checks if text contains Intel platform endpoint patterns (HID Event Filter).
        /// These are platform hotkey controllers (volume/brightness/etc.), not standalone user peripherals.
        /// Examples: HID\INT33D2&COL01 (VID_494E54&PID_33D2), HID\INTC816&COL01 (VID_8087&PID_0000)
        /// </summary>
        /// <param name="upperText">Text to search in (must be uppercase)</param>
        /// <returns>True if Intel platform pattern is found</returns>
        private static bool ContainsIntelPlatformPattern(string upperText)
        {
            if (string.IsNullOrEmpty(upperText))
                return false;

            // Intel HID Event Filter endpoints - platform hotkey controllers, not user peripherals
            // Pattern 1: HID\INT33D2 or HID\INTC816 (device ID format)
            if (upperText.Contains("HID\\INT33D2") || upperText.Contains("HID\\INTC816") ||
                upperText.Contains("HID/INT33D2") || upperText.Contains("HID/INTC816"))
                return true;
            
            // Pattern 2: VID_494E54 (ASCII "INT") or VID_8087 (Intel vendor ID) with platform device markers
            if ((upperText.Contains("VID_494E54") || upperText.Contains("VID_8087")) &&
                (upperText.Contains("&COL") || upperText.Contains("\\COL")))
                return true;
            
            // Pattern 3: VEN_INT with DEV_33D2 (alternative vendor/device format)
            if (upperText.Contains("VEN_INT") && upperText.Contains("DEV_33D2"))
                return true;

            return false;
        }

        /// <summary>
        /// Efficiently checks if text contains any of the specified patterns.
        /// </summary>
        /// <param name="text">Text to search in</param>
        /// <param name="patterns">Patterns to search for</param>
        /// <returns>True if any pattern is found</returns>
        private static bool ContainsAny(string text, string[] patterns)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            foreach (var pattern in patterns)
            {
                if (text.Contains(pattern))
                    return true;
            }
            return false;
        }

        #region Filter Pattern Constants

        /// <summary>
        /// Hardware ID patterns that indicate non-input devices.
        /// Ordered by frequency of occurrence for faster rejection.
        /// </summary>
        private static readonly string[] _excludeHardwarePatterns = {
            // Most common non-input patterns first for faster rejection
            "audio", "sound", "storage", "disk", "network", "ethernet",
            // System devices
            "acpi", "root", "system", "pci", "usb\\root", "composite",
            // Audio devices (continued)
            "speaker", "microphone", "headphone", "headset",
            // Storage devices (continued)
            "drive", "mass", "flash", "cdrom", "dvd",
            // Network devices (continued)
            "wifi", "wlan", "bluetooth\\radio", "bthhfenum",
            // Display devices
            "display", "monitor", "video", "graphics", "capture", "camera", "webcam",
            // Communication devices
            "modem", "serial", "parallel", "com", "lpt",
            // Printers and scanners
            "printer", "print", "scanner", "fax",
            // Power and thermal
            "battery", "power", "thermal", "fan", "temperature",
            // Sensors and other non-input
            "sensor", "accelerometer", "gyroscope", "magnetometer", "proximity",
            "ambient", "light", "biometric", "fingerprint",
            // USB hubs and controllers
            "hub", "controller\\", "host", "ehci", "uhci", "ohci", "xhci",
            // Firmware and BIOS
            "firmware", "bios", "uefi", "tpm",
            // Virtual devices
            "virtual", "software", "null", "teredo",
            // Input configuration and portable device control (non-gaming input)
            "input_config", "inputconfig", "portable_device", "portabledevice"
        };

        /// <summary>
        /// Description patterns that indicate non-input devices.
        /// Ordered by frequency of occurrence for faster rejection.
        /// </summary>
        private static readonly string[] _excludeDescriptionPatterns = {
            // Most common non-input patterns first
            "audio", "sound", "storage", "disk", "network", "ethernet",
            "speaker", "microphone", "headphone", "headset",
            "drive", "mass", "flash", "cdrom",
            "wifi", "bluetooth", "radio",
            "display", "monitor", "video", "graphics", "capture", "camera", "webcam",
            "printer", "scanner", "fax", "modem", "serial",
            "battery", "power", "thermal", "fan", "temperature",
            "sensor", "accelerometer", "gyroscope", "magnetometer", "proximity",
            "hub", "controller", "host", "firmware", "bios", "virtual",
            // Input configuration and portable device control (non-gaming input)
            "input configuration", "portable device control"
        };

        /// <summary>
        /// Patterns that positively identify input devices.
        /// Ordered by frequency of occurrence for faster acceptance.
        /// </summary>
        private static readonly string[] _acceptInputPatterns = {
            // Most common input device patterns first
            "hid\\vid_", "usb\\class_03", "mouse", "keyboard",
            // Gaming devices
            "gamepad", "joystick", "controller", "wheel", "pedal", "throttle",
            "xbox", "playstation", "nintendo", "dualshock", "pro controller",
            // Input devices (continued)
            "trackpad", "touchpad", "trackball",
            "tablet", "digitizer", "stylus", "pen", "touch",
            // Standard HID input usage pages
            "up:0001_u:0002", "up:0001_u:0006", "up:0001_u:0004", "up:0001_u:0005",
            "hid_device_system_mouse", "hid_device_system_keyboard", "hid_device_system_game",
            // Generic input indicator
            "input"
        };

        #endregion

        /// <summary>
        /// Determines if a device is a virtual/converted device that should be excluded.
        /// Checks for "ConvertedDevice" text in DeviceInstanceId or InterfacePath.
        /// </summary>
        /// <param name="deviceInfo">Device information</param>
        /// <returns>True if device is a virtual/converted device</returns>
        private bool IsVirtualConvertedDevice(PnPInputDeviceInfo deviceInfo)
        {
            if (deviceInfo == null)
                return false;

            // Check DeviceInstanceId for "ConvertedDevice" marker
            if (!string.IsNullOrEmpty(deviceInfo.DeviceInstanceId) &&
                deviceInfo.DeviceInstanceId.IndexOf("ConvertedDevice", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            // Check InterfacePath for "ConvertedDevice" marker
            if (!string.IsNullOrEmpty(deviceInfo.InterfacePath) &&
                deviceInfo.InterfacePath.IndexOf("ConvertedDevice", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Determines if a device is a gamepad based on hardware IDs and description.
        /// </summary>
        /// <param name="hardwareIds">Hardware IDs string</param>
        /// <param name="description">Device description</param>
        /// <returns>True if device is a gamepad</returns>
        private bool IsGamepadDevice(string hardwareIds, string description)
        {
            var combinedText = $"{hardwareIds} {description}".ToLowerInvariant();
            
            string[] gamepadPatterns = {
                "gamepad", "joystick", "controller", "wheel", "pedal", "throttle",
                "xbox", "playstation", "nintendo", "dualshock", "pro controller"
            };

            foreach (var pattern in gamepadPatterns)
            {
                if (combinedText.Contains(pattern))
                    return true;
            }

            return false;
        }


        /// <summary>
        /// Extracts VID and PID from all available device properties.
        /// Searches multiple properties in order of likelihood to find VID/PID information.
        /// Falls back to VEN_/DEV_ values when VID/PID are not available.
        /// </summary>
        /// <param name="hardwareIds">Hardware IDs string (primary source)</param>
        /// <param name="deviceInstanceId">Device instance ID (secondary source)</param>
        /// <param name="locationInfo">Location information (tertiary source)</param>
        /// <param name="physicalDeviceObjectName">Physical device object name (quaternary source)</param>
        /// <returns>Tuple containing VID and PID values</returns>
        private (int vid, int pid) ExtractVidPidFromAllProperties(
            string hardwareIds,
            string deviceInstanceId = null,
            string locationInfo = null,
            string physicalDeviceObjectName = null)
        {
            // Try primary source first (most reliable)
            var result = ExtractVidPidFromSingleProperty(hardwareIds);
            if (result.vid != 0 && result.pid != 0)
                return result;
            
            // If either VID or PID is still missing, try other properties
            int vid = result.vid;
            int pid = result.pid;
            
            // Try DeviceInstanceId if VID or PID still missing
            if (vid == 0 || pid == 0)
            {
                var instanceResult = ExtractVidPidFromSingleProperty(deviceInstanceId);
                if (vid == 0) vid = instanceResult.vid;
                if (pid == 0) pid = instanceResult.pid;
            }
            
            // Try LocationInformation if VID or PID still missing
            if (vid == 0 || pid == 0)
            {
                var locationResult = ExtractVidPidFromSingleProperty(locationInfo);
                if (vid == 0) vid = locationResult.vid;
                if (pid == 0) pid = locationResult.pid;
            }
            
            // Try PhysicalDeviceObjectName if VID or PID still missing
            if (vid == 0 || pid == 0)
            {
                var physicalResult = ExtractVidPidFromSingleProperty(physicalDeviceObjectName);
                if (vid == 0) vid = physicalResult.vid;
                if (pid == 0) pid = physicalResult.pid;
            }
            
            return (vid, pid);
        }

        /// <summary>
        /// Extracts VID and PID from a single property string using optimized pattern matching.
        /// Supports multiple VID/PID formats including standard (VID_XXXX) and alternate (VID&XXXXXXXX_PID&XXXX) formats.
        /// Falls back to VEN_/DEV_ values when VID/PID are not available or are 0000.
        /// </summary>
        /// <param name="propertyValue">Property value to search in</param>
        /// <returns>Tuple containing VID and PID values</returns>
        private (int vid, int pid) ExtractVidPidFromSingleProperty(string propertyValue)
        {
            if (string.IsNullOrEmpty(propertyValue))
                return (0, 0);

            try
            {
                var upperValue = propertyValue.ToUpperInvariant();
                
                // Try standard format first: VID_XXXX and PID_XXXX (most common)
                int vid = ExtractHexValue(upperValue, "VID_", 4) ?? 0;
                int pid = ExtractHexValue(upperValue, "PID_", 4) ?? 0;
                
                // Try alternate format: VID&XXXXXXXX
                if (vid == 0)
                    vid = ExtractHexValueVariable(upperValue, "VID&", "_PID&") ?? 0;
                
                // Try alternate format: _PID&XXXX
                if (pid == 0)
                    pid = ExtractHexValueVariable(upperValue, "_PID&", new[] { "&", ";", "\\", " " }) ?? 0;
                
                // Fallback: If VID is still 0 or not found, try VEN_ format
                // This handles cases like HID\VEN_INT&DEV_33D2 where VID is not available
                if (vid == 0)
                {
                    vid = ExtractHexValue(upperValue, "VEN_", 4) ?? 0;
                }
                
                // Fallback: If PID is still 0 or not found, try DEV_ format
                // This handles cases like HID\VEN_INT&DEV_33D2 where PID is not available
                if (pid == 0)
                {
                    pid = ExtractHexValue(upperValue, "DEV_", 4) ?? 0;
                }
                
                return (vid, pid);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PnPInputDevice: Error extracting VID/PID: {ex.Message}");
                return (0, 0);
            }
        }

        /// <summary>
        /// Extracts a hexadecimal value following a specific pattern in a string.
        /// Handles both numeric hex values (e.g., "046A") and alphanumeric vendor codes (e.g., "INT").
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
        /// Extracts a variable-length hexadecimal value between a start pattern and end delimiter(s).
        /// Takes the last 4 characters of the hex string for standard VID/PID format.
        /// </summary>
        /// <param name="text">Text to search in</param>
        /// <param name="startPattern">Pattern marking the start (e.g., "VID&")</param>
        /// <param name="endDelimiters">Delimiter(s) marking the end (string or string array)</param>
        /// <returns>Parsed integer value (last 4 hex digits) or null if not found</returns>
        private static int? ExtractHexValueVariable(string text, string startPattern, object endDelimiters)
        {
            var index = text.IndexOf(startPattern);
            if (index < 0)
                return null;

            var start = index + startPattern.Length;
            if (start >= text.Length)
                return null;

            // Find the end of the hex value (only valid hex digits: 0-9, A-F)
            var end = start;
            while (end < text.Length)
            {
                var ch = text[end];
                if ((ch >= '0' && ch <= '9') || (ch >= 'A' && ch <= 'F'))
                    end++;
                else
                    break;
            }

            if (end <= start)
                return null;

            var hexStr = text.Substring(start, end - start);
            
            // Take last 4 characters for standard VID/PID format
            if (hexStr.Length > 4)
                hexStr = hexStr.Substring(hexStr.Length - 4);

            return int.TryParse(hexStr, System.Globalization.NumberStyles.HexNumber, null, out int value)
                ? value
                : (int?)null;
        }

        /// <summary>
        /// Generates a unique instance GUID for a device.
        /// </summary>
        /// <param name="deviceInstanceId">Device instance ID</param>
        /// <returns>Unique instance GUID</returns>
        private Guid GenerateInstanceGuid(string deviceInstanceId)
        {
            try
            {
                if (string.IsNullOrEmpty(deviceInstanceId))
                    return Guid.NewGuid();

                // Create a deterministic GUID based on device instance ID
                var input = $"PnPInput_{deviceInstanceId}";
                var hash = System.Security.Cryptography.MD5.Create().ComputeHash(Encoding.UTF8.GetBytes(input));
                return new Guid(hash);
            }
            catch
            {
                return Guid.NewGuid();
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
            try
            {
                if (vendorId == 0 && productId == 0)
                    return Guid.NewGuid();

                // Create a deterministic GUID based on VID/PID
                var guidBytes = new byte[16];
                var vidBytes = BitConverter.GetBytes(vendorId);
                var pidBytes = BitConverter.GetBytes(productId);
                
                Array.Copy(vidBytes, 0, guidBytes, 0, 4);
                Array.Copy(pidBytes, 0, guidBytes, 4, 4);
                
                // Add Physical identifier
                guidBytes[8] = 0x50; // 'P'
                guidBytes[9] = 0x48; // 'H'
                guidBytes[10] = 0x59; // 'Y'
                guidBytes[11] = 0x53; // 'S'
                
                return new Guid(guidBytes);
            }
            catch
            {
                return Guid.NewGuid();
            }
        }

        /// <summary>
        /// Gets a human-readable device type name based on class GUID and hardware IDs.
        /// </summary>
        /// <param name="classGuid">Device class GUID</param>
        /// <param name="hardwareIds">Hardware IDs string</param>
        /// <returns>Human-readable device type name</returns>
        private string GetDeviceTypeName(Guid classGuid, string hardwareIds)
        {
            if (classGuid == GUID_DEVCLASS_HIDCLASS)
            {
                if (IsGamepadDevice(hardwareIds, ""))
                    return "Physical HID Gamepad";
                return "Physical HID Device";
            }
            else if (classGuid == GUID_DEVCLASS_KEYBOARD)
            {
                return "Physical Keyboard";
            }
            else if (classGuid == GUID_DEVCLASS_MOUSE)
            {
                return "Physical Mouse";
            }
            
            return $"Physical Device ({classGuid})";
        }

        /// <summary>
        /// Gets a numeric device type from class GUID.
        /// </summary>
        /// <param name="classGuid">Device class GUID</param>
        /// <returns>Numeric device type</returns>
        private int GetDeviceTypeFromClass(Guid classGuid)
        {
            if (classGuid == GUID_DEVCLASS_HIDCLASS)
                return 2; // HID
            else if (classGuid == GUID_DEVCLASS_KEYBOARD)
                return 1; // Keyboard
            else if (classGuid == GUID_DEVCLASS_MOUSE)
                return 0; // Mouse
            
            return 99; // Unknown
        }

        /// <summary>
        /// Sets capability values to indicate unknown since Windows PnP doesn't provide detailed input capabilities.
        /// Only sets values that can be determined from PnP information or are required for compatibility.
        /// </summary>
        /// <param name="deviceInfo">Device information to update</param>
        private void SetUnknownCapabilities(PnPInputDeviceInfo deviceInfo)
        {
            // Windows PnP does NOT provide detailed input capability information
            // Set all capability counts to 0 to indicate "unknown/not available"
            deviceInfo.AxeCount = 0;        // Unknown - PnP doesn't provide axis count
            deviceInfo.SliderCount = 0;     // Unknown - PnP doesn't provide slider count
            deviceInfo.ButtonCount = 0;     // Unknown - PnP doesn't provide button/key count
            deviceInfo.PovCount = 0;        // Unknown - PnP doesn't provide POV count
            deviceInfo.HasForceFeedback = false; // Unknown - PnP doesn't provide force feedback info

            // Set HID usage information only if we can determine it from device class
            if (deviceInfo.ClassGuid == GUID_DEVCLASS_HIDCLASS)
            {
                deviceInfo.Usage = 0x00;        // Unknown - would need HID descriptor
                deviceInfo.UsagePage = 0x00;    // Unknown - would need HID descriptor
            }
            else
            {
                deviceInfo.Usage = 0x00;        // Not HID device
                deviceInfo.UsagePage = 0x00;    // Not HID device
            }

            // Set version information to indicate unknown
            deviceInfo.DriverVersion = 0;       // Unknown - PnP doesn't provide detailed version
            deviceInfo.HardwareRevision = 0;    // Unknown - PnP doesn't provide hardware revision
            deviceInfo.FirmwareRevision = 0;    // Unknown - PnP doesn't provide firmware revision
        }

        /// <summary>
        /// Generates a SortingString and CommonIdentifier for the device by extracting VID, PID, MI, and COL values.
        /// Uses optimized single-pass extraction from combined device properties.
        /// </summary>
        /// <param name="deviceInfo">Device information to process</param>
        private void GenerateSortingString(PnPInputDeviceInfo deviceInfo)
        {
            try
            {
                // Use already-extracted VID/PID values from deviceInfo instead of re-extracting
                // This ensures consistency with the VendorId and ProductId properties
                var vid = deviceInfo.VendorId.ToString("X4");
                var pid = deviceInfo.ProductId.ToString("X4");

                // Prioritize DeviceInstanceId and HardwareIds as they're most likely to contain MI/COL
                var primaryText = $"{deviceInfo.DeviceInstanceId ?? ""};{deviceInfo.HardwareIds ?? ""}".ToUpperInvariant();
                
                // Extract MI and COL values from primary sources first
                var mi = ExtractPatternValue(primaryText, new[] { "&MI_", "\\MI_" }, 2);
                var col = ExtractPatternValue(primaryText, new[] { "&COL", "\\COL" }, 2);
                
                // Only check secondary sources if not found in primary
                if (string.IsNullOrEmpty(mi) && string.IsNullOrEmpty(col))
                {
                    var secondaryText = $"{deviceInfo.InterfacePath ?? ""};{deviceInfo.PhysicalDeviceObjectName ?? ""};{deviceInfo.LocationInformation ?? ""}".ToUpperInvariant();
                    if (!string.IsNullOrEmpty(secondaryText) && secondaryText.Length > 1)
                    {
                        mi = mi ?? ExtractPatternValue(secondaryText, new[] { "&MI_", "\\MI_" }, 2);
                        col = col ?? ExtractPatternValue(secondaryText, new[] { "&COL", "\\COL" }, 2);
                    }
                }

                // Build SortingString efficiently using StringBuilder for better performance
                var sortingString = $"VID_{vid}&PID_{pid}";
                if (!string.IsNullOrEmpty(mi))
                    sortingString += $"&MI_{mi}";
                if (!string.IsNullOrEmpty(col))
                    sortingString += $"&COL_{col}";

                deviceInfo.SortingString = sortingString;
                deviceInfo.CommonIdentifier = sortingString;
                deviceInfo.MiValue = mi ?? "";
                deviceInfo.ColValue = col ?? "";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PnPInputDevice: Error generating sorting string: {ex.Message}");
                deviceInfo.SortingString = "VID_0000&PID_0000";
                deviceInfo.CommonIdentifier = "VID_0000&PID_0000";
                deviceInfo.MiValue = "";
                deviceInfo.ColValue = "";
            }
        }

        /// <summary>
        /// Extracts a value from text using multiple search patterns (optimized version).
        /// </summary>
        /// <param name="text">Text to search in</param>
        /// <param name="patterns">Array of patterns to search for</param>
        /// <param name="maxLength">Maximum length of value to extract</param>
        /// <returns>Extracted value or null if not found</returns>
        private static string ExtractPatternValue(string text, string[] patterns, int maxLength)
        {
            foreach (var pattern in patterns)
            {
                var index = text.IndexOf(pattern);
                if (index < 0)
                    continue;

                var start = index + pattern.Length;
                var remaining = text.Length - start;
                if (remaining <= 0)
                    continue;

                // Find end of value (delimiter or max length)
                var length = 0;
                while (length < remaining && length < maxLength)
                {
                    var ch = text[start + length];
                    if (ch == '&' || ch == ';' || ch == '\\' || ch == ' ' || ch == '\t')
                        break;
                    length++;
                }

                if (length > 0)
                {
                    var value = text.Substring(start, length);
                    if (value != "0000") // Skip default/empty values
                        return value;
                }
            }
            return null;
        }

        /// <summary>
        /// Gets a grouping key to identify devices belonging to the same physical device.
        /// Devices from the same physical device will have the same VID/PID combination and manufacturer.
        /// </summary>
        /// <param name="deviceInfo">Device information</param>
        /// <returns>Grouping key for physical device identification</returns>
        private string GetPhysicalDeviceGroupKey(PnPInputDeviceInfo deviceInfo)
        {
            // Create a composite key that groups devices from the same physical device
            // Use VID/PID as primary grouping factor, with manufacturer as secondary
            var vidPid = $"{deviceInfo.VendorId:X4}_{deviceInfo.ProductId:X4}";
            var manufacturer = deviceInfo.Manufacturer ?? "Unknown";
            
            // For devices without VID/PID (like some system devices), try to extract parent device info
            if (deviceInfo.VendorId == 0 && deviceInfo.ProductId == 0)
            {
                // Try to extract parent device identifier from DeviceInstanceId
                var parentKey = ExtractParentDeviceKey(deviceInfo.DeviceInstanceId);
                if (!string.IsNullOrEmpty(parentKey))
                {
                    return $"{manufacturer}_{parentKey}";
                }
                
                var description = deviceInfo.DeviceDescription ?? deviceInfo.FriendlyName ?? "Unknown";
                return $"{manufacturer}_{description}";
            }
            
            return $"{manufacturer}_{vidPid}";
        }

        /// <summary>
        /// Extracts parent device key from DeviceInstanceId to group related interfaces.
        /// </summary>
        /// <param name="deviceInstanceId">Device instance ID</param>
        /// <returns>Parent device key or empty string if not found</returns>
        private string ExtractParentDeviceKey(string deviceInstanceId)
        {
            if (string.IsNullOrEmpty(deviceInstanceId))
                return "";

            try
            {
                // For USB devices with MI (Multiple Interface) like "USB\VID_046A&PID_C098&MI_00\..."
                // Extract the base VID&PID part to group interfaces from the same physical device
                if (deviceInstanceId.StartsWith("USB\\", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = deviceInstanceId.Split('\\');
                    if (parts.Length >= 2)
                    {
                        // Remove MI identifier (e.g., "&MI_00")
                        var devicePart = parts[1];
                        var miIndex = devicePart.IndexOf("&MI_", StringComparison.OrdinalIgnoreCase);
                        if (miIndex > 0)
                        {
                            devicePart = devicePart.Substring(0, miIndex);
                        }
                        return $"USB_{devicePart}";
                    }
                }
                
                // For HID devices like "HID\INTC816&COL01\3&D2322F2&0&0000"
                // Extract the base part before the collection identifier
                if (deviceInstanceId.StartsWith("HID\\", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = deviceInstanceId.Split('\\');
                    if (parts.Length >= 2)
                    {
                        // Remove collection identifier (e.g., "&COL01")
                        var devicePart = parts[1];
                        var colIndex = devicePart.IndexOf("&COL", StringComparison.OrdinalIgnoreCase);
                        if (colIndex > 0)
                        {
                            devicePart = devicePart.Substring(0, colIndex);
                        }
                        return $"HID_{devicePart}";
                    }
                }
                
                // For other devices, use the first two parts of the instance ID
                var instanceParts = deviceInstanceId.Split('\\');
                if (instanceParts.Length >= 2)
                {
                    return $"{instanceParts[0]}_{instanceParts[1]}";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PnPInputDevice: Error extracting parent device key from '{deviceInstanceId}': {ex.Message}");
            }

            return "";
        }

        /// <summary>
        /// Determines if a device is likely a parent device (physical device) or child interface.
        /// </summary>
        /// <param name="deviceInfo">Device information</param>
        /// <returns>True if device is likely a parent device</returns>
        private bool IsParentDevice(PnPInputDeviceInfo deviceInfo)
        {
            if (string.IsNullOrEmpty(deviceInfo.DeviceInstanceId))
                return true; // Assume parent if we can't determine

            var instanceId = deviceInfo.DeviceInstanceId.ToUpperInvariant();

            // HID collection interfaces are child devices (contain &COL)
            if (instanceId.Contains("&COL"))
                return false;

            // USB composite device interfaces are child devices (contain &MI_)
            if (instanceId.Contains("&MI_"))
                return false;

            // Devices with specific interface indicators are child devices
            if (instanceId.Contains("\\INTERFACE\\") || instanceId.Contains("\\CHILD\\"))
                return false;

            // For HID devices, check if this looks like a base device
            if (deviceInfo.ClassGuid == GUID_DEVCLASS_HIDCLASS)
            {
                // If it's a simple HID device without collection or interface indicators, it's likely a parent
                if (instanceId.StartsWith("HID\\") && !instanceId.Contains("&COL") && !instanceId.Contains("&MI_"))
                    return true;
                
                // If it has collection indicators, it's a child
                return false;
            }

            // For keyboard and mouse devices, they are typically parent devices unless they have interface indicators
            if (deviceInfo.ClassGuid == GUID_DEVCLASS_KEYBOARD || deviceInfo.ClassGuid == GUID_DEVCLASS_MOUSE)
            {
                return true; // Most keyboard/mouse class devices are parent devices
            }

            // Default to parent device
            return true;
        }

        /// <summary>
        /// Logs devices with hierarchical display showing physical device grouping.
        /// </summary>
        /// <param name="deviceList">Ordered device list</param>
        /// <param name="debugLines">Debug lines list</param>
        private void LogHierarchicalDeviceList(List<PnPInputDeviceInfo> deviceList, List<string> debugLines)
        {
            var deviceIndex = 0;
            string currentPhysicalGroup = "";
            bool hasParentInGroup = false;

            foreach (var device in deviceList)
            {
                deviceIndex++;
                var physicalGroupKey = GetPhysicalDeviceGroupKey(device);
                var isParent = IsParentDevice(device);
                
                // Check if we're starting a new physical device group
                if (physicalGroupKey != currentPhysicalGroup)
                {
                    currentPhysicalGroup = physicalGroupKey;
                    hasParentInGroup = false;
                }

                // Track if we've seen a parent device in this group
                if (isParent)
                {
                    hasParentInGroup = true;
                }

                // Log device with appropriate formatting
                LogDeviceInfoHierarchical(device, deviceIndex, debugLines, isParent, hasParentInGroup);
            }
        }

        /// <summary>
        /// Formats a property for debug output only if it has a non-empty value.
        /// </summary>
        /// <param name="name">Property name</param>
        /// <param name="value">Property value</param>
        /// <returns>Formatted string or empty string if value is null/empty</returns>
        private string FormatProperty(string name, string value)
        {
            return !string.IsNullOrEmpty(value) ? $"{name}: {value}, " : "";
        }

        /// <summary>
        /// Logs device information with hierarchical formatting based on MI and COL values.
        /// </summary>
        /// <param name="deviceInfo">Device information</param>
        /// <param name="index">Device index</param>
        /// <param name="debugLines">Debug lines list</param>
        /// <param name="isParent">True if this is a parent device</param>
        /// <param name="hasParentInGroup">True if the group already has a parent device</param>
        private void LogDeviceInfoHierarchical(PnPInputDeviceInfo deviceInfo, int index, List<string> debugLines, bool isParent, bool hasParentInGroup)
        {
            // Determine device type prefix
            string deviceTypePrefix;
            string indentation = "";
            
            if (deviceInfo.ClassGuid == GUID_DEVCLASS_KEYBOARD)
            {
                deviceTypePrefix = "KEYBOARD";
            }
            else if (deviceInfo.ClassGuid == GUID_DEVCLASS_MOUSE)
            {
                deviceTypePrefix = "MOUSE";
            }
            else if (deviceInfo.ClassGuid == GUID_DEVCLASS_HIDCLASS)
            {
                deviceTypePrefix = "HID";
            }
            else
            {
                deviceTypePrefix = "UNKNOWN";
            }

            // Calculate indentation based on MI and COL values
            int spaceCount = 0;
            
            // Add 5 spaces if MI exists and MI value is larger than 00
            if (!string.IsNullOrEmpty(deviceInfo.MiValue))
            {
                if (int.TryParse(deviceInfo.MiValue, out int miNum) && miNum > 0)
                {
                    spaceCount += 5;
                }
            }
            
            // Add 5 spaces if COL value exists
            if (!string.IsNullOrEmpty(deviceInfo.ColValue))
            {
                spaceCount += 5;
            }
            
            indentation = new string(' ', spaceCount);

            // Create hierarchical display with proper indentation
            debugLines.Add($"\n{indentation}{index}. {deviceTypePrefix}: " +
                $"CommonIdentifier (generated): {deviceInfo.CommonIdentifier}, " +
                $"DeviceInstanceId: {deviceInfo.DeviceInstanceId}, " +
                $"ClassGuid: {deviceInfo.ClassGuid}, " +
                FormatProperty("FriendlyName", deviceInfo.FriendlyName) +
                FormatProperty("DeviceDescription", deviceInfo.DeviceDescription) +
                FormatProperty("Manufacturer", deviceInfo.Manufacturer) +
                $"VidPidString: {deviceInfo.VidPidString}, " +
                $"VendorId: {deviceInfo.VendorId} (0x{deviceInfo.VendorId:X4}), " +
                $"ProductId: {deviceInfo.ProductId} (0x{deviceInfo.ProductId:X4}), " +
                $"SortingString (generated): {deviceInfo.SortingString}");

            debugLines.Add($"{indentation}{deviceTypePrefix} Status: " +
                $"IsPresent: {deviceInfo.IsPresent}, " +
                $"IsStarted: {deviceInfo.IsStarted}, " +
                $"StatusDescription: {deviceInfo.StatusDescription}, " +
                $"DeviceTypeName: {deviceInfo.DeviceTypeName}");

            debugLines.Add($"{indentation}{deviceTypePrefix} Hardware: " +
                FormatProperty("HardwareIds", deviceInfo.HardwareIds) +
                FormatProperty("LocationInformation", deviceInfo.LocationInformation) +
                FormatProperty("PhysicalDeviceObjectName", deviceInfo.PhysicalDeviceObjectName).TrimEnd(',', ' '));
            
            debugLines.Add($"{indentation}{deviceTypePrefix} Note: " +
                $"Windows PnP does not provide capability information (AxeCount, SliderCount, ButtonCount, PovCount, HasForceFeedback, Usage, UsagePage) - use DirectInput for device capabilities");
        }

        /// <summary>
        /// Filters out HID-class MI-only devices (USB composite parent nodes) that don't have COL values.
        /// This prevents double-counting the same physical device and removes ambiguous transport nodes.
        /// IMPORTANT: Only filters HID-class devices. Keyboard and Mouse class devices with MI are kept
        /// because they represent actual input endpoints, not transport nodes.
        /// </summary>
        /// <param name="deviceList">List of devices to filter</param>
        /// <returns>Filtered list with HID-class MI-only transport nodes removed</returns>
        private List<PnPInputDeviceInfo> FilterMiOnlyDevices(List<PnPInputDeviceInfo> deviceList)
        {
            var filteredList = new List<PnPInputDeviceInfo>();
            
            foreach (var device in deviceList)
            {
                // Check both DeviceInstanceId and HardwareIds for MI/COL patterns
                bool hasMi = !string.IsNullOrEmpty(device.MiValue) ||
                            device.DeviceInstanceId?.IndexOf("&MI_", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            device.HardwareIds?.IndexOf("&MI_", StringComparison.OrdinalIgnoreCase) >= 0;
                
                bool hasCol = !string.IsNullOrEmpty(device.ColValue) ||
                             device.DeviceInstanceId?.IndexOf("&COL", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             device.HardwareIds?.IndexOf("&COL", StringComparison.OrdinalIgnoreCase) >= 0;
                
                // Only filter HID-class devices with MI but no COL
                // Keyboard and Mouse class devices are always kept, even with MI but no COL
                if (hasMi && !hasCol && device.ClassGuid == GUID_DEVCLASS_HIDCLASS)
                {
                    // Skip this HID-class MI-only device as it's just the parent transport node
                    Debug.WriteLine($"PnPInputDevice: Filtering out HID-class MI-only transport node: {device.DeviceInstanceId}");
                    continue;
                }
                
                // Keep this device (either has COL, or is Keyboard/Mouse class, or has no MI)
                filteredList.Add(device);
            }
            
            return filteredList;
        }

        #endregion
    }
}
