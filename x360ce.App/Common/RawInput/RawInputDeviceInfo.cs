using System;
using x360ce.Engine;

namespace x360ce.App.RawInput
{
	/// <summary>
	/// Represents information about a Raw Input device.
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
	}
}
