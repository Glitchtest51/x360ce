using System;
using x360ce.Engine;
using x360ce.Engine.Data;

namespace x360ce.App.DInput
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
	public class GamingInputProcessor : IInputProcessor
	{
		/// <summary>
		/// Gets the input method supported by this processor.
		/// </summary>
		public InputMethod SupportedMethod => InputMethod.GamingInput;

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
			var helper = DInputHelper.Current;
			if (helper == null)
				return ValidationResult.Error("DInputHelper not available for Gaming Input validation");

			return helper.ValidateGamingInputDevice(device);
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
		public CustomDiState ReadState(UserDevice device)
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
				var result = helper.ProcessGamingInputDevicePublic(device);
				
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
	}
}
