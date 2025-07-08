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
		#region Input Processor Registry


		public DirectInputProcessor directInputProcessor = new DirectInputProcessor();
		public XInputProcessor xInputProcessor = new XInputProcessor();
		public GamingInputProcessor gamingInputProcessor = new GamingInputProcessor();
		public RawInputProcessor rawInputProcessor = new RawInputProcessor();

		/// <summary>
		/// Loads device capabilities using the appropriate processor based on the device's input method.
		/// This is the centralized entry point for capability loading across all input methods.
		/// </summary>
		/// <param name="device">The device to load capabilities for</param>
		/// <remarks>
		/// This method ensures capabilities are loaded consistently across all input methods:
		/// • DirectInput: Real hardware detection via DirectInput API (default method)
		/// • XInput: Standard Xbox controller capabilities (15 buttons, 6 axes)
		/// • Gaming Input: Gaming Input API capabilities (16 buttons, 6 axes)
		/// • Raw Input: HID descriptor-based capabilities with reasonable defaults
		///
		/// Called during device initialization and when input method changes.
		/// Handles capability loading failures gracefully with appropriate fallbacks.
		/// </remarks>
		public void LoadDeviceCapabilities(UserDevice device)
		{
			if (device == null)
				return;

			try
			{
				switch (device.InputMethod)
				{
					case InputMethod.DirectInput:
						directInputProcessor.LoadCapabilities(device);
						break;
					case InputMethod.XInput:
						xInputProcessor.LoadCapabilities(device);
						break;
					case InputMethod.GamingInput:
						gamingInputProcessor.LoadCapabilities(device);
						break;
					case InputMethod.RawInput:
						rawInputProcessor.LoadCapabilities(device);
						break;
					default:
						throw new ArgumentException($"Invalid InputMethod: {device.InputMethod}");
				}

				System.Diagnostics.Debug.WriteLine($"Loaded {device.InputMethod} capabilities for {device.DisplayName} - Buttons: {device.CapButtonCount}, Axes: {device.CapAxeCount}, POVs: {device.CapPovCount}");
			}
			catch (Exception ex)
			{
				// Log error and clear capability values
				System.Diagnostics.Debug.WriteLine($"Capability loading failed for {device.DisplayName} ({device.InputMethod}): {ex.Message}");

				// Clear capability values instead of setting fake ones
				device.CapButtonCount = 0;
				device.CapAxeCount = 0;
				device.CapPovCount = 0;
				device.DeviceObjects = new DeviceObjectItem[0];
				device.DeviceEffects = new DeviceEffectItem[0];
			}
		}

		/// <summary>
		/// Gets detailed capability information using the appropriate processor.
		/// </summary>
		/// <param name="device">The device to get capability information for</param>
		/// <returns>String containing detailed capability information</returns>
		public string GetDeviceCapabilitiesInfo(UserDevice device)
		{
			if (device == null)
				return "Device is null";

			try
			{
				switch (device.InputMethod)
				{
					case InputMethod.DirectInput:
						return directInputProcessor.GetCapabilitiesInfo(device);
					case InputMethod.XInput:
						return xInputProcessor.GetCapabilitiesInfo(device);
					case InputMethod.GamingInput:
						return gamingInputProcessor.GetCapabilitiesInfo(device);
					case InputMethod.RawInput:
						return rawInputProcessor.GetCapabilitiesInfo(device);
					default:
						return $"Unknown InputMethod: {device.InputMethod}";
				}
			}
			catch (Exception ex)
			{
				return $"Error getting capability info: {ex.Message}";
			}
		}

		/// <summary>
		/// Reloads capabilities when the input method changes.
		/// Ensures the device has accurate capabilities for the new input method.
		/// </summary>
		/// <param name="device">The device whose input method changed</param>
		/// <param name="previousMethod">The previous input method (for logging)</param>
		public void OnInputMethodChanged(UserDevice device, InputMethod previousMethod)
		{
			if (device == null)
				return;

			try
			{
				System.Diagnostics.Debug.WriteLine($"Input method changed for {device.DisplayName}: {previousMethod} → {device.InputMethod}");
				
				// Reload capabilities for the new input method
				LoadDeviceCapabilities(device);
				
				System.Diagnostics.Debug.WriteLine($"Capabilities updated for {device.DisplayName} - New method: {device.InputMethod}");
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Failed to update capabilities after input method change for {device.DisplayName}: {ex.Message}");
			}
		}

		/// <summary>
		/// Validates that a device can be processed with its selected input method.
		/// </summary>
		/// <param name="device">The device to validate</param>
		/// <returns>ValidationResult indicating compatibility and any limitations</returns>
		/// <remarks>
		/// This method provides comprehensive validation beyond simple compatibility checking.
		/// It returns detailed information about:
		/// • Device compatibility with the selected input method
		/// • Method-specific limitations and warnings
		/// • Clear error messages for unsupported combinations
		///
		/// VALIDATION EXAMPLES:
		/// • XInput with 5th controller: Error("XInput supports maximum 4 controllers")
		/// • DirectInput with Xbox on Win10: Warning("Xbox controllers may not work in background")
		/// • Gaming Input on Win7: Error("Gaming Input requires Windows 10 or later")
		///
		/// The validation does NOT recommend alternative methods - users must choose manually.
		/// </remarks>
		public ValidationResult ValidateDeviceInputMethod(UserDevice device)
		{
			if (device == null)
				return ValidationResult.Error("Device is null");
			try
			{

				switch (device.InputMethod)
				{
					case InputMethod.DirectInput:
						return directInputProcessor.ValidateDevice(device);
					case InputMethod.XInput:
						return xInputProcessor.ValidateDevice(device);
					case InputMethod.GamingInput:
						return gamingInputProcessor.ValidateDevice(device);
					case InputMethod.RawInput:
						return rawInputProcessor.ValidateDevice(device);
					default:
						return ValidationResult.Error($"Unknown InputMethod: {device.InputMethod}");
				}
			}
			catch (NotSupportedException ex)
			{
				return ValidationResult.Error(ex.Message);
			}
		}

		#endregion

		#region Shared Fields for All Input Methods

		UserDevice[] mappedDevices = new UserDevice[0];
		UserGame currentGame = SettingsManager.CurrentGame;
		Options options = SettingsManager.Options;
		public bool isVirtual = false;

		#endregion

		#region CustomDeviceState Orchestration (Shared Across All Input Methods)

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
		/// • All methods produce consistent CustomDeviceState output for UI compatibility
		/// • Input-specific processors handle method limitations and provide clear error messages
		/// </remarks>
		void UpdateCustomDeviceStates(UserGame game, DeviceDetector detector)
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
						newState = directInputProcessor.ProcessDirectInputDevice(device, detector, options, out newUpdates);
					}
					else if (device.InputMethod == InputMethod.XInput)
					{
						newState = xInputProcessor.ProcessXInputDevice(device);
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
					// Add diagnostic data directly to the exception
					ex.Data["Device"] = device.DisplayName;
					ex.Data["InputMethod"] = ex.InputMethod.ToString();
					ex.Data["OrchestrationMethod"] = "UpdateDiStates";
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
					ex.Data["OrchestrationMethod"] = "UpdateDiStates";
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
						ex.Data["OrchestrationMethod"] = "UpdateDiStates";
						JocysCom.ClassLibrary.Runtime.LogHelper.Current.WriteException(ex);
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

		#endregion

		#region Shared Properties for Input Method Processors

		/// <summary>
		/// Gets whether the current game uses virtual emulation.
		/// Used by all input method processors for force feedback decisions.
		/// </summary>
		public bool IsVirtual => isVirtual;

		/// <summary>
		/// Gets the current InputOrchestrator instance for processors that need access to helper methods.
		/// </summary>
		public static InputOrchestrator Current { get; private set; }

		/// <summary>
		/// Sets the current InputOrchestrator instance.
		/// </summary>
		/// <param name="helper">The helper instance to set</param>
		public static void SetCurrent(InputOrchestrator helper)
		{
			Current = helper;
		}

		#endregion


	}
}
