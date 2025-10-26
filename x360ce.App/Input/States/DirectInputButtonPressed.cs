using System.Collections.Generic;
using x360ce.App.Input.Devices;

namespace x360ce.App.Input.States
{
	/// <summary>
	/// Provides methods to check if any button is pressed on DirectInput devices.
	/// Supports DirectInput devices (joysticks, keyboards, mice).
	/// </summary>
	internal class DirectInputButtonPressed
	{
        private readonly DirectInputState _statesDirectInput = new DirectInputState();
		
		// Cache for DirectInput device to AllInputDeviceInfo mapping
		private Dictionary<string, UnifiedInputDeviceInfo> _deviceMapping;

		/// <summary>
		/// Checks each DirectInput device for button presses and updates the ButtonPressed property
		/// in AllInputDevicesList.
		/// </summary>
		/// <param name="devicesCombined">The combined devices instance containing device lists</param>
		public void IsDirectInputButtonPressed(UnifiedInputDeviceManager devicesCombined)
		{
			if (devicesCombined.DirectInputDeviceInfoList == null || devicesCombined.UnifiedInputDeviceInfoList == null)
				return;
			
			// Build mapping cache on first run or when device list changes
			if (_deviceMapping == null || _deviceMapping.Count != devicesCombined.DirectInputDeviceInfoList.Count)
				BuildDeviceMapping(devicesCombined);
			
			// Check each DirectInput device
			foreach (var diDeviceInfo in devicesCombined.DirectInputDeviceInfoList)
			{
				if (diDeviceInfo?.DirectInputDevice == null)
					continue;


                // Get the latest DirectInput device state (non-blocking)
                var diState = _statesDirectInput.GetDirectInputState(diDeviceInfo);
				if (diState == null)
					continue;

                // Convert DirectInput state to ListTypeState format (non-blocking)
                var listState = DirectInputStateToList.ConvertDirectInputStateToList(diState);
				if (listState == null)
					continue;
			
				// Check if any button is pressed (button list contains value '1')
				// or if any POV is pressed (value > -1, where -1 is neutral)
				bool anyButtonPressed = (listState.Buttons != null && listState.Buttons.Contains(1)) ||
					(listState.POVs != null && listState.POVs.Exists(pov => pov > -1));
			
				// Use cached mapping for faster lookup
				if (_deviceMapping.TryGetValue(diDeviceInfo.InterfacePath, out var allDevice))
				{
					allDevice.ButtonPressed = anyButtonPressed;
				}
			}
		}

		/// <summary>
		/// Builds a mapping dictionary from InterfacePath to AllInputDeviceInfo for fast lookups.
		/// </summary>
		/// <param name="devicesCombined">The combined devices instance containing device lists</param>
		private void BuildDeviceMapping(UnifiedInputDeviceManager devicesCombined)
		{
			_deviceMapping = new Dictionary<string, UnifiedInputDeviceInfo>();
			
			foreach (var device in devicesCombined.UnifiedInputDeviceInfoList)
			{
				if (device.InputType == "DirectInput" && !string.IsNullOrEmpty(device.InterfacePath))
				{
					_deviceMapping[device.InterfacePath] = device;
				}
			}
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
