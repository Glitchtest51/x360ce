using System;
using System.Collections.Generic;
using System.Diagnostics;
using x360ce.App.Input.Devices;

namespace x360ce.App.Input.States
{
	/// <summary>
	/// Provides NON-BLOCKING button press detection for RawInput devices.
	/// Uses WM_INPUT message-based system that doesn't block other input methods.
	/// Simplified implementation using GetRawInputStateAsListTypeState for button detection.
	/// </summary>
	/// <remarks>
	/// NON-BLOCKING IMPLEMENTATION:
	/// • Uses StatesConvertToListType which receives WM_INPUT messages
	/// • Never opens HID device handles
	/// • Safe for concurrent use with DirectInput, XInput, GamingInput
	/// • Checks if button list contains value '1' for simple, reliable detection
	/// • Works with ALL device layouts (buttons before/after axes)
	/// </remarks>
	internal class StatesAnyButtonIsPressedRawInput
	{
		private readonly StatesConvertToListType _statesConverter = new StatesConvertToListType();
		private Dictionary<string, DevicesCombined.AllInputDeviceInfo> _deviceMapping;
		private int _lastDeviceCount;
		private DateTime _lastDebugOutput = DateTime.MinValue;

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
				BuildDeviceMapping(allDevicesList);
				_lastDeviceCount = currentCount;
			}
	
			// Debug output every second for gamepads only
			bool shouldDebug = (DateTime.Now - _lastDebugOutput).TotalSeconds >= 1.0;
			if (shouldDebug)
				_lastDebugOutput = DateTime.Now;
	
			// Check each RawInput device
			foreach (var riDevice in rawInputList)
			{
				// Skip invalid devices
				if (riDevice?.InterfacePath == null)
					continue;
		
				// Fast lookup - single dictionary access
				if (!_deviceMapping.TryGetValue(riDevice.InterfacePath, out var allDevice))
					continue;
		
				// Convert RawInput state to ListTypeState format (non-blocking)
				var listState = _statesConverter.GetRawInputStateAsListTypeState(riDevice);
				
				// Always update ButtonPressed value, even if listState is null
				// If listState is null, no buttons/POVs are pressed
				bool buttonPressed = false;
				if (listState != null)
				{
					// Check if any button is pressed by looking for value '1' in button list
					// or if any POV is pressed (value > -1, where -1 is neutral)
					buttonPressed = (listState.Buttons != null && listState.Buttons.Contains(1)) ||
						(listState.POVs != null && listState.POVs.Exists(pov => pov > -1));
				}
				
				allDevice.ButtonPressed = buttonPressed;
	
				// Debug output for gamepads only (exclude keyboard and mouse)
				if (shouldDebug && IsGamepad(riDevice))
				{
					string deviceName = allDevice.ProductName ?? "Unknown";
					string stateStr = listState?.ToString() ?? "null";
					Debug.WriteLine($"[RawInput Gamepad] {deviceName}: State={stateStr}, ButtonPressed={buttonPressed}");
				}
			}
		}
	
		/// <summary>
		/// Builds a mapping dictionary from InterfacePath to AllInputDeviceInfo for fast lookups.
		/// </summary>
		private void BuildDeviceMapping(
			System.Collections.ObjectModel.ObservableCollection<DevicesCombined.AllInputDeviceInfo> allDevicesList)
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
		/// Determines if a RawInput device is a gamepad (not keyboard or mouse).
		/// </summary>
		private static bool IsGamepad(RawInputDeviceInfo device)
		{
			// Check device type - exclude keyboard (type 1) and mouse (type 0)
			// Gamepads are typically type 2 (HID)
			if (device.DeviceType == 0 || device.DeviceType == 1)
				return false;

			// Additional check: exclude devices with "keyboard" or "mouse" in the name
			string productName = device.ProductName?.ToLowerInvariant() ?? "";
			if (productName.Contains("keyboard") || productName.Contains("mouse"))
				return false;

			return true;
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
