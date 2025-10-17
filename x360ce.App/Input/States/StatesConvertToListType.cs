using x360ce.App.Input.Devices;

namespace x360ce.App.Input.States
{
	/// <summary>
	/// Provides unified conversion methods to convert different input method states
	/// (RawInput, DirectInput, XInput, GamingInput) to standardized ListTypeState format.
	/// </summary>
	/// <remarks>
	/// Standardized Format: ((axes), (sliders), (buttons), (povs))
	/// Example: ((0,0,0,1,0,0,0,0,0,0),(),(32100,3566,0,0,31540),(-1,0))
	///
	/// Format Rules:
	/// • Buttons: false = 0, true = 1
	/// • Axes and Sliders: 0 to 65535 range
	/// • POVs: -1 (neutral), 0 to 27000 (centidegrees)
	/// </remarks>
	internal class StatesConvertToListType
	{
		private readonly StatesRawInput _statesRawInput = new StatesRawInput();
		private readonly StatesDirectInput _statesDirectInput = new StatesDirectInput();
		private readonly StatesXInput _statesXInput = new StatesXInput();
		private readonly StatesGamingInput _statesGamingInput = new StatesGamingInput();

		/// <summary>
		/// Converts RawInput device state to standardized ListTypeState format.
		/// </summary>
		/// <param name="riDeviceInfo">RawInput device information</param>
		/// <returns>ListTypeState with format: ((axes), (sliders), (buttons), (povs)), or null if conversion fails</returns>
		/// <remarks>
		/// RawInput provides raw HID reports that require device-specific parsing.
		/// This method uses the device information to parse the raw report data.
		/// </remarks>
		public ListTypeState GetRawInputStateAsListTypeState(RawInputDeviceInfo riDeviceInfo)
		{
			var rawState = _statesRawInput.GetRawInputDeviceState(riDeviceInfo);
			if (rawState == null)
				return null;

			// Use the dedicated RawInput converter
			return StatesRawInputConvertToListType.ConvertToListTypeState(rawState, riDeviceInfo);
		}

		/// <summary>
		/// Converts DirectInput device state to standardized ListTypeState format.
		/// </summary>
		/// <param name="diDeviceInfo">DirectInput device information</param>
		/// <returns>ListTypeState with format: ((axes), (sliders), (buttons), (povs)), or null if conversion fails</returns>
		/// <remarks>
		/// DirectInput provides JoystickState, KeyboardState, or MouseState objects.
		/// The converter automatically detects the state type and converts accordingly.
		/// </remarks>
		public ListTypeState GetDirectInputStateAsListTypeState(DirectInputDeviceInfo diDeviceInfo)
		{
			var diState = _statesDirectInput.GetDirectInputDeviceState(diDeviceInfo);
			if (diState == null)
				return null;

			// Use the dedicated DirectInput converter
			return StatesDirectInputConvertToListType.ConvertToListTypeState(diState);
		}

		/// <summary>
		/// Converts XInput device state to standardized ListTypeState format.
		/// </summary>
		/// <param name="xiDeviceInfo">XInput device information</param>
		/// <returns>ListTypeState with format: ((axes), (sliders), (buttons), (povs)), or null if conversion fails</returns>
		/// <remarks>
		/// XInput provides State structure with Gamepad data.
		/// Converts thumbsticks, triggers, and buttons to standardized format.
		/// </remarks>
		public ListTypeState GetXInputStateAsListTypeState(XInputDeviceInfo xiDeviceInfo)
		{
			var xiState = _statesXInput.GetXInputDeviceState(xiDeviceInfo);
			if (xiState == null)
				return null;

			// Use the dedicated XInput converter
			return StatesXInputConvertToListType.ConvertToListTypeState(xiState.Value);
		}

		/// <summary>
		/// Converts GamingInput device state to standardized ListTypeState format.
		/// </summary>
		/// <param name="giDeviceInfo">GamingInput device information</param>
		/// <returns>ListTypeState with format: ((axes), (sliders), (buttons), (povs)), or null if conversion fails</returns>
		/// <remarks>
		/// GamingInput Conversion Mapping:
		/// • 6 Axes: Left/Right stick X/Y, Left/Right triggers (normalized to 0-65535)
		/// • 0 Sliders: Gaming Input has no sliders
		/// • 16 Buttons: A, B, X, Y, LB, RB, View, Menu, LS, RS, DPad buttons, Paddles
		/// • 1 POV: D-Pad direction (-1 or 0-27000 centidegrees)
		/// </remarks>
		public ListTypeState GetGamingInputStateAsListTypeState(GamingInputDeviceInfo giDeviceInfo)
		{
			var giState = _statesGamingInput.GetGamingInputDeviceState(giDeviceInfo);
			if (giState == null)
				return null;

			// Use the dedicated GamingInput converter
			return StatesGamingInputConvertToListType.ConvertToListTypeState(giState.Value);
		}
	}
}
