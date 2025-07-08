using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using x360ce.Engine;
using x360ce.Engine.Data;

namespace x360ce.App.Input.Processors
{
	/// <summary>
	/// HID descriptor parsing for Raw Input devices.
	/// This partial class handles proper HID capability parsing following HID specification.
	/// Replaces hardcoded capability assumptions with real HID descriptor analysis.
	/// </summary>
	public partial class RawInputProcessor
	{
		#region HID Capability Parsing

		/// <summary>
		/// Parses actual HID capabilities from device descriptor.
		/// Replaces hardcoded capability loading with real HID descriptor reading.
		/// </summary>
		/// <param name="deviceHandle">Raw Input device handle</param>
		/// <returns>Parsed HID capabilities or null if parsing failed</returns>
		private static HidDeviceCapabilities ParseHidCapabilities(IntPtr deviceHandle)
		{
			try
			{
				// Get preparsed data size
				uint preparsedDataSize = 0;
				GetRawInputDeviceInfo(deviceHandle, RIDI_PREPARSEDDATA, IntPtr.Zero, ref preparsedDataSize);

				if (preparsedDataSize == 0)
				{
					Debug.WriteLine("Raw Input: Failed to get preparsed data size");
					return null;
				}

				// Allocate buffer for preparsed data
				IntPtr preparsedDataBuffer = Marshal.AllocHGlobal((int)preparsedDataSize);
				try
				{
					// Get preparsed data
					uint result = GetRawInputDeviceInfo(deviceHandle, RIDI_PREPARSEDDATA, preparsedDataBuffer, ref preparsedDataSize);
					if (result != preparsedDataSize)
					{
						Debug.WriteLine("Raw Input: Failed to get preparsed data");
						return null;
					}

					// Get HID capabilities
					HIDP_CAPS caps;
					int status = HidP_GetCaps(preparsedDataBuffer, out caps);
					if (status != HIDP_STATUS_SUCCESS)
					{
						Debug.WriteLine($"Raw Input: HidP_GetCaps failed with status 0x{status:X8}");
						return null;
					}

					var hidCapabilities = new HidDeviceCapabilities
					{
						RawCaps = caps,
						PreparsedData = preparsedDataBuffer
					};

					// Parse button capabilities
					ParseButtonCapabilities(preparsedDataBuffer, caps, hidCapabilities);

					// Parse value capabilities (axes, triggers, POVs)
					ParseValueCapabilities(preparsedDataBuffer, caps, hidCapabilities);

					// Calculate final counts
					hidCapabilities.ButtonCount = hidCapabilities.Buttons.Count;
					hidCapabilities.AxisCount = hidCapabilities.Values.Count(v => v.Type == HidValueType.Axis || v.Type == HidValueType.Trigger);
					hidCapabilities.PovCount = hidCapabilities.Values.Count(v => v.Type == HidValueType.Pov);

					Debug.WriteLine($"Raw Input: Parsed HID capabilities - Buttons: {hidCapabilities.ButtonCount}, Axes: {hidCapabilities.AxisCount}, POVs: {hidCapabilities.PovCount}");
					Debug.WriteLine($"Raw Input: HID Report Length: {caps.InputReportByteLength} bytes, Usage: 0x{caps.Usage:X4}, UsagePage: 0x{caps.UsagePage:X4}");

					return hidCapabilities;
				}
				catch (Exception ex)
				{
					Debug.WriteLine($"Raw Input: Exception in HID parsing: {ex.Message}");
					Marshal.FreeHGlobal(preparsedDataBuffer);
					return null;
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Raw Input: Error parsing HID capabilities: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Parses button capabilities from HID descriptor.
		/// Uses HidP_GetButtonCaps to get actual button information.
		/// </summary>
		/// <param name="preparsedData">HID preparsed data</param>
		/// <param name="caps">HID capabilities structure</param>
		/// <param name="hidCapabilities">HID capabilities to populate</param>
		private static void ParseButtonCapabilities(IntPtr preparsedData, HIDP_CAPS caps, HidDeviceCapabilities hidCapabilities)
		{
			if (caps.NumberInputButtonCaps == 0)
			{
				Debug.WriteLine("Raw Input: No input button capabilities");
				return;
			}

			try
			{
				var buttonCaps = new HIDP_BUTTON_CAPS[caps.NumberInputButtonCaps];
				int buttonCapsLength = (int)caps.NumberInputButtonCaps;
				
				int status = HidP_GetButtonCaps(HIDP_REPORT_TYPE.HidP_Input, buttonCaps, ref buttonCapsLength, preparsedData);
				if (status != HIDP_STATUS_SUCCESS)
				{
					Debug.WriteLine($"Raw Input: HidP_GetButtonCaps failed with status 0x{status:X8}");
					return;
				}

				int buttonIndex = 0;
				foreach (var buttonCap in buttonCaps)
				{
					if (buttonCap.IsRange != 0)
					{
						// Handle button range
						var buttonInfo = new HidButtonInfo
						{
							UsagePage = buttonCap.UsagePage,
							Usage = 0, // Range doesn't have single usage
							IsRange = true,
							UsageMin = buttonCap.Union.Range.UsageMin,
							UsageMax = buttonCap.Union.Range.UsageMax,
							CustomDeviceStateIndex = buttonIndex,
							Name = $"Buttons {buttonCap.Union.Range.UsageMin}-{buttonCap.Union.Range.UsageMax}"
						};
						hidCapabilities.Buttons.Add(buttonInfo);

						// Count individual buttons in range
						int rangeCount = buttonCap.Union.Range.UsageMax - buttonCap.Union.Range.UsageMin + 1;
						buttonIndex += rangeCount;

						Debug.WriteLine($"Raw Input: Button range - Page: 0x{buttonCap.UsagePage:X2}, Usage: {buttonCap.Union.Range.UsageMin}-{buttonCap.Union.Range.UsageMax} ({rangeCount} buttons)");
					}
					else
					{
						// Handle single button
						var buttonInfo = new HidButtonInfo
						{
							UsagePage = buttonCap.UsagePage,
							Usage = buttonCap.Union.NotRange.Usage,
							IsRange = false,
							CustomDeviceStateIndex = buttonIndex,
							Name = GetButtonName(buttonCap.Union.NotRange.Usage, buttonCap.UsagePage)
						};
						hidCapabilities.Buttons.Add(buttonInfo);
						buttonIndex++;

						Debug.WriteLine($"Raw Input: Button - Page: 0x{buttonCap.UsagePage:X2}, Usage: 0x{buttonCap.Union.NotRange.Usage:X2}, Name: {buttonInfo.Name}");
					}
				}

				Debug.WriteLine($"Raw Input: Parsed {hidCapabilities.Buttons.Count} button capabilities, total buttons: {buttonIndex}");
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Raw Input: Error parsing button capabilities: {ex.Message}");
			}
		}

		/// <summary>
		/// Parses value capabilities from HID descriptor.
		/// Uses HidP_GetValueCaps to get actual axis/trigger/POV information.
		/// </summary>
		/// <param name="preparsedData">HID preparsed data</param>
		/// <param name="caps">HID capabilities structure</param>
		/// <param name="hidCapabilities">HID capabilities to populate</param>
		private static void ParseValueCapabilities(IntPtr preparsedData, HIDP_CAPS caps, HidDeviceCapabilities hidCapabilities)
		{
			if (caps.NumberInputValueCaps == 0)
			{
				Debug.WriteLine("Raw Input: No input value capabilities");
				return;
			}

			try
			{
				var valueCaps = new HIDP_VALUE_CAPS[caps.NumberInputValueCaps];
				int valueCapsLength = (int)caps.NumberInputValueCaps;
				
				int status = HidP_GetValueCaps(HIDP_REPORT_TYPE.HidP_Input, valueCaps, ref valueCapsLength, preparsedData);
				if (status != HIDP_STATUS_SUCCESS)
				{
					Debug.WriteLine($"Raw Input: HidP_GetValueCaps failed with status 0x{status:X8}");
					return;
				}

				int axisIndex = 0;
				foreach (var valueCap in valueCaps)
				{
					if (valueCap.IsRange != 0)
					{
						// Handle value range
						for (ushort usage = valueCap.Union.Range.UsageMin; usage <= valueCap.Union.Range.UsageMax; usage++)
						{
							var valueInfo = CreateValueInfo(valueCap, usage, ref axisIndex);
							hidCapabilities.Values.Add(valueInfo);

							Debug.WriteLine($"Raw Input: Value range - Page: 0x{valueCap.UsagePage:X2}, Usage: 0x{usage:X2}, Type: {valueInfo.Type}, Range: {valueCap.LogicalMin}-{valueCap.LogicalMax}");
						}
					}
					else
					{
						// Handle single value
						var valueInfo = CreateValueInfo(valueCap, valueCap.Union.NotRange.Usage, ref axisIndex);
						hidCapabilities.Values.Add(valueInfo);

						Debug.WriteLine($"Raw Input: Value - Page: 0x{valueCap.UsagePage:X2}, Usage: 0x{valueCap.Union.NotRange.Usage:X2}, Type: {valueInfo.Type}, Range: {valueCap.LogicalMin}-{valueCap.LogicalMax}");
					}
				}

				Debug.WriteLine($"Raw Input: Parsed {hidCapabilities.Values.Count} value capabilities");
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Raw Input: Error parsing value capabilities: {ex.Message}");
			}
		}

		/// <summary>
		/// Creates a HidValueInfo from HID value capability and usage.
		/// </summary>
		/// <param name="valueCap">HID value capability structure</param>
		/// <param name="usage">HID usage ID</param>
		/// <param name="axisIndex">Current axis index counter</param>
		/// <returns>Populated HidValueInfo</returns>
		private static HidValueInfo CreateValueInfo(HIDP_VALUE_CAPS valueCap, ushort usage, ref int axisIndex)
		{
			var valueType = GetValueType(usage, valueCap.UsagePage);
			int customIndex = -1;

			// Map to CustomDeviceState index based on usage and type
			if (valueType == HidValueType.Axis || valueType == HidValueType.Trigger)
			{
				customIndex = MapUsageToAxisIndex(usage, valueCap.UsagePage, ref axisIndex);
			}
			else if (valueType == HidValueType.Pov)
			{
				customIndex = 0; // POVs start at index 0
			}

			return new HidValueInfo
			{
				UsagePage = valueCap.UsagePage,
				Usage = usage,
				LogicalMin = valueCap.LogicalMin,
				LogicalMax = valueCap.LogicalMax,
				PhysicalMin = valueCap.PhysicalMin,
				PhysicalMax = valueCap.PhysicalMax,
				CustomDeviceStateIndex = customIndex,
				Name = GetAxisName(usage, valueCap.UsagePage),
				Type = valueType,
				IsRange = valueCap.IsRange != 0,
				UsageMin = valueCap.IsRange != 0 ? valueCap.Union.Range.UsageMin : usage,
				UsageMax = valueCap.IsRange != 0 ? valueCap.Union.Range.UsageMax : usage
			};
		}

		/// <summary>
		/// Loads actual device capabilities using HID descriptor parsing.
		/// Replaces the old hardcoded LoadCapabilities method.
		/// </summary>
		/// <param name="device">The device to load capabilities for</param>
		public void LoadCapabilitiesFromHid(UserDevice device)
		{
			if (device == null)
				return;

			try
			{
				// Try to get the Raw Input device handle for this device
				var rawInputHandle = GetOrCreateRawInputMapping(device);
				HidDeviceCapabilities actualCapabilities = null;

				if (rawInputHandle != IntPtr.Zero)
				{
					actualCapabilities = ParseHidCapabilities(rawInputHandle);
				}

				if (actualCapabilities != null)
				{
					// Use actual HID descriptor information
					device.CapButtonCount = actualCapabilities.ButtonCount;
					device.CapAxeCount = actualCapabilities.AxisCount;
					device.CapPovCount = actualCapabilities.PovCount;

					// Store HID capabilities in device info for state reading
					if (_trackedDevices.TryGetValue(rawInputHandle, out var deviceInfo))
					{
						deviceInfo.HidCapabilities = actualCapabilities;
					}

					Debug.WriteLine($"Raw Input: Loaded real HID capabilities for {device.DisplayName} - Buttons: {device.CapButtonCount}, Axes: {device.CapAxeCount}, POVs: {device.CapPovCount}");
				}
				else
				{
					// Use intelligent defaults based on device type
					LoadIntelligentDefaults(device);
				}

				// Create device objects based on actual or estimated capabilities
				CreateDeviceObjectsFromHid(device, actualCapabilities);

				// Set axis mask based on actual axis count
				SetAxisMaskFromCapabilities(device);

				// Set device effects (Raw Input doesn't support effects)
				if (device.DeviceEffects == null)
					device.DeviceEffects = new DeviceEffectItem[0];
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Raw Input: Capability loading failed for {device.DisplayName}: {ex.Message}");
				LoadIntelligentDefaults(device);
			}
		}

		/// <summary>
		/// Loads intelligent defaults when HID parsing fails.
		/// </summary>
		/// <param name="device">Device to load defaults for</param>
		private static void LoadIntelligentDefaults(UserDevice device)
		{
			if (device.IsXboxCompatible)
			{
				device.CapButtonCount = 15;  // Xbox standard: A,B,X,Y,LB,RB,Back,Start,LS,RS,DPad(4),Guide
				device.CapAxeCount = 6;      // Left X/Y, Right X/Y, LT, RT
				device.CapPovCount = 0;      // Xbox controllers typically don't use POV
				Debug.WriteLine($"Raw Input: Using Xbox defaults for {device.DisplayName}");
			}
			else
			{
				// Generic gamepad defaults
				device.CapButtonCount = 12;  // Conservative estimate
				device.CapAxeCount = 4;      // Left X/Y, Right X/Y
				device.CapPovCount = 1;      // Most generic gamepads have 1 POV
				Debug.WriteLine($"Raw Input: Using generic defaults for {device.DisplayName}");
			}
		}

		/// <summary>
		/// Sets the axis mask based on actual capabilities.
		/// </summary>
		/// <param name="device">Device to set axis mask for</param>
		private static void SetAxisMaskFromCapabilities(UserDevice device)
		{
			if (device.DiAxeMask == 0)
			{
				// Set mask for the actual number of axes available
				int mask = 0;
				for (int i = 0; i < device.CapAxeCount && i < 32; i++)
				{
					mask |= (1 << i);
				}
				device.DiAxeMask = mask;
			}
		}

		#endregion
	}
}