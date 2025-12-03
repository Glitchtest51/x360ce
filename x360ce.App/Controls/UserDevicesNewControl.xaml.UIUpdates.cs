//using System;
using System.Linq;
using x360ce.App.Input.Devices;
using x360ce.App.Input.States;
using x360ce.App.Input.Triggers;

namespace x360ce.App.Controls
{
	/// <summary>
	/// Custom button press detection for all input methods (DirectInput, XInput, GamingInput, RawInput).
	/// Gets ListInputState directly from source device lists by InstanceGuid lookup.
	/// This approach is simpler and more reliable than maintaining object references.
	/// </summary>
	internal class UserDevicesNewControl_UIUpdates
	{
		// Reference to the device input handler for updating value labels
		private DevicesTab_DeviceSelectedInput _deviceSelectedInput;

		/// <summary>
		/// Sets the reference to the device input handler for updating value labels.
		/// </summary>
		/// <param name="deviceSelectedInput">The device input handler instance</param>
		public void SetDeviceSelectedInput(DevicesTab_DeviceSelectedInput deviceSelectedInput)
		{
			_deviceSelectedInput = deviceSelectedInput;
		}

		/// <summary>
		/// Checks all input methods for button presses and updates ButtonPressed properties.
		/// Gets current ListInputState directly from source device lists using InstanceGuid lookup.
		/// This ensures we always read the most current state without reference management complexity.
		/// </summary>
		/// <param name="customInputDeviceManager">The combined devices instance containing all device lists</param>
		public void DevicesTab_UIUpdate(CustomInputDeviceManager customInputDeviceManager)
		{
			if (customInputDeviceManager == null)
				return;

			// Single loop through custom list - get current state directly from source lists
			foreach (var device in customInputDeviceManager.CustomInputDeviceInfoList)
			{
				// Get current ListInputState directly from the appropriate source list
				CustomInputState liState = null;

				switch (device.InputType)
                {
                    case "RawInput":
                        liState = customInputDeviceManager.RawInputDeviceInfoList?
                            .FirstOrDefault(d => d.InstanceGuid == device.InstanceGuid)
                            ?.ListInputState;
                        break;
                    case "DirectInput":
                        liState = customInputDeviceManager.DirectInputDeviceInfoList?
                            .FirstOrDefault(d => d.InstanceGuid == device.InstanceGuid)
                            ?.ListInputState;
                        break;
                    case "XInput":
                        liState = customInputDeviceManager.XInputDeviceInfoList?
                            .FirstOrDefault(d => d.InstanceGuid == device.InstanceGuid)
                            ?.ListInputState;
                        break;
                    case "GamingInput":
                        liState = customInputDeviceManager.GamingInputDeviceInfoList?
                            .FirstOrDefault(d => d.InstanceGuid == device.InstanceGuid)
                            ?.ListInputState;
                        break;
            }

				// Skip if no state available
				if (liState == null)
					continue;

				// Check if any button or POV is pressed and Update ButtonPressed property
				device.ButtonPressed = IsAnyButtonOrPovPressed(liState);

                // Update value labels if handler is set
                _deviceSelectedInput?.UpdateValueLabels(device.InstanceGuid, liState);
			}
		}

		/// <summary>
		/// Checks if any button or POV is pressed in the given state.
		/// Optimized for high-frequency execution.
		/// </summary>
		/// <param name="listState">The device state to check</param>
		/// <returns>True if any button is pressed (value 1) or any POV is pressed (value > -1)</returns>
		private static bool IsAnyButtonOrPovPressed(CustomInputState listState)
		{
			// Check buttons.
			if (listState?.Buttons != null)
			{
				var buttons = listState.Buttons;
				for (int i = 0; i < buttons.Count; i++)
				{
                    if (buttons[i] == 1)
						return true;
				}
			}

			// Check POVs.
			if (listState?.POVs != null)
			{
				var povs = listState.POVs;
				for (int i = 0; i < povs.Count; i++)
				{
					if (povs[i] > -1)
						return true;
				}
			}

			return false;
		}
	}
}
