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
	public class DirectInputDeviceInfo : InputDeviceInfo, IDisposable
	{
        public List<JoystickOffset> AvailableAxes { get; set; }
		public List<JoystickOffset> AvailableSliders { get; set; }
        /// <summary>
        /// Mouse axis sensitivity values for: X axis, Y axis, Vertical wheel axis, Horizontal wheel axis.
        /// Defaults: {20, 20, 50, 50}.
        /// Minimum is 1.
        /// </summary>

		public List<int> MouseAxisSensitivity { get; set; } = new List<int> { 20, 20, 50, 50 };

        /// <summary>
        /// Mouse axis delta accumulated positions for: X axis, Y axis, Vertical wheel axis, Horizontal wheel axis.
        /// Defaults: {32767, 32767, 0, 0}.
        /// Minimum is 0, maximum is 65535, center is 32767.
        /// </summary>
        public List<int> MouseAxisAccumulatedDelta { get; set; } = new List<int> { 32767, 32767, 32767, 0 };
        
        /// <summary>
        /// Enable or disable Mouse Axis State retrieval for this device.
        /// DirectInput mouse device interrupts-blocks raw input mouse state messages when Acquired.
        /// Default: false (Button polling only, RawInput friendly).
        /// Set to true to enable Axis polling (Acquires device, blocks RawInput).
        /// </summary>
        public bool MouseAxisStateEnabled { get; set; }

		/// <summary>
		/// Display name combining instance ID and name for easy identification.
		/// </summary>
		public string DisplayName => $"{InstanceGuid.ToString().Substring(0, 8)} - {InstanceName}";

        /// <summary>
        /// The actual DirectInput device object for reading input.
        /// Can be Mouse, Keyboard, or Joystick depending on device type.
        /// </summary>
        public Device DirectInputDevice { get; set; }

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
	internal class DirectInputDevice
	{
		// Standard DirectInput axis offsets for capability detection
		private static readonly JoystickOffset[] AxisOffsets = new[]
		{
			JoystickOffset.X,
			JoystickOffset.Y,
			JoystickOffset.Z,
			JoystickOffset.RotationX,
			JoystickOffset.RotationY,
			JoystickOffset.RotationZ,
			JoystickOffset.AccelerationX,
			JoystickOffset.AccelerationY,
			JoystickOffset.AccelerationZ,
			JoystickOffset.AngularAccelerationX,
			JoystickOffset.AngularAccelerationY,
			JoystickOffset.AngularAccelerationZ,
			JoystickOffset.ForceX,
			JoystickOffset.ForceY,
			JoystickOffset.ForceZ,
			JoystickOffset.TorqueX,
			JoystickOffset.TorqueY,
			JoystickOffset.TorqueZ,
			JoystickOffset.VelocityX,
			JoystickOffset.VelocityY,
			JoystickOffset.VelocityZ,
			JoystickOffset.AngularVelocityX,
			JoystickOffset.AngularVelocityY,
			JoystickOffset.AngularVelocityZ,
		};

		// Standard DirectInput slider offsets for capability detection
		private static readonly JoystickOffset[] SliderOffsets = new[]
		{
			JoystickOffset.Sliders0,
			JoystickOffset.Sliders1,
			JoystickOffset.AccelerationSliders0,
			JoystickOffset.AccelerationSliders1,
			JoystickOffset.ForceSliders0,
			JoystickOffset.ForceSliders1,
			JoystickOffset.VelocitySliders0,
			JoystickOffset.VelocitySliders1
		};

		/// <summary>
		/// Creates a public list of DirectInput devices (gamepads, keyboards, mice) with live device objects and logs their properties.
		/// This method enumerates all available DirectInput devices and outputs detailed information for debugging.
		/// </summary>
		/// <returns>List of DirectInputDeviceInfo objects containing both device information and live DirectInput device objects</returns>
		/// <remarks>
		/// This method performs comprehensive DirectInput device enumeration:
		/// • Discovers all DirectInput-compatible devices (gamepads, keyboards, mice)
		/// • Creates DirectInputDeviceInfo objects with device information AND live DirectInput device objects
		/// • Filters devices by type and availability
		/// • Provides device capability information where available
		/// • Keeps DirectInput devices alive for immediate input reading
		/// • Is self-contained with minimal external dependencies
		///
		/// IMPORTANT: The returned DirectInputDeviceInfo objects contain live DirectInput devices.
		/// Call Dispose() on each DirectInputDeviceInfo when no longer needed to free resources.
		/// </remarks>
		/// 

		public List<DirectInputDeviceInfo> GetDirectInputDeviceInfoList()
		{
			var deviceList = new List<DirectInputDeviceInfo>();

			try
			{
				using (var directInput = new DirectInput())
				{
					// Get all devices and filter to input devices only (early filtering for performance)
					var inputDevices = directInput.GetDevices(DeviceClass.All, DeviceEnumerationFlags.AllDevices)
						.Where(IsInputDevice)
						.ToList();

					foreach (var deviceInstance in inputDevices)
					{
						var deviceInfo = ProcessDevice(directInput, deviceInstance);
						if (deviceInfo != null && !deviceInfo.IsVirtualConvertedDevice())
							deviceList.Add(deviceInfo);
					}
				}
	
				// Filter out MI-only devices (USB composite parent nodes) when sibling COL devices exist
				// This prevents double-counting the same physical device
				var filteredDevices = FilterMiOnlyDevices(deviceList);
				if (filteredDevices.Count != deviceList.Count)
				{
					deviceList = filteredDevices;
				}
			}
			catch (Exception)
			{
			}

			return deviceList;
		}

		/// <summary>
		/// Processes a single DirectInput device instance and creates a DirectInputDeviceInfo object.
		/// </summary>
		private DirectInputDeviceInfo ProcessDevice(DirectInput directInput, DeviceInstance deviceInstance)
		{
			try
			{
				// Create device info with basic properties
				var deviceInfo = new DirectInputDeviceInfo
				{
					InstanceGuid = deviceInstance.InstanceGuid,
					InstanceName = deviceInstance.InstanceName,
					ProductGuid = deviceInstance.ProductGuid,
					ProductName = deviceInstance.ProductName,
					DeviceType = (int)deviceInstance.Type,
					DeviceSubtype = deviceInstance.Subtype,
					Usage = (int)deviceInstance.Usage,
					UsagePage = (int)deviceInstance.UsagePage,
					DeviceTypeName = GetDeviceTypeName(deviceInstance.Type),
					InputType = "DirectInput",
					InterfacePath = "",
					HardwareIds = "",
					ParentDeviceId = "",
					// Initial application profile state
					IsEnabled = false,
					AssignedToPad1 = false,
					AssignedToPad2 = false,
					AssignedToPad3 = false,
					AssignedToPad4 = false,
					MouseAxisStateEnabled = false
				};

				// Create DirectInput device object
				var device = CreateDirectInputDevice(directInput, deviceInstance);
				if (device == null)
				{
					deviceInfo.IsOnline = false;
					return deviceInfo;
				}

				// Populate capabilities
				PopulateDeviceCapabilities(device, deviceInfo);

				// Extract hardware identification and generate CommonIdentifier
				ExtractHardwareIdentification(device, deviceInfo);

				deviceInfo.DirectInputDevice = device;
				deviceInfo.IsOnline = true;

				return deviceInfo;
			}
			catch (Exception)
			{
				return null;
			}
		}

		/// <summary>
		/// Creates the appropriate DirectInput device object based on device type.
		/// </summary>
		private Device CreateDirectInputDevice(DirectInput directInput, DeviceInstance deviceInstance)
		{
			try
			{
				Device device = null;
				
				switch (deviceInstance.Type)
				{
					case DeviceType.Mouse:
						device = new Mouse(directInput);
						// Set cooperative level for mouse (non-exclusive, background access)
						try
						{
							var hwnd = Process.GetCurrentProcess().MainWindowHandle;
							device.SetCooperativeLevel(hwnd, CooperativeLevel.NonExclusive | CooperativeLevel.Background);
						}
						catch (Exception)
						{
						}
						return device;
						
					case DeviceType.Keyboard:
						device = new Keyboard(directInput);
						// Set cooperative level for keyboard (non-exclusive, background access)
						try
						{
							var hwnd = Process.GetCurrentProcess().MainWindowHandle;
							device.SetCooperativeLevel(hwnd, CooperativeLevel.NonExclusive | CooperativeLevel.Background);
						}
						catch (Exception)
						{
						}
						return device;
						
					case DeviceType.Joystick:
					case DeviceType.Gamepad:
					case DeviceType.FirstPerson:
					case DeviceType.Flight:
					case DeviceType.Driving:
						return new Joystick(directInput, deviceInstance.InstanceGuid);
						
					default:
						return null;
				}
			}
			catch (Exception)
			{
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
			deviceInfo.ButtonCount = capabilities.ButtonCount; // Keyboards report keys as ButtonCount
			deviceInfo.PovCount = capabilities.PovCount;
			deviceInfo.HasForceFeedback = capabilities.Flags.HasFlag(DeviceFlags.ForceFeedback);
			deviceInfo.DriverVersion = capabilities.DriverVersion;
			deviceInfo.HardwareRevision = capabilities.HardwareRevision;
			deviceInfo.FirmwareRevision = capabilities.FirmwareRevision;
			
			// Calculate available axes and slider count for joystick devices
			if (device is Joystick joystick)
			{
				deviceInfo.AvailableSliders = GetAvailableSliders(joystick);
				deviceInfo.SliderCount = deviceInfo.AvailableSliders.Count;
				// Calculate number of non-slider axes
				var axisCount = Math.Max(0, deviceInfo.AxeCount - deviceInfo.SliderCount);
				deviceInfo.AvailableAxes = GetAvailableAxes(joystick, axisCount);
			}
			else
			{
				deviceInfo.AvailableAxes = new List<JoystickOffset>();
				deviceInfo.AvailableSliders = new List<JoystickOffset>();
				deviceInfo.SliderCount = 0;
			}
		}

		/// <summary>
		/// Gets the list of available axes on a joystick device.
		/// </summary>
		private List<JoystickOffset> GetAvailableAxes(Joystick joystick, int axisCount)
		{
			var axes = new List<JoystickOffset>();
			foreach (var offset in AxisOffsets)
			{
				if (axes.Count >= axisCount)
					break;
				try
				{
					if (joystick.GetObjectInfoByOffset((int)offset) != null)
						axes.Add(offset);
				}
				catch { }
			}
			return axes;
		}
		
		/// <summary>
		/// Gets the list of available sliders on a joystick device.
		/// </summary>
		private List<JoystickOffset> GetAvailableSliders(Joystick joystick)
		{
			var sliders = new List<JoystickOffset>();
			foreach (var offset in SliderOffsets)
			{
				try
				{
					if (joystick.GetObjectInfoByOffset((int)offset) != null)
						sliders.Add(offset);
				}
				catch { }
			}
			return sliders;
		}

		/// <summary>
		/// Extracts hardware identification properties from the device and generates CommonIdentifier.
		/// Handles both joystick devices (with detailed properties) and keyboard/mouse devices.
		/// </summary>
		private void ExtractHardwareIdentification(Device device, DirectInputDeviceInfo deviceInfo)
		{
			try
			{
				if (device is Joystick joystick)
				{
					// Get interface path
					deviceInfo.InterfacePath = joystick.Properties.InterfacePath ?? "";

					// Extract VID/PID using multiple methods (prioritized by reliability)
					ExtractVidPid(joystick, deviceInfo);

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
				}
				else if (device is Mouse || device is Keyboard)
				{
					var (vid, pid) = ParseVidPidFromGuid(deviceInfo.ProductGuid);
					deviceInfo.VendorId = vid;
					deviceInfo.ProductId = pid;
					deviceInfo.InterfacePath = deviceInfo.ProductGuid.ToString();
				}

				// Generate CommonIdentifier for all device types
				GenerateCommonIdentifier(deviceInfo);
			}
			catch (Exception)
			{
				deviceInfo.CommonIdentifier = "VID_0000&PID_0000";
			}
		}

		/// <summary>
		/// Extracts VID and PID using multiple methods in priority order.
		/// Method 1: DirectInput properties (most reliable)
		/// Method 2: Parse from interface path
		/// Method 3: Extract from ProductGuid (fallback)
		/// </summary>
		private void ExtractVidPid(Joystick joystick, DirectInputDeviceInfo deviceInfo)
		{
			// Method 1: Get VID/PID directly from DirectInput properties (most reliable)
			deviceInfo.VendorId = joystick.Properties.VendorId;
			deviceInfo.ProductId = joystick.Properties.ProductId;

			// Method 2: Parse VID/PID from interface path if properties are empty
			if (deviceInfo.VendorId == 0 && deviceInfo.ProductId == 0 && !string.IsNullOrEmpty(deviceInfo.InterfacePath))
			{
				var (vid, pid) = ParseVidPidFromPath(deviceInfo.InterfacePath);
				deviceInfo.VendorId = vid;
				deviceInfo.ProductId = pid;
			}

			// Method 3: Extract VID/PID from ProductGuid as last resort
			if (deviceInfo.VendorId == 0 && deviceInfo.ProductId == 0)
			{
				var (vid, pid) = ParseVidPidFromGuid(deviceInfo.ProductGuid);
				deviceInfo.VendorId = vid;
				deviceInfo.ProductId = pid;
			}
		}

		/// <summary>
		/// Parses VID and PID from device interface path.
		/// Handles common patterns: \\?\hid#vid_045e&pid_028e#... or \\?\usb#vid_045e&pid_028e#...
		/// </summary>
		private (int vid, int pid) ParseVidPidFromPath(string interfacePath)
		{
			if (string.IsNullOrEmpty(interfacePath))
				return (0, 0);

			try
			{
				var upperPath = interfacePath.ToUpperInvariant();
				var vidIndex = upperPath.IndexOf("VID_", StringComparison.Ordinal);
				var pidIndex = upperPath.IndexOf("PID_", StringComparison.Ordinal);

				if (vidIndex >= 0 && pidIndex >= 0)
				{
					var vidStart = vidIndex + 4;
					var pidStart = pidIndex + 4;

					if (vidStart + 4 <= upperPath.Length && pidStart + 4 <= upperPath.Length)
					{
						var vidStr = upperPath.Substring(vidStart, 4);
						var pidStr = upperPath.Substring(pidStart, 4);

						if (int.TryParse(vidStr, System.Globalization.NumberStyles.HexNumber, null, out int vid) &&
							int.TryParse(pidStr, System.Globalization.NumberStyles.HexNumber, null, out int pid))
						{
							return (vid, pid);
						}
					}
				}
			}
			catch (Exception)
			{
			}

			return (0, 0);
		}

		/// <summary>
		/// Parses VID and PID from ProductGuid (some devices encode hardware IDs in GUID format).
		/// GUID format: first 4 hex chars = PID, next 4 hex chars = VID
		/// </summary>
		private (int vid, int pid) ParseVidPidFromGuid(Guid productGuid)
		{
			try
			{
				var guidString = productGuid.ToString("N");
				if (guidString.Length >= 8)
				{
					if (int.TryParse(guidString.Substring(0, 4), System.Globalization.NumberStyles.HexNumber, null, out int pid) &&
						int.TryParse(guidString.Substring(4, 4), System.Globalization.NumberStyles.HexNumber, null, out int vid))
					{
						return (vid, pid);
					}
				}
			}
			catch (Exception)
			{
			}

			return (0, 0);
		}

		/// <summary>
		/// Extracts device ID from interface path.
		/// Example: \\?\hid#vid_045e&pid_028e&mi_00#7&1234abcd&0&0000#{...} -> vid_045e&pid_028e&mi_00
		/// </summary>
		private string ExtractDeviceIdFromPath(string interfacePath)
		{
			if (string.IsNullOrEmpty(interfacePath))
				return "";

			try
			{
				var parts = interfacePath.Split('#');
				if (parts.Length >= 2)
					return parts[1]; // Return hardware ID part
			}
			catch (Exception)
			{
			}

			return interfacePath; // Return full path as fallback
		}
		
		/// <summary>
		/// Generates CommonIdentifier for the device by extracting VID, PID, MI, and COL values.
		/// Format: VID_XXXX&PID_XXXX[&MI_XX][&COL_XX]
		/// </summary>
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
					
					// Extract MI (interface number)
					var miIndex = upperPath.IndexOf("&MI_", StringComparison.Ordinal);
					if (miIndex < 0) miIndex = upperPath.IndexOf("\\MI_", StringComparison.Ordinal);
					if (miIndex >= 0 && miIndex + 6 <= upperPath.Length)
					{
						var mi = upperPath.Substring(miIndex + 4, 2);
						if (mi != "00") commonId += $"&MI_{mi}";
					}
					
					// Extract COL (collection number)
					var colIndex = upperPath.IndexOf("&COL", StringComparison.Ordinal);
					if (colIndex < 0) colIndex = upperPath.IndexOf("\\COL", StringComparison.Ordinal);
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
			catch (Exception)
			{
				deviceInfo.CommonIdentifier = "VID_0000&PID_0000";
			}
		}


		/// <summary>
		/// Disposes all DirectInput devices in the provided list to free resources.
		/// Call this method when the device list is no longer needed.
		/// </summary>
		/// <param name="deviceList">List of DirectInputDeviceInfo objects to dispose</param>
		public static void DisposeDeviceList(List<DirectInputDeviceInfo> deviceList)
		{
			if (deviceList == null) return;

			foreach (var deviceInfo in deviceList)
			{
				try
				{
					if (deviceInfo?.DirectInputDevice != null)
					{
						deviceInfo.Dispose();
					}
				}
				catch (Exception)
				{
				}
			}
		}

		/// <summary>
		/// Gets a human-readable device type name.
		/// </summary>
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
		/// Determines if a device instance represents an actual input device.
		/// Filters out non-input devices like sound cards, network adapters, etc.
		/// This is used for early filtering to avoid unnecessary device creation.
		/// </summary>
		private bool IsInputDevice(DeviceInstance deviceInstance)
		{
			// Check device name/description for non-gaming input patterns
			var deviceName = (deviceInstance.InstanceName ?? "").ToLowerInvariant();
			var productName = (deviceInstance.ProductName ?? "").ToLowerInvariant();
			var combinedText = $"{deviceName} {productName}";
			
			// Filter out Input Configuration Devices and Portable Device Control
			if (combinedText.Contains("input configuration") ||
			    combinedText.Contains("input_config") ||
			    combinedText.Contains("inputconfig") ||
			    combinedText.Contains("portable device control") ||
			    combinedText.Contains("portable_device") ||
			    combinedText.Contains("portabledevice"))
			{
				return false;
			}
			
			// Filter out Intel platform endpoints (HID Event Filter) - platform hotkey controllers
			// Examples: VID_494E54&PID_33D2 (INT33D2), VID_8087&PID_0000 (INTC816)
			var upperName = deviceName.ToUpperInvariant();
			if (upperName.Contains("INT33D2") || upperName.Contains("INTC816") ||
			    upperName.Contains("494E54") || upperName.Contains("8087"))
			{
				return false;
			}
			
			switch (deviceInstance.Type)
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
	
		/// <summary>
		/// Filters out non-Keyboard/Mouse MI-only devices (USB composite parent nodes) that don't have COL values.
		/// This prevents double-counting the same physical device and removes ambiguous transport nodes.
		/// IMPORTANT: Only filters devices that are NOT Keyboard or Mouse types. Keyboard and Mouse type devices
		/// with MI are kept because they represent actual input endpoints, not transport nodes.
		/// </summary>
		/// <param name="deviceList">List of devices to filter</param>
		/// <returns>Filtered list with non-Keyboard/Mouse MI-only transport nodes removed</returns>
		private List<DirectInputDeviceInfo> FilterMiOnlyDevices(List<DirectInputDeviceInfo> deviceList)
		{
		    var filteredList = new List<DirectInputDeviceInfo>();
		    
		    foreach (var device in deviceList)
		    {
		        // Check InterfacePath for MI/COL patterns
		        var interfacePath = device.InterfacePath ?? "";
		        bool hasMi = interfacePath.IndexOf("&MI_", StringComparison.OrdinalIgnoreCase) >= 0 ||
		                    interfacePath.IndexOf("\\MI_", StringComparison.OrdinalIgnoreCase) >= 0;
		        
		        bool hasCol = interfacePath.IndexOf("&COL", StringComparison.OrdinalIgnoreCase) >= 0 ||
		                     interfacePath.IndexOf("\\COL", StringComparison.OrdinalIgnoreCase) >= 0;
		        
		        // Only filter non-Keyboard/Mouse devices with MI but no COL
		        // Keyboard and Mouse type devices are always kept, even with MI but no COL
		        bool isKeyboardOrMouse = device.DeviceType == (int)DeviceType.Keyboard || device.DeviceType == (int)DeviceType.Mouse;
		        
		        if (hasMi && !hasCol && !isKeyboardOrMouse)
		        {
		            // Skip this non-Keyboard/Mouse MI-only device as it's just the parent transport node
		            continue;
		        }
		        
		        // Keep this device (either has COL, or is Keyboard/Mouse type, or has no MI)
		        filteredList.Add(device);
		    }
		    
		    return filteredList;
		}
	}
	}
