using JocysCom.ClassLibrary.IO;
using SharpDX.DirectInput;
using SharpDX.XInput;
using System;
using System.Diagnostics;
using System.Linq;
using x360ce.Engine;
using x360ce.Engine.Data;

namespace x360ce.App.DInput
{
	public partial class DInputHelper
	{
		// Shared orchestration methods moved to DInputHelper.Step2.CustomDiStates.cs
		// XInput processing moved to DInputHelper.Step2.ReadXInput.cs
		// Gaming Input processing moved to DInputHelper.Step2.ReadGamingInput.cs
		// Raw Input processing moved to DInputHelper.Step2.ReadRawInput.cs

		#region DirectInput-Specific Processing

		/// <summary>
		/// Processes DirectInput devices using the original logic (preserves device acquisition and force feedback).
		/// This maintains backward compatibility and prevents "NotAcquired" errors.
		/// </summary>
		/// <param name="device">The DirectInput device to process</param>
		/// <param name="detector">Device detector for acquisition</param>
		/// <param name="newUpdates">Output parameter for buffered updates</param>
		/// <returns>CustomDiState for the device</returns>
		private CustomDiState ProcessDirectInputDevice(UserDevice device, DeviceDetector detector, out CustomDiUpdate[] newUpdates)
		{
			newUpdates = null;

			bool useData = false;
			if (options.UseDeviceBufferedData && device.Device.Properties.BufferSize == 0)
			{
				// Set BufferSize in order to use buffered data.
				device.Device.Properties.BufferSize = 128;
				useData = true;
			}

			var deviceType = device.Device.Information.Type;
			// Original device will be hidden from the game when acquired in exclusive mode.
			// If device is not keyboard or mouse then apply AcquireMappedDevicesInExclusiveMode option.
			var acquireMappedDevicesInExclusiveMode =
				(deviceType != SharpDX.DirectInput.DeviceType.Keyboard && deviceType != SharpDX.DirectInput.DeviceType.Mouse)
				? options.AcquireMappedDevicesInExclusiveMode
				: false;

			// Exclusive mode required only if force feedback is available and device is virtual or there is no info about effects.
			var hasForceFeedback = device.Device.Capabilities.Flags.HasFlag(DeviceFlags.ForceFeedback);
			var exclusiveRequired = acquireMappedDevicesInExclusiveMode ||
				(hasForceFeedback && (isVirtual || device.DeviceEffects == null));

			// Check if the current mode is unknown or differs from the desired, then reacquire the device.
			if (!device.IsExclusiveMode.HasValue || device.IsExclusiveMode.Value != exclusiveRequired)
			{
				var cooperativeLevel = exclusiveRequired ? CooperativeLevel.Exclusive : CooperativeLevel.NonExclusive;
				// Reacquire device in exclusive or in non exclusive mode (xinput.dll can control force feedback).
				DeviceExclusiveMode(device, detector, device.Device, cooperativeLevel);
			}

			// Polling - Retrieves data from polled objects on a DirectInput device.
			// Some devices require polling (For example original "XBOX Controller S" with XBCD drivers).
			// If the device does not require polling, calling this method has no effect.
			// If a device that requires polling is not polled periodically, no new data is received from the device.
			// Calling this method causes DirectInput to update the device state, generate input
			// events (if buffered data is enabled), and set notification events (if notification is enabled).
			device.Device.Poll();

			CustomDiState newState = null;

			// Use switch based on pattern matching for supported device types.
			switch (device.Device)
			{
				case Mouse mDevice:
					newUpdates = useData ? mDevice.GetBufferedData()?.Select(x => new CustomDiUpdate(x)).ToArray() : null;
					{
						var state = mDevice.GetCurrentState();
						newState = new CustomDiState(state);
						device.DeviceState = state;
					}
					break;
				case Keyboard kDevice:
					newUpdates = useData ? kDevice.GetBufferedData()?.Select(x => new CustomDiUpdate(x)).ToArray() : null;
					{
						var state = kDevice.GetCurrentState();
						newState = new CustomDiState(state);
						device.DeviceState = state;
					}
					break;
				case Joystick jDevice:
					newUpdates = useData ? jDevice.GetBufferedData()?.Select(x => new CustomDiUpdate(x)).ToArray() : null;
					{
						var state = jDevice.GetCurrentState();
						newState = new CustomDiState(state);

						// Test if button 0 was pressed.
						var oldState = device.DeviceState as JoystickState;
						if (oldState != null && oldState.Buttons[0] != state.Buttons[0])
						{

						}
						//-----------------------------

						device.DeviceState = state;
					}
					break;
				default:
					throw new Exception($"Unknown device: {device.Device}");
			}

			// Fill device objects force feedback actuator masks.
			if (device.DeviceObjects == null)
			{
				// var item = AppHelper.GetDeviceObjects(device, device.Device);
				// device.DeviceObjects = item;
				//// Update masks.
				//int axisMask = 0;
				//int actuatorMask = 0;
				//int actuatorCount = 0;
				//if (device.Device is Mouse mDevice2)
				//{
				//	CustomDiState.GetMouseAxisMask(item, mDevice2, out axisMask);
				//}
				//else if (device.Device is Joystick jDevice)
				//{
				//	CustomDiState.GetJoystickAxisMask(item, jDevice, out axisMask, out actuatorMask, out actuatorCount);
				//}
				//device.DiAxeMask = axisMask;
				//// Contains information about which axis have force feedback actuator attached.
				//device.DiActuatorMask = actuatorMask;
				//device.DiActuatorCount = actuatorCount;
			}
			if (device.DeviceEffects == null)
			{
				device.DeviceEffects = AppHelper.GetDeviceEffects(device.Device);
			}

			// Handle force feedback if supported.
			if (hasForceFeedback)
			{
				// Get setting related to user device.
				var setting = SettingsManager.UserSettings.ItemsToArraySynchronized()
					.FirstOrDefault(x => x.InstanceGuid == device.InstanceGuid);
				if (setting != null && setting.MapTo > (int)MapTo.None)
				{
					// Get pad setting attached to device.
					var ps = SettingsManager.GetPadSetting(setting.PadSettingChecksum);
					if (ps != null)
					{
						if (ps.ForceEnable == "1")
						{
							device.FFState = device.FFState ?? new Engine.ForceFeedbackState();
							// If force update supplied then...
							var force = CopyAndClearFeedbacks()[setting.MapTo - 1];
							if (force != null || device.FFState.Changed(ps))
							{
								var vibration = new Vibration
								{
									LeftMotorSpeed = (force == null) ? short.MinValue : ConvertHelper.ConvertMotorSpeed(force.LargeMotor),
									RightMotorSpeed = (force == null) ? short.MinValue : ConvertHelper.ConvertMotorSpeed(force.SmallMotor)
								};
								// For the future: Investigate device states if force feedback is not working. 
								// var st = device.Device.GetForceFeedbackState();
								// st == SharpDX.DirectInput.ForceFeedbackState
								// device.Device.SendForceFeedbackCommand(ForceFeedbackCommand.SetActuatorsOn);
								device.FFState.SetDeviceForces(device, device.Device, ps, vibration);
							}
						}
						else if (device.FFState != null)
						{
							// Stop device forces.
							device.FFState.StopDeviceForces(device.Device);
							device.FFState = null;
						}
					}
				}
			}

			// Mouse needs special update.
			if (device.Device != null && device.Device.Information.Type == SharpDX.DirectInput.DeviceType.Mouse)
			{
				// If original state is missing then...
				if (device.OrgDiState == null)
				{
					// Store current values.
					device.OrgDiState = newState;
					device.OrgDiStateTime = _Stopwatch.ElapsedTicks;
				// Make sure new states have zero values.
				for (int a = 0; a < newState.Axis.Length; a++)
					newState.Axis[a] = -short.MinValue;
				for (int s = 0; s < newState.Sliders.Length; s++)
					newState.Sliders[s] = -short.MinValue;
				}
				var mouseState = new CustomDiState(new JoystickState());
				// Clone button values.
				Array.Copy(newState.Buttons, mouseState.Buttons, mouseState.Buttons.Length);

				//--------------------------------------------------------
				// Map mouse position to axis position. Good for car wheel controls.
				//--------------------------------------------------------
				Calc(device.OrgDiState.Axis, newState.Axis, mouseState.Axis);
				Calc(device.OrgDiState.Sliders, newState.Sliders, mouseState.Sliders);
				newState = mouseState;
			}

			return newState;
		}

		/// <summary>
		/// DirectInput-specific mouse axis calculation helper method.
		/// Converts mouse delta movement to controller axis values for car wheel controls.
		/// </summary>
		/// <param name="orgRange">Original axis range</param>
		/// <param name="newState">New state values</param>
		/// <param name="mouseState">Output mouse state values</param>
		private void Calc(int[] orgRange, int[] newState, int[] mouseState)
		{
			// Sensitivity factor for mouse movement conversion.
			var sensitivity = 16;
			for (int a = 0; a < newState.Length; a++)
			{
				// Use ConvertHelper for mouse scaling with overflow protection
				var value = ConvertHelper.ScaleWithSensitivity(newState[a], orgRange[a], sensitivity, ushort.MinValue, ushort.MaxValue);
				
				// Update original range if value hit limits
				if (value == ushort.MinValue)
				{
					orgRange[a] = newState[a];
				}
				else if (value == ushort.MaxValue)
				{
					orgRange[a] = newState[a] - (ushort.MaxValue / sensitivity);
				}
				
				mouseState[a] = value;
			}
		}

		/// <summary>
		/// Gets buffered updates for DirectInput devices when buffered data is enabled.
		/// DirectInput-specific functionality for detecting rapid button presses.
		/// </summary>
		/// <param name="device">The DirectInput device to get buffered data for</param>
		/// <returns>Array of CustomDiUpdate objects, or null if no buffered data</returns>
		private CustomDiUpdate[] GetBufferedUpdates(UserDevice device)
		{
			if (device.Device == null || device.InputMethod != InputMethod.DirectInput)
				return null;

			try
			{
				// Ensure buffer size is set for DirectInput devices
				if (device.Device.Properties.BufferSize == 0)
				{
					device.Device.Properties.BufferSize = 128;
				}

				// Get buffered data based on device type
				switch (device.Device)
				{
					case Mouse mDevice:
						return mDevice.GetBufferedData()?.Select(x => new CustomDiUpdate(x)).ToArray();
					case Keyboard kDevice:
						return kDevice.GetBufferedData()?.Select(x => new CustomDiUpdate(x)).ToArray();
					case Joystick jDevice:
						return jDevice.GetBufferedData()?.Select(x => new CustomDiUpdate(x)).ToArray();
					default:
						return null;
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Error getting buffered data for {device.DisplayName}: {ex.Message}");
				return null;
			}
		}

		#endregion

		#region DirectInput Device Acquisition Methods

		/// <summary>
		/// DirectInput-specific device acquisition method.
		/// Handles exclusive/non-exclusive mode switching for DirectInput devices.
		/// </summary>
		/// <param name="ud">User device</param>
		/// <param name="detector">Device detector</param>
		/// <param name="device">DirectInput device</param>
		/// <param name="cooperationLevel">Cooperative level to set</param>
		private void DeviceExclusiveMode(UserDevice ud, DeviceDetector detector, Device device, CooperativeLevel cooperationLevel)
		{
			device.Unacquire();
			device.SetCooperativeLevel(detector.DetectorForm.Handle, CooperativeLevel.Background | cooperationLevel);
			device.Acquire();
			ud.IsExclusiveMode = cooperationLevel == CooperativeLevel.Exclusive;
		}

		#endregion
	}
}
