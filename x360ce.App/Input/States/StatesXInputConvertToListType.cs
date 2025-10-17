using SharpDX.XInput;

namespace x360ce.App.Input.States
{
	/// <summary>
	/// Converts XInput device states to standardized ListTypeState format.
	/// Handles XInput State structure with Gamepad data.
	/// </summary>
	internal static class StatesXInputConvertToListType
	{
		/// <summary>
		/// Converts XInput State to ListTypeState format.
		/// </summary>
		/// <param name="state">XInput State structure</param>
		/// <returns>ListTypeState with standardized format</returns>
		/// <remarks>
		/// XInput State Mapping:
		/// • 6 Axes: LeftThumbstickX, LeftThumbstickY, RightThumbstickX, RightThumbstickY, LeftTrigger, RightTrigger
		///   - Thumbsticks: -32768 to 32767 (converted to 0-65535)
		///   - Triggers: 0 to 255 (converted to 0-65535)
		/// • 0 Sliders: XInput has no sliders (triggers are axes)
		/// • 15 Buttons: A, B, X, Y, LeftShoulder, RightShoulder, Back, Start, LeftThumb, RightThumb,
		///   DPadUp, DPadDown, DPadLeft, DPadRight, Guide (if available)
		/// • 0 POVs: XInput D-Pad is represented as 4 separate buttons, not a POV
		/// </remarks>
		public static ListTypeState ConvertToListTypeState(State state)
		{
			var result = new ListTypeState();
			var gamepad = state.Gamepad;

			// Convert axes (6 axes in XInput)
			// Thumbsticks: Convert from -32768..32767 to 0..65535
			result.Axes.Add(ConvertThumbstickToAxis(gamepad.LeftThumbX));
			result.Axes.Add(ConvertThumbstickToAxis(gamepad.LeftThumbY));
			result.Axes.Add(ConvertThumbstickToAxis(gamepad.RightThumbX));
			result.Axes.Add(ConvertThumbstickToAxis(gamepad.RightThumbY));
			
			// Triggers: Convert from 0..255 to 0..65535
			result.Axes.Add(ConvertTriggerToAxis(gamepad.LeftTrigger));
			result.Axes.Add(ConvertTriggerToAxis(gamepad.RightTrigger));

			// XInput has no sliders (list remains empty)

			// Convert buttons (15 buttons in XInput)
			var buttons = gamepad.Buttons;
			result.Buttons.Add((buttons & GamepadButtonFlags.A) != 0 ? 1 : 0);
			result.Buttons.Add((buttons & GamepadButtonFlags.B) != 0 ? 1 : 0);
			result.Buttons.Add((buttons & GamepadButtonFlags.X) != 0 ? 1 : 0);
			result.Buttons.Add((buttons & GamepadButtonFlags.Y) != 0 ? 1 : 0);
			result.Buttons.Add((buttons & GamepadButtonFlags.LeftShoulder) != 0 ? 1 : 0);
			result.Buttons.Add((buttons & GamepadButtonFlags.RightShoulder) != 0 ? 1 : 0);
			result.Buttons.Add((buttons & GamepadButtonFlags.Back) != 0 ? 1 : 0);
			result.Buttons.Add((buttons & GamepadButtonFlags.Start) != 0 ? 1 : 0);
			result.Buttons.Add((buttons & GamepadButtonFlags.LeftThumb) != 0 ? 1 : 0);
			result.Buttons.Add((buttons & GamepadButtonFlags.RightThumb) != 0 ? 1 : 0);
			
			// D-Pad buttons (XInput represents D-Pad as 4 separate buttons)
			result.Buttons.Add((buttons & GamepadButtonFlags.DPadUp) != 0 ? 1 : 0);
			result.Buttons.Add((buttons & GamepadButtonFlags.DPadDown) != 0 ? 1 : 0);
			result.Buttons.Add((buttons & GamepadButtonFlags.DPadLeft) != 0 ? 1 : 0);
			result.Buttons.Add((buttons & GamepadButtonFlags.DPadRight) != 0 ? 1 : 0);

			// XInput has no POVs (D-Pad is represented as buttons, list remains empty)

			return result;
		}

		/// <summary>
		/// Converts XInput thumbstick value from -32768..32767 range to 0..65535 range.
		/// </summary>
		/// <param name="thumbstickValue">Thumbstick value from XInput (-32768 to 32767)</param>
		/// <returns>Converted value in 0-65535 range</returns>
		private static int ConvertThumbstickToAxis(short thumbstickValue)
		{
			// Convert from signed short (-32768..32767) to unsigned short (0..65535)
			// Add 32768 to shift the range
			return thumbstickValue + 32768;
		}

		/// <summary>
		/// Converts XInput trigger value from 0..255 range to 0..65535 range.
		/// </summary>
		/// <param name="triggerValue">Trigger value from XInput (0 to 255)</param>
		/// <returns>Converted value in 0-65535 range</returns>
		private static int ConvertTriggerToAxis(byte triggerValue)
		{
			// Convert from byte (0..255) to unsigned short (0..65535)
			// Multiply by 257 (65535 / 255) for accurate scaling
			return triggerValue * 257;
		}
	}
}
