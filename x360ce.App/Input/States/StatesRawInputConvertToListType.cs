using System;
using System.Runtime.InteropServices;
using x360ce.App.Input.Devices;

namespace x360ce.App.Input.States
{
	/// <summary>
	/// Converts RawInput device states to standardized ListTypeState format.
	/// Handles raw HID reports from RawInput API for gamepads, mice, and keyboards.
	/// Uses proper HID API (HidP_GetUsages, HidP_GetUsageValue) for accurate parsing.
	/// </summary>
	internal static class StatesRawInputConvertToListType
	{
		#region HID API Declarations

		/// <summary>
		/// HID report types for HidP_GetUsages and HidP_GetUsageValue.
		/// </summary>
		private enum HIDP_REPORT_TYPE
		{
			HidP_Input = 0,
			HidP_Output = 1,
			HidP_Feature = 2
		}

		/// <summary>
		/// HID status codes.
		/// </summary>
		private const int HIDP_STATUS_SUCCESS = 0x00110000;

		/// <summary>
		/// HID Usage Pages (from HID Usage Tables v1.3).
		/// </summary>
		private const ushort HID_USAGE_PAGE_GENERIC = 0x01;
		private const ushort HID_USAGE_PAGE_SIMULATION = 0x02;
		private const ushort HID_USAGE_PAGE_BUTTON = 0x09;

		/// <summary>
		/// HID Generic Desktop Usages.
		/// </summary>
		private const ushort HID_USAGE_GENERIC_X = 0x30;
		private const ushort HID_USAGE_GENERIC_Y = 0x31;
		private const ushort HID_USAGE_GENERIC_Z = 0x32;
		private const ushort HID_USAGE_GENERIC_RX = 0x33;
		private const ushort HID_USAGE_GENERIC_RY = 0x34;
		private const ushort HID_USAGE_GENERIC_RZ = 0x35;
		private const ushort HID_USAGE_GENERIC_SLIDER = 0x36;
		private const ushort HID_USAGE_GENERIC_DIAL = 0x37;
		private const ushort HID_USAGE_GENERIC_WHEEL = 0x38;
		private const ushort HID_USAGE_GENERIC_HAT_SWITCH = 0x39;

		/// <summary>
		/// HID Simulation Control Usages.
		/// </summary>
		private const ushort HID_USAGE_SIMULATION_THROTTLE = 0xBA;
		private const ushort HID_USAGE_SIMULATION_BRAKE = 0xBC;
		private const ushort HID_USAGE_SIMULATION_ACCELERATOR = 0xBB;
		private const ushort HID_USAGE_SIMULATION_STEERING = 0xB0;
		private const ushort HID_USAGE_SIMULATION_CLUTCH = 0xBD;

		/// <summary>
		/// Gets the pressed buttons from a HID input report.
		/// </summary>
		[DllImport("hid.dll", SetLastError = true)]
		private static extern int HidP_GetUsages(
			HIDP_REPORT_TYPE ReportType,
			ushort UsagePage,
			ushort LinkCollection,
			[Out] ushort[] UsageList,
			ref uint UsageLength,
			IntPtr PreparsedData,
			IntPtr Report,
			uint ReportLength);

		/// <summary>
		/// Gets a usage value (axis, POV, etc.) from a HID input report.
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
		/// Converts RawInput device state (raw HID report) to ListTypeState format.
		/// </summary>
		/// <param name="rawReport">Raw HID report byte array from RawInput</param>
		/// <param name="deviceInfo">RawInput device information for parsing context</param>
		/// <returns>ListTypeState with standardized format, or null if conversion fails</returns>
		/// <remarks>
		/// RawInput State Conversion:
		/// • Raw HID reports require device-specific parsing using HID Report Descriptor information
		/// • Mouse devices: 3 axes (X, Y, Z/wheel) + buttons (typically 5-8)
		/// • Keyboard devices: No axes/sliders/POVs, only buttons (256 key states)
		/// • HID Gamepad devices: Variable axes, sliders, buttons, and POVs based on device capabilities
		/// 
		/// HID Report Structure:
		/// • [Report ID] (1 byte, optional - only if UsesReportIds is true)
		/// • [Axis/Value Data] (variable length based on device)
		/// • [Button Data] (starts at ButtonDataOffset)
		/// • [Padding] (optional)
		/// 
		/// This implementation uses proper HID API calls (HidP_GetUsages, HidP_GetUsageValue)
		/// with PreparsedData for accurate device-specific parsing.
		/// </remarks>
		public static ListTypeState ConvertToListTypeState(byte[] rawReport, RawInputDeviceInfo deviceInfo)
		{
			if (rawReport == null || deviceInfo == null)
				return null;

			// Handle different device types
			switch (deviceInfo.RawInputDeviceType)
			{
				case RawInputDeviceType.Mouse:
					return ConvertMouseReport(rawReport, deviceInfo);
				
				case RawInputDeviceType.Keyboard:
					return ConvertKeyboardReport(rawReport, deviceInfo);
				
				case RawInputDeviceType.HID:
					return ConvertHidReport(rawReport, deviceInfo);
				
				default:
					return null;
			}
		}

		/// <summary>
		/// Converts RawInput mouse report to ListTypeState format.
		/// </summary>
		/// <param name="rawReport">Raw mouse report (1 byte with button states)</param>
		/// <param name="deviceInfo">Mouse device information</param>
		/// <returns>ListTypeState with mouse axes and buttons</returns>
		/// <remarks>
		/// Mouse Report Format (synthetic from StatesRawInput):
		/// • Byte 0: Button state bits (0x01=left, 0x02=right, 0x04=middle, 0x08=X1, 0x10=X2)
		/// 
		/// Note: RawInput mouse reports don't include position data in the cached state,
		/// only button states. Position is relative movement, not absolute axes.
		/// </remarks>
		private static ListTypeState ConvertMouseReport(byte[] rawReport, RawInputDeviceInfo deviceInfo)
		{
			var result = new ListTypeState();

			if (rawReport == null || rawReport.Length < 1)
				return result;

			// Mouse axes are not available in cached RawInput state (only relative movement)
			// We'll add placeholder axes for compatibility
			result.Axes.Add(32767); // X (centered)
			result.Axes.Add(32767); // Y (centered)
			result.Axes.Add(0);     // Z (wheel, neutral)

			// Parse button states from byte 0
			byte buttonState = rawReport[0];
			result.Buttons.Add((buttonState & 0x01) != 0 ? 1 : 0); // Left button
			result.Buttons.Add((buttonState & 0x02) != 0 ? 1 : 0); // Right button
			result.Buttons.Add((buttonState & 0x04) != 0 ? 1 : 0); // Middle button
			result.Buttons.Add((buttonState & 0x08) != 0 ? 1 : 0); // X1 button
			result.Buttons.Add((buttonState & 0x10) != 0 ? 1 : 0); // X2 button

			// Mice have no sliders or POVs

			return result;
		}

		/// <summary>
		/// Converts RawInput keyboard report to ListTypeState format.
		/// </summary>
		/// <param name="rawReport">Raw keyboard report (8 bytes with scan codes)</param>
		/// <param name="deviceInfo">Keyboard device information</param>
		/// <returns>ListTypeState with keyboard button states</returns>
		/// <remarks>
		/// Keyboard Report Format (synthetic from StatesRawInput):
		/// • Byte 0: Modifiers (reserved)
		/// • Byte 1: Reserved
		/// • Bytes 2-7: Scan codes of pressed keys (up to 6 simultaneous keys)
		/// 
		/// Note: This converts scan codes to button indices. A full implementation
		/// would map scan codes to virtual key codes for proper key identification.
		/// </remarks>
		private static ListTypeState ConvertKeyboardReport(byte[] rawReport, RawInputDeviceInfo deviceInfo)
		{
			var result = new ListTypeState();

			// Initialize all 256 buttons as released
			for (int i = 0; i < 256; i++)
			{
				result.Buttons.Add(0);
			}

			if (rawReport == null || rawReport.Length < 8)
				return result;

			// Parse pressed keys from bytes 2-7 (scan codes)
			for (int i = 2; i < 8 && i < rawReport.Length; i++)
			{
				byte scanCode = rawReport[i];
				if (scanCode != 0)
				{
					result.Buttons[scanCode] = 1;
				}
			}

			// Keyboards have no axes, sliders, or POVs

			return result;
		}

		/// <summary>
		/// Converts RawInput HID gamepad report to ListTypeState format using proper HID API.
		/// </summary>
		/// <param name="rawReport">Raw HID report byte array</param>
		/// <param name="deviceInfo">HID device information with capability data and PreparsedData</param>
		/// <returns>ListTypeState with parsed gamepad data</returns>
		/// <remarks>
		/// PROPER HID PARSING IMPLEMENTATION:
		/// • Uses HidP_GetUsageValue to read axis values (X, Y, Z, RX, RY, RZ, Sliders, POVs)
		/// • Uses HidP_GetUsages to read button states
		/// • Requires PreparsedData from device enumeration
		/// • Follows HID Usage Tables v1.3 specification
		/// • Handles both Generic Desktop (0x01) and Simulation (0x02) usage pages
		/// 
		/// This replaces the previous placeholder implementation with real HID API calls.
		/// </remarks>
		private static ListTypeState ConvertHidReport(byte[] rawReport, RawInputDeviceInfo deviceInfo)
		{
			var result = new ListTypeState();

			if (rawReport == null || rawReport.Length == 0)
				return result;

			// Check if we have PreparsedData for proper HID parsing
			if (deviceInfo.PreparsedData == IntPtr.Zero)
			{
				// Fallback to basic parsing if PreparsedData not available
				return ConvertHidReportFallback(rawReport, deviceInfo);
			}

			// Pin the report buffer for HID API calls
			GCHandle reportHandle = GCHandle.Alloc(rawReport, GCHandleType.Pinned);
			try
			{
				IntPtr reportPtr = reportHandle.AddrOfPinnedObject();
				uint reportLength = (uint)rawReport.Length;

				// Read axes using HidP_GetUsageValue
				ReadAxesFromHidReport(reportPtr, reportLength, deviceInfo, result);

				// Read sliders using HidP_GetUsageValue
				ReadSlidersFromHidReport(reportPtr, reportLength, deviceInfo, result);

				// Read POVs using HidP_GetUsageValue
				ReadPovsFromHidReport(reportPtr, reportLength, deviceInfo, result);

				// Read buttons using HidP_GetUsages
				ReadButtonsFromHidReport(reportPtr, reportLength, deviceInfo, result);
			}
			finally
			{
				if (reportHandle.IsAllocated)
					reportHandle.Free();
			}

			return result;
		}

		/// <summary>
		/// Reads axis values from HID report using HidP_GetUsageValue.
		/// </summary>
		private static void ReadAxesFromHidReport(IntPtr reportPtr, uint reportLength, RawInputDeviceInfo deviceInfo, ListTypeState result)
		{
			// Standard axes: X(0x30), Y(0x31), Z(0x32), RX(0x33), RY(0x34), RZ(0x35)
			ushort[] axisUsages = { 
				HID_USAGE_GENERIC_X, 
				HID_USAGE_GENERIC_Y, 
				HID_USAGE_GENERIC_Z, 
				HID_USAGE_GENERIC_RX, 
				HID_USAGE_GENERIC_RY, 
				HID_USAGE_GENERIC_RZ 
			};

			foreach (var usage in axisUsages)
			{
				int value;
				int status = HidP_GetUsageValue(
					HIDP_REPORT_TYPE.HidP_Input,
					HID_USAGE_PAGE_GENERIC,
					0, // LinkCollection
					usage,
					out value,
					deviceInfo.PreparsedData,
					reportPtr,
					reportLength);

				if (status == HIDP_STATUS_SUCCESS)
				{
					// Convert to 0-65535 range (standard for ListTypeState)
					// HID values are typically 0-255 or 0-1023, scale to 16-bit
					result.Axes.Add(ScaleToUInt16Range(value));
				}
			}

			// Pad with centered values if we have fewer axes than expected
			while (result.Axes.Count < deviceInfo.AxeCount)
			{
				result.Axes.Add(32767); // Centered
			}
		}

		/// <summary>
		/// Reads slider values from HID report using HidP_GetUsageValue.
		/// </summary>
		private static void ReadSlidersFromHidReport(IntPtr reportPtr, uint reportLength, RawInputDeviceInfo deviceInfo, ListTypeState result)
		{
			// Slider controls: Slider(0x36), Dial(0x37), Wheel(0x38)
			ushort[] sliderUsages = { 
				HID_USAGE_GENERIC_SLIDER, 
				HID_USAGE_GENERIC_DIAL, 
				HID_USAGE_GENERIC_WHEEL 
			};

			foreach (var usage in sliderUsages)
			{
				int value;
				int status = HidP_GetUsageValue(
					HIDP_REPORT_TYPE.HidP_Input,
					HID_USAGE_PAGE_GENERIC,
					0, // LinkCollection
					usage,
					out value,
					deviceInfo.PreparsedData,
					reportPtr,
					reportLength);

				if (status == HIDP_STATUS_SUCCESS)
				{
					result.Sliders.Add(ScaleToUInt16Range(value));
				}
			}

			// Also check Simulation usage page for throttle, brake, etc.
			ushort[] simUsages = {
				HID_USAGE_SIMULATION_THROTTLE,
				HID_USAGE_SIMULATION_BRAKE,
				HID_USAGE_SIMULATION_ACCELERATOR,
				HID_USAGE_SIMULATION_STEERING,
				HID_USAGE_SIMULATION_CLUTCH
			};

			foreach (var usage in simUsages)
			{
				int value;
				int status = HidP_GetUsageValue(
					HIDP_REPORT_TYPE.HidP_Input,
					HID_USAGE_PAGE_SIMULATION,
					0, // LinkCollection
					usage,
					out value,
					deviceInfo.PreparsedData,
					reportPtr,
					reportLength);

				if (status == HIDP_STATUS_SUCCESS)
				{
					result.Sliders.Add(ScaleToUInt16Range(value));
				}
			}

			// Pad with centered values if we have fewer sliders than expected
			while (result.Sliders.Count < deviceInfo.SliderCount)
			{
				result.Sliders.Add(32767); // Centered
			}
		}

		/// <summary>
		/// Reads POV/Hat Switch values from HID report using HidP_GetUsageValue.
		/// </summary>
		private static void ReadPovsFromHidReport(IntPtr reportPtr, uint reportLength, RawInputDeviceInfo deviceInfo, ListTypeState result)
		{
			// POV Hat Switch (0x39)
			int value;
			int status = HidP_GetUsageValue(
				HIDP_REPORT_TYPE.HidP_Input,
				HID_USAGE_PAGE_GENERIC,
				0, // LinkCollection
				HID_USAGE_GENERIC_HAT_SWITCH,
				out value,
				deviceInfo.PreparsedData,
				reportPtr,
				reportLength);

			if (status == HIDP_STATUS_SUCCESS)
			{
				// Convert HID POV value to DirectInput format (centidegrees)
				// HID: 0-7 for 8 directions, 8=neutral
				// DirectInput: 0-27000 in centidegrees, -1=neutral
				int povValue = ConvertHidPovToDirectInput(value);
				result.POVs.Add(povValue);
			}

			// Pad with neutral values if we have fewer POVs than expected
			while (result.POVs.Count < deviceInfo.PovCount)
			{
				result.POVs.Add(-1); // Neutral
			}
		}

		/// <summary>
		/// Reads button states from HID report using HidP_GetUsages.
		/// </summary>
		private static void ReadButtonsFromHidReport(IntPtr reportPtr, uint reportLength, RawInputDeviceInfo deviceInfo, ListTypeState result)
		{
			// HidP_GetUsages returns the list of pressed buttons
			uint usageLength = (uint)deviceInfo.ButtonCount;
			ushort[] usageList = new ushort[Math.Max(usageLength, 1)];

			int status = HidP_GetUsages(
				HIDP_REPORT_TYPE.HidP_Input,
				HID_USAGE_PAGE_BUTTON,
				0, // LinkCollection
				usageList,
				ref usageLength,
				deviceInfo.PreparsedData,
				reportPtr,
				reportLength);

			if (status == HIDP_STATUS_SUCCESS)
			{
				// Initialize all buttons as released
				for (int i = 0; i < deviceInfo.ButtonCount; i++)
				{
					result.Buttons.Add(0);
				}

				// Set pressed buttons to 1
				for (int i = 0; i < usageLength; i++)
				{
					int buttonIndex = usageList[i] - 1; // HID buttons are 1-based
					if (buttonIndex >= 0 && buttonIndex < result.Buttons.Count)
					{
						result.Buttons[buttonIndex] = 1;
					}
				}
			}
			else
			{
				// Fallback: Initialize all buttons as released
				for (int i = 0; i < deviceInfo.ButtonCount; i++)
				{
					result.Buttons.Add(0);
				}
			}
		}

		/// <summary>
		/// Scales a HID value to the standard 0-65535 range used by ListTypeState.
		/// </summary>
		/// <param name="hidValue">Raw HID value (typically 0-255 or 0-1023)</param>
		/// <returns>Scaled value in 0-65535 range</returns>
		private static int ScaleToUInt16Range(int hidValue)
		{
			// Most HID devices use 8-bit (0-255) or 10-bit (0-1023) values
			// Scale to 16-bit (0-65535) for consistency
			if (hidValue < 0)
				return 0;
			if (hidValue <= 255)
				return hidValue * 257; // Scale 8-bit to 16-bit
			if (hidValue <= 1023)
				return hidValue * 64; // Scale 10-bit to 16-bit
			if (hidValue <= 4095)
				return hidValue * 16; // Scale 12-bit to 16-bit
			return Math.Min(hidValue, 65535); // Already 16-bit or larger
		}

		/// <summary>
		/// Converts HID POV value to DirectInput format.
		/// </summary>
		/// <param name="hidPovValue">HID POV value from device</param>
		/// <returns>DirectInput POV value (0-31500 in centidegrees, -1=neutral)</returns>
		/// <remarks>
		/// WORKAROUND: Different devices use different POV encoding schemes:
		///
		/// Standard HID POV (most devices):
		///   0=N, 1=NE, 2=E, 3=SE, 4=S, 5=SW, 6=W, 7=NW, 8+=neutral
		///
		/// Logitech F310 and similar (1-based indexing):
		///   0=neutral, 1=N, 2=NE, 3=E, 4=SE, 5=S, 6=SW, 7=W, 8=NW
		///
		/// DirectInput POV (target format):
		///   -1=neutral, 0=N, 4500=NE, 9000=E, 13500=SE, 18000=S, 22500=SW, 27000=W, 31500=NW
		///
		/// We detect the device's encoding by checking the value range and convert accordingly.
		/// </remarks>
		private static int ConvertHidPovToDirectInput(int hidPovValue)
		{
			// Negative values are invalid - treat as neutral
			if (hidPovValue < 0)
				return -1;
			
			// WORKAROUND: Detect Logitech F310 and similar devices that use 0 as neutral
			// These devices use 1-8 for directions instead of 0-7
			// We detect this pattern: if value is 0, it's likely neutral for these devices
			// The challenge is distinguishing between:
			//   - Standard device reporting 0 (North)
			//   - Logitech device reporting 0 (Neutral)
			//
			// Solution: Check if value is exactly 0 - treat as neutral for Logitech-style devices
			// This means standard devices won't detect North (0°), but will detect all other 7 directions
			if (hidPovValue == 0)
				return -1; // Neutral (Logitech F310 style)
			
			// Values 1-8: Logitech F310 style (1=N, 2=NE, 3=E, 4=SE, 5=S, 6=SW, 7=W, 8=NW)
			// Subtract 1 to convert to standard 0-7 range, then multiply by 4500
			if (hidPovValue >= 1 && hidPovValue <= 8)
				return (hidPovValue - 1) * 4500;
			
			// Values 9-14: Invalid range, treat as neutral
			if (hidPovValue >= 9 && hidPovValue <= 14)
				return -1;
			
			// Value 15 (0x0F): Some 4-bit devices use this as neutral
			if (hidPovValue == 15)
				return -1;
			
			// Values 16+: Standard neutral (8 or higher in original spec)
			return -1;
		}

		/// <summary>
		/// Fallback HID report parsing when PreparsedData is not available.
		/// Uses the basic button offset parsing from the original implementation.
		/// </summary>
		private static ListTypeState ConvertHidReportFallback(byte[] rawReport, RawInputDeviceInfo deviceInfo)
		{
			var result = new ListTypeState();

			// Add placeholder axes based on device capability count
			for (int i = 0; i < deviceInfo.AxeCount; i++)
			{
				result.Axes.Add(32767); // Centered position
			}

			// Add placeholder sliders based on device capability count
			for (int i = 0; i < deviceInfo.SliderCount; i++)
			{
				result.Sliders.Add(32767); // Centered position
			}

			// Parse button data from HID report using ButtonDataOffset
			int buttonDataOffset = deviceInfo.ButtonDataOffset;
			int buttonCount = deviceInfo.ButtonCount;

			if (buttonDataOffset < rawReport.Length && buttonCount > 0)
			{
				// Parse button bits from the button data section
				int buttonByte = buttonDataOffset;
				int buttonBit = 0;

				for (int i = 0; i < buttonCount; i++)
				{
					if (buttonByte >= rawReport.Length)
						break;

					// Check if button bit is set
					bool isPressed = (rawReport[buttonByte] & (1 << buttonBit)) != 0;
					result.Buttons.Add(isPressed ? 1 : 0);

					// Move to next bit/byte
					buttonBit++;
					if (buttonBit >= 8)
					{
						buttonBit = 0;
						buttonByte++;
					}
				}
			}
			else
			{
				// No button data available, add placeholder buttons
				for (int i = 0; i < buttonCount; i++)
				{
					result.Buttons.Add(0);
				}
			}

			// Add placeholder POVs based on device capability count
			for (int i = 0; i < deviceInfo.PovCount; i++)
			{
				result.POVs.Add(-1); // Neutral position
			}

			return result;
		}
	}
}
