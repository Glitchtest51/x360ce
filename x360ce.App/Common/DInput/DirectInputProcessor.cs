using SharpDX;
using SharpDX.DirectInput;
using System;
using System.Diagnostics;
using System.Linq;
using x360ce.Engine;
using x360ce.Engine.Data;

namespace x360ce.App.DInput
{
	/// <summary>
	/// DirectInput processor - Handles Microsoft DirectInput API for all controller types.
	/// </summary>
	/// <remarks>
	/// CRITICAL LIMITATIONS (users must be aware):
	/// ⚠️ **Xbox One controllers CANNOT be accessed in the background on Windows 10+**
	/// ⚠️ **Xbox 360/One controllers have triggers on the same axis** (no separate LT/RT)
	/// ⚠️ **No Guide button access** through DirectInput
	/// ⚠️ **No rumble/force feedback** for Xbox controllers via DirectInput
	/// ⚠️ **Windows Store Apps can't use DirectInput**
	/// ⚠️ **Microsoft no longer recommends using DirectInput** (deprecated)
	/// 
	/// CAPABILITIES:
	/// ✅ All controller types supported (Universal compatibility)
	/// ✅ Unlimited device count
	/// ✅ Generic controllers work perfectly
	/// ✅ Mature, stable API
	/// ✅ Comprehensive device information
	/// 
	/// CONTROLLER MAPPING:
	/// This processor maps DirectInput JoystickState to CustomDiState preserving
	/// the original x360ce DirectInput behavior and mapping patterns.
	/// </remarks>
	public class DirectInputProcessor : IInputProcessor
	{
		#region IInputProcessor Implementation

		/// <summary>
		/// Gets the input method supported by this processor.
		/// </summary>
		public InputMethod SupportedMethod => InputMethod.DirectInput;

		/// <summary>
		/// Determines if this processor can handle the specified device.
		/// </summary>
		/// <param name="device">The user device to check</param>
		/// <returns>True if device has DirectInput device attached</returns>
		/// <remarks>
		/// DirectInput can handle any device that has been successfully enumerated
		/// and attached through the DirectInput device detection system.
		/// 
		/// This is the most compatible input method but has significant limitations
		/// for Xbox controllers on modern Windows systems.
		/// </remarks>
		public bool CanProcess(UserDevice device)
		{
			if (device == null || !device.IsOnline)
				return false;

			// DirectInput requires a valid DirectInput Device object
			return device.Device != null;
		}

		/// <summary>
		/// Reads the current state from the device using DirectInput.
		/// </summary>
		/// <param name="device">The device to read from</param>
		/// <returns>CustomDiState representing the current controller state</returns>
		/// <exception cref="InputMethodException">Thrown when DirectInput encounters errors</exception>
		/// <remarks>
		/// This method preserves the original x360ce DirectInput behavior including:
		/// • Device polling for controllers that require it
		/// • State buffering when enabled
		/// • Mouse coordinate conversion
		/// • Exception handling for common DirectInput errors
		/// 
		/// The method maintains backward compatibility with existing configurations.
		/// </remarks>
		public CustomDiState ReadState(UserDevice device)
		{
			if (device == null)
				throw new InputMethodException(InputMethod.DirectInput, device, "Device is null");

			if (device.Device == null)
				throw new InputMethodException(InputMethod.DirectInput, device, "DirectInput Device is null");

			try
			{
				// Polling - Retrieves data from polled objects on a DirectInput device.
				// Some devices require polling (For example original "XBOX Controller S" with XBCD drivers).
				// If the device does not require polling, calling this method has no effect.
				device.Device.Poll();

				CustomDiState newState = null;

				// Use switch based on pattern matching for supported device types.
				switch (device.Device)
				{
					case Mouse mDevice:
						{
							var state = mDevice.GetCurrentState();
							newState = new CustomDiState(state);
							device.DeviceState = state;
						}
						break;
					case Keyboard kDevice:
						{
							var state = kDevice.GetCurrentState();
							newState = new CustomDiState(state);
							device.DeviceState = state;
						}
						break;
					case Joystick jDevice:
						{
							var state = jDevice.GetCurrentState();
							newState = new CustomDiState(state);
							device.DeviceState = state;
						}
						break;
					default:
						throw new InputMethodException(InputMethod.DirectInput, device, $"Unknown DirectInput device type: {device.Device.GetType().Name}");
				}

				// Handle mouse coordinate conversion if needed
				if (device.Device.Information.Type == DeviceType.Mouse)
				{
					newState = ProcessMouseCoordinates(device, newState);
				}

				return newState;
			}
			catch (SharpDXException dex) when (
				dex.ResultCode == ResultCode.InputLost ||
				dex.ResultCode == ResultCode.NotAcquired ||
				dex.ResultCode == ResultCode.Unplugged)
			{
				// Common DirectInput errors that indicate device issues
				var message = $"DirectInput device error: {dex.ResultCode}. Device may need reacquisition.";
				throw new InputMethodException(InputMethod.DirectInput, device, message, dex);
			}
			catch (Exception ex)
			{
				var message = $"DirectInput read error: {ex.Message}";
				throw new InputMethodException(InputMethod.DirectInput, device, message, ex);
			}
		}

		/// <summary>
		/// Handles force feedback for DirectInput devices.
		/// </summary>
		/// <param name="device">The device to send force feedback to</param>
		/// <param name="ffState">The force feedback state to apply</param>
		/// <remarks>
		/// DirectInput force feedback implementation using the original x360ce logic.
		/// 
		/// IMPORTANT LIMITATION:
		/// Xbox controllers do NOT support force feedback through DirectInput.
		/// Only generic controllers with DirectInput force feedback support will work.
		/// 
		/// For Xbox controllers, users should use XInput method for rumble support.
		/// 
		/// NOTE: Force feedback for DirectInput is handled by the main DInputHelper coordinator
		/// through the existing DirectInput processing logic. This method is called from
		/// the processor interface but the actual force feedback is handled in UpdateDiStates.
		/// </remarks>
		public void HandleForceFeedback(UserDevice device, Engine.ForceFeedbackState ffState)
		{
			// Force feedback for DirectInput is handled through the main DInputHelper
			// in the ProcessDirectInputDevice method which calls the existing force feedback logic
			// This method is called from the main UpdateDiStates coordinator
			
			Debug.WriteLine($"DirectInput: Force feedback processing delegated to main coordinator for {device.DisplayName}");
		}

		/// <summary>
		/// Validates if the device can use DirectInput.
		/// </summary>
		/// <param name="device">The device to validate</param>
		/// <returns>ValidationResult indicating compatibility and limitations</returns>
		/// <remarks>
		/// DirectInput validation focuses on warning users about limitations
		/// rather than blocking usage, since DirectInput works with all devices.
		/// 
		/// Key validation points:
		/// • Xbox controller background access issues on Windows 10+
		/// • Limited Xbox controller features (triggers, Guide button, rumble)
		/// • General compatibility with all device types
		/// </remarks>
		public ValidationResult ValidateDevice(UserDevice device)
		{
			if (device == null)
				return ValidationResult.Error("Device is null");

			if (!device.IsOnline)
				return ValidationResult.Error("Device is offline");

			if (device.Device == null)
				return ValidationResult.Error("DirectInput device not available. Device may need to be reconnected.");

			// Check for Xbox controller limitations
			if (device.IsXboxCompatible)
			{
				var osVersion = Environment.OSVersion.Version;
				var isWindows10Plus = osVersion.Major >= 10;
				
				if (isWindows10Plus)
				{
					return ValidationResult.Warning(
						"⚠️ CRITICAL: Xbox controller with DirectInput on Windows 10+: " +
						"Input will be LOST when window loses focus. " +
						"Triggers combined on same axis. No Guide button. No rumble support. " +
						"Strongly consider using XInput method for Xbox controllers.");
				}
				else
				{
					return ValidationResult.Warning(
						"⚠️ Xbox controller limitations with DirectInput: " +
						"Triggers combined on same axis, no Guide button access, no rumble support. " +
						"Consider using XInput method for full Xbox controller features.");
				}
			}

			// DirectInput works with all controller types, but warn about deprecation
			return ValidationResult.Success(
				"DirectInput compatible. ℹ️ Note: Microsoft recommends modern input APIs for new applications, " +
				"but DirectInput provides maximum compatibility with all controller types.");
		}

		#endregion

		#region DirectInput-Specific Methods

		/// <summary>
		/// Processes mouse coordinate conversion for relative movement.
		/// </summary>
		/// <param name="device">The mouse device</param>
		/// <param name="newState">The current mouse state</param>
		/// <returns>Processed CustomDiState with converted coordinates</returns>
		/// <remarks>
		/// This method implements the original x360ce mouse coordinate processing:
		/// • Stores original state for delta calculation
		/// • Applies sensitivity scaling
		/// • Handles coordinate range clamping
		/// • Maps mouse movement to axis position for wheel controls
		/// 
		/// NOTE: Mouse coordinate processing for DirectInput is handled by the main DInputHelper
		/// in the ProcessDirectInputDevice method. This method serves as a reference implementation.
		/// </remarks>
		private CustomDiState ProcessMouseCoordinates(UserDevice device, CustomDiState newState)
		{
			// If original state is missing then store current values
			if (device.OrgDiState == null)
			{
				device.OrgDiState = newState;
				device.OrgDiStateTime = DateTime.UtcNow.Ticks;
				
				// Make sure new states have zero values for first reading
				for (int a = 0; a < newState.Axis.Length; a++)
					newState.Axis[a] = -short.MinValue;
				for (int s = 0; s < newState.Sliders.Length; s++)
					newState.Sliders[s] = -short.MinValue;
			}

			var mouseState = new CustomDiState(new JoystickState());
			
			// Clone button values
			Array.Copy(newState.Buttons, mouseState.Buttons, mouseState.Buttons.Length);

			// Map mouse position to axis position (good for car wheel controls)
			CalcMouseMovement(device.OrgDiState.Axis, newState.Axis, mouseState.Axis);
			CalcMouseMovement(device.OrgDiState.Sliders, newState.Sliders, mouseState.Sliders);
			
			return mouseState;
		}

		/// <summary>
		/// Mouse axis calculation helper method for converting relative movement to absolute position.
		/// </summary>
		/// <param name="orgRange">Original axis values</param>
		/// <param name="newState">Current axis values</param>
		/// <param name="mouseState">Output processed axis values</param>
		/// <remarks>
		/// This implements the original x360ce mouse sensitivity and range conversion logic.
		/// </remarks>
		private void CalcMouseMovement(int[] orgRange, int[] newState, int[] mouseState)
		{
			// Sensitivity factor for mouse movement conversion
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
		/// </summary>
		/// <param name="device">The device to get buffered data for</param>
		/// <returns>Array of CustomDiUpdate objects, or null if no buffered data</returns>
		/// <remarks>
		/// This method provides access to DirectInput's buffered data feature for
		/// applications that need to process individual input events rather than
		/// just polling the current state.
		/// </remarks>
		public CustomDiUpdate[] GetBufferedUpdates(UserDevice device)
		{
			if (device?.Device == null)
				return null;

			try
			{
				// Ensure buffer size is set
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
				Debug.WriteLine($"DirectInput: Error getting buffered data for {device.DisplayName}: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Checks if DirectInput is available on the current system.
		/// </summary>
		/// <returns>True if DirectInput can be initialized</returns>
		/// <remarks>
		/// This method tests DirectInput availability by attempting to create
		/// a DirectInput instance. Used for system compatibility checking.
		/// </remarks>
		public static bool IsDirectInputAvailable()
		{
			try
			{
				using (var directInput = new SharpDX.DirectInput.DirectInput())
				{
					// If we can create the instance, DirectInput is available
					return true;
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"DirectInput availability check failed: {ex.Message}");
				return false;
			}
		}

		/// <summary>
		/// Gets diagnostic information about DirectInput system status.
		/// </summary>
		/// <returns>String containing DirectInput diagnostic information</returns>
		/// <remarks>
		/// This method provides detailed information for troubleshooting DirectInput issues.
		/// </remarks>
		public static string GetDirectInputDiagnosticInfo()
		{
			var info = new System.Text.StringBuilder();
			
			try
			{
				info.AppendLine($"DirectInput Available: {IsDirectInputAvailable()}");
				info.AppendLine($"Operating System: {Environment.OSVersion}");
				
				// Add information about DirectInput version and capabilities
				using (var directInput = new SharpDX.DirectInput.DirectInput())
				{
					info.AppendLine("DirectInput initialized successfully");
					// Additional diagnostic information could be added here
				}
			}
			catch (Exception ex)
			{
				info.AppendLine($"Error getting DirectInput diagnostic info: {ex.Message}");
			}
			
			return info.ToString();
		}

		#endregion
	}
}
