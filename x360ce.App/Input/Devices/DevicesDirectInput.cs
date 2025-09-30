using SharpDX.DirectInput;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace x360ce.App.Input.Devices
{
	/// <summary>
	/// DirectInput device container with both device information and the actual device object.
	/// Contains comprehensive device metadata plus the live DirectInput device for input reading.
	/// </summary>
	public class DirectInputDeviceInfo : IDisposable
	{
		public Guid InstanceGuid { get; set; }
		public string InstanceName { get; set; }
		public Guid ProductGuid { get; set; }
		public string ProductName { get; set; }
		public DeviceType DeviceType { get; set; }
		public int DeviceSubtype { get; set; }
		public int Usage { get; set; }
		public int UsagePage { get; set; }
		public int AxeCount { get; set; }
		public int ButtonCount { get; set; }
		public int PovCount { get; set; }
		public bool HasForceFeedback { get; set; }
		public int DriverVersion { get; set; }
		public int HardwareRevision { get; set; }
		public int FirmwareRevision { get; set; }
		public bool IsOnline { get; set; }
		public string DeviceTypeName { get; set; }
		public string InterfacePath { get; set; }
		
		// Common identifier for grouping devices from same physical hardware
		public string CommonIdentifier { get; set; }

		// Additional identification properties
		public int VendorId { get; set; }
		public int ProductId { get; set; }
		public Guid ClassGuid { get; set; }
		public string HardwareIds { get; set; }
		public string DeviceId { get; set; }
		public string ParentDeviceId { get; set; }

		/// <summary>
		/// The actual DirectInput device object for reading input.
		/// Can be Mouse, Keyboard, or Joystick depending on device type.
		/// </summary>
		public Device DirectInputDevice { get; set; }

		/// <summary>
		/// Display name combining instance ID and name for easy identification.
		/// </summary>
		public string DisplayName => $"{InstanceGuid.ToString().Substring(0, 8)} - {InstanceName}";

		/// <summary>
		/// VID/PID string in standard format for hardware identification.
		/// </summary>
		public string VidPidString => $"VID_{VendorId:X4}&PID_{ProductId:X4}";

		/// <summary>
		/// Dispose the DirectInput device when no longer needed.
		/// </summary>
		public void Dispose()
		{
			DirectInputDevice?.Dispose();
			DirectInputDevice = null;
		}
	}

	/// <summary>
	/// DirectInput device enumeration and management class.
	/// Self-contained implementation with minimal external dependencies.
	/// Provides functionality to discover and list DirectInput devices including gamepads, keyboards, and mice.
	/// Returns live DirectInput device objects that can be used for input reading.
	/// </summary>
	internal class DevicesDirectInput
	{
		/// <summary>
		/// Creates a public list of DirectInput devices (gamepads, keyboards, mice) with live device objects and logs their properties.
		/// This method enumerates all available DirectInput devices and outputs detailed information for debugging.
		/// </summary>
		/// <returns>List of DirectInputDeviceInfo objects containing both device information and live DirectInput device objects</returns>
		/// <remarks>
		/// This method performs comprehensive DirectInput device enumeration:
		/// • Discovers all DirectInput-compatible devices (gamepads, keyboards, mice)
		/// • Creates DirectInputDeviceInfo objects with device information AND live DirectInput device objects
		/// • Logs detailed device properties using Debug.WriteLine for diagnostics
		/// • Filters devices by type and availability
		/// • Provides device capability information where available
		/// • Keeps DirectInput devices alive for immediate input reading
		/// • Is self-contained with minimal external dependencies
		///
		/// IMPORTANT: The returned DirectInputDeviceInfo objects contain live DirectInput devices.
		/// Call Dispose() on each DirectInputDeviceInfo when no longer needed to free resources.
		/// </remarks>
		public List<DirectInputDeviceInfo> GetDirectInputDeviceList()
		{
			var stopwatch = Stopwatch.StartNew();
			var deviceList = new List<DirectInputDeviceInfo>();
			var debugLines = new List<string>();
			int deviceIndex = 0;

			try
			{
				Debug.WriteLine("\n-----------------------------------------------------------------------------------------------------------------\n\n" +
					"DeviceDirectInput: Starting DirectInput device enumeration...");

				using (var directInput = new DirectInput())
				{
					// Enumerate and filter to input devices only (early filtering for performance)
					var inputDevices = directInput.GetDevices(DeviceClass.All, DeviceEnumerationFlags.AllDevices)
						.Where(d => IsInputDevice(d.Type))
						.ToList();

					Debug.WriteLine($"DeviceDirectInput: Found {inputDevices.Count} input devices");

					foreach (var deviceInstance in inputDevices)
					{
						var deviceInfo = ProcessDevice(directInput, deviceInstance, ref deviceIndex, debugLines);
						if (deviceInfo != null)
							deviceList.Add(deviceInfo);
					}
				}

				// Generate summary statistics
				stopwatch.Stop();
				LogSummary(deviceList, stopwatch, debugLines);
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"DeviceDirectInput: Fatal error during device enumeration: {ex.Message}");
				Debug.WriteLine($"DeviceDirectInput: Stack trace: {ex.StackTrace}");
			}

			foreach (var line in debugLines)
				Debug.WriteLine(line);

			return deviceList;
		}

		/// <summary>
		/// Processes a single DirectInput device instance and creates a DirectInputDeviceInfo object.
		/// </summary>
		private DirectInputDeviceInfo ProcessDevice(DirectInput directInput, DeviceInstance deviceInstance, ref int deviceIndex, List<string> debugLines)
		{
			try
			{
				// Create device info with basic properties
				var deviceInfo = CreateDeviceInfo(deviceInstance);

				// Create DirectInput device object
				var device = CreateDirectInputDevice(directInput, deviceInstance);
				if (device == null)
				{
					deviceInfo.IsOnline = false;
					return deviceInfo;
				}

				// Populate capabilities and properties
				PopulateDeviceCapabilities(device, deviceInfo);

				// Extract hardware identification for joystick devices
				if (device is Joystick joystick)
					ExtractJoystickProperties(joystick, deviceInfo);

				deviceInfo.DirectInputDevice = device;
				deviceInfo.IsOnline = true;

				// Log device information
				deviceIndex++;
				LogDeviceInfo(deviceInfo, deviceIndex, debugLines);

				return deviceInfo;
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"DeviceDirectInput: Error processing device {deviceInstance.InstanceName}: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Creates a DirectInputDeviceInfo object with basic device instance properties.
		/// </summary>
		private DirectInputDeviceInfo CreateDeviceInfo(DeviceInstance deviceInstance)
		{
			return new DirectInputDeviceInfo
			{
				InstanceGuid = deviceInstance.InstanceGuid,
				InstanceName = deviceInstance.InstanceName,
				ProductGuid = deviceInstance.ProductGuid,
				ProductName = deviceInstance.ProductName,
				DeviceType = deviceInstance.Type,
				DeviceSubtype = deviceInstance.Subtype,
				Usage = (int)deviceInstance.Usage,
				UsagePage = (int)deviceInstance.UsagePage,
				DeviceTypeName = GetDeviceTypeName(deviceInstance.Type),
				HardwareIds = "",
				ParentDeviceId = "",
				InterfacePath = "" // Only populated for Joystick devices
			};
		}

		/// <summary>
		/// Creates the appropriate DirectInput device object based on device type.
		/// </summary>
		private Device CreateDirectInputDevice(DirectInput directInput, DeviceInstance deviceInstance)
		{
			try
			{
				switch (deviceInstance.Type)
				{
					case DeviceType.Mouse:
						return new Mouse(directInput);
					case DeviceType.Keyboard:
						return new Keyboard(directInput);
					case DeviceType.Joystick:
					case DeviceType.Gamepad:
					case DeviceType.FirstPerson:
					case DeviceType.Flight:
					case DeviceType.Driving:
						return new Joystick(directInput, deviceInstance.InstanceGuid);
					default:
						Debug.WriteLine($"DeviceDirectInput: Unexpected device type {deviceInstance.Type} - {deviceInstance.InstanceName}");
						return null;
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"DeviceDirectInput: Error creating device {deviceInstance.InstanceName}: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Populates device capabilities from the DirectInput device object.
		/// </summary>
		private void PopulateDeviceCapabilities(Device device, DirectInputDeviceInfo deviceInfo)
		{
			var capabilities = device.Capabilities;
			deviceInfo.AxeCount = capabilities.AxeCount;
			deviceInfo.ButtonCount = capabilities.ButtonCount;
			deviceInfo.PovCount = capabilities.PovCount;
			deviceInfo.HasForceFeedback = capabilities.Flags.HasFlag(DeviceFlags.ForceFeedback);
			deviceInfo.DriverVersion = capabilities.DriverVersion;
			deviceInfo.HardwareRevision = capabilities.HardwareRevision;
			deviceInfo.FirmwareRevision = capabilities.FirmwareRevision;
		}

		/// <summary>
		/// Extracts hardware identification properties from joystick devices.
		/// </summary>
		private void ExtractJoystickProperties(Joystick joystick, DirectInputDeviceInfo deviceInfo)
		{
			try
			{
				// Get interface path
				deviceInfo.InterfacePath = joystick.Properties.InterfacePath ?? "";

				// Method 1: Get VID/PID directly from DirectInput properties (most reliable)
				deviceInfo.VendorId = joystick.Properties.VendorId;
				deviceInfo.ProductId = joystick.Properties.ProductId;

				// Method 2: Parse VID/PID from interface path if properties are empty
				if (deviceInfo.VendorId == 0 && deviceInfo.ProductId == 0 && !string.IsNullOrEmpty(deviceInfo.InterfacePath))
				{
					var (vid, pid) = ExtractVidPidFromPath(deviceInfo.InterfacePath);
					deviceInfo.VendorId = vid;
					deviceInfo.ProductId = pid;
				}

				// Method 3: Extract VID/PID from ProductGuid as last resort
				if (deviceInfo.VendorId == 0 && deviceInfo.ProductId == 0)
				{
					var (vid, pid) = ExtractVidPidFromGuid(deviceInfo.ProductGuid);
					deviceInfo.VendorId = vid;
					deviceInfo.ProductId = pid;
				}

				// Get class GUID if available
				try
				{
					deviceInfo.ClassGuid = joystick.Properties.ClassGuid;
				}
				catch
				{
					deviceInfo.ClassGuid = Guid.Empty;
				}

				// Extract device ID from interface path
				if (!string.IsNullOrEmpty(deviceInfo.InterfacePath))
					deviceInfo.DeviceId = ExtractDeviceIdFromPath(deviceInfo.InterfacePath);
				
				// Generate CommonIdentifier
				GenerateCommonIdentifier(deviceInfo);
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"DeviceDirectInput: Error extracting joystick properties for {deviceInfo.InstanceName}: {ex.Message}");
			}
		}

		/// <summary>
		/// Extracts VID and PID from device interface path.
		/// </summary>
		private (int vid, int pid) ExtractVidPidFromPath(string interfacePath)
		{
			if (string.IsNullOrEmpty(interfacePath))
				return (0, 0);

			try
			{
				// Common patterns: \\?\hid#vid_045e&pid_028e#... or \\?\usb#vid_045e&pid_028e#...
				var upperPath = interfacePath.ToUpperInvariant();
				var vidIndex = upperPath.IndexOf("VID_");
				var pidIndex = upperPath.IndexOf("PID_");

				if (vidIndex >= 0 && pidIndex >= 0)
				{
					var vidStart = vidIndex + 4;
					var pidStart = pidIndex + 4;

					if (vidStart + 4 <= interfacePath.Length && pidStart + 4 <= interfacePath.Length)
					{
						var vidStr = interfacePath.Substring(vidStart, 4);
						var pidStr = interfacePath.Substring(pidStart, 4);

						if (int.TryParse(vidStr, System.Globalization.NumberStyles.HexNumber, null, out int vid) &&
							int.TryParse(pidStr, System.Globalization.NumberStyles.HexNumber, null, out int pid))
						{
							return (vid, pid);
						}
					}
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"DeviceDirectInput: Error extracting VID/PID from path: {ex.Message}");
			}

			return (0, 0);
		}

		/// <summary>
		/// Extracts VID and PID from ProductGuid (some devices encode hardware IDs in GUID format).
		/// </summary>
		private (int vid, int pid) ExtractVidPidFromGuid(Guid productGuid)
		{
			try
			{
				var guidString = productGuid.ToString("N");
				if (guidString.Length >= 8)
				{
					// GUID format: first 4 hex chars = PID, next 4 hex chars = VID
					if (int.TryParse(guidString.Substring(0, 4), System.Globalization.NumberStyles.HexNumber, null, out int pid) &&
						int.TryParse(guidString.Substring(4, 4), System.Globalization.NumberStyles.HexNumber, null, out int vid))
					{
						return (vid, pid);
					}
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"DeviceDirectInput: Error extracting VID/PID from GUID: {ex.Message}");
			}

			return (0, 0);
		}

		/// <summary>
		/// Extracts device ID from interface path.
		/// </summary>
		private string ExtractDeviceIdFromPath(string interfacePath)
		{
			if (string.IsNullOrEmpty(interfacePath))
				return "";

			try
			{
				// Extract device ID from paths like: \\?\hid#vid_045e&pid_028e&mi_00#7&1234abcd&0&0000#{...}
				var parts = interfacePath.Split('#');
				if (parts.Length >= 2)
					return parts[1]; // Return hardware ID part (e.g., "vid_045e&pid_028e&mi_00")
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"DeviceDirectInput: Error extracting device ID from path: {ex.Message}");
			}

			return interfacePath; // Return full path as fallback
		}
		
		/// <summary>
		/// Generates CommonIdentifier for the device by extracting VID, PID, MI, and COL values.
		/// </summary>
		/// <param name="deviceInfo">Device information to process</param>
		private void GenerateCommonIdentifier(DirectInputDeviceInfo deviceInfo)
		{
			try
			{
				var vid = deviceInfo.VendorId > 0 ? $"{deviceInfo.VendorId:X4}" : "0000";
				var pid = deviceInfo.ProductId > 0 ? $"{deviceInfo.ProductId:X4}" : "0000";
				
				var commonId = $"VID_{vid}&PID_{pid}";
				
				// Try to extract MI and COL from InterfacePath if available
				if (!string.IsNullOrEmpty(deviceInfo.InterfacePath))
				{
					var upperPath = deviceInfo.InterfacePath.ToUpperInvariant();
					
					// Extract MI
					var miIndex = upperPath.IndexOf("&MI_");
					if (miIndex < 0) miIndex = upperPath.IndexOf("\\MI_");
					if (miIndex >= 0)
					{
						var miStart = miIndex + 4;
						if (miStart + 2 <= upperPath.Length)
						{
							var mi = upperPath.Substring(miStart, 2);
							if (mi != "00") commonId += $"&MI_{mi}";
						}
					}
					
					// Extract COL
					var colIndex = upperPath.IndexOf("&COL");
					if (colIndex < 0) colIndex = upperPath.IndexOf("\\COL");
					if (colIndex >= 0)
					{
						var colStart = colIndex + 4;
						var colEnd = colStart;
						while (colEnd < upperPath.Length && char.IsLetterOrDigit(upperPath[colEnd]))
							colEnd++;
						if (colEnd > colStart)
						{
							var col = upperPath.Substring(colStart, colEnd - colStart);
							commonId += $"&COL_{col}";
						}
					}
				}
				
				deviceInfo.CommonIdentifier = commonId;
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"DeviceDirectInput: Error generating CommonIdentifier: {ex.Message}");
				deviceInfo.CommonIdentifier = "VID_0000&PID_0000";
			}
		}

		/// <summary>
		/// Formats a property for debug output only if it has a non-empty value.
		/// </summary>
		private string FormatProperty(string name, string value)
		{
			return !string.IsNullOrEmpty(value) ? $"{name}: {value}, " : "";
		}

		/// <summary>
		/// Logs comprehensive device information for debugging.
		/// </summary>
		private void LogDeviceInfo(DirectInputDeviceInfo deviceInfo, int deviceIndex, List<string> debugLines)
		{
			debugLines.Add($"\n{deviceIndex}. DeviceDirectInputInfo: " +
				$"CommonIdentifier: {deviceInfo.CommonIdentifier}, " +
				$"InstanceGuid: {deviceInfo.InstanceGuid}, " +
				$"ProductGuid: {deviceInfo.ProductGuid}, " +
				$"InstanceName: {deviceInfo.InstanceName}, " +
				$"ProductName: {deviceInfo.ProductName}, " +
				$"DeviceType: {deviceInfo.DeviceType}, " +
				$"DeviceTypeName: {deviceInfo.DeviceTypeName}, " +
				$"DeviceSubtype: {deviceInfo.DeviceSubtype}, " +
				$"Usage: {deviceInfo.Usage}, " +
				$"UsagePage: {deviceInfo.UsagePage}, " +
				$"DriverVersion: {deviceInfo.DriverVersion}, " +
				$"HardwareRevision: {deviceInfo.HardwareRevision}, " +
				$"FirmwareRevision: {deviceInfo.FirmwareRevision}, " +
				FormatProperty("InterfacePath", deviceInfo.InterfacePath) +
				$"VidPidString: {deviceInfo.VidPidString}, " +
				$"VendorId: {deviceInfo.VendorId} (0x{deviceInfo.VendorId:X4}), " +
				$"ProductId: {deviceInfo.ProductId} (0x{deviceInfo.ProductId:X4})");

			debugLines.Add($"DeviceDirectInputInfo Capabilities: " +
				$"AxeCount: {deviceInfo.AxeCount}, " +
				$"ButtonCount: {deviceInfo.ButtonCount}, " +
				$"PovCount: {deviceInfo.PovCount}, " +
				$"HasForceFeedback: {deviceInfo.HasForceFeedback}");
		}

		/// <summary>
		/// Logs summary statistics for device enumeration.
		/// </summary>
		private void LogSummary(List<DirectInputDeviceInfo> deviceList, Stopwatch stopwatch, List<string> debugLines)
		{
			var gamepadCount = deviceList.Count(d => IsGamepadType(d.DeviceType));
			var keyboardCount = deviceList.Count(d => d.DeviceType == DeviceType.Keyboard);
			var mouseCount = deviceList.Count(d => d.DeviceType == DeviceType.Mouse);
			var offlineCount = deviceList.Count(d => !d.IsOnline);

			debugLines.Add($"\nDeviceDirectInput: ({(int)Math.Round(stopwatch.Elapsed.TotalMilliseconds)} ms) " +
				$"Input Devices found: {deviceList.Count}, " +
				$"Gamepads/Joysticks: {gamepadCount}, " +
				$"Keyboards: {keyboardCount}, " +
				$"Mice: {mouseCount}, " +
				$"Offline/Failed: {offlineCount}\n");
		}

		/// <summary>
		/// Disposes all DirectInput devices in the provided list to free resources.
		/// Call this method when the device list is no longer needed.
		/// </summary>
		/// <param name="deviceList">List of DirectInputDeviceInfo objects to dispose</param>
		public static void DisposeDeviceList(List<DirectInputDeviceInfo> deviceList)
		{
			if (deviceList == null) return;

			Debug.WriteLine($"DeviceDirectInput: Disposing {deviceList.Count} DirectInput devices...");

			foreach (var deviceInfo in deviceList)
			{
				try
				{
					if (deviceInfo?.DirectInputDevice != null)
					{
						Debug.WriteLine($"DeviceDirectInput: Disposing device - {deviceInfo.InstanceName}");
						deviceInfo.Dispose();
					}
				}
				catch (Exception ex)
				{
					Debug.WriteLine($"DeviceDirectInput: Error disposing device {deviceInfo?.InstanceName}: {ex.Message}");
				}
			}

			Debug.WriteLine("DeviceDirectInput: All devices disposed.");
		}

		/// <summary>
		/// Gets a human-readable device type name.
		/// </summary>
		/// <param name="deviceType">DirectInput device type</param>
		/// <returns>Human-readable device type name</returns>
		private string GetDeviceTypeName(DeviceType deviceType)
		{
			switch (deviceType)
			{
				case DeviceType.Mouse:
					return "Mouse";
				case DeviceType.Keyboard:
					return "Keyboard";
				case DeviceType.Joystick:
					return "Joystick";
				case DeviceType.Gamepad:
					return "Gamepad";
				case DeviceType.FirstPerson:
					return "First Person Controller";
				case DeviceType.Flight:
					return "Flight Controller";
				case DeviceType.Driving:
					return "Driving Controller";
				default:
					return $"Unknown ({(int)deviceType})";
			}
		}

		/// <summary>
		/// Determines if a device type is a gamepad/joystick type.
		/// </summary>
		/// <param name="deviceType">DirectInput device type</param>
		/// <returns>True if device is a gamepad/joystick type</returns>
		private bool IsGamepadType(DeviceType deviceType)
		{
			return deviceType == DeviceType.Joystick ||
				   deviceType == DeviceType.Gamepad ||
				   deviceType == DeviceType.FirstPerson ||
				   deviceType == DeviceType.Flight ||
				   deviceType == DeviceType.Driving;
		}

		/// <summary>
		/// Determines if a device type represents an actual input device.
		/// Filters out non-input devices like sound cards, network adapters, etc.
		/// </summary>
		/// <param name="deviceType">DirectInput device type</param>
		/// <returns>True if device is an input device (gamepad, keyboard, mouse)</returns>
		private bool IsInputDevice(DeviceType deviceType)
		{
			switch (deviceType)
			{
				// Input devices to enumerate
				case DeviceType.Mouse:
				case DeviceType.Keyboard:
				case DeviceType.Joystick:
				case DeviceType.Gamepad:
				case DeviceType.FirstPerson:
				case DeviceType.Flight:
				case DeviceType.Driving:
					return true;

				// Non-input devices to ignore
				default:
					return false;
			}
		}
	}
}
