using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using x360ce.Engine;
using x360ce.Engine.Data;

namespace x360ce.App.Input.Processors
{
	/// <summary>
	/// HID state mapping for Raw Input devices.
	/// This partial class handles proper HID report to CustomDeviceState conversion.
	/// Replaces broken byte-guessing with proper HID API usage.
	/// </summary>
	public partial class RawInputProcessor
	{
		#region HID State Reading

		/// <summary>
		/// Reads HID state using proper HID API instead of byte manipulation.
		/// This method replaces the broken ParseHidReport methods.
		/// </summary>
		/// <param name="deviceInfo">Device information with HID capabilities</param>
		/// <param name="buffer">Raw input buffer</param>
		/// <param name="bufferSize">Buffer size</param>
		/// <returns>CustomDeviceState or null if reading failed</returns>
		private static CustomDeviceState ReadHidStateWithApi(RawInputDeviceInfo deviceInfo, IntPtr buffer, uint bufferSize)
		{
			if (deviceInfo?.HidCapabilities?.PreparsedData == IntPtr.Zero)
			{
				Debug.WriteLine("Raw Input: No HID capabilities available for state reading");
				return null;
			}

			try
			{
				var rawInput = Marshal.PtrToStructure<RAWINPUT>(buffer);

				// Get HID report pointer
				IntPtr reportPtr = IntPtr.Add(buffer, Marshal.SizeOf<RAWINPUTHEADER>() + Marshal.SizeOf<RAWHID>());
				uint reportLength = rawInput.hid.dwSizeHid;

				if (reportLength == 0)
				{
					Debug.WriteLine("Raw Input: Invalid HID report length");
					return null;
				}

				var state = new CustomDeviceState();
				var hidCaps = deviceInfo.HidCapabilities;

				// Read button states using HID API
				ReadButtonStatesWithHidApi(hidCaps, reportPtr, reportLength, state);

				// Read axis values using HID API
				ReadAxisValuesWithHidApi(hidCaps, reportPtr, reportLength, state);

				// Read POV values using HID API
				ReadPovValuesWithHidApi(hidCaps, reportPtr, reportLength, state);

				return state;
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Raw Input: Error reading HID state: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Reads button states using HidP_GetUsages API.
		/// This is the correct way to read button states from HID reports.
		/// </summary>
		/// <param name="hidCaps">HID capabilities</param>
		/// <param name="report">HID report pointer</param>
		/// <param name="reportLength">Report length</param>
		/// <param name="state">CustomDeviceState to populate</param>
		private static void ReadButtonStatesWithHidApi(HidDeviceCapabilities hidCaps, IntPtr report, uint reportLength, CustomDeviceState state)
		{
			try
			{
				// Get maximum number of button usages
				int maxUsages = (int)hidCaps.RawCaps.NumberInputDataIndices;
				if (maxUsages == 0)
					return;
	
				// Allocate buffer for usage list
				var usageList = new ushort[maxUsages];
				int usageLength = maxUsages;

				// Get pressed button usages
				int status = HidP_GetUsages(
					HIDP_REPORT_TYPE.HidP_Input,
					HID_USAGE_PAGE_BUTTON,
					0, // LinkCollection
					usageList,
					ref usageLength,
					hidCaps.PreparsedData,
					report,
					(int)reportLength
				);

				if (status == HIDP_STATUS_SUCCESS)
				{
					// Map HID button usages to CustomDeviceState button indices
					for (int i = 0; i < usageLength; i++)
					{
						ushort buttonUsage = usageList[i];
						
						// Find corresponding button info
						var buttonInfo = hidCaps.Buttons.FirstOrDefault(b => 
							b.UsagePage == HID_USAGE_PAGE_BUTTON && 
							((b.IsRange && buttonUsage >= b.UsageMin && buttonUsage <= b.UsageMax) ||
							 (!b.IsRange && buttonUsage == b.Usage)));

						if (buttonInfo != null)
						{
							int buttonIndex;
							if (buttonInfo.IsRange)
							{
								// Calculate index within range
								buttonIndex = buttonInfo.CustomDeviceStateIndex + (buttonUsage - buttonInfo.UsageMin);
							}
							else
							{
								buttonIndex = buttonInfo.CustomDeviceStateIndex;
							}

							// Set button state if index is valid
							if (buttonIndex >= 0 && buttonIndex < state.Buttons.Length)
							{
								state.Buttons[buttonIndex] = true;
								Debug.WriteLine($"Raw Input: Button {buttonIndex} pressed (HID usage: {buttonUsage})");
							}
						}
						else
						{
							Debug.WriteLine($"Raw Input: Unknown button usage: {buttonUsage}");
						}
					}
				}
				else if (status != HIDP_STATUS_USAGE_NOT_FOUND)
				{
					Debug.WriteLine($"Raw Input: HidP_GetUsages for buttons failed with status 0x{status:X8}");
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Raw Input: Error reading button states: {ex.Message}");
			}
		}

		/// <summary>
		/// Reads axis values using HidP_GetUsageValue API.
		/// This is the correct way to read axis values from HID reports.
		/// </summary>
		/// <param name="hidCaps">HID capabilities</param>
		/// <param name="report">HID report pointer</param>
		/// <param name="reportLength">Report length</param>
		/// <param name="state">CustomDeviceState to populate</param>
		private static void ReadAxisValuesWithHidApi(HidDeviceCapabilities hidCaps, IntPtr report, uint reportLength, CustomDeviceState state)
		{
			try
			{
				// Read each axis value using HID API
				foreach (var valueInfo in hidCaps.Values.Where(v => v.Type == HidValueType.Axis || v.Type == HidValueType.Trigger))
				{
					int rawValue;
					int status = HidP_GetUsageValue(
						HIDP_REPORT_TYPE.HidP_Input,
						valueInfo.UsagePage,
						0, // LinkCollection
						valueInfo.Usage,
						out rawValue,
						hidCaps.PreparsedData,
						report,
						(int)reportLength
					);

					if (status == HIDP_STATUS_SUCCESS)
					{
						// Convert raw value to CustomDeviceState range
						int convertedValue = ConvertHidValueToCustomDeviceState(
							rawValue,
							valueInfo.LogicalMin,
							valueInfo.LogicalMax,
							valueInfo.Type
						);

						// Set axis value if index is valid
						if (valueInfo.CustomDeviceStateIndex >= 0 && valueInfo.CustomDeviceStateIndex < state.Axis.Length)
						{
							state.Axis[valueInfo.CustomDeviceStateIndex] = convertedValue;
							
							// Only log significant changes to avoid spam
							if (Math.Abs(convertedValue) > 1000)
							{
								Debug.WriteLine($"Raw Input: Axis {valueInfo.CustomDeviceStateIndex} ({valueInfo.Name}) = {convertedValue} (raw: {rawValue}, range: {valueInfo.LogicalMin}-{valueInfo.LogicalMax})");
							}
						}
					}
					else if (status != HIDP_STATUS_USAGE_NOT_FOUND)
					{
						Debug.WriteLine($"Raw Input: HidP_GetUsageValue for {valueInfo.Name} failed with status 0x{status:X8}");
					}
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Raw Input: Error reading axis values: {ex.Message}");
			}
		}

		/// <summary>
		/// Reads POV values using HidP_GetUsageValue API.
		/// </summary>
		/// <param name="hidCaps">HID capabilities</param>
		/// <param name="report">HID report pointer</param>
		/// <param name="reportLength">Report length</param>
		/// <param name="state">CustomDeviceState to populate</param>
		private static void ReadPovValuesWithHidApi(HidDeviceCapabilities hidCaps, IntPtr report, uint reportLength, CustomDeviceState state)
		{
			try
			{
				// Read each POV value using HID API
				foreach (var valueInfo in hidCaps.Values.Where(v => v.Type == HidValueType.Pov))
				{
					int rawValue;
					int status = HidP_GetUsageValue(
						HIDP_REPORT_TYPE.HidP_Input,
						valueInfo.UsagePage,
						0, // LinkCollection
						valueInfo.Usage,
						out rawValue,
						hidCaps.PreparsedData,
						report,
						(int)reportLength
					);

					if (status == HIDP_STATUS_SUCCESS)
					{
						// Convert POV value to standard POV format
						int povValue = ConvertHidPovValue(rawValue, valueInfo.LogicalMin, valueInfo.LogicalMax);

						// Set POV value if index is valid
						if (valueInfo.CustomDeviceStateIndex >= 0 && valueInfo.CustomDeviceStateIndex < state.POVs.Length)
						{
							state.POVs[valueInfo.CustomDeviceStateIndex] = povValue;
							Debug.WriteLine($"Raw Input: POV {valueInfo.CustomDeviceStateIndex} = {povValue} (raw: {rawValue})");
						}
					}
					else if (status != HIDP_STATUS_USAGE_NOT_FOUND)
					{
						Debug.WriteLine($"Raw Input: HidP_GetUsageValue for POV failed with status 0x{status:X8}");
					}
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Raw Input: Error reading POV values: {ex.Message}");
			}
		}

		/// <summary>
		/// Converts HID value to CustomDeviceState range.
		/// Handles proper scaling and centering based on value type.
		/// </summary>
		/// <param name="rawValue">Raw HID value</param>
		/// <param name="logicalMin">HID logical minimum</param>
		/// <param name="logicalMax">HID logical maximum</param>
		/// <param name="valueType">Type of value (axis or trigger)</param>
		/// <returns>Converted value for CustomDeviceState</returns>
		private static int ConvertHidValueToCustomDeviceState(int rawValue, int logicalMin, int logicalMax, HidValueType valueType)
		{
			if (logicalMin >= logicalMax)
			{
				// Invalid range, return raw value
				return rawValue;
			}

			if (valueType == HidValueType.Trigger)
			{
				// Triggers: Convert to 0-32767 range
				double normalized = (double)(rawValue - logicalMin) / (logicalMax - logicalMin);
				return (int)(normalized * 32767);
			}
			else // Axis
			{
				// Axes: Convert to -32768 to 32767 range, centered
				double normalized = (double)(rawValue - logicalMin) / (logicalMax - logicalMin);
				normalized = (normalized * 2.0) - 1.0; // Convert to -1.0 to 1.0
				return (int)(normalized * 32767);
			}
		}

		/// <summary>
		/// Converts HID POV value to standard POV format.
		/// </summary>
		/// <param name="rawValue">Raw HID POV value</param>
		/// <param name="logicalMin">HID logical minimum</param>
		/// <param name="logicalMax">HID logical maximum</param>
		/// <returns>POV value in centidegrees (0-35999) or -1 for centered</returns>
		private static int ConvertHidPovValue(int rawValue, int logicalMin, int logicalMax)
		{
			if (rawValue < logicalMin || rawValue > logicalMax)
			{
				// Out of range, POV is centered
				return -1;
			}

			if (logicalMin >= logicalMax)
			{
				// Invalid range
				return -1;
			}

			// Convert to 0-35999 centidegrees (0-359.99 degrees)
			double normalized = (double)(rawValue - logicalMin) / (logicalMax - logicalMin);
			return (int)(normalized * 35999);
		}

		/// <summary>
		/// Fallback state reading using byte analysis when HID API fails.
		/// This is a last resort and should be avoided when possible.
		/// </summary>
		/// <param name="deviceInfo">Device information</param>
		/// <param name="buffer">Raw input buffer</param>
		/// <param name="bufferSize">Buffer size</param>
		/// <returns>CustomDeviceState with fallback parsing</returns>
		private static CustomDeviceState ReadHidStateFallback(RawInputDeviceInfo deviceInfo, IntPtr buffer, uint bufferSize)
		{
			try
			{
				var rawInput = Marshal.PtrToStructure<RAWINPUT>(buffer);

				// Get HID data pointer
				IntPtr hidDataPtr = IntPtr.Add(buffer, Marshal.SizeOf<RAWINPUTHEADER>() + Marshal.SizeOf<RAWHID>());
				int hidDataSize = (int)(rawInput.hid.dwSizeHid * rawInput.hid.dwCount);

				if (hidDataSize <= 0)
					return null;

				// Copy HID data for analysis
				byte[] hidData = new byte[hidDataSize];
				Marshal.Copy(hidDataPtr, hidData, 0, hidDataSize);

				Debug.WriteLine($"Raw Input: Fallback parsing HID report ({hidDataSize} bytes): {BitConverter.ToString(hidData)}");

				var state = new CustomDeviceState();

				if (deviceInfo.IsXboxController)
				{
					ParseXboxHidReportFallback(hidData, state);
				}
				else
				{
					ParseGenericHidReportFallback(hidData, state);
				}

				return state;
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Raw Input: Error in fallback parsing: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Fallback Xbox controller parsing.
		/// </summary>
		private static void ParseXboxHidReportFallback(byte[] hidData, CustomDeviceState state)
		{
			if (hidData.Length < 8)
				return;

			Debug.WriteLine("Raw Input: Using Xbox controller fallback parsing");

			// Very basic Xbox parsing as fallback
			// This is not ideal but better than nothing
			if (hidData.Length >= 6)
			{
				// Try to find axis data (look for changing values)
				state.Axis[0] = (short)((hidData[2] - 128) * 256); // Approximate left X
				state.Axis[1] = (short)((hidData[3] - 128) * 256); // Approximate left Y
				
				if (hidData.Length >= 8)
				{
					state.Axis[2] = (short)((hidData[4] - 128) * 256); // Approximate right X
					state.Axis[3] = (short)((hidData[5] - 128) * 256); // Approximate right Y
				}
			}

			// Try to find button data
			if (hidData.Length >= 2)
			{
				byte buttonByte = hidData[1];
				for (int i = 0; i < 8 && i < state.Buttons.Length; i++)
				{
					state.Buttons[i] = (buttonByte & (1 << i)) != 0;
				}
			}
		}

		/// <summary>
		/// Fallback generic controller parsing.
		/// </summary>
		private static void ParseGenericHidReportFallback(byte[] hidData, CustomDeviceState state)
		{
			if (hidData.Length < 4)
				return;

			Debug.WriteLine("Raw Input: Using generic controller fallback parsing");

			// Very conservative generic parsing
			// Assume first few bytes might be axes
			int axisCount = Math.Min(4, hidData.Length - 1);
			for (int i = 0; i < axisCount && i < state.Axis.Length; i++)
			{
				state.Axis[i] = (short)((hidData[i + 1] - 128) * 256);
			}

			// Look for button data in later bytes
			int buttonStartByte = Math.Max(1, axisCount + 1);
			if (buttonStartByte < hidData.Length)
			{
				byte buttonByte = hidData[buttonStartByte];
				for (int i = 0; i < 8 && i < state.Buttons.Length; i++)
				{
					state.Buttons[i] = (buttonByte & (1 << i)) != 0;
				}
			}
		}

		#endregion
	}
}