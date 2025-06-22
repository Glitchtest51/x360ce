using JocysCom.ClassLibrary.IO;
using SharpDX;
using SharpDX.DirectInput;
using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using x360ce.Engine;
using x360ce.Engine.Data;

namespace x360ce.App.DInput
{
	public partial class DInputHelper
	{
		// Input Processor Registry moved to DInputHelper.Step2.InputProcessor.cs
		// XInput processing moved to DInputHelper.Step2.UpdateXiStates.cs

		#region Fields

		UserDevice[] mappedDevices = new UserDevice[0];
		UserGame currentGame = SettingsManager.CurrentGame;
		Options options = SettingsManager.Options;
		Boolean isVirtual = false;

		#endregion

		#region UpdateDiStates Method

		/// <summary>
		/// Updates device states using the appropriate input processors based on each device's selected input method.
		/// This is the main entry point for reading controller input across all input methods.
		/// </summary>
		/// <param name="game">The current game configuration</param>
		/// <param name="detector">The device detector for DirectInput operations</param>
		/// <remarks>
		/// NEW MULTI-INPUT PROCESSOR ARCHITECTURE:
		/// • Each device can use a different input method (DirectInput, XInput, Gaming Input, Raw Input)
		/// • No automatic fallbacks - user must manually select appropriate input method
		/// • Processors handle method-specific limitations and provide clear error messages
		/// • All processors produce consistent CustomDiState output for compatibility
		/// </remarks>
		void UpdateDiStates(UserGame game, DeviceDetector detector)
		{
			// Get all mapped user devices for the specified game (if game or devices changed).
			if (Global.DHelper.SettingsChanged)
			{
				currentGame = game;
				options = SettingsManager.Options;
				mappedDevices = SettingsManager.GetMappedDevices(game?.FileName)
					.Where(x => x != null && x.IsOnline)
					.ToArray();
				isVirtual = ((EmulationType)game.EmulationType).HasFlag(EmulationType.Virtual);
			}

			// Skip processing if testing is enabled but input state reading is disabled
			if (options.TestEnabled && !options.TestGetDInputStates) 
				return;

			foreach (var device in mappedDevices)
			{
				// Skip device if testing is enabled but this device shouldn't be processed
				if (options.TestEnabled && !options.TestGetDInputStates) 
					continue;

				CustomDiState newState = null;
				CustomDiUpdate[] newUpdates = null;

				try
				{
					// Handle test devices (virtual/simulated devices for testing)
					if (TestDeviceHelper.ProductGuid.Equals(device.ProductGuid))
					{
						newState = ProcessTestDevice(device);
					}
					// Handle DirectInput devices using legacy path (maintains existing acquisition logic)
					else if (device.InputMethod == InputMethod.DirectInput && device.Device != null)
					{
						newState = ProcessDirectInputDevice(device, detector, out newUpdates);
					}
					// Handle XInput devices using separated XInput method
					else if (device.InputMethod == InputMethod.XInput)
					{
						newState = ProcessXInputDevice(device);
					}
					// Handle other non-DirectInput devices using processors
					else if (device.InputMethod != InputMethod.DirectInput)
					{
						// Use the appropriate input processor based on device's selected input method
						var processor = GetInputProcessor(device);
						
						// Validate device compatibility with selected input method
						var validation = processor.ValidateDevice(device);
						if (!validation.IsValid)
						{
							Debug.WriteLine($"Input method validation failed for {device.DisplayName}: {validation.Message}");
							continue;
						}

						// Read device state using the selected input method
						newState = processor.ReadState(device);

						// Handle force feedback if the device supports it
						if (device.FFState != null)
						{
							processor.HandleForceFeedback(device, device.FFState);
						}
					}
				}
				catch (InputMethodException ex)
				{
					// Handle input method specific errors
					Debug.WriteLine($"Input method error for {device.DisplayName} using {ex.InputMethod}: {ex.Message}");
					
					// For certain errors, mark devices as needing update
					if (ex.Message.Contains("InputLost") || ex.Message.Contains("NotAcquired"))
					{
						DevicesNeedUpdating = true;
					}
					
					// Continue with next device
					continue;
				}
				catch (NotSupportedException ex)
				{
					// Input method not yet implemented
					Debug.WriteLine($"Input method not supported for {device.DisplayName}: {ex.Message}");
					continue;
				}
				catch (Exception ex)
				{
					// Handle DirectInput exceptions (maintaining original behavior)
					var dex = ex as SharpDXException;
					if (dex != null &&
						(dex.ResultCode == SharpDX.DirectInput.ResultCode.InputLost ||
						 dex.ResultCode == SharpDX.DirectInput.ResultCode.NotAcquired ||
						 dex.ResultCode == SharpDX.DirectInput.ResultCode.Unplugged))
					{
						Debug.WriteLine($"InputLost {DateTime.Now:HH:mm:ss.fff}");
						Debug.WriteLine($"Device {dex.Descriptor.ApiCode}. DisplayName {device.DisplayName}. ProductId {device.DevProductId}. ProductName {device.ProductName}. InstanceName {device.InstanceName}.");
						DevicesNeedUpdating = true;
					}
					else
					{
						// Unexpected error
						var inputMethodName = device.InputMethod.ToString();
						var cx = new DInputException($"UpdateDiStates Exception using {inputMethodName}", ex);
						cx.Data.Add("Device", device.DisplayName);
						cx.Data.Add("InputMethod", inputMethodName);
						JocysCom.ClassLibrary.Runtime.LogHelper.Current.WriteException(cx);
					}
					device.IsExclusiveMode = null;
					continue;
				}

				// Update device state if we successfully read it
				if (newState != null)
				{
					UpdateDeviceState(device, newState, newUpdates);
				}
			}
		}

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
			var exceptionData = new StringBuilder();

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
				DeviceExclusiveMode(device, detector, device.Device, exceptionData, cooperativeLevel);
			}

			exceptionData.AppendFormat($"device.GetCurrentState() // device.IsExclusiveMode = {device.IsExclusiveMode}").AppendLine();

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
				exceptionData.AppendFormat($"AppHelper.GetDeviceObjectsByUsageAndInstanceNumber(device) // device.IsExclusiveMode = {device.IsExclusiveMode}").AppendLine();
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
				exceptionData.AppendFormat($"AppHelper.GetDeviceEffects(device.Device) // device.IsExclusiveMode = {device.IsExclusiveMode}").AppendLine();
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
									LeftMotorSpeed = (force == null) ? short.MinValue : (short)ConvertHelper.ConvertRange(force.LargeMotor, byte.MinValue, byte.MaxValue, short.MinValue, short.MaxValue),
									RightMotorSpeed = (force == null) ? short.MinValue : (short)ConvertHelper.ConvertRange(force.SmallMotor, byte.MinValue, byte.MaxValue, short.MinValue, short.MaxValue)
								};
								// For the future: Investigate device states if force feedback is not working. 
								// var st = device.Device.GetForceFeedbackState();
								// st == SharpDX.DirectInput.ForceFeedbackState
								// device.Device.SendForceFeedbackCommand(ForceFeedbackCommand.SetActuatorsOn);
								exceptionData.AppendFormat("device.FFState.SetDeviceForces(device.Device) // device.IsExclusiveMode = {0}", device.IsExclusiveMode).AppendLine();
								device.FFState.SetDeviceForces(device, device.Device, ps, vibration);
							}
						}
						else if (device.FFState != null)
						{
							// Stop device forces.
							exceptionData.AppendFormat("device.FFState.StopDeviceForces(device.Device) // device.IsExclusiveMode = {0}", device.IsExclusiveMode).AppendLine();
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
		/// Processes test devices (virtual/simulated devices for testing).
		/// </summary>
		/// <param name="device">The test device to process</param>
		/// <returns>CustomDiState for the test device</returns>
		private CustomDiState ProcessTestDevice(UserDevice device)
		{
			// Fill device objects and update masks for test devices
			if (device.DeviceObjects == null)
			{
				device.DeviceObjects = TestDeviceHelper.GetDeviceObjects();
				device.DiAxeMask = 0x1 | 0x2 | 0x4 | 0x8;
				device.DiSliderMask = 0;
			}
			device.DeviceEffects = device.DeviceEffects ?? new DeviceEffectItem[0];
			
			var state = TestDeviceHelper.GetCurrentState(device);
			var customState = new CustomDiState(state);
			device.DeviceState = state;
			
			return customState;
		}

		/// <summary>
		/// Mouse axis calculation helper method.
		/// </summary>
		void Calc(int[] orgRange, int[] newState, int[] mouseState)
		{
			// Sensitivity factor for mouse movement conversion.
			var sensitivity = 16;
			for (int a = 0; a < newState.Length; a++)
			{
				// Get delta from original state and apply sensitivity.
				var value = (newState[a] - orgRange[a]) * sensitivity;
				if (value < ushort.MinValue)
				{
					value = ushort.MinValue;
					orgRange[a] = newState[a];
				}
				if (value > ushort.MaxValue)
				{
					value = ushort.MaxValue;
					orgRange[a] = newState[a] - (ushort.MaxValue / sensitivity);
				}
				mouseState[a] = value;
			}
		}

		/// <summary>
		/// Gets buffered updates for DirectInput devices when buffered data is enabled.
		/// </summary>
		/// <param name="device">The device to get buffered data for</param>
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

		/// <summary>
		/// Updates the device state with new input data and handles button state analysis.
		/// </summary>
		/// <param name="device">The device to update</param>
		/// <param name="newState">The new state read from the device</param>
		/// <param name="newUpdates">Buffered updates (if available)</param>
		private void UpdateDeviceState(UserDevice device, CustomDiState newState, CustomDiUpdate[] newUpdates)
		{
			// Handle button state analysis for buffered data
			if (newUpdates != null && newUpdates.Count(x => x.Type == MapType.Button) > 1 && device.DiState != null)
			{
				// Analyze if state must be modified to ensure button presses are recognized
				for (int b = 0; b < newState.Buttons.Length; b++)
				{
					// If button state was not changed between readings
					if (device.DiState.Buttons[b] == newState.Buttons[b])
					{
						// But buffer contains multiple presses for this button
						if (newUpdates.Count(x => x.Type == MapType.Button && x.Index == b) > 1)
						{
							// Invert state to give the game a chance to recognize the press
							newState.Buttons[b] = !newState.Buttons[b];
						}
					}
				}
			}

			var newTime = _Stopwatch.ElapsedTicks;
			
			// Update state history (remember old values, set new values)
			(device.OldDiState, device.DiState) = (device.DiState, newState);
			(device.OldDiUpdates, device.DiUpdates) = (device.DiUpdates, newUpdates);
			(device.OldDiStateTime, device.DiStateTime) = (device.DiStateTime, newTime);
		}

		#endregion

		#region Legacy DirectInput Support Methods

		/// <summary>
		/// Legacy method for DirectInput device acquisition - used by DirectInputProcessor.
		/// </summary>
		/// <param name="ud">User device</param>
		/// <param name="detector">Device detector</param>
		/// <param name="device">DirectInput device</param>
		/// <param name="exceptionData">Exception data for logging</param>
		/// <param name="cooperationLevel">Cooperative level to set</param>
		private void DeviceExclusiveMode(UserDevice ud, DeviceDetector detector, Device device, StringBuilder exceptionData, CooperativeLevel cooperationLevel)
		{
			string mode = cooperationLevel == CooperativeLevel.Exclusive ? "Exclusive" : "NonExclusive";
			exceptionData.AppendLine($"UnAcquire ({mode})...");
			device.Unacquire();
			exceptionData.AppendLine($"SetCooperativeLevel ({mode})...");
			device.SetCooperativeLevel(detector.DetectorForm.Handle, CooperativeLevel.Background | cooperationLevel);
			exceptionData.AppendLine("Acquire...");
			device.Acquire();
			ud.IsExclusiveMode = cooperationLevel == CooperativeLevel.Exclusive;
		}

		/// <summary>
		/// Gets whether the current game uses virtual emulation.
		/// Used by DirectInputProcessor for force feedback decisions.
		/// </summary>
		public bool IsVirtual => isVirtual;

		/// <summary>
		/// Gets the current DInputHelper instance for processors that need access to helper methods.
		/// </summary>
		public static DInputHelper Current { get; private set; }

		/// <summary>
		/// Sets the current DInputHelper instance.
		/// </summary>
		/// <param name="helper">The helper instance to set</param>
		public static void SetCurrent(DInputHelper helper)
		{
			Current = helper;
		}

		#endregion
	}
}
