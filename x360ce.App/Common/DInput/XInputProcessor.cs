using SharpDX.DirectInput;
using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using x360ce.Engine;
using x360ce.Engine.Data;

namespace x360ce.App.DInput
{
	/// <summary>
	/// XInput processor - Handles Microsoft XInput API for Xbox controllers.
	/// </summary>
	/// <remarks>
	/// CRITICAL LIMITATIONS (users must be aware):
	/// • Maximum 4 controllers ONLY (hard XInput API limit)
	/// • Only XInput capable devices (Xbox 360/One controllers)
	/// • Cannot activate extra 2 rumble motors in Xbox One controller triggers
	/// • No support for generic gamepads or specialized controllers
	/// 
	/// CAPABILITIES:
	/// • XInput controllers CAN be accessed in background (major advantage over DirectInput)
	/// • Proper trigger separation (LT/RT as separate axes, not combined like DirectInput)
	/// • Guide button access available
	/// • Full rumble support available
	/// • Best performance for Xbox controllers
	/// • No cooperative level conflicts
	/// 
	/// CONTROLLER MAPPING:
	/// This processor maps XInput Gamepad state to CustomDiState as follows:
	/// • Buttons[0-15]: A, B, X, Y, LB, RB, Back, Start, LS, RS, DPad (4 directions), Guide, unused
	/// • Axis[0]: Left Thumbstick X (-32768 to 32767)
	/// • Axis[1]: Left Thumbstick Y (-32768 to 32767) 
	/// • Axis[2]: Right Thumbstick X (-32768 to 32767)
	/// • Axis[3]: Right Thumbstick Y (-32768 to 32767)
	/// • Axis[4]: Left Trigger (0 to 32767, converted from 0-255 byte range)
	/// • Axis[5]: Right Trigger (0 to 32767, converted from 0-255 byte range)
	/// </remarks>
	public class XInputProcessor: IInputProcessor
	{

		#region Static Controller Management

		/// <summary>
		/// Maximum number of XInput controllers supported.
		/// </summary>
		public const int MaxControllers = 4;

		/// <summary>
		/// Static array of XInput controllers for the 4 possible slots.
		/// </summary>
		private static Controller[] _xinputControllers;

		/// <summary>
		/// Tracks which XInput slots are currently assigned to which devices.
		/// </summary>
		private static Dictionary<Guid, int> _deviceToSlotMapping = new Dictionary<Guid, int>();

		/// <summary>
		/// Tracks the last applied vibration values for each device to prevent redundant calls.
		/// Key: Device GUID, Value: Tuple of (LeftMotorSpeed, RightMotorSpeed)
		/// </summary>
		private static Dictionary<Guid, (ushort Left, ushort Right)> _lastVibrationValues = new Dictionary<Guid, (ushort, ushort)>();

		/// <summary>
		/// Tracks the previous button states for each device to detect changes.
		/// Key: Device GUID, Value: Array of 15 boolean values for buttons
		/// </summary>
		private static Dictionary<Guid, bool[]> _previousButtonStates = new Dictionary<Guid, bool[]>();

		/// <summary>
		/// Tracks the previous axis values for each device to detect significant changes.
		/// Key: Device GUID, Value: Array of 6 integer values for axes
		/// </summary>
		private static Dictionary<Guid, int[]> _previousAxisValues = new Dictionary<Guid, int[]>();

		/// <summary>
		/// Tracks the last PacketNumber for each device to detect if controller is responding.
		/// Key: Device GUID, Value: Last PacketNumber from XInput
		/// </summary>
		private static Dictionary<Guid, uint> _lastPacketNumbers = new Dictionary<Guid, uint>();

		/// <summary>
		/// Tracks when each device was first assigned to give controllers time to respond before flagging as unresponsive.
		/// Key: Device GUID, Value: Environment.TickCount when controller was first assigned
		/// </summary>
		private static Dictionary<Guid, int> _controllerStartTimes = new Dictionary<Guid, int>();

		/// <summary>
		/// Minimum axis change threshold (10% of axis range) to prevent debug flooding.
		/// </summary>
		private const int AxisChangeThreshold = 3277; // 10% of 32767

		/// <summary>
		/// Initialize static XInput controllers.
		/// </summary>
		static XInputProcessor()
		{
			_xinputControllers = new Controller[MaxControllers];
			for (int i = 0; i < MaxControllers; i++)
			{
				_xinputControllers[i] = new Controller((UserIndex)i);
			}
		}

		#endregion

		#region IInputProcessor

		/// <summary>
		/// Gets the input method supported by this processor.
		/// </summary>
		public InputMethod SupportedMethod => InputMethod.XInput;

		/// <summary>
		/// Determines if this processor can handle the specified device.
		/// </summary>
		/// <param name="device">The user device to check</param>
		/// <returns>True if device is Xbox-compatible and XInput slot is available</returns>
		public bool CanProcess(UserDevice device)
		{
			if (device == null || !device.IsOnline)
				return false;

			// Check if device is Xbox-compatible
			if (!device.IsXboxCompatible)
				return false;

			// Check if we have available XInput slots
			var assignedSlots = _deviceToSlotMapping.Values.ToHashSet();
			var availableSlots = Enumerable.Range(0, MaxControllers).Where(i => !assignedSlots.Contains(i));

			// Device can be processed if it already has a slot or if there's an available slot
			return _deviceToSlotMapping.ContainsKey(device.InstanceGuid) || availableSlots.Any();
		}

		/// <summary>
		/// Reads the current state from the device using XInput.
		/// </summary>
		/// <param name="device">The device to read from</param>
		/// <returns>CustomDiState representing the current controller state</returns>
		/// <exception cref="InputMethodException">Thrown when XInput encounters errors</exception>
		public static CustomDeviceState ReadState(UserDevice device)
		{
			if (device == null)
				throw new InputMethodException(InputMethod.XInput, device, "Device is null");

			if (!device.IsXboxCompatible)
				throw new InputMethodException(InputMethod.XInput, device, "Device is not Xbox-compatible");

			try
			{
				// CRITICAL: Set device properties required for UI to display mapping controls
				// This ensures the PAD UI shows buttons/axes for XInput devices just like DirectInput
				EnsureDevicePropertiesForUI(device);

				// Get or assign XInput slot for this device
				int slotIndex = GetOrAssignSlot(device);
				var controller = _xinputControllers[slotIndex];

				// Read XInput state
				State xinputState;
				bool isConnected = controller.GetState(out xinputState);
				
				if (!isConnected)
				{
					Debug.WriteLine($"XInput: Controller disconnected - removing {device.DisplayName} from slot mapping");
					_deviceToSlotMapping.Remove(device.InstanceGuid);
					return null;
				}

				// DIAGNOSTIC: Track PacketNumber changes to detect if controller is responding
				if (!_lastPacketNumbers.ContainsKey(device.InstanceGuid))
				{
					_lastPacketNumbers[device.InstanceGuid] = (uint)xinputState.PacketNumber;
					Debug.WriteLine($"XInput: Initial PacketNumber for {device.DisplayName}: {xinputState.PacketNumber}");
				}
				else
				{
					var lastPacket = _lastPacketNumbers[device.InstanceGuid];
					if ((uint)xinputState.PacketNumber != lastPacket)
					{
						Debug.WriteLine($"XInput: PacketNumber changed for {device.DisplayName}: {lastPacket} → {xinputState.PacketNumber}");
						_lastPacketNumbers[device.InstanceGuid] = (uint)xinputState.PacketNumber;
					}
					else
					{
						// Generic detection of unresponsive XInput implementations
						DetectUnresponsiveXInputController(device, xinputState.PacketNumber);
					}
				}

				// Convert XInput Gamepad to CustomDiState
				var customState = ConvertGamepadToCustomDiState(xinputState.Gamepad, device);

				// Log input changes for debugging
				LogInputChanges(device, customState);

				// Store the XInput state for potential use by other parts of the system
				device.DeviceState = xinputState.Gamepad;

				return customState;
			}
			catch (Exception ex)
			{
				var message = $"XInput read error: {ex.Message}\nDevice: {device.DisplayName}\nSlot: {GetAssignedSlot(device)}";
				Debug.WriteLine($"XInput: Exception in ReadState - {message}");
				throw new InputMethodException(InputMethod.XInput, device, message, ex);
			}
		}

		/// <summary>
		/// Handles force feedback for the device using XInput vibration.
		/// </summary>
		/// <param name="device">The device to send force feedback to</param>
		/// <param name="ffState">The force feedback state to apply</param>
		/// <remarks>
		/// XInput provides excellent vibration support for Xbox controllers.
		/// This is much more reliable than DirectInput force feedback for Xbox controllers.
		/// 
		/// XInput vibration uses a simple two-motor system:
		/// • LeftMotorSpeed: Large motor for rough, low-frequency vibration (0-65535)
		/// • RightMotorSpeed: Small motor for precise, high-frequency vibration (0-65535)
		/// 
		/// This method integrates with the existing force feedback system by getting
		/// vibration values from the ViGEm virtual controllers (same as DirectInput).
		/// </remarks>
		public void HandleForceFeedback(UserDevice device, Engine.ForceFeedbackState ffState)
		{
			// Force feedback for XInput is handled through integration with DInputHelper
			// The actual vibration values come from the virtual Xbox controllers via ViGEm
			// This method is called from the main UpdateDiStates coordinator

			// Note: XInput force feedback integration is handled in the main DInputHelper
			// through the ProcessXInputDevice method which calls ApplyXInputVibration
			Debug.WriteLine($"XInput: Force feedback processing delegated to main coordinator for {device.DisplayName}");
		}

		/// <summary>
		/// Applies XInput vibration to the device using values from the force feedback system.
		/// This method is called by the main DInputHelper with actual vibration values.
		/// Only applies vibration and logs debug messages when values actually change.
		/// </summary>
		/// <param name="device">The device to apply vibration to</param>
		/// <param name="leftMotorSpeed">Left motor speed (0-65535)</param>
		/// <param name="rightMotorSpeed">Right motor speed (0-65535)</param>
		/// <returns>True if vibration was applied successfully</returns>
		/// <remarks>
		/// This method applies the actual XInput vibration after the main force feedback
		/// system has processed the values from the virtual controllers.
		/// 
		/// Change detection prevents:
		/// • Redundant XInput API calls when values haven't changed
		/// • Debug message flooding in Visual Studio Output window
		/// • Unnecessary processing overhead
		/// 
		/// Called from DInputHelper.Step2.UpdateXiStates.ProcessXInputDevice
		/// </remarks>
		public static bool ApplyXInputVibration(UserDevice device, ushort leftMotorSpeed, ushort rightMotorSpeed)
		{
			if (device == null)
				return false;

			try
			{
				// Get the assigned XInput slot
				if (!_deviceToSlotMapping.TryGetValue(device.InstanceGuid, out int slotIndex))
				{
					Debug.WriteLine($"XInput: No slot assigned for vibration on device {device.DisplayName}");
					return false;
				}

				// Check if vibration values have changed since last application
				var currentValues = (leftMotorSpeed, rightMotorSpeed);
				if (_lastVibrationValues.TryGetValue(device.InstanceGuid, out var lastValues))
				{
					// If values haven't changed, skip XInput call and debug logging
					if (lastValues.Left == leftMotorSpeed && lastValues.Right == rightMotorSpeed)
					{
						return true; // Return success since vibration is already at desired values
					}
				}

				var controller = _xinputControllers[slotIndex];

				// Create XInput Vibration structure with the provided values
				var vibration = new Vibration
				{
					LeftMotorSpeed = ConvertHelper.LimitRange((short)leftMotorSpeed, short.MinValue, short.MaxValue),
					RightMotorSpeed = ConvertHelper.LimitRange((short)rightMotorSpeed, short.MinValue, short.MaxValue)
				};

				// Apply vibration to XInput controller
				var result = controller.SetVibration(vibration);

				if (result.Success)
				{
					// Store the new vibration values to prevent redundant calls
					_lastVibrationValues[device.InstanceGuid] = currentValues;
					
					// Only log when values actually change
					Debug.WriteLine($"XInput: Vibration applied to {device.DisplayName} - L:{leftMotorSpeed}, R:{rightMotorSpeed}");
					return true;
				}
				else
				{
					Debug.WriteLine($"XInput: Failed to set vibration for {device.DisplayName}: {result}");
					return false;
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"XInput vibration error for {device.DisplayName}: {ex.Message}");
				return false;
			}
		}

		/// <summary>
		/// Stops all vibration on the specified device.
		/// Uses change detection to prevent redundant calls.
		/// </summary>
		/// <param name="device">The device to stop vibration on</param>
		/// <remarks>
		/// This method provides a way to stop XInput vibration, similar to
		/// ForceFeedbackState.StopDeviceForces for DirectInput devices.
		/// 
		/// Uses the same change detection mechanism as ApplyXInputVibration
		/// to prevent redundant XInput API calls and debug message flooding.
		/// </remarks>
		public static void StopVibration(UserDevice device)
		{
			if (device == null)
				return;

			try
			{
				// Get the assigned XInput slot
				if (!_deviceToSlotMapping.TryGetValue(device.InstanceGuid, out int slotIndex))
				{
					Debug.WriteLine($"XInput: No slot assigned for stopping vibration on device {device.DisplayName}");
					return;
				}

				// Check if vibration is already stopped (0,0) to prevent redundant calls
				if (_lastVibrationValues.TryGetValue(device.InstanceGuid, out var lastValues))
				{
					if (lastValues.Left == 0 && lastValues.Right == 0)
					{
						return; // Vibration is already stopped
					}
				}

				var controller = _xinputControllers[slotIndex];

				// Stop vibration by setting both motors to 0
				var vibration = new Vibration
				{
					LeftMotorSpeed = 0,
					RightMotorSpeed = 0
				};

				var result = controller.SetVibration(vibration);

				if (result.Success)
				{
					// Update stored vibration values to (0,0)
					_lastVibrationValues[device.InstanceGuid] = (0, 0);
					
					// Only log when actually stopping vibration
					Debug.WriteLine($"XInput: Vibration stopped for {device.DisplayName}");
				}
				else
				{
					Debug.WriteLine($"XInput: Failed to stop vibration for {device.DisplayName}: {result}");
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"XInput stop vibration error for {device.DisplayName}: {ex.Message}");
			}
		}

		/// <summary>
		/// Validates if the device can be used with XInput.
		/// </summary>
		/// <param name="device">The device to validate</param>
		/// <returns>ValidationResult indicating compatibility and any limitations</returns>
		public static ValidationResult ValidateDevice(UserDevice device)
		{
			if (device == null)
				return ValidationResult.Error("Device is null");

			if (!device.IsOnline)
				return ValidationResult.Error("Device is offline");

			if (!device.IsXboxCompatible)
				return ValidationResult.Error("XInput only supports Xbox-compatible controllers (Xbox 360/One)");

			// Check XInput controller limit
			var assignedSlots = _deviceToSlotMapping.Values.ToHashSet();
			var availableSlots = Enumerable.Range(0, MaxControllers).Where(i => !assignedSlots.Contains(i));

			// If device already has a slot, it's valid
			if (_deviceToSlotMapping.ContainsKey(device.InstanceGuid))
			{
				var slotIndex = _deviceToSlotMapping[device.InstanceGuid];
				return ValidationResult.Success($"XInput compatible - assigned to slot {slotIndex + 1}/4");
			}

			// Check if slots are available
			if (!availableSlots.Any())
			{
				return ValidationResult.Error($"XInput maximum {MaxControllers} controllers already in use. Free a slot or use different input method.");
			}

			var nextSlot = availableSlots.First();
			return ValidationResult.Success($"XInput compatible - will use slot {nextSlot + 1}/4");
		}

		#endregion

		#region XInput-Specific Implementation

		/// <summary>
		/// Gets or assigns an XInput slot for the specified device.
		/// </summary>
		/// <param name="device">The device to get/assign a slot for</param>
		/// <returns>The slot index (0-3)</returns>
		/// <exception cref="InputMethodException">Thrown when no slots are available</exception>
		private static int GetOrAssignSlot(UserDevice device)
		{
			// If device already has a slot, return it
			if (_deviceToSlotMapping.TryGetValue(device.InstanceGuid, out int existingSlot))
				return existingSlot;

			// Find an available slot
			var assignedSlots = _deviceToSlotMapping.Values.ToHashSet();
			var availableSlots = Enumerable.Range(0, MaxControllers).Where(i => !assignedSlots.Contains(i));

			if (!availableSlots.Any())
				throw new InputMethodException(InputMethod.XInput, device, $"XInput maximum {MaxControllers} controllers already in use");

			// Assign the first available slot
			int newSlot = availableSlots.First();
			_deviceToSlotMapping[device.InstanceGuid] = newSlot;

			// Record when this controller was first assigned for grace period tracking
			_controllerStartTimes[device.InstanceGuid] = Environment.TickCount;

			Debug.WriteLine($"XInput: Assigned device {device.DisplayName} to slot {newSlot} (display as {newSlot + 1}/4)");
			return newSlot;
		}

		/// <summary>
		/// Gets the currently assigned slot for a device.
		/// </summary>
		/// <param name="device">The device to check</param>
		/// <returns>The slot index if assigned, -1 if not assigned</returns>
		private static int GetAssignedSlot(UserDevice device)
		{
			return _deviceToSlotMapping.TryGetValue(device.InstanceGuid, out int slot) ? slot : -1;
		}

		/// <summary>
		/// Converts XInput Gamepad state to CustomDiState format.
		/// CRITICAL: Must match DirectInput's mapping pattern for Xbox controllers.
		/// </summary>
		/// <param name="gamepad">The XInput Gamepad state</param>
		/// <param name="device">The device for debug logging (optional)</param>
		/// <returns>CustomDiState with mapped values matching DirectInput pattern</returns>
		/// <remarks>
		/// This mapping MUST match how DirectInput would map the same Xbox controller
		/// to preserve user configurations when switching input methods.
		/// 
		/// DirectInput typically maps Xbox controllers as:
		/// • Axis[0] = Left Thumbstick X (matches XInput LeftThumbX)
		/// • Axis[1] = Left Thumbstick Y (matches XInput LeftThumbY)
		/// • Axis[2] = Combined Triggers OR Right Thumbstick X (controller dependent)
		/// • Axis[3] = Right Thumbstick Y (matches XInput RightThumbY) 
		/// • Axis[4] = Left Trigger (separate when available)
		/// • Axis[5] = Right Trigger (separate when available)
		/// 
		/// For XInput, we map to the most common DirectInput pattern for Xbox controllers.
		/// </remarks>
		private static CustomDeviceState ConvertGamepadToCustomDiState(Gamepad gamepad, UserDevice device = null)
		{
			var customState = new CustomDeviceState();

			// Map buttons to match DirectInput button enumeration for Xbox controllers
			// Button mapping follows how DirectInput typically enumerates Xbox controller buttons
			customState.Buttons[0] = gamepad.Buttons.HasFlag(GamepadButtonFlags.A);           // Button 0: A
			customState.Buttons[1] = gamepad.Buttons.HasFlag(GamepadButtonFlags.B);           // Button 1: B
			customState.Buttons[2] = gamepad.Buttons.HasFlag(GamepadButtonFlags.X);           // Button 2: X
			customState.Buttons[3] = gamepad.Buttons.HasFlag(GamepadButtonFlags.Y);           // Button 3: Y
			customState.Buttons[4] = gamepad.Buttons.HasFlag(GamepadButtonFlags.LeftShoulder);  // Button 4: LB
			customState.Buttons[5] = gamepad.Buttons.HasFlag(GamepadButtonFlags.RightShoulder); // Button 5: RB
			customState.Buttons[6] = gamepad.Buttons.HasFlag(GamepadButtonFlags.Back);        // Button 6: Back
			customState.Buttons[7] = gamepad.Buttons.HasFlag(GamepadButtonFlags.Start);       // Button 7: Start
			customState.Buttons[8] = gamepad.Buttons.HasFlag(GamepadButtonFlags.LeftThumb);   // Button 8: LS
			customState.Buttons[9] = gamepad.Buttons.HasFlag(GamepadButtonFlags.RightThumb);  // Button 9: RS

			// D-Pad mapping to buttons (DirectInput POV often mapped to buttons for Xbox controllers)
			customState.Buttons[10] = gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadUp);     // Button 10: D-Up
			customState.Buttons[11] = gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadRight);  // Button 11: D-Right
			customState.Buttons[12] = gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadDown);   // Button 12: D-Down
			customState.Buttons[13] = gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadLeft);   // Button 13: D-Left

			// Guide button (when available) - not always accessible via DirectInput
			customState.Buttons[14] = gamepad.Buttons.HasFlag(GamepadButtonFlags.Guide);      // Button 14: Guide

			// CRITICAL: Map axes to match DirectInput's typical Xbox controller mapping
			customState.Axis[0] = gamepad.LeftThumbX;   // Axis 0: Left Thumbstick X (DirectInput X)
			customState.Axis[1] = gamepad.LeftThumbY;   // Axis 1: Left Thumbstick Y (DirectInput Y)
			customState.Axis[2] = gamepad.RightThumbX;  // Axis 2: Right Thumbstick X (DirectInput Z or RotationX)
			customState.Axis[3] = gamepad.RightThumbY;  // Axis 3: Right Thumbstick Y (DirectInput RotationX or RotationY)

			// Trigger mapping: XInput advantage is separate triggers, but match DirectInput pattern
			customState.Axis[4] = ConvertTriggerToAxis(gamepad.LeftTrigger);   // Axis 4: Left Trigger (DirectInput RotationY)
			customState.Axis[5] = ConvertTriggerToAxis(gamepad.RightTrigger);  // Axis 5: Right Trigger (DirectInput RotationZ)

			// Note: XInput provides cleaner mapping than DirectInput for Xbox controllers
			// DirectInput often combines triggers or uses different axis assignments
			// This mapping preserves the most common DirectInput pattern for Xbox controllers

			return customState;
		}

		/// <summary>
		/// Converts XInput trigger value (0-255) to axis value (0-32767).
		/// </summary>
		/// <param name="triggerValue">Trigger value from XInput (0-255)</param>
		/// <returns>Axis value (0-32767)</returns>
		private static int ConvertTriggerToAxis(byte triggerValue)
		{
			// Convert 0-255 range to 0-32767 range
			// Using positive range only since triggers don't have negative values
			return (int)((triggerValue / 255.0) * 32767.0);
		}

		/// <summary>
		/// Logs XInput button and axis changes for debugging.
		/// Only logs when values actually change to prevent debug output flooding.
		/// </summary>
		/// <param name="device">The device to track changes for</param>
		/// <param name="customState">The current CustomDiState to check for changes</param>
		private static void LogInputChanges(UserDevice device, CustomDeviceState customState)
		{
			if (device?.InstanceGuid == null || customState == null)
				return;

			var deviceGuid = device.InstanceGuid;

			// Initialize tracking arrays if this is the first time we see this device
			if (!_previousButtonStates.ContainsKey(deviceGuid))
			{
				_previousButtonStates[deviceGuid] = new bool[15];
				_previousAxisValues[deviceGuid] = new int[6];
				
				// CRITICAL FIX: Initialize previous axis values to current values to prevent false change detection
				// This prevents logging of "changes" from initialized zero values to actual controller state
				for (int i = 0; i < 6; i++)
				{
					_previousAxisValues[deviceGuid][i] = customState.Axis[i];
				}
				
				// Count actual controller capabilities
				int buttonCount = GetControllerButtonCount(device, customState);
				int axisCount = GetControllerAxisCount(device, customState);
				
				Debug.WriteLine($"XInput: Started tracking input changes for {device.DisplayName} - Buttons: {buttonCount}, Axes: {axisCount} - Initial axis values: [{customState.Axis[0]}, {customState.Axis[1]}, {customState.Axis[2]}, {customState.Axis[3]}, {customState.Axis[4]}, {customState.Axis[5]}]");
				return; // Skip logging changes on first initialization
			}

			var prevButtons = _previousButtonStates[deviceGuid];
			var prevAxes = _previousAxisValues[deviceGuid];

			// Check for button changes
			string[] buttonNames = { "A", "B", "X", "Y", "LB", "RB", "Back", "Start", "LS", "RS", "D-Up", "D-Right", "D-Down", "D-Left", "Guide" };
			
			for (int i = 0; i < 15; i++)
			{
				bool currentState = customState.Buttons[i];
				if (currentState != prevButtons[i])
				{
					string action = currentState ? "pressed" : "released";
					Debug.WriteLine($"XInput: Button {buttonNames[i]} {action} on {device.DisplayName}");
					prevButtons[i] = currentState;
				}
			}

			// Check for axis changes (only log significant changes)
			string[] axisNames = { "Left Stick X", "Left Stick Y", "Right Stick X", "Right Stick Y", "Left Trigger", "Right Trigger" };
			
			for (int i = 0; i < 6; i++)
			{
				int currentValue = customState.Axis[i];
				int previousValue = prevAxes[i];
				int delta = Math.Abs(currentValue - previousValue);
				
				// Only log if change is significant (≥10% of axis range)
				if (delta >= AxisChangeThreshold)
				{
					// Additional validation: Check if we're getting extreme oscillations (likely data corruption)
					bool isExtremeOscillation = (Math.Abs(currentValue) >= 32000 && Math.Abs(previousValue) <= 1000) ||
												(Math.Abs(currentValue) <= 1000 && Math.Abs(previousValue) >= 32000);
					
					if (isExtremeOscillation)
					{
						Debug.WriteLine($"XInput: WARNING - Extreme axis oscillation detected on {axisNames[i]} for {device.DisplayName}: {previousValue} → {currentValue} (possible data corruption or device issue)");
					}
					else
					{
						Debug.WriteLine($"XInput: Axis {axisNames[i]} changed to {currentValue} on {device.DisplayName} (delta: {currentValue - previousValue})");
					}
					
					prevAxes[i] = currentValue;
				}
			}
		}

		/// <summary>
		/// Gets the current number of assigned XInput controllers.
		/// </summary>
		/// <returns>Number of currently assigned controllers (0-4)</returns>
		public static int GetAssignedControllerCount()
		{
			return _deviceToSlotMapping.Count;
		}

		/// <summary>
		/// Gets information about XInput slot assignments.
		/// </summary>
		/// <returns>Dictionary mapping device GUIDs to slot indices</returns>
		public static Dictionary<Guid, int> GetSlotAssignments()
		{
			return new Dictionary<Guid, int>(_deviceToSlotMapping);
		}

		/// <summary>
		/// Releases the XInput slot for a specific device.
		/// Also clears vibration tracking and input change tracking for the device.
		/// </summary>
		/// <param name="deviceGuid">The device GUID to release</param>
		/// <returns>True if a slot was released, false if device wasn't assigned</returns>
		public static bool ReleaseSlot(Guid deviceGuid)
		{
			if (_deviceToSlotMapping.Remove(deviceGuid))
			{
				// Also remove vibration tracking for this device
				_lastVibrationValues.Remove(deviceGuid);
				
				// Remove input change tracking for this device
				_previousButtonStates.Remove(deviceGuid);
				_previousAxisValues.Remove(deviceGuid);
				
				// Remove PacketNumber tracking for this device
				_lastPacketNumbers.Remove(deviceGuid);
				
				// Remove start time tracking for this device
				_controllerStartTimes.Remove(deviceGuid);
				
				Debug.WriteLine($"XInput: Released slot for device {deviceGuid}");
				return true;
			}
			return false;
		}

		/// <summary>
		/// Clears all XInput slot assignments.
		/// Also clears all vibration tracking and input change tracking.
		/// </summary>
		public static void ClearAllSlots()
		{
			_deviceToSlotMapping.Clear();
			_lastVibrationValues.Clear();
			_previousButtonStates.Clear();
			_previousAxisValues.Clear();
			_lastPacketNumbers.Clear();
			_controllerStartTimes.Clear();
			
			Debug.WriteLine("XInput: Cleared all slot assignments");
		}

		/// <summary>
		/// Ensures device has the properties required for the UI to display mapping controls.
		/// This populates the same properties that DirectInput sets so the PAD UI works.
		/// </summary>
		/// <param name="device">The device to ensure properties for</param>
		private static void EnsureDevicePropertiesForUI(UserDevice device)
		{
			// Set device objects if not already set (required for UI to show button/axis mapping)
			if (device.DeviceObjects == null)
			{
				// Create Xbox controller device objects that match what DirectInput would provide
				var deviceObjects = new List<DeviceObjectItem>();

				// Add button objects (A, B, X, Y, LB, RB, Back, Start, LS, RS, DPad, Guide)
				for (int i = 0; i < 15; i++)
				{
					deviceObjects.Add(new DeviceObjectItem(
						i * 4, // offset
						ObjectGuid.Button, // guid
						ObjectAspect.Position, // aspect
						DeviceObjectTypeFlags.PushButton, // type
						i, // instance
						GetXboxButtonName(i) // name
					));
				}

				// Add axis objects (Left Stick X/Y, Right Stick X/Y, Left Trigger, Right Trigger)
				string[] axisNames = { "Left Stick X", "Left Stick Y", "Right Stick X", "Right Stick Y", "Left Trigger", "Right Trigger" };
				for (int i = 0; i < 6; i++)
				{
					deviceObjects.Add(new DeviceObjectItem(
						64 + (i * 4), // offset
						ObjectGuid.XAxis, // guid (simplified)
						ObjectAspect.Position, // aspect
						DeviceObjectTypeFlags.AbsoluteAxis, // type
						i, // instance
						axisNames[i] // name
					));
				}

				device.DeviceObjects = deviceObjects.ToArray();
			}

			// Set axis mask (which axes are available) - required for UI
			if (device.DiAxeMask == 0)
			{
				// XInput Xbox controllers have 6 axes: Left Stick X/Y, Right Stick X/Y, Left/Right Triggers
				device.DiAxeMask = 0x1 | 0x2 | 0x4 | 0x8 | 0x10 | 0x20; // First 6 axes
			}

			// Set device effects (required for force feedback UI)
			if (device.DeviceEffects == null)
			{
				// XInput supports basic vibration effects
				device.DeviceEffects = new DeviceEffectItem[]
				{
					new DeviceEffectItem { Name = "XInput Vibration" }
				};
			}
		}

		/// <summary>
		/// Gets the display name for Xbox controller buttons.
		/// </summary>
		/// <param name="buttonIndex">Button index (0-14)</param>
		/// <returns>Button name</returns>
		private static string GetXboxButtonName(int buttonIndex)
		{
			string[] buttonNames = {
				"A", "B", "X", "Y", "LB", "RB", "Back", "Start", "LS", "RS",
				"D-Up", "D-Right", "D-Down", "D-Left", "Guide"
			};

			return buttonIndex < buttonNames.Length ? buttonNames[buttonIndex] : $"Button {buttonIndex}";
		}

		/// <summary>
		/// Detects controllers with unresponsive XInput implementations.
		/// Provides generic detection for any controller with stuck PacketNumber.
		/// Includes grace period to prevent false positives for controllers that need time to respond.
		/// </summary>
		/// <param name="device">The device to check for unresponsive behavior</param>
		/// <param name="currentPacketNumber">The current PacketNumber from XInput</param>
		private static void DetectUnresponsiveXInputController(UserDevice device, int currentPacketNumber)
		{
			var deviceGuid = device.InstanceGuid;
			var now = Environment.TickCount;
			
			// Grace period: Don't warn immediately after controller assignment
			// Give controllers 10 seconds to respond before flagging as unresponsive
			if (_controllerStartTimes.TryGetValue(deviceGuid, out int startTime))
			{
				var timeSinceAssignment = now - startTime;
				if (timeSinceAssignment < 10000) // 10 seconds grace period
				{
					return; // Still within grace period, don't warn yet
				}
			}
			
			// Track when we last warned about unresponsive behavior to prevent spam
			if (!_lastUnresponsiveWarnings.ContainsKey(deviceGuid))
			{
				_lastUnresponsiveWarnings[deviceGuid] = 0;
			}
			
			var lastWarning = _lastUnresponsiveWarnings[deviceGuid];
			
			// Only warn every 30 seconds to avoid debug spam
			if (now - lastWarning >= 30000)
			{
				_lastUnresponsiveWarnings[deviceGuid] = now;
				
				Debug.WriteLine($"XInput: {device.DisplayName} has unresponsive XInput implementation - PacketNumber stuck at {currentPacketNumber}");
				Debug.WriteLine($"XInput: Controller appears connected but PacketNumber never changes, indicating broken XInput support");
				Debug.WriteLine($"XInput: Recommendation: Switch to DirectInput for this controller model");
			}
		}

		/// <summary>
		/// Tracks when we last warned about unresponsive controllers to prevent debug spam.
		/// </summary>
		private static Dictionary<Guid, int> _lastUnresponsiveWarnings = new Dictionary<Guid, int>();

		/// <summary>
		/// Gets the number of buttons available on the controller.
		/// For XInput controllers, this returns the standard XInput button count.
		/// </summary>
		/// <param name="device">The device to count buttons for</param>
		/// <param name="customState">The current state (used to validate button functionality)</param>
		/// <returns>Number of available buttons</returns>
		private static int GetControllerButtonCount(UserDevice device, CustomDeviceState customState)
		{
			// XInput has a standard set of 15 buttons: A, B, X, Y, LB, RB, Back, Start, LS, RS, DPad (4), Guide
			int standardXInputButtons = 15;
			
			// If device has DeviceObjects, count them for verification (simple length check)
			if (device.DeviceObjects != null && device.DeviceObjects.Length > 0)
			{
				// Count DeviceObjects that might be buttons (simple approach)
				int deviceObjectCount = device.DeviceObjects.Length;
				Debug.WriteLine($"XInput: Device reports {deviceObjectCount} total objects via DeviceObjects");
				
				// For XInput, we expect at least 21 objects (15 buttons + 6 axes), but return standard count
				return standardXInputButtons;
			}
			
			// For XInput controllers, return standard button count
			return standardXInputButtons;
		}

		/// <summary>
		/// Gets the number of axes available on the controller.
		/// For XInput controllers, this returns the standard XInput axis count.
		/// </summary>
		/// <param name="device">The device to count axes for</param>
		/// <param name="customState">The current state (used to validate axis functionality)</param>
		/// <returns>Number of available axes</returns>
		private static int GetControllerAxisCount(UserDevice device, CustomDeviceState customState)
		{
			// XInput has a standard set of 6 axes: Left Stick X/Y, Right Stick X/Y, Left/Right Triggers
			int standardXInputAxes = 6;
			
			// Check DiAxeMask if available (most reliable method)
			if (device.DiAxeMask != 0)
			{
				int maskAxisCount = 0;
				for (int i = 0; i < 32; i++) // Check first 32 possible axes
				{
					if ((device.DiAxeMask & (1 << i)) != 0)
						maskAxisCount++;
				}
				
				if (maskAxisCount > 0)
				{
					Debug.WriteLine($"XInput: Device reports {maskAxisCount} axes via DiAxeMask");
					return Math.Max(standardXInputAxes, maskAxisCount);
				}
			}
			
			// For XInput controllers, return standard axis count
			return standardXInputAxes;
		}

		#endregion
	}
}
