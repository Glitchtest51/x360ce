using SharpDX.DirectInput;

namespace x360ce.App.Input.States
{
	/// <summary>
	/// Converts DirectInput device states to standardized ListTypeState format.
	/// Handles JoystickState, MouseState, and KeyboardState conversions.
	/// </summary>
	internal static class StatesDirectInputConvertToListType
	{
		/// <summary>
		/// Converts DirectInput device state to ListTypeState format.
		/// Automatically detects state type (JoystickState, MouseState, or KeyboardState).
		/// </summary>
		/// <param name="diState">DirectInput state object (JoystickState, MouseState, or KeyboardState)</param>
		/// <returns>ListTypeState with standardized format, or null if state is null or unsupported type</returns>
		public static ListTypeState ConvertToListTypeState(object diState)
		{
			if (diState == null)
				return null;

			// Detect state type and convert accordingly
			if (diState is JoystickState joystickState)
				return ConvertJoystickState(joystickState);
			else if (diState is MouseState mouseState)
				return ConvertMouseState(mouseState);
			else if (diState is KeyboardState keyboardState)
				return ConvertKeyboardState(keyboardState);

			return null;
		}

		/// <summary>
		/// Converts DirectInput JoystickState to ListTypeState format.
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
		/// </remarks>
		private static ListTypeState ConvertJoystickState(JoystickState state)
		{
			var result = new ListTypeState();

			// Convert axes (24 axes in DirectInput)
			result.Axes.Add(state.X);
			result.Axes.Add(state.Y);
			result.Axes.Add(state.Z);
			result.Axes.Add(state.RotationX);
			result.Axes.Add(state.RotationY);
			result.Axes.Add(state.RotationZ);
			result.Axes.Add(state.AccelerationX);
			result.Axes.Add(state.AccelerationY);
			result.Axes.Add(state.AccelerationZ);
			result.Axes.Add(state.AngularAccelerationX);
			result.Axes.Add(state.AngularAccelerationY);
			result.Axes.Add(state.AngularAccelerationZ);
			result.Axes.Add(state.ForceX);
			result.Axes.Add(state.ForceY);
			result.Axes.Add(state.ForceZ);
			result.Axes.Add(state.TorqueX);
			result.Axes.Add(state.TorqueY);
			result.Axes.Add(state.TorqueZ);
			result.Axes.Add(state.VelocityX);
			result.Axes.Add(state.VelocityY);
			result.Axes.Add(state.VelocityZ);
			result.Axes.Add(state.AngularVelocityX);
			result.Axes.Add(state.AngularVelocityY);
			result.Axes.Add(state.AngularVelocityZ);

			// Convert sliders (8 sliders in DirectInput)
			result.Sliders.Add(state.Sliders[0]);
			result.Sliders.Add(state.Sliders[1]);
			result.Sliders.Add(state.AccelerationSliders[0]);
			result.Sliders.Add(state.AccelerationSliders[1]);
			result.Sliders.Add(state.ForceSliders[0]);
			result.Sliders.Add(state.ForceSliders[1]);
			result.Sliders.Add(state.VelocitySliders[0]);
			result.Sliders.Add(state.VelocitySliders[1]);

			// Convert buttons (DirectInput reports as bool array)
			foreach (var button in state.Buttons)
			{
				result.Buttons.Add(button ? 1 : 0);
			}

			// Convert POVs (DirectInput reports -1 for neutral, 0-35900 for directions)
			foreach (var pov in state.PointOfViewControllers)
			{
				result.POVs.Add(pov);
			}

			return result;
		}

		/// <summary>
		/// Converts DirectInput MouseState to ListTypeState format.
		/// </summary>
		/// <param name="state">MouseState from DirectInput device</param>
		/// <returns>ListTypeState with axes and buttons (no sliders or POVs)</returns>
		/// <remarks>
		/// DirectInput MouseState Mapping:
		/// • Axes (3): X (horizontal movement), Y (vertical movement), Z (wheel)
		/// • Buttons (8 max): Mouse button states as 0 or 1
		/// • No sliders or POVs for mouse devices
		/// </remarks>
		private static ListTypeState ConvertMouseState(MouseState state)
		{
			var result = new ListTypeState();

			// Convert axes (X, Y, Z for mouse)
			result.Axes.Add(state.X);
			result.Axes.Add(state.Y);
			result.Axes.Add(state.Z);

			// Convert buttons (DirectInput reports as bool array)
			foreach (var button in state.Buttons)
			{
				result.Buttons.Add(button ? 1 : 0);
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
		private static ListTypeState ConvertKeyboardState(KeyboardState state)
		{
			var result = new ListTypeState();

			// Initialize all 256 buttons as released (0)
			for (int i = 0; i < 256; i++)
			{
				result.Buttons.Add(0);
			}

			// Set pressed keys to 1
			foreach (var key in state.PressedKeys)
			{
				int keyIndex = (int)key;
				if (keyIndex >= 0 && keyIndex < 256)
				{
					result.Buttons[keyIndex] = 1;
				}
			}

			// Keyboards have no axes, sliders, or POVs (lists remain empty)

			return result;
		}
	}
}
