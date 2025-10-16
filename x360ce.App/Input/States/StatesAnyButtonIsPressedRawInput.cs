using System.Collections.Generic;
using x360ce.App.Input.Devices;

namespace x360ce.App.Input.States
{
	/// <summary>
	/// Provides NON-BLOCKING button press detection for RawInput devices.
	/// Uses WM_INPUT message-based system that doesn't block other input methods.
	/// </summary>
	/// <remarks>
	/// NON-BLOCKING IMPLEMENTATION:
	/// • Uses StatesRawInput which receives WM_INPUT messages
	/// • Never opens HID device handles
	/// • Safe for concurrent use with DirectInput, XInput, GamingInput
	/// • Analyzes cached HID reports from background messages
	/// </remarks>
	internal class StatesAnyButtonIsPressedRawInput
	{
		private readonly StatesRawInput _statesRawInput = new StatesRawInput();
		private Dictionary<string, DevicesCombined.AllInputDeviceInfo> _deviceMapping;
		private Dictionary<string, int> _deviceButtonByteCount; // Cache button byte counts per device
		private Dictionary<string, RawInputDeviceType> _deviceTypes; // Cache device types for fast lookup
		private int _lastDeviceCount;

		/// <summary>
		/// Checks each RawInput device for button presses using cached WM_INPUT message data.
		/// CRITICAL: This method is NON-BLOCKING - uses message-based system, never opens handles.
		/// Safe for concurrent use with all other input methods.
		/// Button state persists until a new WM_INPUT message arrives with different state.
		/// </summary>
		/// <param name="devicesCombined">The combined devices instance containing device lists</param>
		public void CheckRawInputDevicesIfAnyButtonIsPressed(DevicesCombined devicesCombined)
		{
			var rawInputList = devicesCombined?.RawInputDevicesList;
			var allDevicesList = devicesCombined?.AllInputDevicesList;
			
			if (rawInputList == null || allDevicesList == null)
				return;

			// Build mapping cache on first run or when device count changes
			int currentCount = rawInputList.Count;
			if (_deviceMapping == null || _lastDeviceCount != currentCount)
			{
				BuildDeviceMapping(allDevicesList, rawInputList);
				_lastDeviceCount = currentCount;
			}

			// Check each RawInput device
			foreach (var riDevice in rawInputList)
			{
				// Skip invalid devices
				if (riDevice?.InterfacePath == null)
					continue;

				// Fast lookup - single dictionary access
				if (!_deviceMapping.TryGetValue(riDevice.InterfacePath, out var allDevice))
					continue;

				// Get cached state from WM_INPUT messages (non-blocking)
				var report = _statesRawInput.GetRawInputDeviceState(riDevice);
				
				// Get device type for this device
				RawInputDeviceType deviceType = RawInputDeviceType.HID;
				_deviceTypes?.TryGetValue(riDevice.InterfacePath, out deviceType);
				
				// Get stored value for this device (0 if not found = use default)
				int storedValue = 0;
				_deviceButtonByteCount?.TryGetValue(riDevice.InterfacePath, out storedValue);
				
				// Update button state: explicitly set true if button pressed, false otherwise
				allDevice.ButtonPressed = report != null && HasButtonPressed(report, storedValue, deviceType);
			}
		}

		/// <summary>
		/// Builds a mapping dictionary from InterfacePath to AllInputDeviceInfo for fast lookups.
		/// Also builds button byte count cache from RawInputDeviceInfo.
		/// </summary>
		private void BuildDeviceMapping(
			System.Collections.ObjectModel.ObservableCollection<DevicesCombined.AllInputDeviceInfo> allDevicesList,
			System.Collections.Generic.List<RawInputDeviceInfo> rawInputList)
		{
			// Pre-allocate with estimated capacity to reduce resizing
			_deviceMapping = new Dictionary<string, DevicesCombined.AllInputDeviceInfo>(allDevicesList.Count);
			_deviceButtonByteCount = new Dictionary<string, int>(rawInputList.Count);
			_deviceTypes = new Dictionary<string, RawInputDeviceType>(rawInputList.Count);

			foreach (var device in allDevicesList)
			{
				// Single condition check with null-coalescing
				if (device.InputType == "RawInput" && device.InterfacePath != null)
					_deviceMapping[device.InterfacePath] = device;
			}
			
			// Build button byte count cache and device type cache from RawInputDeviceInfo
			foreach (var riDevice in rawInputList)
			{
				if (riDevice?.InterfacePath == null)
					continue;
				
				// Store device type for fast lookup
				_deviceTypes[riDevice.InterfacePath] = riDevice.RawInputDeviceType;
					
				// Calculate button byte count from actual button/key count
				// For keyboards, use KeyCount instead of ButtonCount
				// Each byte holds 8 buttons (bit-packed), so divide by 8 and round up
				int count = riDevice.RawInputDeviceType == RawInputDeviceType.Keyboard
					? riDevice.KeyCount
					: riDevice.ButtonCount;
				int buttonByteCount = (count + 7) / 8;
				
				// Store for fast lookup during button detection
				_deviceButtonByteCount[riDevice.InterfacePath] = buttonByteCount;
			}
		}

		/// <summary>
		/// Checks if any button/key is pressed in the input report using actual device button count.
		/// Supports HID devices (gamepads), keyboards, and mice.
		/// Optimized for performance in high-frequency loops.
		/// </summary>
		/// <param name="report">The raw input report data from WM_INPUT messages</param>
		/// <param name="buttonByteCount">Number of bytes containing button data (from HID descriptor), 0 = use default</param>
		/// <param name="deviceType">Type of device (HID, Keyboard, or Mouse)</param>
		/// <returns>True if any button/key is pressed, false otherwise</returns>
		/// <remarks>
		/// HID Report Structure (typical gamepad):
		/// • Byte 0: Report ID
		/// • Bytes 1-N: Button states (bit-packed, N = buttonByteCount)
		/// • Bytes N+1+: Axis data (X, Y, Z, Rz, etc.) - EXCLUDED from button detection
		///
		/// Keyboard/Mouse Reports:
		/// • Different structure - any non-zero byte indicates key/button press
		/// • Keyboards: Scan codes in report indicate pressed keys
		/// • Mice: Button flags in report indicate pressed buttons
		///
		/// ENHANCED FIX: Uses actual button count from HID descriptor to determine exact button byte positions.
		/// This eliminates false positives from axis data and works correctly for ALL input device types.
		///
		/// Performance optimizations:
		/// • Removed try-catch (exception handling is expensive in hot paths)
		/// • Direct array access with bounds check
		/// • Early return on first button press
		/// • Simplified loop logic
		/// • Uses device-specific button byte count from HID descriptor
		/// </remarks>
		private static bool HasButtonPressed(byte[] report, int buttonByteCount, RawInputDeviceType deviceType)
		{
			// Validate minimum report size
			if (report == null || report.Length < 1)
				return false;

			// Handle keyboard and mouse devices differently from HID gamepads
			if (deviceType == RawInputDeviceType.Keyboard)
			{
				// Keyboard RawInput reports structure:
				// - Byte 0: Modifier keys (Ctrl, Shift, Alt, etc.) - bit flags
				// - Byte 1: Reserved (usually 0)
				// - Bytes 2-7: Up to 6 simultaneous key scan codes (0 = no key)
				// A key is pressed if any scan code byte (2-7) is non-zero
				// We ignore modifier-only presses (byte 0) to avoid false positives
				
				if (report.Length < 3)
					return false; // Need at least bytes 0-2 for valid keyboard report
				
				// Check scan code bytes (skip byte 0 for modifiers, byte 1 is reserved)
				for (int i = 2; i < report.Length && i < 8; i++)
				{
					if (report[i] != 0)
						return true;
				}
				return false;
			}
			else if (deviceType == RawInputDeviceType.Mouse)
			{
				// Mouse RawInput reports structure:
				// - Byte 0: Button flags (bit 0=left, bit 1=right, bit 2=middle, bit 3=button4, bit 4=button5)
				// - Bytes 1-4: Movement data (X, Y deltas)
				// - Bytes 5+: Wheel data (optional)
				// We only check byte 0 for button states
				
				if (report.Length < 1)
					return false;
				
				// Check all button bits in first byte (bits 0-7)
				// Common buttons: 0x01 (left), 0x02 (right), 0x04 (middle), 0x08 (button4), 0x10 (button5)
				// Some mice may use additional bits for extra buttons
				if (report[0] != 0)
					return true;
				
				return false;
			}
			else // RawInputDeviceType.HID (gamepads, joysticks, etc.)
			{
				// Validate minimum report size for HID (Report ID + at least 1 button byte)
				if (report.Length < 2)
					return false;

				// PRECISE BUTTON DETECTION:
				// If buttonByteCount is known (> 0), use it for precise detection
				// Button bytes start at index 1 (after Report ID at index 0)
				// Each byte contains 8 button states as individual bits
				
				if (buttonByteCount > 0)
				{
					// PRECISE MODE: We know exactly how many button bytes to check
					// Calculate the end index for button bytes (exclusive)
					int buttonEndIndex = 1 + buttonByteCount;
					
					// Ensure we don't read beyond the report buffer
					if (buttonEndIndex > report.Length)
						buttonEndIndex = report.Length;
					
					// Check ONLY the button bytes (indices 1 to buttonEndIndex-1)
					// Any non-zero byte means at least one button is pressed
					for (int i = 1; i < buttonEndIndex; i++)
					{
						if (report[i] != 0)
							return true;
					}
					
					// No buttons pressed - explicitly return false
					return false;
				}
				else
				{
					// FALLBACK MODE: ButtonCount not available
					// Without knowing the exact button byte count, we cannot reliably
					// distinguish button bytes from axis bytes in the HID report.
					// Return false to avoid false positives from axis movement.
					// This is safer than guessing and incorrectly detecting axis data as buttons.
					return false;
				}
			}
		}

		/// <summary>
		/// Invalidates the device mapping cache, forcing a rebuild on next check.
		/// </summary>
		public void InvalidateCache()
		{
			_deviceMapping = null;
			_deviceButtonByteCount = null;
			_deviceTypes = null;
			_lastDeviceCount = 0;
		}
	}
}
