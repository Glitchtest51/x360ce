using JocysCom.ClassLibrary.IO;
using SharpDX;
using System;
using System.Linq;
using x360ce.Engine;
using x360ce.Engine.Data;

namespace x360ce.App.DInput
{
	public partial class DInputHelper
	{
		#region Shared Fields for All Input Methods

		UserDevice[] mappedDevices = new UserDevice[0];
		UserGame currentGame = SettingsManager.CurrentGame;
		Options options = SettingsManager.Options;
		Boolean isVirtual = false;

		#endregion

		#region CustomDiState Orchestration (Shared Across All Input Methods)

		/// <summary>
		/// Updates device states using the appropriate input methods based on each device's selected input method.
		/// This is the main entry point for reading controller input across all input methods.
		/// </summary>
		/// <param name="game">The current game configuration</param>
		/// <param name="detector">The device detector for DirectInput operations</param>
		/// <remarks>
		/// MULTI-INPUT METHOD ARCHITECTURE:
		/// • Each device can use a different input method (DirectInput, XInput, Gaming Input, Raw Input)
		/// • No automatic fallbacks - user must manually select appropriate input method
		/// • All methods produce consistent CustomDiState output for UI compatibility
		/// • Input-specific processors handle method limitations and provide clear error messages
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

				CustomDeviceState newState = null;
				CustomDeviceUpdate[] newUpdates = null;

				try
				{
					// Handle test devices (virtual/simulated devices for testing)
					if (TestDeviceHelper.ProductGuid.Equals(device.ProductGuid))
					{
						newState = ProcessTestDevice(device);
					}
					else if (device.InputMethod == InputMethod.DirectInput)
					{
						newState = ProcessDirectInputDevice(device, detector, out newUpdates);
					}
					else if (device.InputMethod == InputMethod.XInput)
					{
						newState = ProcessXInputDevice(device);
					}
					else if (device.InputMethod == InputMethod.GamingInput)
					{
						newState = gamingInputProcessor.GetCustomState(device);
					}
					else if (device.InputMethod == InputMethod.RawInput)
					{
						newState = rawInputProcessor.GetCustomState(device);
					}
				}
				catch (InputMethodException ex)
				{
					// Log input method specific errors for debugging
					var cx = new DInputException($"Input method error for {device.DisplayName} using {ex.InputMethod}", ex);
					cx.Data.Add("Device", device.DisplayName);
					cx.Data.Add("InputMethod", ex.InputMethod.ToString());
					JocysCom.ClassLibrary.Runtime.LogHelper.Current.WriteException(cx);
					
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
					// Log unimplemented input methods for debugging
					var cx = new DInputException($"Input method not supported for {device.DisplayName}", ex);
					cx.Data.Add("Device", device.DisplayName);
					cx.Data.Add("InputMethod", device.InputMethod.ToString());
					JocysCom.ClassLibrary.Runtime.LogHelper.Current.WriteException(cx);
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
		/// Updates the device state with new input data and handles button state analysis.
		/// This method is shared across all input methods (DirectInput, XInput, Gaming Input, Raw Input).
		/// </summary>
		/// <param name="device">The device to update</param>
		/// <param name="newState">The new CustomDiState read from the device</param>
		/// <param name="newUpdates">Buffered updates (if available, typically from DirectInput)</param>
		/// <remarks>
		/// This method handles:
		/// • Button state analysis for buffered data (prevents missed button presses)
		/// • State history management (old/new state tracking)
		/// • Timestamp tracking for input timing analysis
		/// </remarks>
		private void UpdateDeviceState(UserDevice device, CustomDeviceState newState, CustomDeviceUpdate[] newUpdates)
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

		/// <summary>
		/// Processes test devices (virtual/simulated devices for testing).
		/// This method is shared and not specific to any input method.
		/// </summary>
		/// <param name="device">The test device to process</param>
		/// <returns>CustomDiState for the test device</returns>
		/// <remarks>
		/// Test devices provide simulated controller input for testing purposes.
		/// They generate consistent CustomDiState output without requiring physical hardware.
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
			device.DeviceState = state;
			
			return customState;
		}

		#endregion

		#region Shared Properties for Input Method Processors

		/// <summary>
		/// Gets whether the current game uses virtual emulation.
		/// Used by all input method processors for force feedback decisions.
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
