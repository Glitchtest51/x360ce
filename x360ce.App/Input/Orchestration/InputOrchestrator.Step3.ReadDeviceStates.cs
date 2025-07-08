using JocysCom.ClassLibrary.IO;
using SharpDX;
using System;
using System.Linq;
using x360ce.App.Input.Processors;
using x360ce.Engine;
using x360ce.Engine.Data;

namespace x360ce.App.Input.Orchestration
{
	public partial class InputOrchestrator
	{
		/// <summary>
		/// Step 3: Read raw device states from all mapped devices using their configured input methods.
		/// This step focuses purely on reading input data without conversion to CustomDeviceState.
		/// </summary>
		/// <param name="game">The current game configuration</param>
		/// <param name="detector">The device detector for DirectInput operations</param>
		/// <remarks>
		/// RAW STATE READING:
		/// • Reads native state data from each input method (DirectInput, XInput, Gaming Input, Raw Input)
		/// • Does not perform CustomDeviceState conversion (handled in Step4)
		/// • Handles input method exceptions and device access errors
		/// • Sets DevicesNeedUpdating flag for device reconnection scenarios
		/// </remarks>
		void ReadDeviceStates(UserGame game, DeviceDetector detector)
		{
			// Get all mapped user devices for the specified game (if game or devices changed).
			if (Global.Orchestrator.SettingsChanged)
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

				object rawState = null;
				CustomDeviceUpdate[] newUpdates = null;
				bool stateReadSuccessfully = false;

				try
				{
					// Handle test devices (virtual/simulated devices for testing)
					if (TestDeviceHelper.ProductGuid.Equals(device.ProductGuid))
					{
						// For test devices, use the existing method and store as CustomDeviceState
						var customState = ProcessTestDevice(device);
						rawState = customState;
						stateReadSuccessfully = true;
					}
					else if (device.InputMethod == InputMethod.DirectInput)
					{
						// Use existing DirectInput processor method
						var customState = directInputProcessor.ProcessDirectInputDevice(device, detector, options, out newUpdates);
						rawState = customState;
						stateReadSuccessfully = (customState != null);
					}
					else if (device.InputMethod == InputMethod.XInput)
					{
						// Use existing XInput processor method
						var customState = xInputProcessor.ProcessXInputDevice(device);
						rawState = customState;
						stateReadSuccessfully = (customState != null);
					}
					else if (device.InputMethod == InputMethod.GamingInput)
					{
						// Use existing Gaming Input processor method
						var customState = gamingInputProcessor.GetCustomState(device);
						rawState = customState;
						stateReadSuccessfully = (customState != null);
					}
					else if (device.InputMethod == InputMethod.RawInput)
					{
						// Use existing Raw Input processor method
						var customState = rawInputProcessor.GetCustomState(device);
						rawState = customState;
						stateReadSuccessfully = (customState != null);
					}

					// Store raw state data in device for Step4 processing
					if (stateReadSuccessfully)
					{
						device.RawInputState = rawState;
						device.RawInputUpdates = newUpdates;
						device.RawStateReadTime = _Stopwatch.ElapsedTicks;
					}
				}
				catch (InputMethodException ex)
				{
					// Add diagnostic data directly to the exception
					ex.Data["Device"] = device.DisplayName;
					ex.Data["InputMethod"] = ex.InputMethod.ToString();
					ex.Data["OrchestrationMethod"] = "ReadDeviceStates";
					JocysCom.ClassLibrary.Runtime.LogHelper.Current.WriteException(ex);

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
					// Add diagnostic data directly to the exception
					ex.Data["Device"] = device.DisplayName;
					ex.Data["InputMethod"] = device.InputMethod.ToString();
					ex.Data["OrchestrationMethod"] = "ReadDeviceStates";
					JocysCom.ClassLibrary.Runtime.LogHelper.Current.WriteException(ex);
					continue;
				}
				catch (Exception ex)
				{
					// Handle DirectInput exceptions (maintaining original behavior for backward compatibility)
					var dex = ex as SharpDXException;
					if (dex != null &&
					(dex.ResultCode == SharpDX.DirectInput.ResultCode.InputLost ||
					 dex.ResultCode == SharpDX.DirectInput.ResultCode.NotAcquired ||
					 dex.ResultCode == SharpDX.DirectInput.ResultCode.Unplugged))
					{
						DevicesNeedUpdating = true;
					}
					else
					{
						// Add diagnostic data directly to the exception
						ex.Data["Device"] = device.DisplayName;
						ex.Data["InputMethod"] = device.InputMethod.ToString();
						ex.Data["OrchestrationMethod"] = "ReadDeviceStates";
						JocysCom.ClassLibrary.Runtime.LogHelper.Current.WriteException(ex);
					}
					device.IsExclusiveMode = null;
					continue;
				}
			}
		}

		/// <summary>
		/// Processes test devices (virtual/simulated devices for testing).
		/// This method is shared and not specific to any input method.
		/// </summary>
		/// <param name="device">The test device to process</param>
		/// <returns>CustomDeviceState for the test device</returns>
		/// <remarks>
		/// Test devices provide simulated controller input for testing purposes.
		/// They generate consistent CustomDeviceState output without requiring physical hardware.
		/// </remarks>
		private CustomDeviceState ProcessTestDevice(UserDevice device)
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
			var customState = new CustomDeviceState(state);
			device.DirectInputDeviceState = state;

			return customState;
		}
	}
}
