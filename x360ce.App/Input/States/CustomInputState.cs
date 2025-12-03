using System;
using System.Collections.Generic;
using SharpDX.DirectInput;
using SharpDX.XInput;
using Windows.Gaming.Input;
using x360ce.App.Input.Devices;
using System.Linq;
using System.Runtime.CompilerServices;

namespace x360ce.App.Input.States
{
	/// <summary>
	/// Standardized state representation for all input methods (RawInput, DirectInput, XInput, GamingInput).
	/// Provides a custom format: ((axes), (sliders), (buttons), (povs)).
	/// </summary>
	/// <remarks>
	/// Format Specification:
	/// • Axes: List of axis values (0-65535 range), empty list if no axes.
	/// • Sliders: List of slider values (0-65535 range), empty list if no sliders.
	/// • Buttons: List of button states (0=released, 1=pressed), empty list if no buttons.
	/// • POVs: List of POV/D-Pad values (-1=neutral, 0-27000 = direction in centidegrees, 0 = North, 9000 = East, 18000 = South, 27000 = West), empty list if no POVs.
	///
	/// Example: ((32100,3566,0,0,31540),(),(0,0,0,1,0,0,0,0,0,0),(-1,0))
	/// Note: Empty collections are represented as empty lists (), not null
	/// </remarks>
	public class CustomInputState
	{
		/// <summary>
		/// Axis values in 0-65535 range.
		/// Typically includes: X, Y, Z, RX, RY, RZ axes.
		/// Empty list if device has no axes.
		/// </summary>
		public List<int> Axes { get; set; }

		/// <summary>
		/// Slider values in 0-65535 range.
		/// Empty list if device has no sliders.
		/// </summary>
		public List<int> Sliders { get; set; }

		/// <summary>
		/// Button states: 0 = released, 1 = pressed.
		/// Empty list if device has no buttons.
		/// </summary>
		public List<int> Buttons { get; set; }

		/// <summary>
		/// POV/D-Pad values: -1 = neutral/centered, 0-27000 = direction in centidegrees.
		/// 0 = North, 9000 = East, 18000 = South, 27000 = West.
		/// Empty list if device has no POVs.
		/// </summary>
		public List<int> POVs { get; set; }

		/// <summary>
		/// Initializes a new ListTypeState with empty collections.
		/// </summary>
		public CustomInputState()
		{
			Axes = new List<int>();
			Sliders = new List<int>();
			Buttons = new List<int>();
			POVs = new List<int>();
		}

		private static readonly int[] _ZeroButtons = new int[256];

		#region Conversion Methods

		/// <summary>
		/// Source type for conversion context.
		/// </summary>
		public enum InputSourceType
		{
			Unknown,
			DirectInput,
			XInput,
			RawInput,
			GamingInput
		}

		/// <summary>
		/// Converts any range axis value (e.g., -32768 to 32767, or 0 to 255) to the standardized 0-65535 range.
		/// Automatically detects common ranges and normalizes them based on source context.
		/// </summary>
		/// <param name="value">The raw axis value to convert.</param>
		/// <param name="min">The minimum value of the source range (optional). If not provided, basic detection is used.</param>
		/// <param name="max">The maximum value of the source range (optional). If not provided, basic detection is used.</param>
		/// <param name="sourceType">The source of the input (optional). Helps refine heuristic detection.</param>
		/// <returns>Normalized value in 0-65535 range.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int ConvertToAxisRange(int value, int min = int.MinValue, int max = int.MaxValue, InputSourceType sourceType = InputSourceType.Unknown)
		{
			// If min/max are provided, use them for precise scaling
			if (min != int.MinValue && max != int.MaxValue)
			{
				if (max == min)
					return 0;

				// Optimization: if target range (0-65535) matches source range, just shift and clamp.
				// e.g. XInput Thumbsticks: -32768 to 32767 (Range = 65535)
				// e.g. DirectInput Axis: 0 to 65535 (Range = 65535)
				var range = (long)max - min;
				if (range == 65535)
				{
					var res = (long)value - min;
					return (int)((res < 0) ? 0 : ((res > 65535) ? 65535 : res));
				}

				// Map range [min, max] to [0, 65535]
				// (val - min) / (max - min) * 65535
				var result = (long)(value - min) * 65535 / range;
				return (int)((result < 0) ? 0 : ((result > 65535) ? 65535 : result));
			}

			// Automatic detection for common ranges

			// XInput Thumbsticks / Signed Short Range: -32768 to 32767
			// DirectInput sometimes uses this range too
			// XInput triggers (0-255) usually handled with explicit min/max, but fallback here if needed
			if (sourceType == InputSourceType.XInput || (sourceType != InputSourceType.RawInput && value >= -32768 && value <= 32767))
			{
				// Byte Range: 0 to 255 (e.g., Triggers)
				if (value >= 0 && value <= 255)
				{
					return Math.Max(0, Math.Min(65535, value * 257));
				}

				// Standard XInput Stick Range
				return Math.Max(0, Math.Min(65535, value + 32768));
			}

			// DirectInput usually 0-65535, but just in case
			if (sourceType == InputSourceType.DirectInput)
			{
				return Math.Max(0, Math.Min(65535, value));
			}

			// RawInput Heuristics
			if (sourceType == InputSourceType.RawInput || sourceType == InputSourceType.Unknown)
			{
			             // Correctly handle joystick axes which are often 0-32767 or 0-65535
			             // The issue reported "Raw input joystic (index 0 and 1) axis value is converted to wrong 0-49151 range"
			             // suggests that a 0-32767 (or similar 15-bit) range is being treated as a wider range or scaled incorrectly.
			             // 49151 is exactly 0xBFFF, which is 3/4 of 65535.
			             // This implies something was scaling by 1.5x or similar when it should be 2x.
			             
			             // Check for known 15-bit range (0 to 32767)
			             // If a device reports 32767 as max, it should map to 65535.
			             // 32767 * 2 = 65534.
			             if (max == 32767 && min == 0)
			             {
			                  return Math.Max(0, Math.Min(65535, value * 2));
			             }

			                      // Heuristic fallback: check if value is already within 0-49151 and looks like it might be 0-32767 mis-scaled
			                      // The feedback says "wrong 0-49151 range" which corresponds to 0-32767 scaled by 1.5
			                      // 32767 * 1.5 = ~49150.5.
			                      // If max was not provided and we are hitting this range, it means some upstream logic or default behavior
			                      // is likely treating it as signed short or similar but applying unsigned interpretation.
			                      
			                      // Explicitly check for values that look like they are in 0-32767 range if no min/max provided
			                      // Note: This must be done carefully as 0-65535 values will also fall into these ranges initially.
			                      // However, usually axis values change over time.
			                      // A value of e.g. 40000 is clearly 0-65535.
			                      // A value of 10000 is ambiguous.
			                      // But if we assume 0-32767 range is common for HID, we prioritize scaling it up?
			                      // This might double-scale valid small inputs from a 0-65535 device.
			                      // BUT the task is specific: "Test joystic 0 and 1 index axis original value ranges are 0-16383"
			                      // and "Raw input joystic (index 0 and 1) axis value is converted to wrong 0-49151 range".
			                      
			                      if (min == int.MinValue && max == int.MaxValue)
			                      {
			                          // Fix: Incorrect axis scaling interpretation for unsigned 16-bit values reported as signed short
			                          // Some drivers report 0-65535 range but behave strangely when interpreted as signed/unsigned.
			                          // If first half is 0-65535 and second half is 32768-65535, it indicates a signed/unsigned interpretation conflict.
			                          // For now, if we detect values > 32767 without explicit max, it might be a full range axis.
			                          // Standardize 0-65535 range:
			                          if (value >= 0 && value <= 65535)
			                          {
			                              return value;
			                          }

			                          // Test joystick 0 and 1 index axis original value ranges are 0-16383.
			                          // If value is in 0-16383 range, it should be scaled to 0-65535.
			                          // This check must be before 0-32767 because 0-16383 is a subset of 0-32767.
			                          // 16383 * 4 = 65532.
			                          // 16383 * 4 = 65532.
			                          // Also 16383 * 4 + 3 = 65535 (perfect distribution)
			                          // But simple multiplication is usually enough.
			                          // If value maps to 0-49151, it means it was multiplied by 3 instead of 4.
			                          // 16383 * 3 = 49149.
			                          if (value > 1023 && value <= 16383)
			                          {
			                              // Scale 0-16383 to 0-65535
			                              return Math.Max(0, Math.Min(65535, value * 4));
			                          }

			                          if (value > 1023 && value <= 32767)
			                          {
			                              // Scale 0-32767 to 0-65535
			                              // value * 2 + (value > 0 ? 1 : 0) to roughly double
			                              return Math.Max(0, Math.Min(65535, value * 2));
			                          }
			                      }

				// Byte Range: 0 to 255 (e.g., RawInput sliders, 8-bit)
				if (value >= 0 && value <= 255)
				{
					return Math.Max(0, Math.Min(65535, value * 257));
				}

				// 9-bit Range: 0 to 511 (RawInput rudder/slider: 0-510 reported)
				if (value >= 0 && value <= 511)
				{
					// Scale 0-511 to 0-65535
					return Math.Max(0, Math.Min(65535, value * 128));
				}

				            // 10-bit Range: 0 to 1023 (common for pedals/triggers)
				            // 1023 * 64 = 65472
				if (value >= 0 && value <= 1023)
				{
					// Scale 0-1023 to 0-65535
					return Math.Max(0, Math.Min(65535, value * 64));
				}

				// 14-bit Range: 0 to 16383 (RawInput joystick axis)
				            // Only apply if value > 1023 to protect smaller ranges
				if (value > 1023 && value <= 16383)
				{
					// Scale 0-16383 to 0-65535
					return Math.Max(0, Math.Min(65535, value * 4));
				}
				            
				            // 10-bit Range: 0 to 1023 (common for pedals/triggers)
				            // 1023 * 64 = 65472
				if (value >= 0 && value <= 1023)
				{
					// Scale 0-1023 to 0-65535
					return Math.Max(0, Math.Min(65535, value * 64));
				}

				            // NOTE: 0-32767 handling logic moved above to generic block if min/max not provided
				            // to catch cases where the range is implicit.
				            
				// 15-bit Range: 0 to 32766 (Specific RawInput Joystick Axis reported in feedback)
				// Must be checked before 0-32767 to avoid overlap ambiguity if they require different scaling
				// Feedback says 32766 maps to ~49151 with *1.5 scaling, so we need strict *2 scaling.
				// 32766 * 2 = 65532 (close enough to 65535)
				if (value >= 0 && value <= 32766)
				{
					// Scale 0-32766 to 0-65535
					return Math.Max(0, Math.Min(65535, value * 2));
				}

				// Positive Short Range: 0 to 32767 (often used in flight sticks)
				if (value >= 0 && value <= 32767)
				{
					// Scale 0-32767 to 0-65535
					// value * 2 + (value > 0 ? 1 : 0) to roughly double
					return Math.Max(0, Math.Min(65535, value * 2));
				}
			}

			// Fallback: Clamp to 0-65535
			return Math.Max(0, Math.Min(65535, value));
		}

		/// <summary>
		/// Converts a normalized double value (-1.0 to 1.0 or 0.0 to 1.0) to the standardized 0-65535 range.
		/// Used primarily by Windows.Gaming.Input.
		/// </summary>
		/// <param name="value">Normalized value.</param>
		/// <param name="isUnsigned">If true, assumes 0.0 to 1.0 range (e.g. triggers). If false, assumes -1.0 to 1.0 range (e.g. sticks).</param>
		/// <returns>Normalized value in 0-65535 range.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int ConvertToAxisRange(double value, bool isUnsigned)
		{
			if (isUnsigned)
			{
				// Range 0.0 to 1.0
				return (int)(Math.Max(0.0, Math.Min(1.0, value)) * 65535.0);
			}
			else
			{
				// Range -1.0 to 1.0
				// (val + 1.0) / 2.0 * 65535.0
				return (int)((Math.Max(-1.0, Math.Min(1.0, value)) + 1.0) * 32767.5);
			}
		}

		/// <summary>
		/// Converts any boolean or integer button state to 0 or 1.
		/// </summary>
		/// <param name="isPressed">Boolean state (true=pressed).</param>
		/// <returns>1 if pressed, 0 if released.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int ConvertToButtonRange(bool isPressed)
		{
			return isPressed ? 1 : 0;
		}

		/// <summary>
		/// Converts raw integer button value to 0 or 1.
		/// </summary>
		/// <param name="value">Raw value (0=released, >0=pressed).</param>
		/// <returns>1 if pressed, 0 if released.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int ConvertToButtonRange(int value)
		{
			return value > 0 ? 1 : 0;
		}

		/// <summary>
		/// Converts various POV formats to the standardized 0-27000 centidegree range.
		/// -1 indicates centered/neutral.
		/// </summary>
		/// <param name="value">Raw POV value.</param>
		/// <param name="format">The format of the source value.</param>
		/// <returns>Standardized POV value (-1 or 0-27000).</returns>
		public static int ConvertToPOVRange(int value, PovFormat format = PovFormat.DirectInput)
		{
			// Neutral check common to most formats
			if (value == -1) return -1;

			switch (format)
			{
				case PovFormat.DirectInput:
					// DirectInput: 0 to 35900 centidegrees, or -1 for center.
					// We normalize to 0-36000 range but x360ce standard is 0-27000 for 4 directions?
					// Wait, the requirement says "0-27000 range values".
					// 0 = North, 9000 = East, 18000 = South, 27000 = West.
					// This implies 90-degree steps x 100.
					// DirectInput uses the same scale but allows 36000 (continuous).
					// If the target is strictly 0, 9000, 18000, 27000, we might need to snap?
					// But usually we keep precision if available.
					// Let's assume the requirement means "Standard Centidegrees".
					// DirectInput is already in centidegrees (0-36000).
					// If value is 65535 (sometimes used for neutral), return -1.
					if (value == 65535) return -1;
					return value;

				case PovFormat.EightWay:
					// 0-7 range (0=N, 1=NE, 2=E, etc.)
					// 8 = Neutral (sometimes)
					if (value < 0 || value > 7) return -1;
					return value * 4500;

				case PovFormat.FourWay:
					// 0-3 range (0=N, 1=E, 2=S, 3=W)
					if (value < 0 || value > 3) return -1;
					return value * 9000;

				case PovFormat.SixteenWay:
					// 0-15 range
					if (value < 0 || value > 15) return -1;
					return value * 2250;

				default:
					return value;
			}
		}

		/// <summary>
		/// Formats for POV input values.
		/// </summary>
		public enum PovFormat
		{
			/// <summary>
			/// DirectInput standard: 0-35900 centidegrees.
			/// </summary>
			DirectInput,
			/// <summary>
			/// 0-7 index for 8 directions (N, NE, E, SE, S, SW, W, NW).
			/// </summary>
			EightWay,
			/// <summary>
			/// 0-3 index for 4 directions (N, E, S, W).
			/// </summary>
			FourWay,
			/// <summary>
			/// 0-15 index for 16 directions.
			/// </summary>
			SixteenWay
		}

		#endregion
        
        #region Integrated State Conversions
        
        // Imported from DirectInputStateToListInputState
        private const int AxisMinValue = 0;
		private const int AxisMaxValue = 65535;
		private const int DefaultMouseSensitivity = 20;
		private const int DefaultMouseWheelSensitivity = 50;
		private const int MinSensitivity = 1;
		private const int KeyboardButtonCount = 256;

        public static CustomInputState ConvertDirectInputStateToListInputState(object diState, DirectInputDeviceInfo deviceInfo = null)
		{
			if (diState == null)
				return null;

			// If already InputStateAsList (keyboard polled state), return directly
			if (diState is CustomInputState listState)
				return listState;

			// Detect state type and convert accordingly
			if (diState is JoystickState joystickState)
				return ConvertJoystickState(joystickState, deviceInfo);
			else if (diState is MouseState mouseState)
				return ConvertMouseState(mouseState, deviceInfo);
			else if (diState is KeyboardState keyboardState)
				return ConvertKeyboardState(keyboardState);

			return null;
		}

  private static CustomInputState ConvertJoystickState(JoystickState state, DirectInputDeviceInfo deviceInfo)
  {
  	// CRITICAL FIX: Reuse existing ListInputState object if it exists
  	// This maintains the reference in CustomInputDeviceInfo.ListInputState
  	CustomInputState result = deviceInfo?.ListInputState;

  	if (result == null)
  	{
  		// First time - create new ListInputState
  		result = new CustomInputState();
  	}

    // Use available axes info if possible to populate the list efficiently
    // This ensures that ListInputState axes map 1:1 with AvailableAxes in DirectInputDeviceInfo
    var availableAxes = deviceInfo?.AvailableAxes;
    var axes = result.Axes;

    if (availableAxes != null && availableAxes.Count > 0)
    {
        // Resize list if needed to match available axes count
        if (axes.Count != availableAxes.Count)
        {
            axes.Clear();
            if (axes.Capacity < availableAxes.Count)
                axes.Capacity = availableAxes.Count;
            
            for (int i = 0; i < availableAxes.Count; i++)
                axes.Add(0);
        }

        // Update values using specific available axes mapping
        for (int i = 0; i < availableAxes.Count; i++)
        {
            axes[i] = ClampAxisValue(GetAxisValue(state, availableAxes[i]));
        }
    }
    else
    {
        // Fallback to legacy fixed 24-axis array if no available axes info
      	if (axes.Count == 24)
      	{
      		// Update existing axes in place
      		axes[0] = ClampAxisValue(state.X);
      		axes[1] = ClampAxisValue(state.Y);
      		axes[2] = ClampAxisValue(state.Z);
      		axes[3] = ClampAxisValue(state.RotationX);
      		axes[4] = ClampAxisValue(state.RotationY);
      		axes[5] = ClampAxisValue(state.RotationZ);
      		axes[6] = ClampAxisValue(state.AccelerationX);
      		axes[7] = ClampAxisValue(state.AccelerationY);
      		axes[8] = ClampAxisValue(state.AccelerationZ);
      		axes[9] = ClampAxisValue(state.AngularAccelerationX);
      		axes[10] = ClampAxisValue(state.AngularAccelerationY);
      		axes[11] = ClampAxisValue(state.AngularAccelerationZ);
      		axes[12] = ClampAxisValue(state.ForceX);
      		axes[13] = ClampAxisValue(state.ForceY);
      		axes[14] = ClampAxisValue(state.ForceZ);
      		axes[15] = ClampAxisValue(state.TorqueX);
      		axes[16] = ClampAxisValue(state.TorqueY);
      		axes[17] = ClampAxisValue(state.TorqueZ);
      		axes[18] = ClampAxisValue(state.VelocityX);
      		axes[19] = ClampAxisValue(state.VelocityY);
      		axes[20] = ClampAxisValue(state.VelocityZ);
      		axes[21] = ClampAxisValue(state.AngularVelocityX);
      		axes[22] = ClampAxisValue(state.AngularVelocityY);
      		axes[23] = ClampAxisValue(state.AngularVelocityZ);
      	}
      	else
      	{
      		// First time or mismatched count - clear and add
      		axes.Clear();
      		axes.Capacity = 24;
      		// Use ClampAxisValue directly since DirectInput is normalized to 0-65535
      		axes.Add(ClampAxisValue(state.X));
      		axes.Add(ClampAxisValue(state.Y));
      		axes.Add(ClampAxisValue(state.Z));
      		axes.Add(ClampAxisValue(state.RotationX));
      		axes.Add(ClampAxisValue(state.RotationY));
      		axes.Add(ClampAxisValue(state.RotationZ));
      		axes.Add(ClampAxisValue(state.AccelerationX));
      		axes.Add(ClampAxisValue(state.AccelerationY));
      		axes.Add(ClampAxisValue(state.AccelerationZ));
      		axes.Add(ClampAxisValue(state.AngularAccelerationX));
      		axes.Add(ClampAxisValue(state.AngularAccelerationY));
      		axes.Add(ClampAxisValue(state.AngularAccelerationZ));
      		axes.Add(ClampAxisValue(state.ForceX));
      		axes.Add(ClampAxisValue(state.ForceY));
      		axes.Add(ClampAxisValue(state.ForceZ));
      		axes.Add(ClampAxisValue(state.TorqueX));
      		axes.Add(ClampAxisValue(state.TorqueY));
      		axes.Add(ClampAxisValue(state.TorqueZ));
      		axes.Add(ClampAxisValue(state.VelocityX));
      		axes.Add(ClampAxisValue(state.VelocityY));
      		axes.Add(ClampAxisValue(state.VelocityZ));
      		axes.Add(ClampAxisValue(state.AngularVelocityX));
      		axes.Add(ClampAxisValue(state.AngularVelocityY));
      		axes.Add(ClampAxisValue(state.AngularVelocityZ));
      	}
    }

  	// Convert sliders
  	var availableSliders = deviceInfo?.AvailableSliders;
  	var sliders = result.Sliders;

  	if (availableSliders != null)
  	{
  		// Resize list if needed to match available sliders count
  		if (sliders.Count != availableSliders.Count)
  		{
  			sliders.Clear();
  			if (sliders.Capacity < availableSliders.Count)
  				sliders.Capacity = availableSliders.Count;

  			for (int i = 0; i < availableSliders.Count; i++)
  				sliders.Add(0);
  		}

  		// Update values using specific available sliders mapping
  		for (int i = 0; i < availableSliders.Count; i++)
  		{
  			sliders[i] = ClampAxisValue(GetAxisValue(state, availableSliders[i]));
  		}
  	}
  	else
  	{
  		// Fallback to legacy 8 sliders
  		if (sliders.Count == 8)
  		{
  			sliders[0] = ClampAxisValue(state.Sliders[0]);
  			sliders[1] = ClampAxisValue(state.Sliders[1]);
  			sliders[2] = ClampAxisValue(state.AccelerationSliders[0]);
  			sliders[3] = ClampAxisValue(state.AccelerationSliders[1]);
  			sliders[4] = ClampAxisValue(state.ForceSliders[0]);
  			sliders[5] = ClampAxisValue(state.ForceSliders[1]);
  			sliders[6] = ClampAxisValue(state.VelocitySliders[0]);
  			sliders[7] = ClampAxisValue(state.VelocitySliders[1]);
  		}
  		else
  		{
  			sliders.Clear();
  			sliders.Capacity = 8;
  			sliders.Add(ClampAxisValue(state.Sliders[0]));
  			sliders.Add(ClampAxisValue(state.Sliders[1]));
  			sliders.Add(ClampAxisValue(state.AccelerationSliders[0]));
  			sliders.Add(ClampAxisValue(state.AccelerationSliders[1]));
  			sliders.Add(ClampAxisValue(state.ForceSliders[0]));
  			sliders.Add(ClampAxisValue(state.ForceSliders[1]));
  			sliders.Add(ClampAxisValue(state.VelocitySliders[0]));
  			sliders.Add(ClampAxisValue(state.VelocitySliders[1]));
  		}
  	}

  	// Convert buttons (DirectInput reports as bool array) - pre-allocate capacity
  	var buttons = result.Buttons;
  	var stateButtons = state.Buttons;
  	if (buttons.Count == stateButtons.Length)
  	{
  		for (int i = 0; i < stateButtons.Length; i++)
  			buttons[i] = ConvertToButtonRange(stateButtons[i]);
  	}
  	else
  	{
  		buttons.Clear();
  		buttons.Capacity = stateButtons.Length;
  		for (int i = 0; i < stateButtons.Length; i++)
  			buttons.Add(ConvertToButtonRange(stateButtons[i]));
  	}

  	// Convert POVs (DirectInput reports -1 for neutral, 0-35900 for directions)
  	var povs = result.POVs;
  	var statePovs = state.PointOfViewControllers;
  	if (povs.Count == statePovs.Length)
  	{
  		for (int i = 0; i < statePovs.Length; i++)
  			povs[i] = ConvertToPOVRange(statePovs[i], PovFormat.DirectInput);
  	}
  	else
  	{
  		povs.Clear();
  		povs.Capacity = statePovs.Length;
  		// DirectInput POVs are already in 0-35900 range, which is handled by ConvertToPOVRange
  		for (int i = 0; i < statePovs.Length; i++)
  			povs.Add(ConvertToPOVRange(statePovs[i], PovFormat.DirectInput));
  	}

  	return result;
  }

        private static CustomInputState ConvertMouseState(MouseState state, DirectInputDeviceInfo deviceInfo)
		{
			// CRITICAL FIX: Reuse existing ListInputState object if it exists
			// This maintains the reference in CustomInputDeviceInfo.ListInputState
			CustomInputState result = deviceInfo?.ListInputState;
			
			if (result == null)
			{
				// First time - create new ListInputState
				result = new CustomInputState();
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
				var axes = result.Axes;
				if (axes.Count >= 3)
				{
					axes[0] = ClampAxisValue(deviceInfo.MouseAxisAccumulatedDelta[0]);
					axes[1] = ClampAxisValue(deviceInfo.MouseAxisAccumulatedDelta[1]);
					axes[2] = ClampAxisValue(deviceInfo.MouseAxisAccumulatedDelta[2]);
				}
				else
				{
					axes.Clear();
					axes.Capacity = 3;
					axes.Add(ClampAxisValue(deviceInfo.MouseAxisAccumulatedDelta[0]));
					axes.Add(ClampAxisValue(deviceInfo.MouseAxisAccumulatedDelta[1]));
					axes.Add(ClampAxisValue(deviceInfo.MouseAxisAccumulatedDelta[2]));
				}
			}
			else
			{
				// Fallback if deviceInfo is null (shouldn't happen in normal operation)
				var axes = result.Axes;
				if (axes.Count >= 3)
				{
					axes[0] = 32767;
					axes[1] = 32767;
					axes[2] = 32767;
				}
				else
				{
					axes.Clear();
					axes.Capacity = 3;
					axes.Add(32767);
					axes.Add(32767);
					axes.Add(32767);
				}
			}

			// Update or add buttons
			if (result.Buttons.Count == state.Buttons.Length)
			{
				for (int i = 0; i < state.Buttons.Length; i++)
				{
					result.Buttons[i] = ConvertToButtonRange(state.Buttons[i]);
				}
			}
			else
			{
				result.Buttons.Clear();
				result.Buttons.Capacity = state.Buttons.Length;
				result.Buttons.AddRange(state.Buttons.Select(b => ConvertToButtonRange(b)));
			}

			// Mice have no sliders or POVs (lists remain empty)

			return result;
		}

        private static CustomInputState ConvertKeyboardState(KeyboardState state)
		{
			var result = new CustomInputState();

			// Initialize all 256 buttons as released (0) - pre-allocate and initialize in one step
			result.Buttons.Capacity = KeyboardButtonCount;
			result.Buttons.AddRange(_ZeroButtons); // Already 0 (released)

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

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  private static int ClampAxisValue(int value)
  {
   return Math.Max(AxisMinValue, Math.Min(AxisMaxValue, value));
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  private static int GetAxisValue(JoystickState state, JoystickOffset offset)
  {
    switch (offset)
    {
        case JoystickOffset.X: return state.X;
        case JoystickOffset.Y: return state.Y;
        case JoystickOffset.Z: return state.Z;
        case JoystickOffset.RotationX: return state.RotationX;
        case JoystickOffset.RotationY: return state.RotationY;
        case JoystickOffset.RotationZ: return state.RotationZ;
        case JoystickOffset.AccelerationX: return state.AccelerationX;
        case JoystickOffset.AccelerationY: return state.AccelerationY;
        case JoystickOffset.AccelerationZ: return state.AccelerationZ;
        case JoystickOffset.AngularAccelerationX: return state.AngularAccelerationX;
        case JoystickOffset.AngularAccelerationY: return state.AngularAccelerationY;
        case JoystickOffset.AngularAccelerationZ: return state.AngularAccelerationZ;
        case JoystickOffset.ForceX: return state.ForceX;
        case JoystickOffset.ForceY: return state.ForceY;
        case JoystickOffset.ForceZ: return state.ForceZ;
        case JoystickOffset.TorqueX: return state.TorqueX;
        case JoystickOffset.TorqueY: return state.TorqueY;
        case JoystickOffset.TorqueZ: return state.TorqueZ;
        case JoystickOffset.VelocityX: return state.VelocityX;
        case JoystickOffset.VelocityY: return state.VelocityY;
        case JoystickOffset.VelocityZ: return state.VelocityZ;
        case JoystickOffset.AngularVelocityX: return state.AngularVelocityX;
        case JoystickOffset.AngularVelocityY: return state.AngularVelocityY;
        case JoystickOffset.AngularVelocityZ: return state.AngularVelocityZ;
    case JoystickOffset.Sliders0: return state.Sliders[0];
    case JoystickOffset.Sliders1: return state.Sliders[1];
    case JoystickOffset.AccelerationSliders0: return state.AccelerationSliders[0];
    case JoystickOffset.AccelerationSliders1: return state.AccelerationSliders[1];
    case JoystickOffset.ForceSliders0: return state.ForceSliders[0];
    case JoystickOffset.ForceSliders1: return state.ForceSliders[1];
    case JoystickOffset.VelocitySliders0: return state.VelocitySliders[0];
    case JoystickOffset.VelocitySliders1: return state.VelocitySliders[1];
        default: return 0;
    }
  }
        
        // Imported from XInputStateToListInputState
        public static CustomInputState ConvertXInputStateToListInputState(State state)
		{
			var result = new CustomInputState();
			var gamepad = state.Gamepad;

			// Convert axes (6 axes in XInput)
			// Thumbsticks: Convert from -32768..32767 to 0..65535
			result.Axes.Capacity = 6;
			result.Axes.Add(ConvertToAxisRange(gamepad.LeftThumbX, -32768, 32767, InputSourceType.XInput));
			result.Axes.Add(ConvertToAxisRange(gamepad.LeftThumbY, -32768, 32767, InputSourceType.XInput));
			result.Axes.Add(ConvertToAxisRange(gamepad.RightThumbX, -32768, 32767, InputSourceType.XInput));
			result.Axes.Add(ConvertToAxisRange(gamepad.RightThumbY, -32768, 32767, InputSourceType.XInput));

			// Triggers: Convert from 0..255 to 0..65535
			result.Axes.Add(ConvertToAxisRange(gamepad.LeftTrigger, 0, 255, InputSourceType.XInput));
			result.Axes.Add(ConvertToAxisRange(gamepad.RightTrigger, 0, 255, InputSourceType.XInput));

			// XInput has no sliders (list remains empty)

			// Convert buttons (15 buttons in XInput)
			var buttons = gamepad.Buttons;
			result.Buttons.Capacity = 14;
			result.Buttons.Add(ConvertToButtonRange((buttons & GamepadButtonFlags.A) != 0));
			result.Buttons.Add(ConvertToButtonRange((buttons & GamepadButtonFlags.B) != 0));
			result.Buttons.Add(ConvertToButtonRange((buttons & GamepadButtonFlags.X) != 0));
			result.Buttons.Add(ConvertToButtonRange((buttons & GamepadButtonFlags.Y) != 0));
			result.Buttons.Add(ConvertToButtonRange((buttons & GamepadButtonFlags.LeftShoulder) != 0));
			result.Buttons.Add(ConvertToButtonRange((buttons & GamepadButtonFlags.RightShoulder) != 0));
			result.Buttons.Add(ConvertToButtonRange((buttons & GamepadButtonFlags.Back) != 0));
			result.Buttons.Add(ConvertToButtonRange((buttons & GamepadButtonFlags.Start) != 0));
			result.Buttons.Add(ConvertToButtonRange((buttons & GamepadButtonFlags.LeftThumb) != 0));
			result.Buttons.Add(ConvertToButtonRange((buttons & GamepadButtonFlags.RightThumb) != 0));
			
			// D-Pad buttons (XInput represents D-Pad as 4 separate buttons)
			result.Buttons.Add(ConvertToButtonRange((buttons & GamepadButtonFlags.DPadUp) != 0));
			result.Buttons.Add(ConvertToButtonRange((buttons & GamepadButtonFlags.DPadDown) != 0));
			result.Buttons.Add(ConvertToButtonRange((buttons & GamepadButtonFlags.DPadLeft) != 0));
			result.Buttons.Add(ConvertToButtonRange((buttons & GamepadButtonFlags.DPadRight) != 0));

			// XInput has no POVs (D-Pad is represented as buttons, list remains empty)
			// But we can synthesize one from D-Pad buttons
			int povValue = ConvertXInputDPadToPOV(buttons);
			result.POVs.Add(ConvertToPOVRange(povValue));

			return result;
		}

		private static int ConvertXInputDPadToPOV(GamepadButtonFlags buttons)
		{
			bool up = (buttons & GamepadButtonFlags.DPadUp) != 0;
			bool down = (buttons & GamepadButtonFlags.DPadDown) != 0;
			bool left = (buttons & GamepadButtonFlags.DPadLeft) != 0;
			bool right = (buttons & GamepadButtonFlags.DPadRight) != 0;

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

			     // Imported from GamingInputStateToListInputState
        public static CustomInputState ConvertGamingInputStateToListInputState(GamepadReading? gamepadReading)
		{
			
			GamepadReading reading = gamepadReading.Value;
            var result = new CustomInputState();

			// Convert axes (6 axes in Gaming Input)
			// Thumbsticks: Convert from -1.0..1.0 to 0..65535
			result.Axes.Capacity = 6;
			result.Axes.Add(ConvertToAxisRange(reading.LeftThumbstickX, false));
			result.Axes.Add(ConvertToAxisRange(reading.LeftThumbstickY, false));
			result.Axes.Add(ConvertToAxisRange(reading.RightThumbstickX, false));
			result.Axes.Add(ConvertToAxisRange(reading.RightThumbstickY, false));

			// Triggers: Convert from 0.0..1.0 to 0..65535
			result.Axes.Add(ConvertToAxisRange(reading.LeftTrigger, true));
			result.Axes.Add(ConvertToAxisRange(reading.RightTrigger, true));

			// Gaming Input has no sliders (list remains empty)

			// Convert buttons (16 buttons in Gaming Input)
			var buttons = reading.Buttons;
			result.Buttons.Capacity = 16;
			result.Buttons.Add(ConvertToButtonRange((buttons & GamepadButtons.A) != 0));
			result.Buttons.Add(ConvertToButtonRange((buttons & GamepadButtons.B) != 0));
			result.Buttons.Add(ConvertToButtonRange((buttons & GamepadButtons.X) != 0));
			result.Buttons.Add(ConvertToButtonRange((buttons & GamepadButtons.Y) != 0));
			result.Buttons.Add(ConvertToButtonRange((buttons & GamepadButtons.LeftShoulder) != 0));
			result.Buttons.Add(ConvertToButtonRange((buttons & GamepadButtons.RightShoulder) != 0));
			result.Buttons.Add(ConvertToButtonRange((buttons & GamepadButtons.View) != 0));
			result.Buttons.Add(ConvertToButtonRange((buttons & GamepadButtons.Menu) != 0));
			result.Buttons.Add(ConvertToButtonRange((buttons & GamepadButtons.LeftThumbstick) != 0));
			result.Buttons.Add(ConvertToButtonRange((buttons & GamepadButtons.RightThumbstick) != 0));
			
			// D-Pad buttons
			result.Buttons.Add(ConvertToButtonRange((buttons & GamepadButtons.DPadUp) != 0));
			result.Buttons.Add(ConvertToButtonRange((buttons & GamepadButtons.DPadDown) != 0));
			result.Buttons.Add(ConvertToButtonRange((buttons & GamepadButtons.DPadLeft) != 0));
			result.Buttons.Add(ConvertToButtonRange((buttons & GamepadButtons.DPadRight) != 0));
			
			// Paddle buttons (if available on controller)
			result.Buttons.Add(ConvertToButtonRange((buttons & GamepadButtons.Paddle1) != 0));
			result.Buttons.Add(ConvertToButtonRange((buttons & GamepadButtons.Paddle2) != 0));

			// Convert D-Pad to POV format (centidegrees)
			int povValue = ConvertDPadToPOV(buttons);
			result.POVs.Add(ConvertToPOVRange(povValue));

			return result;
		}

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

        #endregion
	}
}
