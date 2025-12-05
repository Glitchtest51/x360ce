using System;
using x360ce.App.Input.States;

namespace x360ce.App.Input.Devices
{
	/// <summary>
	/// Base class defining common properties for all input devices (DirectInput, XInput, etc.).
	/// Used to abstract device details across different APIs.
	/// </summary>
	public abstract class InputDeviceInfo
	{
		/// <summary>Unique instance identifier (InstanceGuid for DirectInput, etc.).</summary>
		public Guid InstanceGuid { get; set; }

		/// <summary>Human-readable product name.</summary>
		public string ProductName { get; set; }

		/// <summary>Human-readable instance name.</summary>
		public string InstanceName { get; set; }

		/// <summary>Product identifier.</summary>
		public Guid ProductGuid { get; set; }

		/// <summary>Device type identifier.</summary>
		public int DeviceType { get; set; }

		/// <summary>Device subtype identifier.</summary>
		public int DeviceSubtype { get; set; }

		/// <summary>Type of input API (DirectInput, XInput, etc.).</summary>
		public string InputType { get; set; }

		/// <summary>Human-readable device type name.</summary>
		public string DeviceTypeName { get; set; }

		/// <summary>Common identifier used to match devices across APIs.</summary>
		public string CommonIdentifier { get; set; }

		/// <summary>System path to the device interface.</summary>
		public string InterfacePath { get; set; }

		/// <summary>HID Usage.</summary>
		public int Usage { get; set; }

		/// <summary>HID Usage Page.</summary>
		public int UsagePage { get; set; }

		/// <summary>Number of axes available.</summary>
		public int AxeCount { get; set; }

		/// <summary>Number of sliders available.</summary>
		public int SliderCount { get; set; }

		/// <summary>Number of buttons available.</summary>
		public int ButtonCount { get; set; }

		/// <summary>Number of POV hats available.</summary>
		public int PovCount { get; set; }

		/// <summary>Indicates if the device supports force feedback.</summary>
		public bool HasForceFeedback { get; set; }

		/// <summary>Vendor identifier.</summary>
		public int VendorId { get; set; }

		/// <summary>Product identifier.</summary>
		public int ProductId { get; set; }

		/// <summary>Driver version.</summary>
		public int DriverVersion { get; set; }

		/// <summary>Hardware revision.</summary>
		public int HardwareRevision { get; set; }

		/// <summary>Firmware revision.</summary>
		public int FirmwareRevision { get; set; }

		/// <summary>Indicates if the device is currently connected/online.</summary>
		public bool IsOnline { get; set; }

		/// <summary>Device class GUID.</summary>
		public Guid ClassGuid { get; set; }

		/// <summary>Hardware IDs string.</summary>
		public string HardwareIds { get; set; }

		/// <summary>Device identifier.</summary>
		public string DeviceId { get; set; }

		/// <summary>Parent device identifier.</summary>
		public string ParentDeviceId { get; set; }

		/// <summary>Indicates if the device is enabled in x360ce.</summary>
		public bool IsEnabled { get; set; }

		/// <summary>Assigned to Controller 1.</summary>
		public bool AssignedToPad1 { get; set; }

		/// <summary>Assigned to Controller 2.</summary>
		public bool AssignedToPad2 { get; set; }

		/// <summary>Assigned to Controller 3.</summary>
		public bool AssignedToPad3 { get; set; }

		/// <summary>Assigned to Controller 4.</summary>
		public bool AssignedToPad4 { get; set; }

		/// <summary>Current input state normalized to custom format.</summary>
		public CustomInputState CustomInputState { get; set; }

		/// <summary>
		/// VID/PID string in standard format for hardware identification.
		/// </summary>
		public virtual string VidPidString => $"VID_{VendorId:X4}&PID_{ProductId:X4}";

		/// <summary>
		/// Determines if a device is a virtual/converted device that should be excluded.
		/// Checks for "ConvertedDevice" text in InterfacePath or DeviceId/DeviceInstanceId.
		/// </summary>
		/// <returns>True if device is a virtual/converted device</returns>
		public virtual bool IsVirtualConvertedDevice()
		{
			// Check InterfacePath for "ConvertedDevice" marker
			if (!string.IsNullOrEmpty(InterfacePath) &&
				InterfacePath.IndexOf("ConvertedDevice", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return true;
			}

			// Check DeviceId (which usually stores DeviceInstanceId for PnP devices) for "ConvertedDevice" marker
			if (!string.IsNullOrEmpty(DeviceId) &&
				DeviceId.IndexOf("ConvertedDevice", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return true;
			}

			return false;
		}

		/// <summary>
		/// Extracts a hexadecimal value following a specific pattern in a string.
		/// Handles both numeric hex values (e.g., "046A") and alphanumeric vendor codes (e.g., "INT").
		/// </summary>
		/// <param name="text">Text to search in</param>
		/// <param name="pattern">Pattern to search for (e.g., "VID_", "VEN_")</param>
		/// <param name="length">Expected length of hex value</param>
		/// <returns>Parsed integer value or null if not found</returns>
		public static int? ExtractHexValue(string text, string pattern, int length)
		{
			var index = text.IndexOf(pattern, StringComparison.Ordinal);
			if (index < 0)
				return null;

			var start = index + pattern.Length;
			if (start >= text.Length)
				return null;

			// Extract characters that could be hex digits or alphanumeric vendor codes
			var end = start;
			var maxEnd = Math.Min(start + length, text.Length);

			while (end < maxEnd)
			{
				var ch = text[end];
				// Accept hex digits (0-9, A-F) and letters (for vendor codes like "INT")
				if ((ch >= '0' && ch <= '9') || (ch >= 'A' && ch <= 'Z'))
					end++;
				else
					break;
			}

			if (end <= start)
				return null;

			var hexStr = text.Substring(start, end - start);

			// Try to parse as hexadecimal number
			if (int.TryParse(hexStr, System.Globalization.NumberStyles.HexNumber, null, out int value))
				return value;

			// If parsing fails but we have a valid string (like "INT"),
			// treat each character as a hex digit to create a unique identifier
			// This ensures vendor codes like "INT" get converted to a numeric value
			if (hexStr.Length > 0 && System.Linq.Enumerable.All(hexStr, c => (c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z')))
			{
				// For vendor codes, use ASCII-based conversion to create unique numeric ID
				// This ensures "INT" becomes a valid numeric identifier
				int vendorCode = 0;
				for (int i = 0; i < Math.Min(hexStr.Length, 4); i++)
				{
					vendorCode = (vendorCode << 8) | (byte)hexStr[i];
				}
				return vendorCode;
			}

			return null;
		}
	}
}