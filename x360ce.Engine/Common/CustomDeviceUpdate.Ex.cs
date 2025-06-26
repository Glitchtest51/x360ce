using SharpDX.DirectInput;

namespace x360ce.Engine
{
	/// <summary>
	///  Custom X360CE direct input update class used for configuration.
	/// </summary>
	public partial class CustomDeviceUpdate
	{

		public CustomDeviceUpdate(MouseUpdate update)
		{
			Value = update.Value;
			Index = CustomDeviceHelper.MouseAxisOffsets.IndexOf(update.Offset);
			if (Index > -1)
			{
				Type = MapType.Axis;
				return;
			}
			Type = MapType.Button;
		}

		public CustomDeviceUpdate(KeyboardUpdate update)
		{
			Value = update.Value;
			Index = (int)update.Key;
			Value = update.IsPressed ? 1 : 0;
			Type = MapType.Button;
		}

		public CustomDeviceUpdate(JoystickUpdate update)
		{
			Value = update.Value;
			Index = CustomDeviceHelper.AxisOffsets.IndexOf(update.Offset);
			if (Index > -1)
			{
				Type = MapType.Axis;
				return;
			}
			Index = CustomDeviceHelper.SliderOffsets.IndexOf(update.Offset);
			if (Index > -1)
			{
				Type = MapType.Slider;
				return;
			}
			Index = CustomDeviceHelper.POVOffsets.IndexOf(update.Offset);
			if (Index > -1)
			{
				Type = MapType.POV;
				return;
			}
			Index = CustomDeviceHelper.ButtonOffsets.IndexOf(update.Offset);
			if (Index > -1)
			{
				Type = MapType.Button;
				return;
			}
		}

	}
}
