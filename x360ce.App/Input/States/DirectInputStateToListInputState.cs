using SharpDX.DirectInput;
using System;
using System.Linq;
using x360ce.App.Input.Devices;

namespace x360ce.App.Input.States
{
	/// <summary>
	/// Converts DirectInput device states to standardized ListTypeState format.
	/// Handles JoystickState, MouseState, and KeyboardState conversions.
	/// For mouse devices, accumulated values are stored in DirectInputDeviceInfo properties.
	/// </summary>
	internal static class DirectInputStateToListInputState
	{
		#region Constants

		private const int AxisMinValue = 0;
		private const int AxisMaxValue = 65535;
		private const int DefaultMouseSensitivity = 20;
		private const int DefaultMouseWheelSensitivity = 50;
		private const int MinSensitivity = 1;
		private const int KeyboardButtonCount = 256;

		#endregion
		
		/// <summary>
		/// Converts DirectInput device state to ListTypeState format.
		/// Automatically detects state type (JoystickState, MouseState, KeyboardState, or InputStateAsList).
		/// </summary>
		/// <param name="diState">DirectInput state object (JoystickState, MouseState, KeyboardState, or InputStateAsList)</param>
		/// <param name="deviceInfo">Device information containing sensitivity settings (required for mouse devices)</param>
		/// <returns>ListTypeState with standardized format, or null if state is null or unsupported type</returns>
		/// <remarks>
		/// For keyboard devices, DirectInputState returns InputStateAsList directly (polled using GetAsyncKeyState).
		/// For mouse devices, DirectInputState returns MouseState with relative movement deltas.
		/// This method accumulates mouse movement with sensitivity control.
		/// </remarks>
		public static ListInputState ConvertDirectInputStateToListInputState(object diState, DirectInputDeviceInfo deviceInfo = null)
		{
			if (diState == null)
				return null;

			// If already InputStateAsList (keyboard polled state), return directly
			if (diState is ListInputState listState)
				return listState;

			// Detect state type and convert accordingly
			if (diState is JoystickState joystickState)
				return ConvertJoystickState(joystickState);
			else if (diState is MouseState mouseState)
				return ConvertMouseState(mouseState, deviceInfo);
			else if (diState is KeyboardState keyboardState)
				return ConvertKeyboardState(keyboardState);

			return null;
		}

		/// <summary>
		/// Converts DirectInput JoystickState to ListTypeState format.
		/// Note: Joystick states are always created fresh since we don't have a deviceInfo reference to reuse from.
		/// </summary>
		/// <param name="state">JoystickState from DirectInput device</param>
		/// <returns>ListTypeState with axes, sliders, buttons, and POVs</returns>
		/// <remarks>
		/// DirectInput JoystickState Mapping:
		/// • Axes (24 total): X, Y, Z, RotationX, RotationY, RotationZ, AccelerationX/Y/Z,
		///   AngularAccelerationX/Y/Z, ForceX/Y/Z, TorqueX/Y/Z, VelocityX/Y/Z, AngularVelocityX/Y/Z
		/// • Sliders (8 total): Sliders[0-1], AccelerationSliders[0-1], ForceSliders[0-1], VelocitySliders[0-1]
		/// • Buttons (128 max): Button states as 0 or 1
		/// • POVs (4 max): Point-of-view controllers (-1 or 0-35900 centidegrees)
		///
		/// NOTE: This method creates a new ListInputState each time because we don't have access to deviceInfo
		/// to reuse the existing object. The calling code in InputStateManager will handle the assignment.
		/// </remarks>
		private static ListInputState ConvertJoystickState(JoystickState state)
		{
			var result = new ListInputState();

			// Convert axes (24 axes in DirectInput) - pre-allocate capacity for performance
			result.Axes.Capacity = 24;
			result.Axes.AddRange(new[]
			{
				state.X, state.Y, state.Z,
				state.RotationX, state.RotationY, state.RotationZ,
				state.AccelerationX, state.AccelerationY, state.AccelerationZ,
				state.AngularAccelerationX, state.AngularAccelerationY, state.AngularAccelerationZ,
				state.ForceX, state.ForceY, state.ForceZ,
				state.TorqueX, state.TorqueY, state.TorqueZ,
				state.VelocityX, state.VelocityY, state.VelocityZ,
				state.AngularVelocityX, state.AngularVelocityY, state.AngularVelocityZ
			});

			// Convert sliders (8 sliders in DirectInput) - pre-allocate capacity for performance
			result.Sliders.Capacity = 8;
			result.Sliders.AddRange(new[]
			{
				state.Sliders[0], state.Sliders[1],
				state.AccelerationSliders[0], state.AccelerationSliders[1],
				state.ForceSliders[0], state.ForceSliders[1],
				state.VelocitySliders[0], state.VelocitySliders[1]
			});

			// Convert buttons (DirectInput reports as bool array) - pre-allocate capacity
			result.Buttons.Capacity = state.Buttons.Length;
			result.Buttons.AddRange(state.Buttons.Select(b => b ? 1 : 0));

			// Convert POVs (DirectInput reports -1 for neutral, 0-35900 for directions)
			result.POVs.Capacity = state.PointOfViewControllers.Length;
			result.POVs.AddRange(state.PointOfViewControllers);

			return result;
		}

		/// <summary>
		/// Converts DirectInput MouseState to ListTypeState format with accumulator tracking.
		/// CRITICAL: Reuses existing ListInputState object to maintain reference consistency.
		/// </summary>
		/// <param name="state">MouseState from DirectInput device (contains relative movement deltas)</param>
		/// <param name="deviceInfo">Device information containing per-axis sensitivity settings and accumulated values</param>
		/// <returns>ListTypeState with accumulated axes and buttons (no sliders or POVs)</returns>
		/// <remarks>
		/// DirectInput MouseState Mapping:
		/// • Axes (3): X (horizontal delta), Y (vertical delta), Z (wheel delta)
		/// • Buttons (8 max): Mouse button states as 0 or 1
		/// • No sliders or POVs for mouse devices
		///
		/// Accumulator Logic:
		/// • DirectInput reports relative movement (delta values)
		/// • Each axis delta is multiplied by its respective sensitivity (X, Y, Z) before accumulating
		/// • Higher sensitivity values increase responsiveness (e.g., sensitivity=20 means delta of 1 becomes 20)
		/// • Accumulated values are stored in deviceInfo properties and clamped to 0-65535 range
		/// • Center position is 32767 for X/Y, 0 for Z (wheel)
		/// • Default sensitivities: X=20, Y=20, Z=50
		/// </remarks>
		private static ListInputState ConvertMouseState(MouseState state, DirectInputDeviceInfo deviceInfo)
		{
			// CRITICAL FIX: Reuse existing ListInputState object if it exists
			// This maintains the reference in UnifiedInputDeviceInfo.ListInputState
			ListInputState result = deviceInfo?.ListInputState;
			
			if (result == null)
			{
				// First time - create new ListInputState
				result = new ListInputState();
			}
			
			// Get per-axis sensitivity values with defaults and minimum enforcement
			int sensitivityX = Math.Max(MinSensitivity, deviceInfo?.MouseAxisSensitivity[0] ?? DefaultMouseSensitivity);
			int sensitivityY = Math.Max(MinSensitivity, deviceInfo?.MouseAxisSensitivity[1] ?? DefaultMouseSensitivity);
			int sensitivityZ = Math.Max(MinSensitivity, deviceInfo?.MouseAxisSensitivity[2] ?? DefaultMouseWheelSensitivity);
			
			// Apply relative movement with per-axis sensitivity multipliers to accumulated values in deviceInfo
			// DirectInput mouse reports relative movement (delta values)
			// Each axis has its own sensitivity: higher values = more responsive
			if (deviceInfo != null)
			{
				deviceInfo.MouseAxisAccumulatedDelta[0] = ClampAxisValue(deviceInfo.MouseAxisAccumulatedDelta[0] + state.X * sensitivityX);
				deviceInfo.MouseAxisAccumulatedDelta[1] = ClampAxisValue(deviceInfo.MouseAxisAccumulatedDelta[1] + state.Y * sensitivityY);
				deviceInfo.MouseAxisAccumulatedDelta[2] = ClampAxisValue(deviceInfo.MouseAxisAccumulatedDelta[2] + state.Z * sensitivityZ);
				
				// Update or add accumulated axis values
				if (result.Axes.Count >= 3)
				{
					result.Axes[0] = deviceInfo.MouseAxisAccumulatedDelta[0];
					result.Axes[1] = deviceInfo.MouseAxisAccumulatedDelta[1];
					result.Axes[2] = deviceInfo.MouseAxisAccumulatedDelta[2];
				}
				else
				{
					result.Axes.Clear();
					result.Axes.Capacity = 3;
					result.Axes.AddRange(new[] { deviceInfo.MouseAxisAccumulatedDelta[0], deviceInfo.MouseAxisAccumulatedDelta[1], deviceInfo.MouseAxisAccumulatedDelta[2] });
				}
			}
			else
			{
				// Fallback if deviceInfo is null (shouldn't happen in normal operation)
				if (result.Axes.Count >= 3)
				{
					result.Axes[0] = 32767;
					result.Axes[1] = 32767;
					result.Axes[2] = 0;
				}
				else
				{
					result.Axes.Clear();
					result.Axes.Capacity = 3;
					result.Axes.AddRange(new[] { 32767, 32767, 0 });
				}
			}

			// Update or add buttons
			if (result.Buttons.Count == state.Buttons.Length)
			{
				for (int i = 0; i < state.Buttons.Length; i++)
				{
					result.Buttons[i] = state.Buttons[i] ? 1 : 0;
				}
			}
			else
			{
				result.Buttons.Clear();
				result.Buttons.Capacity = state.Buttons.Length;
				result.Buttons.AddRange(state.Buttons.Select(b => b ? 1 : 0));
			}

			// Mice have no sliders or POVs (lists remain empty)

			return result;
		}

		/// <summary>
		/// Converts DirectInput KeyboardState to ListTypeState format.
		/// </summary>
		/// <param name="state">KeyboardState from DirectInput device</param>
		/// <returns>ListTypeState with buttons only (no axes, sliders, or POVs)</returns>
		/// <remarks>
		/// DirectInput KeyboardState Mapping:
		/// • Buttons (256): Each key is represented as a button (0=released, 1=pressed)
		/// • No axes, sliders, or POVs for keyboard devices
		/// • Uses PressedKeys collection to determine which keys are pressed
		/// </remarks>
		private static ListInputState ConvertKeyboardState(KeyboardState state)
		{
			var result = new ListInputState();

			// Initialize all 256 buttons as released (0) - pre-allocate and initialize in one step
			result.Buttons.Capacity = KeyboardButtonCount;
			result.Buttons.AddRange(Enumerable.Repeat(0, KeyboardButtonCount));

			// Set pressed keys to 1
			foreach (var key in state.PressedKeys)
			{
				int keyIndex = (int)key;
				if (keyIndex >= 0 && keyIndex < KeyboardButtonCount)
				{
					result.Buttons[keyIndex] = 1;
				}
			}

			// Keyboards have no axes, sliders, or POVs (lists remain empty)

			return result;
		}

		/// <summary>
		/// Clamps an axis value to the valid range (0-65535).
		/// </summary>
		/// <param name="value">Value to clamp</param>
		/// <returns>Clamped value within valid axis range</returns>
		private static int ClampAxisValue(int value)
		{
			return Math.Max(AxisMinValue, Math.Min(AxisMaxValue, value));
		}
	}
}
