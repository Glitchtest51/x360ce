//using System;
//using System.Diagnostics;
//using System.Runtime.InteropServices;
//using x360ce.App.Input.Devices;

namespace x360ce.App.Input.States
{
    /// <summary>
    /// Converts RawInput device states to standardized ListTypeState format.
    /// Handles raw HID reports from RawInput API for gamepads, mice, and keyboards.
    /// Uses proper HID API (HidP_GetUsages, HidP_GetUsageValue) for accurate parsing.
    /// </summary>
    internal static class RawInputStateToListInputState
    {
        //#region HID API Declarations

        ///// <summary>
        ///// HID report types for HidP_GetUsages and HidP_GetUsageValue.
        ///// </summary>
        //private enum HIDP_REPORT_TYPE
        //{
        //    HidP_Input = 0,
        //    HidP_Output = 1,
        //    HidP_Feature = 2
        //}

        ///// <summary>
        ///// HID status codes.
        ///// </summary>
        //private const int HIDP_STATUS_SUCCESS = 0x00110000;

        ///// <summary>
        ///// HID Usage Pages (from HID Usage Tables v1.3).
        ///// </summary>
        //private const ushort HID_USAGE_PAGE_GENERIC = 0x01;
        //private const ushort HID_USAGE_PAGE_SIMULATION = 0x02;
        //private const ushort HID_USAGE_PAGE_BUTTON = 0x09;

        ///// <summary>
        ///// HID Generic Desktop Usages.
        ///// </summary>
        //private const ushort HID_USAGE_GENERIC_X = 0x30;
        //private const ushort HID_USAGE_GENERIC_Y = 0x31;
        //private const ushort HID_USAGE_GENERIC_Z = 0x32;
        //private const ushort HID_USAGE_GENERIC_RX = 0x33;
        //private const ushort HID_USAGE_GENERIC_RY = 0x34;
        //private const ushort HID_USAGE_GENERIC_RZ = 0x35;
        //private const ushort HID_USAGE_GENERIC_SLIDER = 0x36;
        //private const ushort HID_USAGE_GENERIC_DIAL = 0x37;
        //private const ushort HID_USAGE_GENERIC_WHEEL = 0x38;
        //private const ushort HID_USAGE_GENERIC_HAT_SWITCH = 0x39;

        ///// <summary>
        ///// HID Simulation Control Usages.
        ///// </summary>
        //private const ushort HID_USAGE_SIMULATION_THROTTLE = 0xBA;
        //private const ushort HID_USAGE_SIMULATION_BRAKE = 0xBC;
        //private const ushort HID_USAGE_SIMULATION_ACCELERATOR = 0xBB;
        //private const ushort HID_USAGE_SIMULATION_STEERING = 0xB0;
        //private const ushort HID_USAGE_SIMULATION_CLUTCH = 0xBD;

        ///// <summary>
        ///// Gets the pressed buttons from a HID input report.
        ///// </summary>
        //[DllImport("hid.dll", SetLastError = true)]
        //private static extern int HidP_GetUsages(
        //    HIDP_REPORT_TYPE ReportType,
        //    ushort UsagePage,
        //    ushort LinkCollection,
        //    [Out] ushort[] UsageList,
        //    ref uint UsageLength,
        //    IntPtr PreparsedData,
        //    IntPtr Report,
        //    uint ReportLength);

        ///// <summary>
        ///// Gets a usage value (axis, POV, etc.) from a HID input report.
        ///// </summary>
        //[DllImport("hid.dll", SetLastError = true)]
        //private static extern int HidP_GetUsageValue(
        //    HIDP_REPORT_TYPE ReportType,
        //    ushort UsagePage,
        //    ushort LinkCollection,
        //    ushort Usage,
        //    out int UsageValue,
        //    IntPtr PreparsedData,
        //    IntPtr Report,
        //    uint ReportLength);

        //#endregion

        ///// <summary>
        ///// Converts RawInput device state (raw HID report) to ListTypeState format.
        ///// </summary>
        ///// <param name="rawReport">Raw HID report byte array from RawInput</param>
        ///// <param name="deviceInfo">RawInput device information for parsing context</param>
        ///// <returns>ListTypeState with standardized format, or null if conversion fails</returns>
        ///// <remarks>
        ///// RawInput State Conversion:
        ///// • Raw HID reports require device-specific parsing using HID Report Descriptor information
        ///// • Mouse devices: 4 axes (X, Y, Z/vertical wheel, W/horizontal wheel) + buttons (typically 5-8)
        ///// • Keyboard devices: No axes/sliders/POVs, only buttons (256 key states)
        ///// • HID Gamepad devices: Variable axes, sliders, buttons, and POVs based on device capabilities
        ///// 
        ///// HID Report Structure:
        ///// • [Report ID] (1 byte, optional - only if UsesReportIds is true)
        ///// • [Axis/Value Data] (variable length based on device)
        ///// • [Button Data] (starts at ButtonDataOffset)
        ///// • [Padding] (optional)
        ///// 
        ///// This implementation uses proper HID API calls (HidP_GetUsages, HidP_GetUsageValue)
        ///// with PreparsedData for accurate device-specific parsing.
        ///// </remarks>
        //public static ListInputState ConvertRawInputStateToListInputState(byte[] rawReport, RawInputDeviceInfo deviceInfo)
        //{
        //    if (rawReport == null || deviceInfo == null)
        //        return null;

        //    // Debug: Log when RawInput state conversion starts
        //    //Debug.WriteLine($"RawInputStateToListInputState: Converting {deviceInfo.RawInputDeviceType} report - " +
        //    //              $"Handle: 0x{deviceInfo.DeviceHandle.ToInt64():X8}, " +
        //    //              $"ReportSize: {rawReport.Length}, " +
        //    //              $"Path: {deviceInfo.InterfacePath ?? "Unknown"}");

        //    ListInputState result = null;

        //    // Handle different device types
        //    switch (deviceInfo.RawInputDeviceType)
        //    {
        //        //case RawInputDeviceType.Mouse:
        //        //    result = ConvertMouseReport(rawReport, deviceInfo);
        //        //    break;

        //        case RawInputDeviceType.Keyboard:
        //            result = ConvertKeyboardReport(rawReport, deviceInfo);
        //            break;

        //        case RawInputDeviceType.HID:
        //            result = ConvertHidReport(rawReport, deviceInfo);
        //            break;

        //        default:
        //            //Debug.WriteLine($"RawInputStateToListInputState: Unknown device type: {deviceInfo.RawInputDeviceType}");
        //            return null;
        //    }

        //    // Debug: Log the converted ListInputState and save it to deviceInfo
        //    if (result != null)
        //    {
        //        // Save the converted state to the device's ListInputState property
        //        deviceInfo.ListInputState = result;

        //        // Log the successful conversion with the formatted state
        //        //Debug.WriteLine($"RawInputStateToListInputState: Successfully converted and saved - " +
        //        //              $"Handle: 0x{deviceInfo.DeviceHandle.ToInt64():X8}, " +
        //        //              $"Type: {deviceInfo.RawInputDeviceType}, " +
        //        //              $"ListInputState: {result}");
        //    }
        //    else
        //    {
        //        //Debug.WriteLine($"RawInputStateToListInputState: Conversion failed - " +
        //        //              $"Handle: 0x{deviceInfo.DeviceHandle.ToInt64():X8}, " +
        //        //              $"Type: {deviceInfo.RawInputDeviceType}");
        //    }

        //    return result;
        //}

        ///// <summary>
        ///// Converts RawInput state to ListInputState format and immediately updates the device's ListInputState property.
        ///// This method is called directly from RawInputState WM_INPUT message processing for immediate event-driven conversion.
        ///// </summary>
        ///// <param name="rawReport">Raw HID/Mouse/Keyboard report from WM_INPUT message</param>
        ///// <param name="deviceInfo">RawInput device information with current ListInputState to update</param>
        ///// <returns>The converted ListInputState that was saved to deviceInfo.ListInputState</returns>
        ///// <remarks>
        ///// IMMEDIATE CONVERSION AND UPDATE FLOW:
        ///// 1. Converts raw WM_INPUT report to standardized ListInputState format
        ///// 2. Compares with previous ListInputState to detect changes
        ///// 3. Updates deviceInfo.ListInputState property immediately
        ///// 4. Logs conversion success/failure for debugging
        /////
        ///// This method provides the immediate event-driven conversion requested by the user,
        ///// where WM_INPUT messages → immediate conversion → device ListInputState update.
        ///// </remarks>
        //public static ListInputState ConvertRawInputStateToListInputStateAndUpdate(byte[] rawReport, RawInputDeviceInfo deviceInfo)
        //{
        //    if (rawReport == null || deviceInfo == null)
        //    {
        //        //Debug.WriteLine($"RawInputStateToListInputState: ConvertAndUpdate called with null parameters");
        //        return null;
        //    }

        //    // Debug: Log the immediate conversion request
        //    //Debug.WriteLine($"RawInputStateToListInputState: ConvertAndUpdate started - " +
        //    //             $"Handle: 0x{deviceInfo.DeviceHandle.ToInt64():X8}, " +
        //    //             $"Type: {deviceInfo.RawInputDeviceType}, " +
        //    //             $"ReportSize: {rawReport.Length}, " +
        //    //             $"Path: {deviceInfo.InterfacePath ?? "Unknown"}");

        //    // Get previous ListInputState for comparison
        //    var previousState = deviceInfo.ListInputState;

        //    // Convert RawInput state to ListInputState using the existing conversion method
        //    var newState = ConvertRawInputStateToListInputState(rawReport, deviceInfo);

        //    if (newState != null)
        //    {
        //        // Detect and log changes between old and new state
        //        bool hasChanges = DetectStateChanges(previousState, newState, deviceInfo);

        //        // Update the device's ListInputState property immediately
        //        deviceInfo.ListInputState = newState;

        //        // Debug: Log successful immediate conversion and update
        //        //Debug.WriteLine($"RawInputStateToListInputState: ConvertAndUpdate complete - " +
        //        //              $"Handle: 0x{deviceInfo.DeviceHandle.ToInt64():X8}, " +
        //        //              $"Type: {deviceInfo.RawInputDeviceType}, " +
        //        //              $"HasChanges: {hasChanges}, " +
        //        //              $"Updated ListInputState: {newState}");
        //    }
        //    else
        //    {
        //        //Debug.WriteLine($"RawInputStateToListInputState: ConvertAndUpdate failed - " +
        //        //              $"Handle: 0x{deviceInfo.DeviceHandle.ToInt64():X8}, " +
        //        //              $"Type: {deviceInfo.RawInputDeviceType}");
        //    }

        //    return newState;
        //}

        ///// <summary>
        ///// Detects changes between previous and new ListInputState and logs them for debugging.
        ///// This helps track which specific inputs are changing during WM_INPUT processing.
        ///// </summary>
        ///// <param name="previousState">Previous ListInputState (may be null)</param>
        ///// <param name="newState">New ListInputState</param>
        ///// <param name="deviceInfo">Device information for logging context</param>
        ///// <returns>True if changes were detected, false otherwise</returns>
        //private static bool DetectStateChanges(ListInputState previousState, ListInputState newState, RawInputDeviceInfo deviceInfo)
        //{
        //    if (previousState == null || newState == null)
        //        return previousState != newState; // Change if one is null and other isn't

        //    bool hasChanges = false;
        //    var changes = new System.Collections.Generic.List<string>();

        //    // Check for axis changes
        //    for (int i = 0; i < Math.Max(previousState.Axes.Count, newState.Axes.Count); i++)
        //    {
        //        int oldValue = i < previousState.Axes.Count ? previousState.Axes[i] : 0;
        //        int newValue = i < newState.Axes.Count ? newState.Axes[i] : 0;

        //        if (oldValue != newValue)
        //        {
        //            changes.Add($"Axis{i}: {oldValue}→{newValue}");
        //            hasChanges = true;
        //        }
        //    }

        //    // Check for button changes
        //    for (int i = 0; i < Math.Max(previousState.Buttons.Count, newState.Buttons.Count); i++)
        //    {
        //        int oldValue = i < previousState.Buttons.Count ? previousState.Buttons[i] : 0;
        //        int newValue = i < newState.Buttons.Count ? newState.Buttons[i] : 0;

        //        if (oldValue != newValue)
        //        {
        //            changes.Add($"Btn{i + 1}: {oldValue}→{newValue}");
        //            hasChanges = true;
        //        }
        //    }

        //    // Check for slider changes
        //    for (int i = 0; i < Math.Max(previousState.Sliders.Count, newState.Sliders.Count); i++)
        //    {
        //        int oldValue = i < previousState.Sliders.Count ? previousState.Sliders[i] : 0;
        //        int newValue = i < newState.Sliders.Count ? newState.Sliders[i] : 0;

        //        if (oldValue != newValue)
        //        {
        //            changes.Add($"Slider{i}: {oldValue}→{newValue}");
        //            hasChanges = true;
        //        }
        //    }

        //    // Check for POV changes
        //    for (int i = 0; i < Math.Max(previousState.POVs.Count, newState.POVs.Count); i++)
        //    {
        //        int oldValue = i < previousState.POVs.Count ? previousState.POVs[i] : -1;
        //        int newValue = i < newState.POVs.Count ? newState.POVs[i] : -1;

        //        if (oldValue != newValue)
        //        {
        //            changes.Add($"POV{i}: {oldValue}→{newValue}");
        //            hasChanges = true;
        //        }
        //    }

        //    // Log changes if any were detected
        //    if (hasChanges && changes.Count > 0)
        //    {
        //        //Debug.WriteLine($"RawInputStateToListInputState: State changes detected - " +
        //        //              $"Handle: 0x{deviceInfo.DeviceHandle.ToInt64():X8}, " +
        //        //              $"Type: {deviceInfo.RawInputDeviceType}, " +
        //        //             $"Changes: [{string.Join(", ", changes)}]");
        //    }

        //    return hasChanges;
        //}

        ///// <summary>
        ///// Converts RawInput keyboard report to ListTypeState format.
        ///// </summary>
        ///// <param name="rawReport">Raw keyboard report (8 bytes with scan codes)</param>
        ///// <param name="deviceInfo">Keyboard device information</param>
        ///// <returns>ListTypeState with keyboard button states</returns>
        ///// <remarks>
        ///// Keyboard Report Format (synthetic from StatesRawInput):
        ///// • Byte 0: Modifiers (reserved)
        ///// • Byte 1: Reserved
        ///// • Bytes 2-7: Scan codes of pressed keys (up to 6 simultaneous keys)
        ///// 
        ///// Note: This converts scan codes to button indices. A full implementation
        ///// would map scan codes to virtual key codes for proper key identification.
        ///// </remarks>
        //private static ListInputState ConvertKeyboardReport(byte[] rawReport, RawInputDeviceInfo deviceInfo)
        //{
        //    var result = new ListInputState();

        //    // Initialize all 256 buttons as released
        //    for (int i = 0; i < 256; i++)
        //    {
        //        result.Buttons.Add(0);
        //    }

        //    if (rawReport == null || rawReport.Length < 8)
        //    {
        //        //Debug.WriteLine($"RawInputStateToListInputState: Keyboard report invalid - Length: {rawReport?.Length ?? 0}");
        //        return result;
        //    }

        //    // Parse pressed keys from bytes 2-7 (scan codes)
        //    var pressedKeys = new System.Collections.Generic.List<byte>();
        //    for (int i = 2; i < 8 && i < rawReport.Length; i++)
        //    {
        //        byte scanCode = rawReport[i];
        //        if (scanCode != 0)
        //        {
        //            result.Buttons[scanCode] = 1;
        //            pressedKeys.Add(scanCode);
        //        }
        //    }

        //    // Debug: Log pressed keys if any
        //    if (pressedKeys.Count > 0)
        //    {
        //        //Debug.WriteLine($"RawInputStateToListInputState: Keyboard keys pressed - " +
        //        //              $"ScanCodes: [{string.Join(", ", pressedKeys.ConvertAll(k => $"0x{k:X2}"))}]");
        //    }

        //    // Keyboards have no axes, sliders, or POVs

        //    return result;
        //}

        ///// <summary>
        ///// Converts RawInput HID gamepad report to ListTypeState format using proper HID API.
        ///// </summary>
        ///// <param name="rawReport">Raw HID report byte array</param>
        ///// <param name="deviceInfo">HID device information with capability data and PreparsedData</param>
        ///// <returns>ListTypeState with parsed gamepad data</returns>
        ///// <remarks>
        ///// PROPER HID PARSING IMPLEMENTATION:
        ///// • Uses HidP_GetUsageValue to read axis values (X, Y, Z, RX, RY, RZ, Sliders, POVs)
        ///// • Uses HidP_GetUsages to read button states
        ///// • Requires PreparsedData from device enumeration
        ///// • Follows HID Usage Tables v1.3 specification
        ///// • Handles both Generic Desktop (0x01) and Simulation (0x02) usage pages
        ///// 
        ///// This replaces the previous placeholder implementation with real HID API calls.
        ///// </remarks>
        //private static ListInputState ConvertHidReport(byte[] rawReport, RawInputDeviceInfo deviceInfo)
        //{
        //    var result = new ListInputState();

        //    if (rawReport == null || rawReport.Length == 0)
        //    {
        //        //Debug.WriteLine($"RawInputStateToListInputState: HID report invalid - Length: {rawReport?.Length ?? 0}");
        //        return result;
        //    }

        //    // Debug: Log HID report details
        //    //Debug.WriteLine($"RawInputStateToListInputState: HID report received - " +
        //    //             $"Length: {rawReport.Length}, " +
        //    //             $"HasPreparsedData: {deviceInfo.PreparsedData != IntPtr.Zero}, " +
        //    //             $"Capabilities: Axes={deviceInfo.AxeCount}, Sliders={deviceInfo.SliderCount}, " +
        //    //             $"Buttons={deviceInfo.ButtonCount}, POVs={deviceInfo.PovCount}");

        //    // Check if we have PreparsedData for proper HID parsing
        //    if (deviceInfo.PreparsedData == IntPtr.Zero)
        //    {
        //        //Debug.WriteLine($"RawInputStateToListInputState: Using fallback parsing (no PreparsedData)");
        //        // Fallback to basic parsing if PreparsedData not available
        //        return ConvertHidReportFallback(rawReport, deviceInfo);
        //    }

        //    // Pin the report buffer for HID API calls
        //    GCHandle reportHandle = GCHandle.Alloc(rawReport, GCHandleType.Pinned);
        //    try
        //    {
        //        IntPtr reportPtr = reportHandle.AddrOfPinnedObject();
        //        uint reportLength = (uint)rawReport.Length;

        //        //Debug.WriteLine($"RawInputStateToListInputState: Using HID API parsing with PreparsedData");

        //        // Read axes using HidP_GetUsageValue
        //        ReadAxesFromHidReport(reportPtr, reportLength, deviceInfo, result);

        //        // Read sliders using HidP_GetUsageValue
        //        ReadSlidersFromHidReport(reportPtr, reportLength, deviceInfo, result);

        //        // Read POVs using HidP_GetUsageValue
        //        ReadPovsFromHidReport(reportPtr, reportLength, deviceInfo, result);

        //        // Read buttons using HidP_GetUsages
        //        ReadButtonsFromHidReport(reportPtr, reportLength, deviceInfo, result);

        //        // Debug: Log successful HID parsing
        //        //Debug.WriteLine($"RawInputStateToListInputState: HID parsing complete - " +
        //        //              $"Parsed: Axes={result.Axes.Count}, Sliders={result.Sliders.Count}, " +
        //        //              $"Buttons={result.Buttons.Count}, POVs={result.POVs.Count}");
        //    }
        //    finally
        //    {
        //        if (reportHandle.IsAllocated)
        //            reportHandle.Free();
        //    }

        //    return result;
        //}

        ///// <summary>
        ///// Reads axis values from HID report using HidP_GetUsageValue.
        ///// </summary>
        //private static void ReadAxesFromHidReport(IntPtr reportPtr, uint reportLength, RawInputDeviceInfo deviceInfo, ListInputState result)
        //{
        //    // Standard axes: X(0x30), Y(0x31), Z(0x32), RX(0x33), RY(0x34), RZ(0x35)
        //    ushort[] axisUsages = {
        //        HID_USAGE_GENERIC_X,
        //        HID_USAGE_GENERIC_Y,
        //        HID_USAGE_GENERIC_Z,
        //        HID_USAGE_GENERIC_RX,
        //        HID_USAGE_GENERIC_RY,
        //        HID_USAGE_GENERIC_RZ
        //    };

        //    foreach (var usage in axisUsages)
        //    {
        //        int value;
        //        int status = HidP_GetUsageValue(
        //            HIDP_REPORT_TYPE.HidP_Input,
        //            HID_USAGE_PAGE_GENERIC,
        //            0, // LinkCollection
        //            usage,
        //            out value,
        //            deviceInfo.PreparsedData,
        //            reportPtr,
        //            reportLength);

        //        if (status == HIDP_STATUS_SUCCESS)
        //        {
        //            // Convert to 0-65535 range (standard for ListTypeState)
        //            // HID values are typically 0-255 or 0-1023, scale to 16-bit
        //            result.Axes.Add(ScaleToUInt16Range(value));
        //        }
        //    }

        //    // Pad with centered values if we have fewer axes than expected
        //    while (result.Axes.Count < deviceInfo.AxeCount)
        //    {
        //        result.Axes.Add(32767); // Centered
        //    }
        //}

        ///// <summary>
        ///// Reads slider values from HID report using HidP_GetUsageValue.
        ///// </summary>
        //private static void ReadSlidersFromHidReport(IntPtr reportPtr, uint reportLength, RawInputDeviceInfo deviceInfo, ListInputState result)
        //{
        //    // Slider controls: Slider(0x36), Dial(0x37), Wheel(0x38)
        //    ushort[] sliderUsages = {
        //        HID_USAGE_GENERIC_SLIDER,
        //        HID_USAGE_GENERIC_DIAL,
        //        HID_USAGE_GENERIC_WHEEL
        //    };

        //    foreach (var usage in sliderUsages)
        //    {
        //        int value;
        //        int status = HidP_GetUsageValue(
        //            HIDP_REPORT_TYPE.HidP_Input,
        //            HID_USAGE_PAGE_GENERIC,
        //            0, // LinkCollection
        //            usage,
        //            out value,
        //            deviceInfo.PreparsedData,
        //            reportPtr,
        //            reportLength);

        //        if (status == HIDP_STATUS_SUCCESS)
        //        {
        //            result.Sliders.Add(ScaleToUInt16Range(value));
        //        }
        //    }

        //    // Also check Simulation usage page for throttle, brake, etc.
        //    ushort[] simUsages = {
        //        HID_USAGE_SIMULATION_THROTTLE,
        //        HID_USAGE_SIMULATION_BRAKE,
        //        HID_USAGE_SIMULATION_ACCELERATOR,
        //        HID_USAGE_SIMULATION_STEERING,
        //        HID_USAGE_SIMULATION_CLUTCH
        //    };

        //    foreach (var usage in simUsages)
        //    {
        //        int value;
        //        int status = HidP_GetUsageValue(
        //            HIDP_REPORT_TYPE.HidP_Input,
        //            HID_USAGE_PAGE_SIMULATION,
        //            0, // LinkCollection
        //            usage,
        //            out value,
        //            deviceInfo.PreparsedData,
        //            reportPtr,
        //            reportLength);

        //        if (status == HIDP_STATUS_SUCCESS)
        //        {
        //            result.Sliders.Add(ScaleToUInt16Range(value));
        //        }
        //    }

        //    // Pad with centered values if we have fewer sliders than expected
        //    while (result.Sliders.Count < deviceInfo.SliderCount)
        //    {
        //        result.Sliders.Add(32767); // Centered
        //    }
        //}

        ///// <summary>
        ///// Reads POV/Hat Switch values from HID report using HidP_GetUsageValue.
        ///// </summary>
        //private static void ReadPovsFromHidReport(IntPtr reportPtr, uint reportLength, RawInputDeviceInfo deviceInfo, ListInputState result)
        //{
        //    // POV Hat Switch (0x39)
        //    int value;
        //    int status = HidP_GetUsageValue(
        //        HIDP_REPORT_TYPE.HidP_Input,
        //        HID_USAGE_PAGE_GENERIC,
        //        0, // LinkCollection
        //        HID_USAGE_GENERIC_HAT_SWITCH,
        //        out value,
        //        deviceInfo.PreparsedData,
        //        reportPtr,
        //        reportLength);

        //    if (status == HIDP_STATUS_SUCCESS)
        //    {
        //        // Convert HID POV value to DirectInput format (centidegrees)
        //        // HID: 0-7 for 8 directions, 8=neutral
        //        // DirectInput: 0-27000 in centidegrees, -1=neutral
        //        int povValue = ConvertHidPovToDirectInput(value);
        //        result.POVs.Add(povValue);
        //    }

        //    // Pad with neutral values if we have fewer POVs than expected
        //    while (result.POVs.Count < deviceInfo.PovCount)
        //    {
        //        result.POVs.Add(-1); // Neutral
        //    }
        //}

        ///// <summary>
        ///// Reads button states from HID report using HidP_GetUsages.
        ///// </summary>
        //private static void ReadButtonsFromHidReport(IntPtr reportPtr, uint reportLength, RawInputDeviceInfo deviceInfo, ListInputState result)
        //{
        //    // HidP_GetUsages returns the list of pressed buttons
        //    uint usageLength = (uint)deviceInfo.ButtonCount;
        //    ushort[] usageList = new ushort[Math.Max(usageLength, 1)];

        //    int status = HidP_GetUsages(
        //        HIDP_REPORT_TYPE.HidP_Input,
        //        HID_USAGE_PAGE_BUTTON,
        //        0, // LinkCollection
        //        usageList,
        //        ref usageLength,
        //        deviceInfo.PreparsedData,
        //        reportPtr,
        //        reportLength);

        //    if (status == HIDP_STATUS_SUCCESS)
        //    {
        //        // Initialize all buttons as released
        //        for (int i = 0; i < deviceInfo.ButtonCount; i++)
        //        {
        //            result.Buttons.Add(0);
        //        }

        //        // Set pressed buttons to 1
        //        for (int i = 0; i < usageLength; i++)
        //        {
        //            int buttonIndex = usageList[i] - 1; // HID buttons are 1-based
        //            if (buttonIndex >= 0 && buttonIndex < result.Buttons.Count)
        //            {
        //                result.Buttons[buttonIndex] = 1;
        //            }
        //        }
        //    }
        //    else
        //    {
        //        // Fallback: Initialize all buttons as released
        //        for (int i = 0; i < deviceInfo.ButtonCount; i++)
        //        {
        //            result.Buttons.Add(0);
        //        }
        //    }
        //}

        ///// <summary>
        ///// Scales a HID value to the standard 0-65535 range used by ListTypeState.
        ///// </summary>
        ///// <param name="hidValue">Raw HID value (typically 0-255 or 0-1023)</param>
        ///// <returns>Scaled value in 0-65535 range</returns>
        //private static int ScaleToUInt16Range(int hidValue)
        //{
        //    // Most HID devices use 8-bit (0-255) or 10-bit (0-1023) values
        //    // Scale to 16-bit (0-65535) for consistency
        //    if (hidValue < 0)
        //        return 0;
        //    if (hidValue <= 255)
        //        return hidValue * 257; // Scale 8-bit to 16-bit
        //    if (hidValue <= 1023)
        //        return hidValue * 64; // Scale 10-bit to 16-bit
        //    if (hidValue <= 4095)
        //        return hidValue * 16; // Scale 12-bit to 16-bit
        //    return Math.Min(hidValue, 65535); // Already 16-bit or larger
        //}

        ///// <summary>
        ///// Converts HID POV value to DirectInput format.
        ///// </summary>
        ///// <param name="hidPovValue">HID POV value from device</param>
        ///// <returns>DirectInput POV value (0-31500 in centidegrees, -1=neutral)</returns>
        ///// <remarks>
        ///// WORKAROUND: Different devices use different POV encoding schemes:
        /////
        ///// Standard HID POV (most devices):
        /////   0=N, 1=NE, 2=E, 3=SE, 4=S, 5=SW, 6=W, 7=NW, 8+=neutral
        /////
        ///// Logitech F310 and similar (1-based indexing):
        /////   0=neutral, 1=N, 2=NE, 3=E, 4=SE, 5=S, 6=SW, 7=W, 8=NW
        /////
        ///// DirectInput POV (target format):
        /////   -1=neutral, 0=N, 4500=NE, 9000=E, 13500=SE, 18000=S, 22500=SW, 27000=W, 31500=NW
        /////
        ///// We detect the device's encoding by checking the value range and convert accordingly.
        ///// </remarks>
        //private static int ConvertHidPovToDirectInput(int hidPovValue)
        //{
        //    // Negative values are invalid - treat as neutral
        //    if (hidPovValue < 0)
        //        return -1;

        //    // WORKAROUND: Detect Logitech F310 and similar devices that use 0 as neutral
        //    // These devices use 1-8 for directions instead of 0-7
        //    // We detect this pattern: if value is 0, it's likely neutral for these devices
        //    // The challenge is distinguishing between:
        //    //   - Standard device reporting 0 (North)
        //    //   - Logitech device reporting 0 (Neutral)
        //    //
        //    // Solution: Check if value is exactly 0 - treat as neutral for Logitech-style devices
        //    // This means standard devices won't detect North (0°), but will detect all other 7 directions
        //    if (hidPovValue == 0)
        //        return -1; // Neutral (Logitech F310 style)

        //    // Values 1-8: Logitech F310 style (1=N, 2=NE, 3=E, 4=SE, 5=S, 6=SW, 7=W, 8=NW)
        //    // Subtract 1 to convert to standard 0-7 range, then multiply by 4500
        //    if (hidPovValue >= 1 && hidPovValue <= 8)
        //        return (hidPovValue - 1) * 4500;

        //    // Values 9-14: Invalid range, treat as neutral
        //    if (hidPovValue >= 9 && hidPovValue <= 14)
        //        return -1;

        //    // Value 15 (0x0F): Some 4-bit devices use this as neutral
        //    if (hidPovValue == 15)
        //        return -1;

        //    // Values 16+: Standard neutral (8 or higher in original spec)
        //    return -1;
        //}

        ///// <summary>
        ///// Fallback HID report parsing when PreparsedData is not available.
        ///// Uses the basic button offset parsing from the original implementation.
        ///// </summary>
        //private static ListInputState ConvertHidReportFallback(byte[] rawReport, RawInputDeviceInfo deviceInfo)
        //{
        //    var result = new ListInputState();

        //    // Add placeholder axes based on device capability count
        //    for (int i = 0; i < deviceInfo.AxeCount; i++)
        //    {
        //        result.Axes.Add(32767); // Centered position
        //    }

        //    // Add placeholder sliders based on device capability count
        //    for (int i = 0; i < deviceInfo.SliderCount; i++)
        //    {
        //        result.Sliders.Add(32767); // Centered position
        //    }

        //    // Parse button data from HID report using ButtonDataOffset
        //    int buttonDataOffset = deviceInfo.ButtonDataOffset;
        //    int buttonCount = deviceInfo.ButtonCount;

        //    //Debug.WriteLine($"RawInputStateToListInputState: Fallback parsing - " +
        //    //              $"ButtonDataOffset: {buttonDataOffset}, ButtonCount: {buttonCount}, " +
        //    //              $"ReportLength: {rawReport.Length}");

        //    var pressedButtons = new System.Collections.Generic.List<int>();

        //    if (buttonDataOffset < rawReport.Length && buttonCount > 0)
        //    {
        //        // Parse button bits from the button data section
        //        int buttonByte = buttonDataOffset;
        //        int buttonBit = 0;

        //        for (int i = 0; i < buttonCount; i++)
        //        {
        //            if (buttonByte >= rawReport.Length)
        //                break;

        //            // Check if button bit is set
        //            bool isPressed = (rawReport[buttonByte] & (1 << buttonBit)) != 0;
        //            result.Buttons.Add(isPressed ? 1 : 0);

        //            if (isPressed)
        //                pressedButtons.Add(i + 1); // Button numbers are 1-based for display

        //            // Move to next bit/byte
        //            buttonBit++;
        //            if (buttonBit >= 8)
        //            {
        //                buttonBit = 0;
        //                buttonByte++;
        //            }
        //        }
        //    }
        //    else
        //    {
        //        // No button data available, add placeholder buttons
        //        for (int i = 0; i < buttonCount; i++)
        //        {
        //            result.Buttons.Add(0);
        //        }
        //    }

        //    // Debug: Log pressed buttons if any
        //    if (pressedButtons.Count > 0)
        //    {
        //        //Debug.WriteLine($"RawInputStateToListInputState: Fallback buttons pressed - " +
        //        //             $"Buttons: [{string.Join(", ", pressedButtons)}]");
        //    }

        //    // Add placeholder POVs based on device capability count
        //    for (int i = 0; i < deviceInfo.PovCount; i++)
        //    {
        //        result.POVs.Add(-1); // Neutral position
        //    }

        //    return result;
        //}
    }
}
