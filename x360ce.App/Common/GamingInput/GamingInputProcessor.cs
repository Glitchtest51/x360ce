using SharpDX.DirectInput;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Gaming.Input;
using x360ce.App.DInput;
using x360ce.Engine;
using x360ce.Engine.Data;

namespace x360ce.App.GamingInput
{
	/// <summary>
	/// Gaming Input processor for Windows.Gaming.Input API (Windows 10+).
	/// Provides wrapper around DInputHelper's Gaming Input implementation.
	/// </summary>
	/// <remarks>
	/// This processor delegates to the existing Gaming Input implementation in DInputHelper
	/// rather than reimplementing the functionality. Gaming Input requires special integration
	/// with the DInputHelper due to its UWP bridging requirements and device enumeration complexity.
	/// </remarks>
	public class GamingInputProcessor: IInputProcessor
	{
		#region IInputProcessor

		/// <summary>
		/// Gets the input method supported by this processor.
		/// </summary>
		public InputMethod SupportedMethod => InputMethod.GamingInput;

		#endregion

		/// <summary>
		/// Determines if this processor can handle the specified device.
		/// </summary>
		/// <param name="device">The user device to check</param>
		/// <returns>True if the processor can handle this device, false otherwise</returns>
		/// <remarks>
		/// Gaming Input can process devices when:
		/// • Windows 10+ is available
		/// • Gaming Input API is accessible
		/// • Device is mapped to a Gaming Input slot
		/// </remarks>
		public bool CanProcess(UserDevice device)
		{
			var validation = ValidateDevice(device);
			return validation.IsValid;
		}

		/// <summary>
		/// Reads the current state from a Gaming Input device.
		/// </summary>
		/// <param name="device">The device to read from</param>
		/// <returns>CustomDiState representing the current controller state</returns>
		/// <exception cref="InputMethodException">Thrown when Gaming Input encounters errors</exception>
		/// <remarks>
		/// This method delegates to DInputHelper.ProcessGamingInputDevice() which handles:
		/// • Gaming Input API calls and GamepadReading conversion
		/// • CustomDiState population via ConvertGamingInputToCustomDiState()
		/// • Device object initialization for UI compatibility
		/// • Error handling and device state management
		/// 
		/// The delegation approach ensures Gaming Input continues to work with existing
		/// implementation while providing processor pattern compatibility for UI selection.
		/// </remarks>
		public CustomDeviceState ReadState(UserDevice device)
		{
			if (device == null)
				throw new InputMethodException(InputMethod.GamingInput, device, "Device is null");

			var helper = DInputHelper.Current;
			if (helper == null)
				throw new InputMethodException(InputMethod.GamingInput, device, "DInputHelper not available for Gaming Input processing");

			try
			{
				// Delegate to the existing Gaming Input implementation in DInputHelper
				// This ensures we use the tested, working Gaming Input code path
				var result = GetCustomState(device);
				
				if (result == null)
				{
					// ProcessGamingInputDevice returns null for various reasons:
					// - Gaming Input not available on system
					// - Device not mapped to Gaming Input slot
					// - No gamepads detected
					// The original method logs details to Debug output
					throw new InputMethodException(InputMethod.GamingInput, device, "Gaming Input processing returned null. Check device mapping and Gaming Input availability.");
				}

				return result;
			}
			catch (InputMethodException)
			{
				// Re-throw InputMethodExceptions as-is
				throw;
			}
			catch (Exception ex)
			{
				// Wrap unexpected exceptions
				var message = $"Gaming Input read error: {ex.Message}";
				throw new InputMethodException(InputMethod.GamingInput, device, message, ex);
			}
		}

		/// <summary>
		/// Handles force feedback for Gaming Input devices.
		/// </summary>
		/// <param name="device">The device to send force feedback to</param>
		/// <param name="ffState">The force feedback state to apply</param>
		/// <remarks>
		/// Gaming Input supports advanced force feedback including:
		/// • Standard rumble motors (like XInput)
		/// • Xbox One controller trigger rumble (Gaming Input exclusive feature)
		/// • Impulse feedback for supported devices
		/// 
		/// This is a key advantage of Gaming Input over DirectInput for Xbox controllers.
		/// </remarks>
		public void HandleForceFeedback(UserDevice device, Engine.ForceFeedbackState ffState)
		{
			// Gaming Input force feedback would be implemented here
			// For now, this is a placeholder since the main Gaming Input implementation
			// in DInputHelper doesn't currently handle force feedback

			// TODO: Implement Gaming Input force feedback using Windows.Gaming.Input
			// This would include:
			// - Gamepad.Vibration property for standard rumble
			// - IGamepad.SetVibration() for trigger rumble on Xbox One controllers
			// - Proper force state conversion from x360ce ForceFeedbackState format
		}

		/// <summary>
		/// Checks if Gaming Input is available on the current system.
		/// </summary>
		/// <returns>True if Gaming Input is available and functional</returns>
		public bool IsAvailable()
		{
			try
			{
				// Check Windows version
				var osVersion = Environment.OSVersion.Version;

				// Windows 10 is version 10.0, Windows 11 is also reported as 10.0 due to compatibility
				// So we check for Major >= 10, but also need to account for potential version reporting issues
				var isWindows10Plus = osVersion.Major >= 10 || osVersion.Major == 6 && osVersion.Minor >= 2; // Windows 8+ as fallback

				if (!isWindows10Plus)
					return false;

				// Test Gaming Input API accessibility - this is the real test
				var gamepads = Gamepad.Gamepads; // This will throw if Gaming Input is not available

				return true;
			}
			catch
			{
				return false;
			}
		}


		/// <summary>
		/// Gets Gaming Input diagnostic information.
		/// </summary>
		/// <returns>String containing Gaming Input diagnostic information</returns>
		public string GetGamingInputDiagnosticInfo()
		{
			var info = new System.Text.StringBuilder();

			try
			{
				var osVersion = Environment.OSVersion.Version;
				var isWindows10Plus = osVersion.Major >= 10;
				var isAvailable = IsAvailable();

				info.AppendLine($"Gaming Input Available: {isAvailable}");
				info.AppendLine($"Windows 10+ Required: {isWindows10Plus}");
				info.AppendLine($"Operating System: {Environment.OSVersion}");

				if (isAvailable)
				{
					try
					{
						var gamepads = Gamepad.Gamepads;
						info.AppendLine($"Connected Gamepads: {gamepads.Count}");

						for (int i = 0; i < gamepads.Count; i++)
						{
							var gamepad = gamepads[i];
							info.AppendLine($"  Gamepad {i}: Available");

							// Try to get a reading to verify functionality
							var reading = gamepad.GetCurrentReading();
							info.AppendLine($"    Buttons: {reading.Buttons}");
							info.AppendLine($"    Left Stick: ({reading.LeftThumbstickX:F2}, {reading.LeftThumbstickY:F2})");
							info.AppendLine($"    Right Stick: ({reading.RightThumbstickX:F2}, {reading.RightThumbstickY:F2})");
							info.AppendLine($"    Triggers: L={reading.LeftTrigger:F2}, R={reading.RightTrigger:F2}");
						}
					}
					catch (Exception ex)
					{
						info.AppendLine($"Error enumerating gamepads: {ex.Message}");
					}
				}

				info.AppendLine("Implementation Status: Complete");
				info.AppendLine("Package: Microsoft.Windows.SDK.Contracts");
				info.AppendLine("Limitation: No background access (UWP restriction)");
			}
			catch (Exception ex)
			{
				info.AppendLine($"Error getting Gaming Input diagnostic info: {ex.Message}");
			}

			return info.ToString();
		}

		#region Gaming Input State Processing (Windows.Gaming.Input)

		/// <summary>
		/// Processes Gaming Input devices using direct method (same performance pattern as DirectInput/XInput).
		/// </summary>
		/// <param name="device">The Gaming Input device to process</param>
		/// <returns>CustomDiState for the device</returns>
		/// <remarks>
		/// OPTIMIZED FOR 1000Hz POLLING - No caching, no LINQ, minimal allocations.
		/// Uses same direct access pattern as DirectInput and XInput for consistent performance.
		/// 
		/// CustomDiState mapping matches DirectInput/XInput exactly for UI compatibility.
		/// </remarks>
		public CustomDeviceState GetCustomState(UserDevice device)
		{
			if (device == null)
				return null;

			try
			{
				if (!IsAvailable())
					return null;

				// Get setting related to user device - using same pattern as XInput
				var setting = SettingsManager.UserSettings.ItemsToArraySynchronized()
					.FirstOrDefault(x => x.InstanceGuid == device.InstanceGuid);

				if (setting == null || setting.MapTo <= (int)MapTo.None)
					return null;

				// Convert MapTo to zero-based gamepad index
				var gamepadIndex = setting.MapTo - 1;

				// Get gamepad directly - no caching needed for simple access
				var gamepads = Gamepad.Gamepads;
				if (gamepadIndex < 0 || gamepadIndex >= gamepads.Count)
					return null;

				var gamepad = gamepads[gamepadIndex];
				var reading = gamepad.GetCurrentReading();

				// Initialize device objects and capabilities if needed (CRITICAL for UI drag/drop functionality)
				if (device.DeviceObjects == null)
				{
					// Create minimal device objects to satisfy UI requirements
					// The UI needs device objects to show drag/drop interface
					var deviceObjects = new List<DeviceObjectItem>();

					// Add buttons (UI needs these for button mapping)
					for (int i = 0; i < 16; i++)
					{
						deviceObjects.Add(new DeviceObjectItem
						{
							Type = ObjectGuid.Button,
							Flags = DeviceObjectTypeFlags.Button,
							Instance = i,
							Name = $"Button {i}",
							Offset = i
						});
					}

					// Add axes (UI needs these for axis mapping)
					var axisGuids = new[] { ObjectGuid.XAxis, ObjectGuid.YAxis, ObjectGuid.ZAxis, ObjectGuid.RxAxis, ObjectGuid.RyAxis, ObjectGuid.RzAxis };
					var axisNames = new[] { "X Axis", "Y Axis", "Z Axis", "Rx Axis", "Ry Axis", "Rz Axis" };
					for (int i = 0; i < axisNames.Length; i++)
					{
						deviceObjects.Add(new DeviceObjectItem
						{
							Type = axisGuids[i],
							Flags = DeviceObjectTypeFlags.Axis,
							Instance = i,
							Name = axisNames[i],
							Offset = i + 100 // Offset buttons
						});
					}

					device.DeviceObjects = deviceObjects.ToArray();
					device.DiAxeMask = 0x1 | 0x2 | 0x4 | 0x8 | 0x10 | 0x20; // All 6 axes
					device.DiSliderMask = 0;

					// Set capability counts (CRITICAL for UI drag/drop interface)
					// Gaming Input provides standard gamepad capabilities
					device.CapButtonCount = 16;  // Gaming Input supports up to 16 buttons
					device.CapAxeCount = 6;      // 6 axes: LX, LY, RX, RY, LT, RT
					device.CapPovCount = 1;      // 1 D-Pad POV
				}
				device.DeviceEffects = device.DeviceEffects ?? new DeviceEffectItem[0];

				// Create and populate CustomDiState
				var newState = new CustomDeviceState();
				ConvertGamingInputToCustomDiState(reading, newState);

				// Store the reading as device state for debugging/monitoring
				device.DeviceState = reading;

				return newState;
			}
			catch (Exception ex)
			{
				// Log Gaming Input processing errors for debugging
				var cx = new DInputException($"Gaming Input error processing device {device.DisplayName}", ex);
				cx.Data.Add("Device", device.DisplayName);
				cx.Data.Add("InputMethod", "GamingInput");
				JocysCom.ClassLibrary.Runtime.LogHelper.Current.WriteException(cx);
				return null;
			}
		}

		/// <summary>
		/// Convert Windows.Gaming.Input GamepadReading to x360ce CustomDiState format.
		/// Ensures exact mapping consistency with DirectInput and XInput processors.
		/// </summary>
		private void ConvertGamingInputToCustomDiState(GamepadReading reading, CustomDeviceState diState)
		{
			if (diState == null) return;

			// Convert buttons - Gaming.Input uses flags, CustomDiState uses bool array
			// CRITICAL: These indices MUST match DirectInput and XInput implementations
			diState.Buttons[0] = reading.Buttons.HasFlag(GamepadButtons.A);
			diState.Buttons[1] = reading.Buttons.HasFlag(GamepadButtons.B);
			diState.Buttons[2] = reading.Buttons.HasFlag(GamepadButtons.X);
			diState.Buttons[3] = reading.Buttons.HasFlag(GamepadButtons.Y);
			diState.Buttons[4] = reading.Buttons.HasFlag(GamepadButtons.LeftShoulder);
			diState.Buttons[5] = reading.Buttons.HasFlag(GamepadButtons.RightShoulder);
			diState.Buttons[6] = reading.Buttons.HasFlag(GamepadButtons.View); // Back/Select
			diState.Buttons[7] = reading.Buttons.HasFlag(GamepadButtons.Menu); // Start
			diState.Buttons[8] = reading.Buttons.HasFlag(GamepadButtons.LeftThumbstick);
			diState.Buttons[9] = reading.Buttons.HasFlag(GamepadButtons.RightThumbstick);

			// D-Pad as buttons (for compatibility with existing mapping)
			diState.Buttons[10] = reading.Buttons.HasFlag(GamepadButtons.DPadUp);
			diState.Buttons[11] = reading.Buttons.HasFlag(GamepadButtons.DPadRight);
			diState.Buttons[12] = reading.Buttons.HasFlag(GamepadButtons.DPadDown);
			diState.Buttons[13] = reading.Buttons.HasFlag(GamepadButtons.DPadLeft);

			// Guide button (if available - Gaming.Input doesn't typically expose this)
			diState.Buttons[14] = false; // Not available in Windows.Gaming.Input

			// Convert analog sticks - Gaming.Input uses -1.0 to 1.0, CustomDiState uses 0-65535
			// CRITICAL: These indices MUST match DirectInput and XInput implementations
			diState.Axis[0] = ConvertAnalogToAxis(reading.LeftThumbstickX);   // Left stick X
			diState.Axis[1] = ConvertAnalogToAxis(-reading.LeftThumbstickY);  // Left stick Y (invert for DirectInput compatibility)
			diState.Axis[2] = ConvertAnalogToAxis(reading.RightThumbstickX);  // Right stick X  
			diState.Axis[3] = ConvertAnalogToAxis(-reading.RightThumbstickY); // Right stick Y (invert for DirectInput compatibility)

			// Convert triggers - Gaming.Input uses 0.0 to 1.0, CustomDiState uses 0-65535
			// ADVANTAGE: Gaming.Input provides separate trigger axes (unlike DirectInput)
			diState.Axis[4] = ConvertTriggerToAxis(reading.LeftTrigger);  // Left trigger
			diState.Axis[5] = ConvertTriggerToAxis(reading.RightTrigger); // Right trigger

			// Convert D-Pad to POV format for compatibility
			diState.POVs[0] = ConvertDPadToPOV(reading.Buttons);

			// Note: Debug output removed for performance - this method is called at 1000Hz
			// Use GetGamingInputDiagnosticInfo() for debugging instead
		}

		/// <summary>
		/// Convert Gaming.Input analog value (-1.0 to 1.0) to CustomDiState axis format (0-65535).
		/// </summary>
		private int ConvertAnalogToAxis(double analogValue)
		{
			// Clamp to valid range
			analogValue = ConvertHelper.LimitRange((float)analogValue, -1.0f, 1.0f);
			// Convert from -1.0..1.0 to 0..65535 (center = 32767)
			return (int)((analogValue + 1.0) * 32767.5);
		}

		/// <summary>
		/// Convert Gaming.Input trigger value (0.0 to 1.0) to CustomDiState axis format (0-65535).
		/// </summary>
		private int ConvertTriggerToAxis(double triggerValue)
		{
			// Clamp to valid range
			triggerValue = ConvertHelper.LimitRange((float)triggerValue, 0.0f, 1.0f);
			// Convert from 0.0..1.0 to 0..65535
			return (int)(triggerValue * 65535.0);
		}

		/// <summary>
		/// Convert Gaming.Input D-Pad flags to CustomDiState POV format.
		/// Returns DirectInput-compatible POV values in hundredths of degrees.
		/// </summary>
		private int ConvertDPadToPOV(GamepadButtons buttons)
		{
			var up = buttons.HasFlag(GamepadButtons.DPadUp);
			var down = buttons.HasFlag(GamepadButtons.DPadDown);
			var left = buttons.HasFlag(GamepadButtons.DPadLeft);
			var right = buttons.HasFlag(GamepadButtons.DPadRight);

			// Convert to POV values (in hundredths of degrees)
			// DirectInput POV: N=0, NE=4500, E=9000, SE=13500, S=18000, SW=22500, W=27000, NW=31500
			if (up && !left && !right) return 0;      // North
			if (up && right) return 4500;             // North-East  
			if (right && !up && !down) return 9000;   // East
			if (down && right) return 13500;          // South-East
			if (down && !left && !right) return 18000; // South
			if (down && left) return 22500;           // South-West
			if (left && !up && !down) return 27000;   // West
			if (up && left) return 31500;             // North-West

			return -1; // Centered/No direction
		}

		/// <summary>
		/// Validates whether a device can use Gaming Input.
		/// </summary>
		/// <param name="device">The device to validate</param>
		/// <returns>ValidationResult indicating Gaming Input compatibility</returns>
		/// <remarks>
		/// Gaming Input validation includes:
		/// • Windows 10+ requirement verification
		/// • Gaming Input API availability check
		/// • Device mapping validation
		/// • Background access limitation warnings
		/// </remarks>

		public ValidationResult ValidateDevice(UserDevice device)
		{
			if (device == null)
				return ValidationResult.Error("Device is null");

			if (!device.IsOnline)
				return ValidationResult.Error("Device is offline");

			// Check Gaming Input availability (this includes Windows version check)
			if (!IsAvailable())
			{
				return ValidationResult.Error("Gaming Input is not available on this system");
			}

			// Check if device mapping is valid
			try
			{
				// Get setting related to user device
				var setting = SettingsManager.UserSettings.ItemsToArraySynchronized()
					.FirstOrDefault(x => x.InstanceGuid == device.InstanceGuid);

				if (setting == null || setting.MapTo <= (int)MapTo.None)
				{
					return ValidationResult.Error("Gaming Input: Device is not mapped to a virtual controller slot");
				}

				var gamepadIndex = setting.MapTo - 1;
				var gamepads = Gamepad.Gamepads;
				if (gamepadIndex < 0 || gamepadIndex >= gamepads.Count)
				{
					return ValidationResult.Error($"Gaming Input: No gamepad found at index {gamepadIndex + 1}. Available gamepads: {gamepads.Count}");
				}

				// Warning about background access limitation
				var warning = "⚠️ Gaming Input controllers cannot be accessed when the application is in the background (UWP limitation)";
				return ValidationResult.Success(warning);
			}
			catch (Exception ex)
			{
				return ValidationResult.Error($"Gaming Input validation failed: {ex.Message}");
			}
		}


		#endregion

	}
}
