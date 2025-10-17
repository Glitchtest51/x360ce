using System.Collections.Generic;
using x360ce.App.Input.Devices;

namespace x360ce.App.Input.States
{
	/// <summary>
	/// Provides methods to check if any button is pressed on Gaming Input devices.
	/// Supports Gaming Input devices (modern gamepads using Windows.Gaming.Input API).
	/// </summary>
	internal class StatesAnyButtonIsPressedGamingInput
	{
		private readonly StatesConvertToListType _statesConverter = new StatesConvertToListType();

		// Cache for Gaming Input device to AllInputDeviceInfo mapping
		private Dictionary<string, DevicesCombined.AllInputDeviceInfo> _deviceMapping;

		/// <summary>
		/// Checks each Gaming Input device for button presses and updates the ButtonPressed property
		/// in AllInputDevicesList.
		/// </summary>
		/// <param name="devicesCombined">The combined devices instance containing device lists</param>
		public void CheckGamingInputDevicesIfAnyButtonIsPressed(DevicesCombined devicesCombined)
		{
			if (devicesCombined.GamingInputDevicesList == null || devicesCombined.AllInputDevicesList == null)
				return;

			// Build mapping cache on first run or when device list changes
			if (_deviceMapping == null || _deviceMapping.Count != devicesCombined.GamingInputDevicesList.Count)
				BuildDeviceMapping(devicesCombined);

			// Check each Gaming Input device
			foreach (var giDevice in devicesCombined.GamingInputDevicesList)
			{
				if (giDevice?.GamingInputDevice == null)
					continue;

				// Get the current state as ListTypeState and check for button presses
				var listState = _statesConverter.GetGamingInputStateAsListTypeState(giDevice);
				if (listState == null)
					continue;

				// Determine if any button is pressed by checking if button list contains value '1'
				bool anyButtonPressed = IsAnyButtonPressed(listState);
	
				// Use cached mapping for faster lookup using CommonIdentifier
				if (_deviceMapping.TryGetValue(giDevice.CommonIdentifier, out var allDevice))
				{
					allDevice.ButtonPressed = anyButtonPressed;
				}
			}
		}

		/// <summary>
		/// Builds a mapping dictionary from CommonIdentifier to AllInputDeviceInfo for fast lookups.
		/// </summary>
		/// <param name="devicesCombined">The combined devices instance containing device lists</param>
		private void BuildDeviceMapping(DevicesCombined devicesCombined)
		{
			_deviceMapping = new Dictionary<string, DevicesCombined.AllInputDeviceInfo>();

			foreach (var device in devicesCombined.AllInputDevicesList)
			{
				if (device.InputType == "GamingInput" && !string.IsNullOrEmpty(device.CommonIdentifier))
				{
					_deviceMapping[device.CommonIdentifier] = device;
				}
			}
		}

		/// <summary>
		/// Checks if any button is pressed in the given ListTypeState.
		/// </summary>
		/// <param name="listState">The standardized ListTypeState containing button states</param>
		/// <returns>True if any button is pressed (button list contains value '1') or any POV is pressed (value > -1), false otherwise</returns>
		/// <remarks>
		/// This method checks the Buttons list in ListTypeState format where:
		/// • 0 = button released
		/// • 1 = button pressed
		///
		/// And POV list where:
		/// • -1 = neutral/centered
		/// • 0-31500 = direction pressed (in centidegrees)
		///
		/// Returns true if the button list contains at least one value of '1' or POVs list contains any value > -1.
		/// Returns false if the button list is empty or contains no value of '1' and POVs list is empty or contains only -1 values.
		/// </remarks>
		private static bool IsAnyButtonPressed(ListTypeState listState)
		{
			// Check if button list contains value '1' (pressed)
			bool buttonPressed = listState?.Buttons != null && listState.Buttons.Contains(1);
			
			// Check if POV list contains any value > -1 (pressed)
			bool povPressed = listState?.POVs != null && listState.POVs.Exists(pov => pov > -1);
			
			return buttonPressed || povPressed;
		}

		/// <summary>
		/// Invalidates the device mapping cache, forcing a rebuild on next check.
		/// Call this when device lists change.
		/// </summary>
		public void InvalidateCache()
		{
			_deviceMapping = null;
		}
	}
}
