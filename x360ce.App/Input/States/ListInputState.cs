using System.Collections.Generic;

namespace x360ce.App.Input.States
{
    /// <summary>
    /// Standardized state representation for all input methods (RawInput, DirectInput, XInput, GamingInput).
    /// Provides a unified format: ((axes), (sliders), (buttons), (povs)).
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
    public class ListInputState
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
		public ListInputState()
		{
			Axes = new List<int>();
			Sliders = new List<int>();
			Buttons = new List<int>();
			POVs = new List<int>();
		}

		/// <summary>
		/// Returns a string representation of the state in the format:
		/// ((axes), (sliders), (buttons), (povs))
		/// Empty collections are shown as (), not null.
		/// </summary>
		public override string ToString()
		{
			string axesStr = Axes != null && Axes.Count > 0 ? $"({string.Join(",", Axes)})" : "()";
			string slidersStr = Sliders != null && Sliders.Count > 0 ? $"({string.Join(",", Sliders)})" : "()";
			string buttonsStr = Buttons != null && Buttons.Count > 0 ? $"({string.Join(",", Buttons)})" : "()";
			string povsStr = POVs != null && POVs.Count > 0 ? $"({string.Join(",", POVs)})" : "()";
			
			return $"({axesStr},{slidersStr},{buttonsStr},{povsStr})";
		}
	}
}
