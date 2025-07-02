using System;
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
	}
}
