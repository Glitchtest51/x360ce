using System;
using System.Collections.Generic;
using System.Diagnostics;
using SharpDX.DirectInput;
using x360ce.Engine;
using x360ce.Engine.Data;

namespace x360ce.App.Input.Processors
{
	/// <summary>
	/// Device information and capability management for Raw Input devices.
	/// This partial class handles HID device capability structures and metadata.
	/// </summary>
	public partial class RawInputProcessor
	{
		#region Device Capability Classes

		/// <summary>
		/// Represents parsed HID device capabilities.
		/// Contains actual device capabilities read from HID descriptor.
		/// </summary>
		internal class HidDeviceCapabilities
		{
			public int ButtonCount { get; set; }
			public int AxisCount { get; set; }
			public int PovCount { get; set; }
			public List<HidButtonInfo> Buttons { get; set; } = new List<HidButtonInfo>();
			public List<HidValueInfo> Values { get; set; } = new List<HidValueInfo>();
			public HIDP_CAPS RawCaps { get; set; }
			public IntPtr PreparsedData { get; set; }
		}

		/// <summary>
		/// Information about a HID button capability.
		/// Maps HID usage information to CustomDeviceState button indices.
		/// </summary>
		internal class HidButtonInfo
		{
			public ushort UsagePage { get; set; }
			public ushort Usage { get; set; }
			public int CustomDeviceStateIndex { get; set; }
			public string Name { get; set; }
			public bool IsRange { get; set; }
			public ushort UsageMin { get; set; }
			public ushort UsageMax { get; set; }
		}

		/// <summary>
		/// Information about a HID value capability (axes, triggers, POVs).
		/// Maps HID usage information to CustomDeviceState axis indices.
		/// </summary>
		internal class HidValueInfo
		{
			public ushort UsagePage { get; set; }
			public ushort Usage { get; set; }
			public int LogicalMin { get; set; }
			public int LogicalMax { get; set; }
			public int PhysicalMin { get; set; }
			public int PhysicalMax { get; set; }
			public int CustomDeviceStateIndex { get; set; }
			public string Name { get; set; }
			public HidValueType Type { get; set; }
			public bool IsRange { get; set; }
			public ushort UsageMin { get; set; }
			public ushort UsageMax { get; set; }
		}

		/// <summary>
		/// Type of HID value for proper semantic interpretation.
		/// </summary>
		internal enum HidValueType
		{
			Unknown,
			Axis,        // Linear axis (X, Y, Z, Rx, Ry, Rz)
			Trigger,     // Trigger/slider (typically 0-max range)
			Pov,         // Point of view/hat switch
			Wheel,       // Wheel/dial
			Throttle,    // Throttle control
			Rudder       // Rudder control
		}

		#endregion

		#region DeviceObjects Creation

		/// <summary>
		/// Creates DeviceObjects based on actual HID capabilities.
		/// Replaces hardcoded DeviceObject creation with real HID-based information.
		/// </summary>
		/// <param name="device">UserDevice to create objects for</param>
		/// <param name="hidCapabilities">Parsed HID capabilities (can be null)</param>
		private static void CreateDeviceObjectsFromHid(UserDevice device, HidDeviceCapabilities hidCapabilities)
		{
			if (device.DeviceObjects != null)
				return; // Already created

			var deviceObjects = new List<DeviceObjectItem>();

			// If HID capabilities are available, use them
			if (hidCapabilities != null && hidCapabilities.Buttons != null)
			{
				// Add button objects based on actual HID button capabilities
				foreach (var buttonInfo in hidCapabilities.Buttons)
				{
					if (buttonInfo.IsRange)
					{
						// Handle button range
						for (ushort usage = buttonInfo.UsageMin; usage <= buttonInfo.UsageMax; usage++)
						{
							int buttonIndex = deviceObjects.Count;
							deviceObjects.Add(new DeviceObjectItem(
								buttonIndex * 4, // offset
								ObjectGuid.Button, // guid
								ObjectAspect.Position, // aspect
								DeviceObjectTypeFlags.PushButton, // type
								buttonIndex, // instance
								GetButtonName(usage, buttonInfo.UsagePage) // HID-based name
							));
						}
					}
					else
					{
						// Handle single button
						int buttonIndex = deviceObjects.Count;
						deviceObjects.Add(new DeviceObjectItem(
							buttonIndex * 4, // offset
							ObjectGuid.Button, // guid
							ObjectAspect.Position, // aspect
							DeviceObjectTypeFlags.PushButton, // type
							buttonIndex, // instance
							GetButtonName(buttonInfo.Usage, buttonInfo.UsagePage) // HID-based name
						));
					}
				}

				// Add axis objects based on actual HID value capabilities
				if (hidCapabilities.Values != null)
				{
					foreach (var valueInfo in hidCapabilities.Values)
					{
						if (valueInfo.Type == HidValueType.Axis || valueInfo.Type == HidValueType.Trigger)
						{
							deviceObjects.Add(new DeviceObjectItem(
								64 + (valueInfo.CustomDeviceStateIndex * 4), // offset
								GetAxisGuidFromUsage(valueInfo.Usage), // appropriate axis GUID
								ObjectAspect.Position, // aspect
								DeviceObjectTypeFlags.AbsoluteAxis, // type
								valueInfo.CustomDeviceStateIndex, // instance
								valueInfo.Name // HID-based name
							));
						}
						else if (valueInfo.Type == HidValueType.Pov)
						{
							deviceObjects.Add(new DeviceObjectItem(
								128 + (valueInfo.CustomDeviceStateIndex * 4), // offset
								ObjectGuid.PovController, // guid
								ObjectAspect.Position, // aspect
								DeviceObjectTypeFlags.PointOfViewController, // type
								valueInfo.CustomDeviceStateIndex, // instance
								valueInfo.Name // HID-based name
							));
						}
					}
				}
			}
			else
			{
				// Fallback: Create DeviceObjects based on device capability counts
				CreateFallbackDeviceObjects(device, deviceObjects);
			}

			device.DeviceObjects = deviceObjects.ToArray();

			Debug.WriteLine($"Raw Input: Created {deviceObjects.Count} DeviceObjects for {device.DisplayName}");
		}

		/// <summary>
		/// Creates fallback DeviceObjects when HID parsing is not available.
		/// </summary>
		/// <param name="device">UserDevice to create objects for</param>
		/// <param name="deviceObjects">List to add objects to</param>
		private static void CreateFallbackDeviceObjects(UserDevice device, List<DeviceObjectItem> deviceObjects)
		{
			// Add button objects based on capability count
			for (int i = 0; i < device.CapButtonCount; i++)
			{
				deviceObjects.Add(new DeviceObjectItem(
					i * 4, // offset
					ObjectGuid.Button, // guid
					ObjectAspect.Position, // aspect
					DeviceObjectTypeFlags.PushButton, // type
					i, // instance
					GetButtonName((ushort)(i + 1), HID_USAGE_PAGE_BUTTON) // fallback name
				));
			}

			// Add axis objects based on capability count
			for (int i = 0; i < device.CapAxeCount; i++)
			{
				deviceObjects.Add(new DeviceObjectItem(
					64 + (i * 4), // offset
					GetFallbackAxisGuid(i), // appropriate axis GUID
					ObjectAspect.Position, // aspect
					DeviceObjectTypeFlags.AbsoluteAxis, // type
					i, // instance
					GetFallbackAxisName(i, device.IsXboxCompatible) // device-appropriate name
				));
			}

			// Add POV objects based on capability count
			for (int i = 0; i < device.CapPovCount; i++)
			{
				deviceObjects.Add(new DeviceObjectItem(
					128 + (i * 4), // offset
					ObjectGuid.PovController, // guid
					ObjectAspect.Position, // aspect
					DeviceObjectTypeFlags.PointOfViewController, // type
					i, // instance
					$"POV {i}" // name
				));
			}
		}

		/// <summary>
		/// Gets appropriate axis GUID for fallback creation.
		/// </summary>
		private static Guid GetFallbackAxisGuid(int axisIndex)
		{
			switch (axisIndex)
			{
				case 0: return ObjectGuid.XAxis;
				case 1: return ObjectGuid.YAxis;
				case 2: return ObjectGuid.ZAxis;
				case 3: return ObjectGuid.RzAxis;
				case 4: return ObjectGuid.Slider; // Left Trigger
				case 5: return ObjectGuid.Slider; // Right Trigger
				default: return ObjectGuid.XAxis; // Fallback
			}
		}

		/// <summary>
		/// Gets appropriate axis name for fallback creation.
		/// </summary>
		private static string GetFallbackAxisName(int axisIndex, bool isXboxController)
		{
			if (isXboxController)
			{
				string[] xboxAxisNames = { "Left Stick X", "Left Stick Y", "Right Stick X", "Right Stick Y", "Left Trigger", "Right Trigger" };
				return axisIndex < xboxAxisNames.Length ? xboxAxisNames[axisIndex] : $"Axis {axisIndex}";
			}
			else
			{
				string[] genericAxisNames = { "X Axis", "Y Axis", "Z Axis", "RZ Axis", "Throttle", "Rudder" };
				return axisIndex < genericAxisNames.Length ? genericAxisNames[axisIndex] : $"Axis {axisIndex}";
			}
		}

		/// <summary>
		/// Gets appropriate button name based on HID usage information.
		/// </summary>
		/// <param name="usage">HID usage ID</param>
		/// <param name="usagePage">HID usage page</param>
		/// <returns>Descriptive button name</returns>
		private static string GetButtonName(ushort usage, ushort usagePage)
		{
			if (usagePage == HID_USAGE_PAGE_BUTTON)
			{
				return $"Button {usage}";
			}
			else if (usagePage == HID_USAGE_PAGE_GENERIC_DESKTOP)
			{
				switch (usage)
				{
					case HID_USAGE_GENERIC_SELECT: return "Select";
					case HID_USAGE_GENERIC_START: return "Start";
					default: return $"Desktop Button {usage}";
				}
			}
			else if (usagePage == HID_USAGE_PAGE_GAME)
			{
				switch (usage)
				{
					case HID_USAGE_GAME_NEW_GAME: return "New Game";
					case HID_USAGE_GAME_SHOOT_BALL: return "Shoot";
					case HID_USAGE_GAME_FLIPPER: return "Flipper";
					case HID_USAGE_GAME_SECONDARY_FLIPPER: return "Secondary Flipper";
					default: return $"Game Button {usage}";
				}
			}

			return $"Button {usagePage:X2}:{usage:X2}";
		}

		/// <summary>
		/// Gets appropriate axis name based on HID usage information.
		/// </summary>
		/// <param name="usage">HID usage ID</param>
		/// <param name="usagePage">HID usage page</param>
		/// <returns>Descriptive axis name</returns>
		private static string GetAxisName(ushort usage, ushort usagePage)
		{
			if (usagePage == HID_USAGE_PAGE_GENERIC_DESKTOP)
			{
				switch (usage)
				{
					case HID_USAGE_GENERIC_X: return "X Axis";
					case HID_USAGE_GENERIC_Y: return "Y Axis";
					case HID_USAGE_GENERIC_Z: return "Z Axis";
					case HID_USAGE_GENERIC_RX: return "X Rotation";
					case HID_USAGE_GENERIC_RY: return "Y Rotation";
					case HID_USAGE_GENERIC_RZ: return "Z Rotation";
					case HID_USAGE_GENERIC_SLIDER: return "Slider";
					case HID_USAGE_GENERIC_DIAL: return "Dial";
					case HID_USAGE_GENERIC_WHEEL: return "Wheel";
					case HID_USAGE_GENERIC_HATSWITCH: return "Hat Switch";
					default: return $"Desktop Axis {usage:X2}";
				}
			}
			else if (usagePage == HID_USAGE_PAGE_GAME)
			{
				switch (usage)
				{
					case HID_USAGE_GAME_POINT_OF_VIEW: return "Point of View";
					case HID_USAGE_GAME_TURN_RIGHT_LEFT: return "Turn";
					case HID_USAGE_GAME_PITCH_FORWARD_BACKWARD: return "Pitch";
					case HID_USAGE_GAME_ROLL_RIGHT_LEFT: return "Roll";
					case HID_USAGE_GAME_MOVE_RIGHT_LEFT: return "Move Horizontal";
					case HID_USAGE_GAME_MOVE_FORWARD_BACKWARD: return "Move Forward/Back";
					case HID_USAGE_GAME_MOVE_UP_DOWN: return "Move Up/Down";
					default: return $"Game Axis {usage:X2}";
				}
			}

			return $"Axis {usagePage:X2}:{usage:X2}";
		}

		/// <summary>
		/// Gets appropriate axis GUID based on HID usage.
		/// </summary>
		/// <param name="usage">HID usage ID</param>
		/// <returns>DirectInput ObjectGuid</returns>
		private static Guid GetAxisGuidFromUsage(ushort usage)
		{
			switch (usage)
			{
				case HID_USAGE_GENERIC_X: return ObjectGuid.XAxis;
				case HID_USAGE_GENERIC_Y: return ObjectGuid.YAxis;
				case HID_USAGE_GENERIC_Z: return ObjectGuid.ZAxis;
				case HID_USAGE_GENERIC_RX: return ObjectGuid.RxAxis;
				case HID_USAGE_GENERIC_RY: return ObjectGuid.RyAxis;
				case HID_USAGE_GENERIC_RZ: return ObjectGuid.RzAxis;
				case HID_USAGE_GENERIC_SLIDER: return ObjectGuid.Slider;
				case HID_USAGE_GENERIC_DIAL: return ObjectGuid.Slider;
				case HID_USAGE_GENERIC_WHEEL: return ObjectGuid.Slider;
				default: return ObjectGuid.XAxis; // Fallback
			}
		}

		/// <summary>
		/// Determines the semantic type of a HID value based on usage information.
		/// </summary>
		/// <param name="usage">HID usage ID</param>
		/// <param name="usagePage">HID usage page</param>
		/// <returns>Semantic value type</returns>
		private static HidValueType GetValueType(ushort usage, ushort usagePage)
		{
			if (usagePage == HID_USAGE_PAGE_GENERIC_DESKTOP)
			{
				switch (usage)
				{
					case HID_USAGE_GENERIC_X:
					case HID_USAGE_GENERIC_Y:
					case HID_USAGE_GENERIC_Z:
					case HID_USAGE_GENERIC_RX:
					case HID_USAGE_GENERIC_RY:
					case HID_USAGE_GENERIC_RZ:
						return HidValueType.Axis;

					case HID_USAGE_GENERIC_SLIDER:
						return HidValueType.Trigger; // Sliders are often triggers

					case HID_USAGE_GENERIC_WHEEL:
						return HidValueType.Wheel;

					case HID_USAGE_GENERIC_HATSWITCH:
						return HidValueType.Pov;

					default:
						return HidValueType.Unknown;
				}
			}
			else if (usagePage == HID_USAGE_PAGE_GAME)
			{
				switch (usage)
				{
					case HID_USAGE_GAME_POINT_OF_VIEW:
						return HidValueType.Pov;

					case HID_USAGE_GAME_TURN_RIGHT_LEFT:
					case HID_USAGE_GAME_PITCH_FORWARD_BACKWARD:
					case HID_USAGE_GAME_ROLL_RIGHT_LEFT:
					case HID_USAGE_GAME_MOVE_RIGHT_LEFT:
					case HID_USAGE_GAME_MOVE_FORWARD_BACKWARD:
					case HID_USAGE_GAME_MOVE_UP_DOWN:
						return HidValueType.Axis;

					default:
						return HidValueType.Unknown;
				}
			}

			return HidValueType.Unknown;
		}

		/// <summary>
		/// Maps HID usage to CustomDeviceState axis index.
		/// Follows standard controller axis mapping conventions.
		/// </summary>
		/// <param name="usage">HID usage ID</param>
		/// <param name="usagePage">HID usage page</param>
		/// <param name="currentAxisIndex">Current axis index counter</param>
		/// <returns>CustomDeviceState axis index</returns>
		private static int MapUsageToAxisIndex(ushort usage, ushort usagePage, ref int currentAxisIndex)
		{
			if (usagePage == HID_USAGE_PAGE_GENERIC_DESKTOP)
			{
				switch (usage)
				{
					case HID_USAGE_GENERIC_X: return 0;  // Left stick X
					case HID_USAGE_GENERIC_Y: return 1;  // Left stick Y
					case HID_USAGE_GENERIC_Z: return 2;  // Right stick X (or Z axis)
					case HID_USAGE_GENERIC_RZ: return 3; // Right stick Y (or RZ axis)
					case HID_USAGE_GENERIC_RX: return 4; // May be right stick X on some controllers
					case HID_USAGE_GENERIC_RY: return 5; // May be right stick Y on some controllers
					case HID_USAGE_GENERIC_SLIDER: 
						// Sliders are often triggers - assign to next available axis
						return Math.Min(currentAxisIndex++, 5);
					default:
						return Math.Min(currentAxisIndex++, 5);
				}
			}

			// For other usage pages, assign sequentially
			return Math.Min(currentAxisIndex++, 5);
		}

		#endregion
	}
}
