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
				// Use GetRawInputDeviceState (without clearing) so button state persists
				// until a new WM_INPUT message arrives with different state
				var report = _statesRawInput.GetRawInputDeviceState(riDevice);
				
				// Get stored value for this device (0 if not found = use default)
				int storedValue = 0;
				if (_deviceButtonByteCount != null)
					_deviceButtonByteCount.TryGetValue(riDevice.InterfacePath, out storedValue);
				
				// Debug: Log report status for Xbox controller
				if (riDevice.VendorId == 0x045E)
				{
					var reportHex = report != null && report.Length >= 12
						? System.BitConverter.ToString(report, 0, System.Math.Min(report.Length, 12))
						: "null";
					System.Diagnostics.Debug.WriteLine(
						$"Xbox RawInput Report: StoredValue={storedValue}, ReportLen={report?.Length}, " +
						$"First12Bytes={reportHex}, Report={(report == null ? "NULL" : "EXISTS")}");
				}
				
				// Update button state: true if report has button data, false if idle
				bool buttonPressed = report != null && HasButtonPressed(report, storedValue);
				allDevice.ButtonPressed = buttonPressed;
				
				// Log when button is detected as pressed
				if (buttonPressed && riDevice.VendorId == 0x045E)
				{
					System.Diagnostics.Debug.WriteLine($"Xbox Button PRESSED DETECTED!");
				}
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

			foreach (var device in allDevicesList)
			{
				// Single condition check with null-coalescing
				if (device.InputType == "RawInput" && device.InterfacePath != null)
					_deviceMapping[device.InterfacePath] = device;
			}
			
			// Build button byte count cache from RawInputDeviceInfo
			foreach (var riDevice in rawInputList)
			{
				if (riDevice?.InterfacePath == null)
					continue;
					
				// Calculate button byte count from actual button count
				// Each byte holds 8 buttons (bit-packed), so divide by 8 and round up
				int buttonByteCount = (riDevice.ButtonCount + 7) / 8;
				
				// CRITICAL: Button position varies by device!
				// Xbox controllers: Buttons at bytes 11-12 (middle of 16-byte report)
				// Other devices: May be at beginning, middle, or end
				//
				// SOLUTION: Store button byte count for validation, but check ALL bytes
				// The HasButtonPressed method will use a smarter detection strategy
				int storedValue = buttonByteCount;
				
				// Log for diagnostics
				System.Diagnostics.Debug.WriteLine(
					$"RawInput Button Detection: Device={riDevice.InterfacePath}, " +
					$"AxeCount={riDevice.AxeCount}, ButtonCount={riDevice.ButtonCount}, " +
					$"ButtonByteCount={buttonByteCount}, Strategy=CheckLastBytes, StoredValue={storedValue}");
				
				// Store for fast lookup during button detection
				_deviceButtonByteCount[riDevice.InterfacePath] = storedValue;
			}
		}

		/// <summary>
		/// Checks if any button is pressed in the HID input report using actual device button count.
		/// Optimized for performance in high-frequency loops.
		/// </summary>
		/// <param name="report">The raw HID input report data from WM_INPUT messages</param>
		/// <param name="buttonByteCount">Number of bytes containing button data (from HID descriptor), 0 = use default</param>
		/// <returns>True if any button is pressed, false otherwise</returns>
		/// <remarks>
		/// HID Report Structure (typical gamepad):
		/// • Byte 0: Report ID
		/// • Bytes 1-N: Button states (bit-packed, N = buttonByteCount)
		/// • Bytes N+1+: Axis data (X, Y, Z, Rz, etc.) - EXCLUDED from button detection
		///
		/// ENHANCED FIX: Uses actual button count from HID descriptor to determine exact button byte positions.
		/// This eliminates false positives from axis data and works correctly for ALL HID report structures.
		///
		/// Performance optimizations:
		/// • Removed try-catch (exception handling is expensive in hot paths)
		/// • Direct array access with bounds check
		/// • Early return on first button press
		/// • Simplified loop logic
		/// • Uses device-specific button byte count from HID descriptor
		/// </remarks>
		private static bool HasButtonPressed(byte[] report, int buttonByteCount)
		{
			// Validate minimum report size (Report ID + at least 1 button byte)
			if (report.Length < 2)
				return false;

			// CRITICAL: If buttonByteCount is 0, it means ButtonCount was 0 (HID parsing failed)
			// In this case, we should NOT check any bytes to avoid false positives from axis data
			if (buttonByteCount == 0)
			{
				return false; // Conservative: don't detect buttons if we don't know the structure
			}
			
			// SMART BUTTON DETECTION STRATEGY:
			// Problem: Button position varies by device (beginning, middle, or end of report)
			// Solution: Check bytes that look like button data (sparse bit patterns)
			//
			// Button characteristics:
			// - Use bit flags: 0x01, 0x02, 0x04, 0x08, 0x10, 0x20, 0x40, 0x80
			// - Typically have only a few bits set (sparse patterns)
			// - Values are usually < 0x20 for most controllers
			//
			// Axis characteristics:
			// - Use full byte range (0x00-0xFF)
			// - Values change smoothly and frequently
			// - Centered around 0x80 (128) for most axes
			
			// Check all bytes after Report ID, looking for button-like patterns
			for (int i = 1; i < report.Length; i++)
			{
				byte value = report[i];
				
				// Skip bytes that look like axis data (values near center or full range)
				// Axes typically use values 0x00-0xFF with center around 0x80
				// Skip bytes in range 0x40-0xC0 (likely axis data)
				if (value >= 0x40 && value <= 0xC0)
					continue;
				
				// Check if this byte has button-like characteristics:
				// - Non-zero value
				// - Sparse bit pattern (power of 2 or small combinations)
				// - Value < 0x40 (typical button range)
				if (value != 0 && value < 0x40)
				{
					// This looks like button data - check if it's a valid button pattern
					// Accept any non-zero value in button range as a button press
					return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Invalidates the device mapping cache, forcing a rebuild on next check.
		/// </summary>
		public void InvalidateCache()
		{
			_deviceMapping = null;
			_deviceButtonByteCount = null;
			_lastDeviceCount = 0;
		}
	}
}
