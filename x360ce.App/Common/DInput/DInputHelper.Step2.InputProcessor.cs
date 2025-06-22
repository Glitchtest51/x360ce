using System;
using System.Collections.Generic;
using x360ce.Engine;
using x360ce.Engine.Data;

namespace x360ce.App.DInput
{
	public partial class DInputHelper
	{
		#region Input Processor Registry

		/// <summary>
		/// Registry of available input processors for all input methods.
		/// </summary>
		/// <remarks>
		/// This registry maintains the mapping between InputMethod enums and their corresponding
		/// processor implementations. Each processor handles method-specific input reading,
		/// validation, and force feedback.
		/// 
		/// PROCESSOR ARCHITECTURE:
		/// • DirectInput: Uses hybrid approach (legacy path + processor)
		/// • XInput: Uses XInputProcessor for Xbox controllers (max 4)
		/// • Gaming Input: Future implementation for Windows 10+ UWP bridging
		/// • Raw Input: Future implementation for HID report parsing
		/// 
		/// All processors produce consistent CustomDiState output for compatibility.
		/// </remarks>
		private static readonly Dictionary<InputMethod, IInputProcessor> _processors = new Dictionary<InputMethod, IInputProcessor>
		{
			{ InputMethod.DirectInput, new DirectInputProcessor() },
			{ InputMethod.XInput, new XInputProcessor() },
			// Gaming Input and Raw Input processors will be added when implemented
		};

		/// <summary>
		/// Gets the appropriate input processor for the specified device's selected input method.
		/// </summary>
		/// <param name="device">The device to get a processor for</param>
		/// <returns>The input processor for the device's selected input method</returns>
		/// <exception cref="NotSupportedException">Thrown when the input method is not yet implemented</exception>
		/// <remarks>
		/// This method performs processor lookup based on the device's InputMethod property.
		/// It does NOT automatically select the best processor - the user must manually choose
		/// the appropriate input method for their device.
		/// 
		/// PROCESSOR DISPATCH:
		/// • InputMethod.DirectInput → DirectInputProcessor (but uses legacy hybrid path)
		/// • InputMethod.XInput → XInputProcessor (new processor architecture)
		/// • InputMethod.GamingInput → Not yet implemented
		/// • InputMethod.RawInput → Not yet implemented
		/// </remarks>
		private IInputProcessor GetInputProcessor(UserDevice device)
		{
			var inputMethod = device.InputMethod;
			
			if (_processors.TryGetValue(inputMethod, out var processor))
				return processor;
				
			throw new NotSupportedException($"Input method {inputMethod} is not yet implemented");
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
				var processor = GetInputProcessor(device);
				return processor.ValidateDevice(device);
			}
			catch (NotSupportedException ex)
			{
				return ValidationResult.Error(ex.Message);
			}
		}

		/// <summary>
		/// Gets information about all available input processors.
		/// </summary>
		/// <returns>Dictionary mapping InputMethod to processor instances</returns>
		/// <remarks>
		/// This method provides access to the processor registry for UI components
		/// that need to display available input methods and their capabilities.
		/// 
		/// Used for:
		/// • Populating InputMethod dropdown in device configuration UI
		/// • Checking which methods are currently implemented
		/// • Displaying method-specific information and limitations
		/// </remarks>
		public static Dictionary<InputMethod, IInputProcessor> GetAvailableProcessors()
		{
			return new Dictionary<InputMethod, IInputProcessor>(_processors);
		}

		/// <summary>
		/// Checks if the specified input method is currently implemented and available.
		/// </summary>
		/// <param name="inputMethod">The input method to check</param>
		/// <returns>True if the method is implemented and available, false otherwise</returns>
		/// <remarks>
		/// This method can be used by UI components to:
		/// • Gray out unimplemented options in dropdowns
		/// • Show "Coming Soon" messages for future implementations
		/// • Validate user selections before applying changes
		/// </remarks>
		public static bool IsInputMethodAvailable(InputMethod inputMethod)
		{
			return _processors.ContainsKey(inputMethod);
		}

		/// <summary>
		/// Gets a user-friendly description of the input method including key limitations.
		/// </summary>
		/// <param name="inputMethod">The input method to describe</param>
		/// <returns>Description string suitable for UI display</returns>
		/// <remarks>
		/// These descriptions are used in the UI to help users understand:
		/// • What each input method does
		/// • Key limitations they should be aware of
		/// • When to choose each method
		/// 
		/// The descriptions emphasize critical limitations that affect user experience.
		/// </remarks>
		public static string GetInputMethodDescription(InputMethod inputMethod)
		{
			switch (inputMethod)
			{
				case InputMethod.DirectInput:
					return "DirectInput - All controllers ⚠️ Xbox background issue on Win10+";
				case InputMethod.XInput:
					return "XInput - Xbox only (Max 4) ✅ Background access";
				case InputMethod.GamingInput:
					return "Gaming Input - Win10+ only ⚠️ No background access (Not implemented)";
				case InputMethod.RawInput:
					return "Raw Input - All controllers ✅ Background access (Not implemented)";
				default:
					return $"Unknown input method: {inputMethod}";
			}
		}

		#endregion
	}
}
