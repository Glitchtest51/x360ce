using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using x360ce.Engine;
using x360ce.Engine.Data;

namespace x360ce.App.DInput
{
	public partial class DInputHelper
	{
		#region XInput State Processing

		/// <summary>
		/// Processes devices using XInput API for Xbox controllers.
		/// </summary>
		/// <param name="device">The Xbox-compatible device to process</param>
		/// <returns>CustomDiState for the device, or null if reading failed</returns>
		/// <remarks>
		/// ⚠️ CRITICAL: MUST OUTPUT CONSISTENT CustomDiState FORMAT ⚠️
		/// 
		/// CustomDiState is the ONLY format used by the existing UI and mapping system.
		/// This method MUST map XInput controls to the EXACT SAME CustomDiState indices
		/// used by DirectInput and other input methods for consistency.
		/// 
		/// MANDATORY CUSTOMDISTATE MAPPING (MUST match other input methods):
		/// • Buttons[0] = A button (primary action)
		/// • Buttons[1] = B button (secondary action)
		/// • Buttons[2] = X button (third action)
		/// • Buttons[3] = Y button (fourth action)
		/// • Buttons[4] = Left Shoulder (LB)
		/// • Buttons[5] = Right Shoulder (RB)
		/// • Buttons[6] = Back/Select button
		/// • Buttons[7] = Start/Menu button
		/// • Buttons[8] = Left Thumbstick Click (LS)
		/// • Buttons[9] = Right Thumbstick Click (RS)
		/// • Buttons[10] = D-Pad Up
		/// • Buttons[11] = D-Pad Right
		/// • Buttons[12] = D-Pad Down
		/// • Buttons[13] = D-Pad Left
		/// • Buttons[14] = Guide/Xbox button
		/// • Axis[0] = Left Thumbstick X (-32768 to 32767)
		/// • Axis[1] = Left Thumbstick Y (-32768 to 32767)
		/// • Axis[2] = Right Thumbstick X (-32768 to 32767)
		/// • Axis[3] = Right Thumbstick Y (-32768 to 32767)
		/// • Axis[4] = Left Trigger (0 to 32767)
		/// • Axis[5] = Right Trigger (0 to 32767)
		/// 
		/// XINPUT METHOD CAPABILITIES:
		/// • Xbox controllers CAN be accessed in background (major advantage over DirectInput)
		/// • Proper trigger separation (LT/RT as separate axes, not combined like DirectInput)
		/// • Guide button access available
		/// • Full rumble support available
		/// • Best performance for Xbox controllers
		/// • No cooperative level conflicts
		/// 
		/// XINPUT METHOD LIMITATIONS:
		/// • Maximum 4 controllers ONLY (hard XInput API limit)
		/// • Only XInput capable devices (Xbox 360/One controllers)
		/// • Cannot activate extra 2 rumble motors in Xbox One controller triggers
		/// • No support for generic gamepads or specialized controllers
		/// </remarks>
		private CustomDiState ProcessXInputDevice(UserDevice device)
		{
			if (device == null)
			{
				Debug.WriteLine("XInput: Device is null");
				return null;
			}

			if (!device.IsXboxCompatible)
			{
				Debug.WriteLine($"XInput: Device {device.DisplayName} is not Xbox-compatible");
				return null;
			}

			try
			{
				// Use the XInputProcessor for actual processing
				var processor = new XInputProcessor();
				
				// Validate device compatibility
				var validation = processor.ValidateDevice(device);
				if (!validation.IsValid)
				{
					Debug.WriteLine($"XInput validation failed for {device.DisplayName}: {validation.Message}");
					return null;
				}

				// Read device state using XInput
				var customState = processor.ReadState(device);

				// Handle XInput force feedback integration with existing system
				HandleXInputForceFeedback(device, processor);

				return customState;
			}
			catch (InputMethodException ex)
			{
				Debug.WriteLine($"XInput error for {device.DisplayName}: {ex.Message}");
				
				// For slot limit errors, mark devices as needing update
				if (ex.Message.Contains("maximum") || ex.Message.Contains("controllers already in use"))
				{
					DevicesNeedUpdating = true;
				}
				
				return null;
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Unexpected XInput error for {device.DisplayName}: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Handles XInput force feedback integration with the existing force feedback system.
		/// </summary>
		/// <param name="device">The Xbox device to handle force feedback for</param>
		/// <param name="processor">The XInput processor instance</param>
		/// <remarks>
		/// This method integrates XInput vibration with the existing force feedback system:
		/// 1. Gets force feedback values from ViGEm virtual controllers (same as DirectInput)
		/// 2. Converts ViGEm values to XInput vibration format
		/// 3. Applies vibration to the physical Xbox controller via XInput
		/// 
		/// This maintains compatibility with existing force feedback configuration while
		/// providing the reliability advantages of XInput vibration for Xbox controllers.
		/// </remarks>
		private void HandleXInputForceFeedback(UserDevice device, XInputProcessor processor)
		{
			try
			{
				// Get setting related to user device (same logic as DirectInput)
				var setting = SettingsManager.UserSettings.ItemsToArraySynchronized()
					.FirstOrDefault(x => x.InstanceGuid == device.InstanceGuid);
				
				if (setting != null && setting.MapTo > (int)MapTo.None)
				{
					// Get pad setting attached to device
					var ps = SettingsManager.GetPadSetting(setting.PadSettingChecksum);
					if (ps != null && ps.ForceEnable == "1")
					{
						// Initialize force feedback state if needed
						device.FFState = device.FFState ?? new Engine.ForceFeedbackState();
						
						// Get force feedback from virtual controllers (same source as DirectInput)
						var feedbacks = CopyAndClearFeedbacks();
						var force = feedbacks[setting.MapTo - 1];
						
						if (force != null || device.FFState.Changed(ps))
						{
							// Convert ViGEm feedback values to XInput vibration format
							// Use same conversion logic as existing DirectInput code
							var leftMotorSpeed = (force == null) ? (ushort)0 : ConvertByteToUshort(force.LargeMotor);
							var rightMotorSpeed = (force == null) ? (ushort)0 : ConvertByteToUshort(force.SmallMotor);
							
							// Apply XInput vibration
							var success = processor.ApplyXInputVibration(device, leftMotorSpeed, rightMotorSpeed);
							
							if (success)
							{
								Debug.WriteLine($"XInput: Applied vibration to {device.DisplayName} - L:{leftMotorSpeed}, R:{rightMotorSpeed}");
							}
							else
							{
								Debug.WriteLine($"XInput: Failed to apply vibration to {device.DisplayName}");
							}
						}
					}
					else if (device.FFState != null)
					{
						// Force feedback disabled - stop vibration
						processor.StopVibration(device);
						device.FFState = null;
					}
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"XInput force feedback integration error for {device.DisplayName}: {ex.Message}");
				// Force feedback errors are not critical - continue processing
			}
		}

		/// <summary>
		/// Converts ViGEm byte motor speed (0-255) to XInput ushort motor speed (0-65535).
		/// </summary>
		/// <param name="byteValue">Motor speed from ViGEm (0-255)</param>
		/// <returns>Motor speed for XInput (0-65535)</returns>
		private ushort ConvertByteToUshort(byte byteValue)
		{
			// Convert 0-255 range to 0-65535 range
			return (ushort)(byteValue * 257); // 257 = 65535 / 255 (rounded)
		}

		/// <summary>
		/// Validates if a device can use XInput and provides detailed validation results.
		/// </summary>
		/// <param name="device">The device to validate for XInput compatibility</param>
		/// <returns>ValidationResult with detailed compatibility information</returns>
		/// <remarks>
		/// This method checks:
		/// • Device Xbox compatibility (VID/PID and name matching)
		/// • XInput slot availability (maximum 4 controllers)
		/// • Device online status
		/// 
		/// VALIDATION RESULTS:
		/// • Success: Device is Xbox-compatible and has available slot
		/// • Warning: Device might work but with limitations
		/// • Error: Device cannot use XInput (not Xbox-compatible or no slots)
		/// 
		/// The method provides clear error messages without recommending alternatives.
		/// Users must manually choose appropriate input methods for their devices.
		/// </remarks>
		public ValidationResult ValidateXInputDevice(UserDevice device)
		{
			if (device == null)
				return ValidationResult.Error("Device is null");

			if (!device.IsOnline)
				return ValidationResult.Error("Device is offline");

			if (!device.IsXboxCompatible)
			{
				return ValidationResult.Error(
					"XInput only supports Xbox-compatible controllers (Xbox 360/One). " +
					"This device does not appear to be an Xbox controller based on its VID/PID and name.");
			}

			// Use XInputProcessor for detailed validation
			try
			{
				var processor = new XInputProcessor();
				return processor.ValidateDevice(device);
			}
			catch (Exception ex)
			{
				return ValidationResult.Error($"XInput validation error: {ex.Message}");
			}
		}

		/// <summary>
		/// Gets the current XInput controller slot assignments for monitoring and debugging.
		/// </summary>
		/// <returns>Dictionary mapping device GUIDs to XInput slot indices (0-3)</returns>
		/// <remarks>
		/// This method provides information about which devices are currently assigned
		/// to XInput slots. Used for:
		/// • UI status displays showing "Controller X/4 used"
		/// • Debugging XInput slot conflicts
		/// • Monitoring controller capacity
		/// 
		/// Slot indices are 0-based (0-3), but displayed to users as 1-based (1-4).
		/// </remarks>
		public Dictionary<Guid, int> GetXInputSlotAssignments()
		{
			return XInputProcessor.GetSlotAssignments();
		}

		/// <summary>
		/// Gets the current number of XInput controllers in use.
		/// </summary>
		/// <returns>Number of currently assigned XInput controllers (0-4)</returns>
		/// <remarks>
		/// This method returns the count of devices currently using XInput slots.
		/// Used for UI displays like "XInput Controllers: 2/4" and capacity planning.
		/// </remarks>
		public int GetXInputControllerCount()
		{
			return XInputProcessor.GetAssignedControllerCount();
		}

		/// <summary>
		/// Releases an XInput slot for a specific device.
		/// </summary>
		/// <param name="deviceGuid">The device GUID to release from XInput slots</param>
		/// <returns>True if a slot was released, false if device wasn't using XInput</returns>
		/// <remarks>
		/// This method is used when:
		/// • User changes device from XInput to another input method
		/// • Device is disconnected or removed
		/// • Resetting XInput slot assignments
		/// 
		/// Releasing slots makes them available for other Xbox controllers.
		/// </remarks>
		public bool ReleaseXInputSlot(Guid deviceGuid)
		{
			return XInputProcessor.ReleaseSlot(deviceGuid);
		}

		/// <summary>
		/// Clears all XInput slot assignments.
		/// </summary>
		/// <remarks>
		/// This method is used for:
		/// • Resetting all XInput assignments
		/// • Handling application shutdown
		/// • Debugging and testing scenarios
		/// 
		/// After calling this method, all XInput slots become available and devices
		/// will be reassigned slots the next time they're processed.
		/// </remarks>
		public void ClearAllXInputSlots()
		{
			XInputProcessor.ClearAllSlots();
		}

		/// <summary>
		/// Checks if XInput is available and functioning on the current system.
		/// </summary>
		/// <returns>True if XInput is available, false if there are system issues</returns>
		/// <remarks>
		/// This method performs a basic XInput availability check by:
		/// • Testing if XInput library is loaded
		/// • Checking if any XInput controllers can be detected
		/// • Verifying system XInput support
		/// 
		/// Used for:
		/// • System diagnostics and troubleshooting
		/// • Deciding whether to show XInput option in UI
		/// • Providing helpful error messages to users
		/// </remarks>
		public bool IsXInputAvailable()
		{
			try
			{
				// Test XInput availability by creating a controller instance
				var testController = new Controller(UserIndex.One);
				
				// Try to get state (will fail gracefully if XInput not available)
				State testState;
				testController.GetState(out testState);
				
				return true;
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"XInput availability check failed: {ex.Message}");
				return false;
			}
		}

		/// <summary>
		/// Gets diagnostic information about XInput system status.
		/// </summary>
		/// <returns>String containing XInput diagnostic information</returns>
		/// <remarks>
		/// This method provides detailed information for troubleshooting:
		/// • XInput library status
		/// • Currently assigned controllers
		/// • Available slots
		/// • System version information
		/// 
		/// Used for diagnostic logs and support information.
		/// </remarks>
		public string GetXInputDiagnosticInfo()
		{
			var info = new System.Text.StringBuilder();
			
			try
			{
				info.AppendLine($"XInput Available: {IsXInputAvailable()}");
				info.AppendLine($"Controllers in use: {GetXInputControllerCount()}/4");
				
				var assignments = GetXInputSlotAssignments();
				if (assignments.Count > 0)
				{
					info.AppendLine("Slot assignments:");
					foreach (var assignment in assignments)
					{
						info.AppendLine($"  Slot {assignment.Value + 1}: {assignment.Key}");
					}
				}
				else
				{
					info.AppendLine("No XInput slot assignments");
				}
				
				info.AppendLine($"Operating System: {Environment.OSVersion}");
			}
			catch (Exception ex)
			{
				info.AppendLine($"Error getting XInput diagnostic info: {ex.Message}");
			}
			
			return info.ToString();
		}

		#endregion
	}
}
