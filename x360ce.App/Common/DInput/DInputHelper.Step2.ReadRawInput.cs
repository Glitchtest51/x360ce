using System;
using System.Collections.Generic;
using System.Diagnostics;
using x360ce.Engine;
using x360ce.Engine.Data;

namespace x360ce.App.DInput
{
	public partial class DInputHelper
	{
		#region Raw Input State Processing

		/// <summary>
		/// Processes devices using Raw Input API for HID-compliant controllers.
		/// </summary>
		/// <param name="device">The HID-compliant device to process</param>
		/// <returns>CustomDiState for the device, or null if reading failed</returns>
		/// <remarks>
		/// ⚠️ CRITICAL: MUST OUTPUT CONSISTENT CustomDiState FORMAT ⚠️
		/// 
		/// CustomDiState is the ONLY format used by the existing UI and mapping system.
		/// This method MUST map Raw Input controls to the EXACT SAME CustomDiState indices
		/// used by DirectInput, XInput, and Gaming Input for consistency.
		/// 
		/// MANDATORY CUSTOMDISTATE MAPPING (MUST match other input methods):
		/// Raw Input uses HID reports, so mapping depends on device-specific HID descriptors.
		/// However, for common controllers (Xbox, PlayStation), MUST map to:
		/// • Buttons[0] = Primary action button (A on Xbox, Cross on PlayStation)
		/// • Buttons[1] = Secondary action button (B on Xbox, Circle on PlayStation)
		/// • Buttons[2] = Third action button (X on Xbox, Square on PlayStation)
		/// • Buttons[3] = Fourth action button (Y on Xbox, Triangle on PlayStation)
		/// • Buttons[4] = Left Shoulder (LB/L1)
		/// • Buttons[5] = Right Shoulder (RB/R1)
		/// • Buttons[6] = Back/Select/Share button
		/// • Buttons[7] = Start/Menu/Options button
		/// • Buttons[8] = Left Thumbstick Click (LS/L3)
		/// • Buttons[9] = Right Thumbstick Click (RS/R3)
		/// • Buttons[10] = D-Pad Up
		/// • Buttons[11] = D-Pad Right
		/// • Buttons[12] = D-Pad Down
		/// • Buttons[13] = D-Pad Left
		/// • Buttons[14] = Guide/Home button (when available via HID)
		/// • Axis[0] = Left Thumbstick X (-32768 to 32767)
		/// • Axis[1] = Left Thumbstick Y (-32768 to 32767)
		/// • Axis[2] = Right Thumbstick X (-32768 to 32767)
		/// • Axis[3] = Right Thumbstick Y (-32768 to 32767)
		/// • Axis[4] = Left Trigger OR Combined Triggers (limitation for some controllers)
		/// • Axis[5] = Right Trigger (when separate) or unused
		/// 
		/// RAW INPUT METHOD CAPABILITIES:
		/// • Controllers CAN be accessed in the background (major advantage)
		/// • Unlimited number of controllers
		/// • Works with any HID-compliant device
		/// • Low-level access to device data
		/// • Most direct access to hardware input
		/// 
		/// RAW INPUT METHOD LIMITATIONS:
		/// • Xbox 360/One controllers have triggers on same axis (same as DirectInput)
		/// • No Guide button access (most HID reports exclude it)
		/// • NO rumble support (Raw Input is input-only)
		/// • Requires manual HID report parsing (complex implementation)
		/// • No built-in controller abstraction (custom profiles needed)
		/// • Complex setup and device registration required
		/// </remarks>
		private CustomDiState ProcessRawInputDevice(UserDevice device)
		{
			if (device == null)
				return null;
			try
			{
				// Use the RawInputProcessor for actual processing
				// Validate device compatibility
				var validation = rawInputProcessor.ValidateDevice(device);
				if (!validation.IsValid)
					return null;
				// Read device state using Raw Input
				var customState = RawInputProcessor.ReadState(device);

				// Handle force feedback (Raw Input doesn't support output, just log)
				if (device.FFState != null)
				{
					RawInputProcessor.HandleForceFeedback(device, device.FFState);
				}
				return customState;
			}
			catch (InputMethodException ex)
			{
				// Log Raw Input specific errors for debugging
				var cx = new DInputException($"Raw Input error for {device.DisplayName}", ex);
				cx.Data.Add("Device", device.DisplayName);
				cx.Data.Add("InputMethod", "RawInput");
				JocysCom.ClassLibrary.Runtime.LogHelper.Current.WriteException(cx);
				return null;
			}
			catch (Exception ex)
			{
				// Log unexpected Raw Input errors for debugging
				var cx = new DInputException($"Unexpected Raw Input error for {device.DisplayName}", ex);
				cx.Data.Add("Device", device.DisplayName);
				cx.Data.Add("InputMethod", "RawInput");
				JocysCom.ClassLibrary.Runtime.LogHelper.Current.WriteException(cx);
				return null;
			}
		}

		/// <summary>
		/// Validates if a device can use Raw Input and provides detailed validation results.
		/// </summary>
		/// <param name="device">The device to validate for Raw Input compatibility</param>
		/// <returns>ValidationResult with detailed compatibility information</returns>
		/// <remarks>
		/// This method checks:
		/// • Device HID compliance and information availability
		/// • Raw Input API availability on the system
		/// • Device online status
		/// 
		/// VALIDATION RESULTS:
		/// • Success: Device is HID-compliant and Raw Input is available
		/// • Warning: Device might work but with limitations (no HID info, Xbox controller)
		/// • Error: Device cannot use Raw Input (offline, system incompatible)
		/// 
		/// The method provides clear error messages without recommending alternatives.
		/// Users must manually choose appropriate input methods for their devices.
		/// </remarks>
		public ValidationResult ValidateRawInputDevice(UserDevice device)
		{
			if (device == null)
				return ValidationResult.Error("Device is null");

			if (!device.IsOnline)
				return ValidationResult.Error("Device is offline");

			try
			{
				// Use RawInputProcessor for detailed validation
				return rawInputProcessor.ValidateDevice(device);
			}
			catch (Exception ex)
			{
				return ValidationResult.Error($"Raw Input validation error: {ex.Message}");
			}
		}


		#endregion
	}
}
