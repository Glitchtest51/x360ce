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
		private int _lastDeviceCount;

		/// <summary>
		/// Checks each RawInput device for button presses using cached WM_INPUT message data.
		/// CRITICAL: This method is NON-BLOCKING - uses message-based system, never opens handles.
		/// Safe for concurrent use with all other input methods.
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
				BuildDeviceMapping(allDevicesList);
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
				// This retrieves AND clears the cache in one operation
				var report = _statesRawInput.GetAndClearRawInputDeviceState(riDevice);
				
				// Update button state: true if report has button data, false if idle
				allDevice.ButtonPressed = report != null && HasButtonPressed(report);
			}
		}

		/// <summary>
		/// Builds a mapping dictionary from InterfacePath to AllInputDeviceInfo for fast lookups.
		/// </summary>
		private void BuildDeviceMapping(System.Collections.ObjectModel.ObservableCollection<DevicesCombined.AllInputDeviceInfo> allDevicesList)
		{
			// Pre-allocate with estimated capacity to reduce resizing
			_deviceMapping = new Dictionary<string, DevicesCombined.AllInputDeviceInfo>(allDevicesList.Count);

			foreach (var device in allDevicesList)
			{
				// Single condition check with null-coalescing
				if (device.InputType == "RawInput" && device.InterfacePath != null)
					_deviceMapping[device.InterfacePath] = device;
			}
		}

		/// <summary>
		/// Checks if any button is pressed in the HID input report.
		/// Optimized for performance in high-frequency loops.
		/// </summary>
		/// <param name="report">The raw HID input report data from WM_INPUT messages</param>
		/// <returns>True if any button is pressed, false otherwise</returns>
		/// <remarks>
		/// HID Report Structure (typical gamepad):
		/// • Byte 0: Report ID
		/// • Bytes 1-2: Button states (bit-packed) - ONLY these bytes for button detection
		/// • Bytes 3+: Axis data (X, Y, Z, Rz, etc.) - EXCLUDED from button detection
		///
		/// CRITICAL FIX: Only check bytes 1-2 which contain button data.
		/// Axis data in bytes 3+ can have non-zero values during stick movement,
		/// which was incorrectly triggering button press detection.
		///
		/// Performance optimizations:
		/// • Removed try-catch (exception handling is expensive in hot paths)
		/// • Direct array access with bounds check
		/// • Early return on first button press
		/// • Simplified loop logic
		/// • Fixed to check ONLY button bytes (1-2), not axis bytes (3+)
		/// </remarks>
		private static bool HasButtonPressed(byte[] report)
		{
			// Validate minimum report size (Report ID + at least 1 button byte)
			if (report.Length < 2)
				return false;

			// FIXED: Check ONLY button bytes 1-2 (not axis bytes 3+)
			// Most HID gamepads use bytes 1-2 for buttons (up to 16 buttons)
			// Bytes 3+ contain axis data which should NOT trigger button detection
			
			// Check byte 1 (buttons 1-8)
			if (report[1] != 0)
				return true;
			
			// Check byte 2 (buttons 9-16) if report is long enough
			if (report.Length >= 3 && report[2] != 0)
				return true;

			return false;
		}

		/// <summary>
		/// Invalidates the device mapping cache, forcing a rebuild on next check.
		/// </summary>
		public void InvalidateCache()
		{
			_deviceMapping = null;
			_lastDeviceCount = 0;
		}
	}
}
