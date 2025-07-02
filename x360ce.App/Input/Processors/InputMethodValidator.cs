using System;
using System.Diagnostics;
using x360ce.App.Input.Orchestration;
using x360ce.Engine;
using x360ce.Engine.Data;

namespace x360ce.App.Input.Processors
{
	/// <summary>
	/// Centralized validation service for input methods.
	/// Provides comprehensive validation and compatibility checking for all input methods.
	/// </summary>
	/// <remarks>
	/// This service coordinates validation across all input processors and provides
	/// system-wide compatibility checking functionality.
	/// 
	/// VALIDATION PHILOSOPHY:
	/// • No automatic fallbacks - user must manually select appropriate input method
	/// • Clear, specific error messages explaining limitations
	/// • Graceful handling of system compatibility issues
	/// • Consistent validation results across all input methods
	/// </remarks>
	public static class InputMethodValidator
	{
		#region System Compatibility Checks

		/// <summary>
		/// Performs comprehensive system compatibility check for all input methods.
		/// </summary>
		/// <returns>SystemCompatibilityResult with detailed compatibility information</returns>
		/// <remarks>
		/// This method checks system-wide compatibility for:
		/// • DirectInput API availability
		/// • XInput API availability and controller slots
		/// • Gaming Input API availability (Windows 10+ requirement)
		/// • Raw Input API availability
		/// • Windows version requirements
		/// </remarks>
		public static SystemCompatibilityResult CheckSystemCompatibility()
		{
			var result = new SystemCompatibilityResult();

			try
			{
				// Check DirectInput availability
				result.DirectInputAvailable = CheckDirectInputAvailability();

				// Check XInput availability and controller count
				result.XInputAvailable = InputOrchestrator.Current.xInputProcessor.IsAvailable();
				result.XInputControllerCount = InputOrchestrator.Current.xInputProcessor.GetAssignedControllerCount();
				result.XInputSlotsAvailable = 4 - result.XInputControllerCount;

				// Check Gaming Input availability (Windows 10+ requirement)
				result.GamingInputAvailable = InputOrchestrator.Current.gamingInputProcessor.IsAvailable();
				result.WindowsVersion = Environment.OSVersion.Version;
				result.IsWindows10Plus = result.WindowsVersion.Major >= 10;

				// Check Raw Input availability
				result.RawInputAvailable = InputOrchestrator.Current.rawInputProcessor.IsAvailable();

				// Overall system status
				result.IsSystemCompatible = result.DirectInputAvailable || result.XInputAvailable;

				Debug.WriteLine($"System Compatibility Check: DirectInput={result.DirectInputAvailable}, XInput={result.XInputAvailable}, GamingInput={result.GamingInputAvailable}, RawInput={result.RawInputAvailable}");
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"System compatibility check failed: {ex.Message}");
				result.SystemError = ex.Message;
				result.IsSystemCompatible = false;
			}

			return result;
		}

		/// <summary>
		/// Validates a specific device against a specific input method.
		/// </summary>
		/// <param name="device">The device to validate</param>
		/// <param name="inputMethod">The input method to validate against</param>
		/// <returns>ValidationResult with detailed validation information</returns>
		/// <remarks>
		/// This method provides centralized device validation by dispatching to the
		/// appropriate input processor's validation method.
		/// </remarks>
		public static ValidationResult ValidateDeviceForInputMethod(UserDevice device, InputMethod inputMethod)
		{
			if (device == null)
				return ValidationResult.Error("Device is null");

			if (!device.IsOnline)
				return ValidationResult.Error("Device is offline");

			try
			{
				// Get the appropriate processor for validation
				var orchestrator = InputOrchestrator.Current;
				if (orchestrator == null)
					return ValidationResult.Error("DInputHelper not available");

				switch (inputMethod)
				{
					case InputMethod.DirectInput:
						return ValidateDirectInputDevice(device);

					case InputMethod.XInput:
						return orchestrator.xInputProcessor.ValidateXInputDevice(device);

					case InputMethod.GamingInput:
						return orchestrator.gamingInputProcessor.ValidateDevice(device);

					case InputMethod.RawInput:
						return orchestrator.rawInputProcessor.ValidateDevice(device);

					default:
						return ValidationResult.Error($"Unknown input method: {inputMethod}");
				}
			}
			catch (Exception ex)
			{
				return ValidationResult.Error($"Validation error: {ex.Message}");
			}
		}

		/// <summary>
		/// Gets validation recommendations for a device across all input methods.
		/// </summary>
		/// <param name="device">The device to get recommendations for</param>
		/// <returns>Array of InputMethodRecommendation objects</returns>
		/// <remarks>
		/// This method evaluates the device against all input methods and provides
		/// recommendations with explanations of capabilities and limitations.
		/// 
		/// Results are ordered by compatibility (Success > Warning > Error).
		/// </remarks>
		public static InputMethodRecommendation[] GetInputMethodRecommendations(UserDevice device)
		{
			if (device == null)
				return new InputMethodRecommendation[0];

			var recommendations = new[]
			{
				new InputMethodRecommendation
				{
					InputMethod = InputMethod.DirectInput,
					Validation = ValidateDeviceForInputMethod(device, InputMethod.DirectInput),
					Description = "DirectInput - Universal compatibility",
					Capabilities = "✅ All controllers ✅ Unlimited devices",
					Limitations = "⚠️ Xbox background issues ⚠️ Combined triggers ⚠️ No rumble for Xbox"
				},
				new InputMethodRecommendation
				{
					InputMethod = InputMethod.XInput,
					Validation = ValidateDeviceForInputMethod(device, InputMethod.XInput),
					Description = "XInput - Xbox controllers only",
					Capabilities = "✅ Background access ✅ Separate triggers ✅ Full rumble ✅ Guide button",
					Limitations = "❌ Max 4 controllers ❌ Xbox only"
				},
				new InputMethodRecommendation
				{
					InputMethod = InputMethod.GamingInput,
					Validation = ValidateDeviceForInputMethod(device, InputMethod.GamingInput),
					Description = "Gaming Input - Windows 10+ only",
					Capabilities = "✅ Unlimited controllers ✅ Modern API ✅ Trigger rumble",
					Limitations = "❌ Windows 10+ only ⚠️ No background access ❌ Complex setup"
				},
				new InputMethodRecommendation
				{
					InputMethod = InputMethod.RawInput,
					Validation = ValidateDeviceForInputMethod(device, InputMethod.RawInput),
					Description = "Raw Input - Low-level access",
					Capabilities = "✅ Background access ✅ Unlimited controllers ✅ Any HID device",
					Limitations = "⚠️ Combined triggers ⚠️ No rumble ❌ Complex implementation"
				}
			};

			// Sort by validation status (Success > Warning > Error)
			Array.Sort(recommendations, (a, b) =>
			{
				if (a.Validation.Status != b.Validation.Status)
					return a.Validation.Status.CompareTo(b.Validation.Status);
				return 0;
			});

			return recommendations;
		}

		#endregion

		#region Individual Input Method Availability Checks

		/// <summary>
		/// Checks if DirectInput API is available on the current system.
		/// </summary>
		/// <returns>True if DirectInput is available</returns>
		private static bool CheckDirectInputAvailability()
		{
			try
			{
				// Test DirectInput availability by creating a DirectInput instance
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
		/// Validates DirectInput compatibility for a device.
		/// </summary>
		/// <param name="device">The device to validate</param>
		/// <returns>ValidationResult for DirectInput compatibility</returns>
		private static ValidationResult ValidateDirectInputDevice(UserDevice device)
		{
			if (device == null)
				return ValidationResult.Error("Device is null");

			if (!device.IsOnline)
				return ValidationResult.Error("Device is offline");

			// Check for Xbox controller limitations on Windows 10+
			if (device.IsXboxCompatible)
			{
				var osVersion = Environment.OSVersion.Version;
				var isWindows10Plus = osVersion.Major >= 10;

				if (isWindows10Plus)
				{
					return ValidationResult.Warning(
						"Xbox controller with DirectInput on Windows 10+: " +
						"Input will be lost when window loses focus. " +
						"Consider using XInput for background access.");
				}
				else
				{
					return ValidationResult.Warning(
						"Xbox controller with DirectInput: " +
						"Triggers on same axis, no Guide button, no rumble support. " +
						"Consider using XInput for full Xbox controller features.");
				}
			}

			// DirectInput works with all controller types
			return ValidationResult.Success("DirectInput compatible (works with all controller types)");
		}

		#endregion
	}

	#region Support Classes

	/// <summary>
	/// Represents system-wide compatibility information for all input methods.
	/// </summary>
	public class SystemCompatibilityResult
	{
		/// <summary>
		/// Gets or sets whether DirectInput is available on the system.
		/// </summary>
		public bool DirectInputAvailable { get; set; }

		/// <summary>
		/// Gets or sets whether XInput is available on the system.
		/// </summary>
		public bool XInputAvailable { get; set; }

		/// <summary>
		/// Gets or sets the current number of XInput controllers in use.
		/// </summary>
		public int XInputControllerCount { get; set; }

		/// <summary>
		/// Gets or sets the number of available XInput controller slots.
		/// </summary>
		public int XInputSlotsAvailable { get; set; }

		/// <summary>
		/// Gets or sets whether Gaming Input is available on the system.
		/// </summary>
		public bool GamingInputAvailable { get; set; }

		/// <summary>
		/// Gets or sets whether Raw Input is available on the system.
		/// </summary>
		public bool RawInputAvailable { get; set; }

		/// <summary>
		/// Gets or sets the Windows version.
		/// </summary>
		public Version WindowsVersion { get; set; }

		/// <summary>
		/// Gets or sets whether the system is Windows 10 or later.
		/// </summary>
		public bool IsWindows10Plus { get; set; }

		/// <summary>
		/// Gets or sets whether the overall system is compatible with input methods.
		/// </summary>
		public bool IsSystemCompatible { get; set; }

		/// <summary>
		/// Gets or sets any system error that occurred during compatibility checking.
		/// </summary>
		public string SystemError { get; set; }
	}

	/// <summary>
	/// Represents a recommendation for using a specific input method with a device.
	/// </summary>
	public class InputMethodRecommendation
	{
		/// <summary>
		/// Gets or sets the input method being recommended.
		/// </summary>
		public InputMethod InputMethod { get; set; }

		/// <summary>
		/// Gets or sets the validation result for this input method.
		/// </summary>
		public ValidationResult Validation { get; set; }

		/// <summary>
		/// Gets or sets a human-readable description of the input method.
		/// </summary>
		public string Description { get; set; }

		/// <summary>
		/// Gets or sets a description of the input method's capabilities.
		/// </summary>
		public string Capabilities { get; set; }

		/// <summary>
		/// Gets or sets a description of the input method's limitations.
		/// </summary>
		public string Limitations { get; set; }

		/// <summary>
		/// Gets whether this input method is recommended (Success or Warning validation).
		/// </summary>
		public bool IsRecommended => Validation.IsValid;

		/// <summary>
		/// Gets the priority level of this recommendation (0 = highest).
		/// </summary>
		public int Priority
		{
			get
			{
				switch (Validation.Status)
				{
					case ValidationStatus.Success: return 0;
					case ValidationStatus.Warning: return 1;
					case ValidationStatus.Error: return 2;
					default: return 3;
				}
			}
		}
	}

	#endregion
}
