using SharpDX.DirectInput;
using System;
using System.Collections.Generic;
using System.Linq;
using x360ce.Engine;
using x360ce.Engine.Data;

namespace x360ce.App.Input.Processors
{
	public partial class DirectInputProcessor : IInputProcessor
	{
		/// <summary>
		/// Calculates detailed axis masks using CustomDeviceHelper offsets.
		/// </summary>
		/// <param name="ud">UserDevice to update</param>
		private void CalculateAxisMasks(UserDevice ud)
		{
			if (ud.DeviceObjects == null || ud.DirectInputDevice == null)
				return;

			// Handle different device types
			switch (ud.DirectInputDevice)
			{
				case Mouse mouseDevice:
					CalculateMouseAxisMask(ud.DeviceObjects, mouseDevice, ud);
					break;
				case Joystick joystickDevice:
					CalculateJoystickAxisMask(ud.DeviceObjects, joystickDevice, ud);
					CalculateJoystickSlidersMask(ud.DeviceObjects, joystickDevice, ud);
					break;
			}
		}

		/// <summary>
		/// Calculates mouse axis mask (moved from CustomDeviceState.GetMouseAxisMask).
		/// </summary>
		private void CalculateMouseAxisMask(DeviceObjectItem[] items, Mouse device, UserDevice ud)
		{
			// Must have same order as in Axis[] property.
			// Important: These values are not the same as on DeviceObjectInstance.Offset.
			var list = new List<MouseOffset>{
				MouseOffset.X,
				MouseOffset.Y,
				MouseOffset.Z,
			};

			int axisMask = 0;
			for (int i = 0; i < list.Count; i++)
			{
				try
				{
					// This function accepts JoystickOffset enumeration values.
					// Important: These values are not the same as on DeviceObjectInstance.Offset.
					var o = device.GetObjectInfoByOffset((int)list[i]);
					if (o != null)
					{
						// Now we can find same object by raw offset (DeviceObjectInstance.Offset).
						var item = items.First(x => x.Offset == o.Offset);
						item.DiIndex = i;
						axisMask |= (int)Math.Pow(2, i);
					}
				}
				catch { }
			}

			// Update UserDevice with calculated mask
			ud.DiAxeMask = axisMask;
		}

		/// <summary>
		/// Calculates joystick axis mask (moved from CustomDeviceState.GetJoystickAxisMask).
		/// </summary>
		private void CalculateJoystickAxisMask(DeviceObjectItem[] items, Joystick device, UserDevice ud)
		{
			int axisMask = 0;
			int actuatorMask = 0;
			int actuatorCount = 0;

			for (int i = 0; i < CustomDeviceHelper.AxisOffsets.Count; i++)
			{
				try
				{
					// This function accepts JoystickOffset enumeration values.
					// Important: These values are not the same as on DeviceObjectInstance.Offset.
					var o = device.GetObjectInfoByOffset((int)CustomDeviceHelper.AxisOffsets[i]);
					if (o != null)
					{
						// Now we can find same object by raw offset (DeviceObjectInstance.Offset).
						var item = items.First(x => x.Offset == o.Offset);
						item.DiIndex = i;
						axisMask |= (int)Math.Pow(2, i);
						// Create mask to know which axis have force feedback motor.
						if (item.Flags.HasFlag(DeviceObjectTypeFlags.ForceFeedbackActuator))
						{
							actuatorMask |= (int)Math.Pow(2, i);
							actuatorCount += 1;
						}
					}
				}
				catch
				{
					// Ignore exceptions from GetObjectInfoByOffset(int offset) method.
				}
			}

			// Update UserDevice with calculated masks
			ud.DiAxeMask = axisMask;
			ud.DiActuatorMask = actuatorMask;
			ud.DiActuatorCount = actuatorCount;
		}

		/// <summary>
		/// Calculates joystick sliders mask (moved from CustomDeviceState.GetJoystickSlidersMask).
		/// </summary>
		private void CalculateJoystickSlidersMask(DeviceObjectItem[] items, Joystick device, UserDevice ud)
		{
			int slidersMask = 0;
			for (int i = 0; i < CustomDeviceHelper.SliderOffsets.Count; i++)
			{
				try
				{
					// This function accepts JoystickOffset enumeration values.
					// Important: These values are not the same as on DeviceObjectInstance.Offset.
					var o = device.GetObjectInfoByOffset((int)CustomDeviceHelper.SliderOffsets[i]);
					if (o != null)
					{
						// Now we can find same object by raw offset (DeviceObjectInstance.Offset).
						var item = items.First(x => x.Offset == o.Offset);
						item.DiIndex = i;
						slidersMask |= (int)Math.Pow(2, i);
					}
				}
				catch { }
			}

			// Note: UserDevice doesn't currently have a DiSlidersMask property,
			// but the calculation is preserved for future use
		}

	}
}
