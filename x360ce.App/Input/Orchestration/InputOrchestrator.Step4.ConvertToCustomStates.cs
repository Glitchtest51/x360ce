using System;
using System.Linq;
using x360ce.Engine;
using x360ce.Engine.Data;

namespace x360ce.App.Input.Orchestration
{
	public partial class InputOrchestrator
	{
		/// <summary>
		/// Step 4: Convert raw device states to CustomDeviceState format.
		/// This step processes the raw input data read in Step3 and converts it to the standardized CustomDeviceState.
		/// </summary>
		/// <param name="game">The current game configuration</param>
		/// <remarks>
		/// CUSTOMDEVICESTATE CONVERSION:
		/// • Converts input method-specific raw states to unified CustomDeviceState format
		/// • Handles button state analysis for buffered DirectInput data
		/// • Manages state history (old/new state tracking)
		/// • Processes test device simulation
		/// • Updates device timing information
		/// </remarks>
		void ConvertToCustomStates(UserGame game)
		{
			foreach (var device in mappedDevices)
			{
				// Skip device if no raw state was read in Step3
				if (device.RawInputState == null)
					continue;

				try
				{
					// The RawInputState is already a CustomDeviceState from Step3
					// So we just need to process it and update the device state
					var newState = device.RawInputState as CustomDeviceState;

					if (newState != null)
					{
						UpdateDeviceState(device, newState, device.RawInputUpdates);
					}
				}
				catch (Exception ex)
				{
					// Log conversion errors but continue with other devices
					System.Diagnostics.Debug.WriteLine($"Step4: State processing failed for {device.DisplayName} ({device.InputMethod}): {ex.Message}");
					JocysCom.ClassLibrary.Runtime.LogHelper.Current.WriteException(ex);
				}

				// Clear raw state data after processing to free memory
				device.RawInputState = null;
				device.RawInputUpdates = null;
			}
		}

		/// <summary>
		/// Updates the device state with new input data and handles button state analysis.
		/// This method is shared across all input methods (DirectInput, XInput, Gaming Input, Raw Input).
		/// </summary>
		/// <param name="device">The device to update</param>
		/// <param name="newState">The new CustomDeviceState read from the device</param>
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
			if (newUpdates != null && newUpdates.Count(x => x.Type == MapType.Button) > 1 && device.DeviceState != null)
			{
				// Analyze if state must be modified to ensure button presses are recognized
				for (int b = 0; b < newState.Buttons.Length; b++)
				{
					// If button state was not changed between readings
					if (device.DeviceState.Buttons[b] == newState.Buttons[b])
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
			(device.OldDeviceState, device.DeviceState) = (device.DeviceState, newState);
			(device.OldDeviceUpdates, device.DeviceUpdates) = (device.DeviceUpdates, newUpdates);
			(device.OldDiStateTime, device.DeviceStateTime) = (device.DeviceStateTime, newTime);
		}

	}
}
