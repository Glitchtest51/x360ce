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
		private Dictionary<string, RawInputDeviceInfo> _rawInputDeviceInfo; // Cache full device info for report layout
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
				
				// Get device info for this device (contains report layout information)
				RawInputDeviceInfo deviceInfo = null;
				_rawInputDeviceInfo?.TryGetValue(riDevice.InterfacePath, out deviceInfo);
				
				// Update button state: explicitly set true if button pressed, false otherwise
				bool buttonPressed = report != null && HasButtonPressed(report, deviceInfo);
				
				allDevice.ButtonPressed = buttonPressed;
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
			_rawInputDeviceInfo = new Dictionary<string, RawInputDeviceInfo>(rawInputList.Count);
	
			foreach (var device in allDevicesList)
			{
				// Single condition check with null-coalescing
				if (device.InputType == "RawInput" && device.InterfacePath != null)
					_deviceMapping[device.InterfacePath] = device;
			}
			
			// Build device info cache from RawInputDeviceInfo (contains report layout information)
			foreach (var riDevice in rawInputList)
			{
				if (riDevice?.InterfacePath == null)
					continue;
				
				// Store full device info for report layout access
				_rawInputDeviceInfo[riDevice.InterfacePath] = riDevice;
			}
		}

		/// <summary>
		/// Checks if any button/key is pressed in the input report using actual device button count.
		/// Supports HID devices (gamepads), keyboards, and mice.
		/// Optimized for performance in high-frequency loops.
		/// </summary>
		/// <param name="report">The raw input report data from WM_INPUT messages</param>
		/// <param name="deviceInfo">Device information containing report layout (report IDs, button counts, offsets)</param>
		/// <returns>True if any button/key is pressed, false otherwise</returns>
		/// <remarks>
		/// HID Report Structure:
		/// • Byte 0: Report ID (if UsesReportIds is true), otherwise button data starts at byte 0
		/// • Bytes [ButtonDataOffset] to [ButtonDataOffset + buttonByteCount - 1]: Button states (bit-packed)
		/// • Remaining bytes: Axis data (X, Y, Z, Rz, etc.) - EXCLUDED from button detection
		///
		/// Keyboard/Mouse Reports:
		/// • Different structure - any non-zero byte indicates key/button press
		/// • Keyboards: Scan codes in report indicate pressed keys
		/// • Mice: Button flags in report indicate pressed buttons
		///
		/// ENHANCED FIX: Uses Report ID detection and actual button count from HID descriptor.
		/// This eliminates false positives from axis data and correctly handles devices with/without Report IDs.
		///
		/// Performance optimizations:
		/// • Removed try-catch (exception handling is expensive in hot paths)
		/// • Direct array access with bounds check
		/// • Early return on first button press
		/// • Simplified loop logic
		/// • Uses device-specific report layout from HID descriptor
		/// </remarks>
		private static bool HasButtonPressed(byte[] report, RawInputDeviceInfo deviceInfo)
		{
			// Validate inputs
			if (report == null || report.Length < 1 || deviceInfo == null)
				return false;
	
			// Handle keyboard devices
			if (deviceInfo.RawInputDeviceType == RawInputDeviceType.Keyboard)
			{
				// Keyboard: Check scan code bytes (2-7), ignore modifiers (byte 0) and reserved (byte 1)
				if (report.Length < 3)
					return false;
				
				// Optimized: Check up to 6 scan codes with single loop
				int maxIndex = report.Length < 8 ? report.Length : 8;
				for (int i = 2; i < maxIndex; i++)
				{
					if (report[i] != 0)
						return true;
				}
				return false;
			}
			
			// Handle mouse devices
			if (deviceInfo.RawInputDeviceType == RawInputDeviceType.Mouse)
			{
				// Mouse: Check button flags in first byte only
				return report[0] != 0;
			}
			
			// Handle HID devices (gamepads, joysticks)
			int buttonCount = deviceInfo.ButtonCount;
			if (buttonCount <= 0)
				return false;
			
			int buttonDataOffset = deviceInfo.ButtonDataOffset;
			int buttonByteCount = (buttonCount + 7) >> 3; // Optimized: bit shift instead of division
			int minReportSize = buttonDataOffset + buttonByteCount;
			
			if (report.Length < minReportSize)
				return false;
			
			// Calculate end index, clamped to report length
			int buttonEndIndex = buttonDataOffset + buttonByteCount;
			if (buttonEndIndex > report.Length)
				buttonEndIndex = report.Length;
			
			// Check button bytes for any non-zero value
			for (int i = buttonDataOffset; i < buttonEndIndex; i++)
			{
				if (report[i] != 0)
					return true;
			}
			
			return false;
		}

		/// <summary>
		/// Invalidates the device mapping cache, forcing a rebuild on next check.
		/// </summary>
		public void InvalidateCache()
		{
			_deviceMapping = null;
			_rawInputDeviceInfo = null;
			_lastDeviceCount = 0;
		}
	}
}
