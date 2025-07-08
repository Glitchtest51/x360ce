using System;
using x360ce.Engine;

namespace x360ce.App.Input.Processors
{
	/// <summary>
	/// Represents information about a Raw Input device.
	/// Enhanced to include parsed HID capabilities for proper state reading.
	/// </summary>
	internal class RawInputDeviceInfo
	{
		public IntPtr Handle { get; set; }
		public uint VendorId { get; set; }
		public uint ProductId { get; set; }
		public ushort UsagePage { get; set; }
		public ushort Usage { get; set; }
		public bool IsXboxController { get; set; }
		public CustomDeviceState LastState { get; set; }
		
		/// <summary>
		/// Parsed HID capabilities for this device.
		/// Used for proper HID API-based state reading.
		/// </summary>
		public RawInputProcessor.HidDeviceCapabilities HidCapabilities { get; set; }
	}
}
