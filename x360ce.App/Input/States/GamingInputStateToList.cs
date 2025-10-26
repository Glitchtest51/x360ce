using Windows.Gaming.Input;

namespace x360ce.App.Input.States
{
	/// <summary>
	/// Converts Gaming Input device states to standardized ListTypeState format.
	/// Handles GamepadReading structure from Windows.Gaming.Input API.
	/// </summary>
	internal static class GamingInputStateToList
	{
		/// <summary>
		/// Converts Gaming Input GamepadReading to ListTypeState format.
		/// </summary>
		/// <param name="reading">GamepadReading from Windows.Gaming.Input</param>
		/// <returns>ListTypeState with standardized format</returns>
		/// <remarks>
		/// Gaming Input GamepadReading Mapping:
		/// • 6 Axes: LeftThumbstickX, LeftThumbstickY, RightThumbstickX, RightThumbstickY, LeftTrigger, RightTrigger
		///   - Thumbsticks: -1.0 to 1.0 (converted to 0-65535)
		///   - Triggers: 0.0 to 1.0 (converted to 0-65535)
		/// • 0 Sliders: Gaming Input has no sliders (triggers are axes)
		/// • 16 Buttons: A, B, X, Y, LeftShoulder, RightShoulder, View, Menu, LeftThumbstick, RightThumbstick,
		///   DPadUp, DPadDown, DPadLeft, DPadRight, Paddle1-4 (if available)
		/// • 1 POV: D-Pad direction converted to centidegrees (-1 for neutral, 0-27000 for directions)
		/// </remarks>
		public static InputStateAsList ConvertGamingInputStateToList(GamepadReading? gamepadReading)
		{
			
			GamepadReading reading = gamepadReading.Value;
            var result = new InputStateAsList();

			// Convert axes (6 axes in Gaming Input)
			// Thumbsticks: Convert from -1.0..1.0 to 0..65535
			result.Axes.Add(ConvertNormalizedToAxis(reading.LeftThumbstickX));
			result.Axes.Add(ConvertNormalizedToAxis(reading.LeftThumbstickY));
			result.Axes.Add(ConvertNormalizedToAxis(reading.RightThumbstickX));
			result.Axes.Add(ConvertNormalizedToAxis(reading.RightThumbstickY));
			
			// Triggers: Convert from 0.0..1.0 to 0..65535
			result.Axes.Add(ConvertTriggerToAxis(reading.LeftTrigger));
			result.Axes.Add(ConvertTriggerToAxis(reading.RightTrigger));

			// Gaming Input has no sliders (list remains empty)

			// Convert buttons (16 buttons in Gaming Input)
			var buttons = reading.Buttons;
			result.Buttons.Add((buttons & GamepadButtons.A) != 0 ? 1 : 0);
			result.Buttons.Add((buttons & GamepadButtons.B) != 0 ? 1 : 0);
			result.Buttons.Add((buttons & GamepadButtons.X) != 0 ? 1 : 0);
			result.Buttons.Add((buttons & GamepadButtons.Y) != 0 ? 1 : 0);
			result.Buttons.Add((buttons & GamepadButtons.LeftShoulder) != 0 ? 1 : 0);
			result.Buttons.Add((buttons & GamepadButtons.RightShoulder) != 0 ? 1 : 0);
			result.Buttons.Add((buttons & GamepadButtons.View) != 0 ? 1 : 0);
			result.Buttons.Add((buttons & GamepadButtons.Menu) != 0 ? 1 : 0);
			result.Buttons.Add((buttons & GamepadButtons.LeftThumbstick) != 0 ? 1 : 0);
			result.Buttons.Add((buttons & GamepadButtons.RightThumbstick) != 0 ? 1 : 0);
			
			// D-Pad buttons
			result.Buttons.Add((buttons & GamepadButtons.DPadUp) != 0 ? 1 : 0);
			result.Buttons.Add((buttons & GamepadButtons.DPadDown) != 0 ? 1 : 0);
			result.Buttons.Add((buttons & GamepadButtons.DPadLeft) != 0 ? 1 : 0);
			result.Buttons.Add((buttons & GamepadButtons.DPadRight) != 0 ? 1 : 0);
			
			// Paddle buttons (if available on controller)
			result.Buttons.Add((buttons & GamepadButtons.Paddle1) != 0 ? 1 : 0);
			result.Buttons.Add((buttons & GamepadButtons.Paddle2) != 0 ? 1 : 0);

			// Convert D-Pad to POV format (centidegrees)
			int povValue = ConvertDPadToPOV(buttons);
			result.POVs.Add(povValue);

			return result;
		}

		/// <summary>
		/// Converts Gaming Input normalized thumbstick value from -1.0..1.0 range to 0..65535 range.
		/// </summary>
		/// <param name="normalizedValue">Thumbstick value from Gaming Input (-1.0 to 1.0)</param>
		/// <returns>Converted value in 0-65535 range</returns>
		private static int ConvertNormalizedToAxis(double normalizedValue)
		{
			// Convert from -1.0..1.0 to 0..65535
			// Add 1.0 to shift to 0..2.0, then multiply by 32767.5
			return (int)((normalizedValue + 1.0) * 32767.5);
		}

		/// <summary>
		/// Converts Gaming Input trigger value from 0.0..1.0 range to 0..65535 range.
		/// </summary>
		/// <param name="triggerValue">Trigger value from Gaming Input (0.0 to 1.0)</param>
		/// <returns>Converted value in 0-65535 range</returns>
		private static int ConvertTriggerToAxis(double triggerValue)
		{
			// Convert from 0.0..1.0 to 0..65535
			return (int)(triggerValue * 65535.0);
		}

		/// <summary>
		/// Converts Gaming Input D-Pad button flags to POV centidegrees format.
		/// </summary>
		/// <param name="buttons">GamepadButtons flags</param>
		/// <returns>POV value: -1 for neutral, 0-27000 for directions in centidegrees</returns>
		/// <remarks>
		/// POV Direction Mapping:
		/// • -1 = Neutral (no D-Pad pressed)
		/// • 0 = North (Up)
		/// • 4500 = Northeast (Up+Right)
		/// • 9000 = East (Right)
		/// • 13500 = Southeast (Down+Right)
		/// • 18000 = South (Down)
		/// • 22500 = Southwest (Down+Left)
		/// • 27000 = West (Left)
		/// • 31500 = Northwest (Up+Left) - wraps to 0
		/// </remarks>
		private static int ConvertDPadToPOV(GamepadButtons buttons)
		{
			bool up = (buttons & GamepadButtons.DPadUp) != 0;
			bool down = (buttons & GamepadButtons.DPadDown) != 0;
			bool left = (buttons & GamepadButtons.DPadLeft) != 0;
			bool right = (buttons & GamepadButtons.DPadRight) != 0;

			// Handle 8-way directional input
			if (up && !down && !left && !right)
				return 0;        // North
			if (up && right && !down && !left)
				return 4500;     // Northeast
			if (right && !up && !down && !left)
				return 9000;     // East
			if (down && right && !up && !left)
				return 13500;    // Southeast
			if (down && !up && !left && !right)
				return 18000;    // South
			if (down && left && !up && !right)
				return 22500;    // Southwest
			if (left && !up && !down && !right)
				return 27000;    // West
			if (up && left && !down && !right)
				return 31500;    // Northwest

			// Neutral (no direction or invalid combination)
			return -1;
		}
	}
}
