using System.Collections.Generic;
using x360ce.App.Input.Devices;

namespace x360ce.App.Input.States
{
	/// <summary>
	/// Provides methods to check if any button is pressed on XInput devices.
	/// </summary>
	internal class StatesAnyButtonIsPressedXInput
	{
		private readonly StatesConvertToListType _statesConverter = new StatesConvertToListType();

		// Cache for XInput device to AllInputDeviceInfo mapping
		private Dictionary<string, DevicesCombined.AllInputDeviceInfo> _deviceMapping;

		/// <summary>
		/// Checks each XInput device for button presses and updates the ButtonPressed property
		/// in AllInputDevicesList.
		/// </summary>
		/// <param name="devicesCombined">The combined devices instance containing device lists</param>
		public void CheckXInputDevicesIfAnyButtonIsPressed(DevicesCombined devicesCombined)
		{
			if (devicesCombined.XInputDevicesList == null || devicesCombined.AllInputDevicesList == null)
				return;

			// Build mapping cache on first run or when device list changes
			if (_deviceMapping == null || _deviceMapping.Count != devicesCombined.XInputDevicesList.Count)
				BuildDeviceMapping(devicesCombined);

			// Check each XInput device
			foreach (var xiDevice in devicesCombined.XInputDevicesList)
			{
				if (xiDevice == null)
					continue;

				// Get the current state as ListTypeState and check for button presses
				var listState = _statesConverter.GetXInputStateAsListTypeState(xiDevice);
				if (listState == null)
					continue;

				// Determine if any button is pressed by checking if Buttons list contains value 1
				bool anyButtonPressed = IsAnyButtonPressed(listState);
	
				// Use cached mapping for faster lookup using CommonIdentifier
				if (_deviceMapping.TryGetValue(xiDevice.CommonIdentifier, out var allDevice))
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
				if (device.InputType == "XInput" && !string.IsNullOrEmpty(device.CommonIdentifier))
				{
					_deviceMapping[device.CommonIdentifier] = device;
				}
			}
		}

		/// <summary>
		/// Checks if any button is pressed in the given ListTypeState.
		/// </summary>
		/// <param name="listState">The standardized ListTypeState containing button states</param>
		/// <returns>True if any button is pressed (value 1 in Buttons list) or any POV is pressed (value > -1), false otherwise</returns>
		/// <remarks>
		/// This method checks the standardized button list where:
		/// • 0 = button released
		/// • 1 = button pressed
		///
		/// And POV list where:
		/// • -1 = neutral/centered
		/// • 0-31500 = direction pressed (in centidegrees)
		///
		/// Returns true if the Buttons list contains at least one value of 1 or POVs list contains any value > -1.
		/// Returns false if the Buttons list is empty or contains only 0 values and POVs list is empty or contains only -1 values.
		/// </remarks>
		private static bool IsAnyButtonPressed(ListTypeState listState)
		{
			// Check if Buttons list exists and contains any pressed button (value 1)
			// or if POVs list exists and contains any pressed POV (value > -1)
			return (listState?.Buttons != null && listState.Buttons.Contains(1)) ||
				(listState?.POVs != null && listState.POVs.Exists(pov => pov > -1));
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
