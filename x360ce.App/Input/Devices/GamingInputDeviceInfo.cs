using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Windows.Gaming.Input;

namespace x360ce.App.Input.Devices
{
	/// <summary>
	/// Gaming Input device container with both device information and the actual Gaming Input gamepad object.
	/// Contains comprehensive device metadata plus the live Gaming Input gamepad for input reading.
	/// </summary>
	public class GamingInputDeviceInfo : InputDeviceInfo, IDisposable
	{
        // Gaming Input-specific properties
        public int GamepadIndex { get; set; }
		public uint LastTimestamp { get; set; }
		public bool SupportsVibration { get; set; }
		public bool SupportsTriggerRumble { get; set; }
	
		/// <summary>
		/// Display name combining index and name for easy identification.
		/// </summary>
		public string DisplayName => $"Gaming Input {GamepadIndex + 1} - {InstanceName}";

        /// <summary>
        /// The actual Gaming Input gamepad object for reading input.
        /// </summary>
        public Gamepad GamingInputDevice { get; set; }

        /// <summary>
        /// Dispose the Gaming Input gamepad when no longer needed.
        /// </summary>
        public void Dispose()
		{
			GamingInputDevice = null;
		}
	}

	/// <summary>
	/// Gaming Input device enumeration and management class.
	/// Self-contained implementation with minimal external dependencies.
	/// Provides functionality to discover and list Gaming Input devices including modern gamepads.
	/// Returns live Gaming Input gamepad objects that can be used for input reading.
	/// </summary>
	internal class GamingInputDevice
	{
		// Constants for device identification
		private const int MICROSOFT_VENDOR_ID = 0x045E;
		private const int GAMING_INPUT_PRODUCT_ID = 0x02FF;
		private const int STANDARD_GAMEPAD_SUBTYPE = 1;
		private const int GAME_CONTROLS_USAGE = 0x05;
		private const int GENERIC_DESKTOP_USAGE_PAGE = 0x01;
		private const int GAMING_INPUT_VERSION = 0x0100;
		
		// Standard Gaming Input capabilities
		private const int AXES_COUNT = 6;      // Left Stick X/Y, Right Stick X/Y, Left/Right Triggers
		private const int SLIDER_COUNT = 0;    // Gaming Input has no sliders (triggers are axes)
		private const int BUTTON_COUNT = 16;   // Gaming Input supports up to 16 buttons
		private const int POV_COUNT = 1;       // D-Pad as POV
		
		// Detection timeout constants (in milliseconds)
		private const int ADMIN_INITIAL_DELAY = 50;
		private const int ADMIN_RETRY_DELAY = 100;
		private const int USER_INITIAL_DELAY = 25;
		private const int USER_RETRY_DELAY = 25;

		/// <summary>
		/// Creates a public list of Gaming Input devices (modern gamepads) with live gamepad objects and logs their properties.
		/// This method enumerates all available Gaming Input gamepads and outputs detailed information for debugging.
		/// </summary>
		/// <returns>List of GamingInputDeviceInfo objects containing both device information and live Gaming Input gamepad objects</returns>
		/// <remarks>
		/// This method performs comprehensive Gaming Input device enumeration:
		/// • Discovers all Gaming Input-compatible devices (modern gamepads)
		/// • Creates GamingInputDeviceInfo objects with device information AND live Gaming Input gamepad objects
		/// • Logs detailed device properties using Debug.WriteLine for diagnostics
		/// • Requires Windows 10+ for Gaming Input API availability
		/// • Provides device capability information for modern gamepads
		/// • Keeps Gaming Input gamepads alive for immediate input reading
		/// • Is self-contained with minimal external dependencies
		///
		/// IMPORTANT: The returned GamingInputDeviceInfo objects contain live Gaming Input gamepads.
		/// Call Dispose() on each GamingInputDeviceInfo when no longer needed to free resources.
		/// </remarks>
		public List<GamingInputDeviceInfo> GetGamingInputDeviceInfoList()
		{
			var stopwatch = Stopwatch.StartNew();
			var deviceList = new List<GamingInputDeviceInfo>();

			try
			{
				Debug.WriteLine("\n-----------------------------------------------------------------------------------------------------------------\n\n" +
					"GamingInputDevice: Starting Gaming Input device enumeration...");
				
				// Early exit if Gaming Input is not available
				if (!IsGamingInputAvailable())
				{
					LogNoGamingInputAvailable();
					return deviceList;
				}
				
				Debug.WriteLine("GamingInputDevice: Gaming Input API is available");
				
				// Detect gamepads with privilege-aware retry strategy
				bool isAdmin = IsRunningAsAdministrator();
				Debug.WriteLine($"GamingInputDevice: Running as {(isAdmin ? "Administrator" : "normal user")}");
				
				var gamepads = DetectGamepadsWithRetry(isAdmin);
				Debug.WriteLine($"GamingInputDevice: Found {gamepads.Count} gamepad(s)");
				
				// Process functional gamepads only
				ProcessFunctionalGamepads(gamepads, deviceList);
				
				// Log guidance if no devices found
				if (deviceList.Count == 0)
					LogDetectionGuidance(isAdmin);
				
				// Log summary
				LogDeviceListSummary(deviceList, stopwatch);
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"GamingInputDevice: Fatal error during enumeration: {ex.Message}");
				Debug.WriteLine($"GamingInputDevice: Stack trace: {ex.StackTrace}");
			}

			return deviceList;
		}
		
		/// <summary>
		/// Disposes all Gaming Input devices in the provided list to free resources.
		/// Call this method when the device list is no longer needed.
		/// </summary>
		/// <param name="deviceList">List of GamingInputDeviceInfo objects to dispose</param>
		public static void DisposeDeviceList(List<GamingInputDeviceInfo> deviceList)
		{
			if (deviceList == null) return;
			
			Debug.WriteLine($"GamingInputDevice: Disposing {deviceList.Count} Gaming Input devices...");
			
			foreach (var deviceInfo in deviceList)
			{
				try
				{
					if (deviceInfo != null)
					{
						Debug.WriteLine($"GamingInputDevice: Disposing device - {deviceInfo.InstanceName}");
						deviceInfo.Dispose();
					}
				}
				catch (Exception ex)
				{
					Debug.WriteLine($"GamingInputDevice: Error disposing device {deviceInfo?.InstanceName}: {ex.Message}");
				}
			}
			
			Debug.WriteLine("GamingInputDevice: All Gaming Input devices disposed.");
		}
		
		/// <summary>
		/// Gets comprehensive Gaming Input system information for diagnostics.
		/// </summary>
		/// <returns>String containing detailed Gaming Input diagnostic information</returns>
		public string GetGamingInputDiagnosticInfo()
		{
			var info = new System.Text.StringBuilder();
			
			try
			{
				var isAvailable = IsGamingInputAvailable();
				
				info.AppendLine("=== Gaming Input Diagnostic Information ===");
				info.AppendLine($"Gaming Input Available: {isAvailable}");
				info.AppendLine($"Reported OS Version: {Environment.OSVersion}");
				info.AppendLine($"Note: OS version may be incorrect due to compatibility manifests");
				info.AppendLine($"Gaming Input API Test: {(isAvailable ? "PASSED" : "FAILED")}");
				info.AppendLine($"Gaming Input Version: {GAMING_INPUT_VERSION:X4}");
				info.AppendLine();
				
				if (isAvailable)
				{
					AppendGamepadDiagnostics(info);
				}
				else
				{
					AppendUnavailableReasons(info);
				}
				
				AppendGamingInputFeatures(info);
			}
			catch (Exception ex)
			{
				info.AppendLine($"Error getting Gaming Input diagnostic info: {ex.Message}");
			}
			
			return info.ToString();
		}

		#region Private Helper Methods - Detection

		/// <summary>
		/// Detects gamepads with privilege-aware retry logic.
		/// </summary>
		private IReadOnlyList<Gamepad> DetectGamepadsWithRetry(bool isAdmin)
		{
			var gamepads = Gamepad.Gamepads;
			
			if (gamepads.Count > 0)
			{
				Debug.WriteLine($"GamingInputDevice: Immediate detection found {gamepads.Count} gamepad(s)");
				return gamepads;
			}
			
			// Apply privilege-specific retry delays
			var delays = isAdmin
				? new[] { ADMIN_INITIAL_DELAY, ADMIN_RETRY_DELAY, ADMIN_RETRY_DELAY }
				: new[] { USER_INITIAL_DELAY, USER_RETRY_DELAY };
			
			return RetryDetectionWithDelays(delays, isAdmin ? "Admin" : "User");
		}

		/// <summary>
		/// Retries gamepad detection with specified delays.
		/// </summary>
		private IReadOnlyList<Gamepad> RetryDetectionWithDelays(int[] delays, string mode)
		{
			int totalDelay = 0;
			
			foreach (var delay in delays)
			{
				Debug.WriteLine($"GamingInputDevice: [{mode}] Retrying with {delay}ms delay...");
				System.Threading.Thread.Sleep(delay);
				totalDelay += delay;
				
				var gamepads = Gamepad.Gamepads;
				if (gamepads.Count > 0)
				{
					Debug.WriteLine($"GamingInputDevice: [{mode}] Found {gamepads.Count} gamepad(s) after {totalDelay}ms");
					return gamepads;
				}
			}
			
			Debug.WriteLine($"GamingInputDevice: [{mode}] No gamepads found after {totalDelay}ms total delay");
			return Gamepad.Gamepads;
		}

		/// <summary>
		/// Processes functional gamepads and adds them to the device list.
		/// Filters out non-responsive devices early for efficiency.
		/// </summary>
		private void ProcessFunctionalGamepads(IReadOnlyList<Gamepad> gamepads, List<GamingInputDeviceInfo> deviceList)
		{
			for (int i = 0; i < gamepads.Count; i++)
			{
				try
				{
					// Early filtering: Test gamepad functionality before creating device info
					if (!TryGetGamepadReading(gamepads[i], out var reading))
					{
						Debug.WriteLine($"GamingInputDevice: Gamepad {i} is not responding - skipping");
						continue;
					}
					
					var deviceInfo = CreateDeviceInfo(gamepads[i], i, reading);
					
					// Filter out virtual/converted devices
					if (!IsVirtualConvertedDevice(deviceInfo))
					{
						deviceList.Add(deviceInfo);
						LogDeviceInfo(deviceInfo, deviceList.Count);
					}
					else
					{
						Debug.WriteLine($"GamingInputDevice: Skipping virtual/converted device at index {i}");
					}
				}
				catch (Exception ex)
				{
					Debug.WriteLine($"GamingInputDevice: Error processing gamepad {i}: {ex.Message}");
				}
			}
		}

		/// <summary>
		/// Creates a GamingInputDeviceInfo object from a functional gamepad.
		/// </summary>
		private GamingInputDeviceInfo CreateDeviceInfo(Gamepad gamepad, int index, GamepadReading reading)
		{
			
			return new GamingInputDeviceInfo
			{
				// Identity
				InstanceGuid = GenerateGamingInputGuid(index),
				InstanceName = $"Gaming Input Gamepad {index + 1}",
				ProductGuid = GenerateGamingInputProductGuid(),
				ProductName = "Gaming Input Gamepad",
				GamepadIndex = index,
				
				// Device type
				DeviceType = (int)SharpDX.DirectInput.DeviceType.Gamepad,
				DeviceSubtype = STANDARD_GAMEPAD_SUBTYPE,
				DeviceTypeName = "Gaming Input Gamepad",
				Usage = GAME_CONTROLS_USAGE,
				UsagePage = GENERIC_DESKTOP_USAGE_PAGE,
				InputType = "GamingInput",
				
				// Hardware identification - GamingInput uses generic Microsoft VID/PID
				VendorId = MICROSOFT_VENDOR_ID,
				ProductId = GAMING_INPUT_PRODUCT_ID,
				ClassGuid = GenerateGamingInputClassGuid(),
				CommonIdentifier = $"VID_{MICROSOFT_VENDOR_ID:X4}&PID_{GAMING_INPUT_PRODUCT_ID:X4}",
				
				// Capabilities
				AxeCount = AXES_COUNT,
				SliderCount = SLIDER_COUNT,
				ButtonCount = BUTTON_COUNT,
				PovCount = POV_COUNT,
				HasForceFeedback = true,
				SupportsVibration = true,
				SupportsTriggerRumble = true,
				
				// State
				IsOnline = true,
				LastTimestamp = (uint)reading.Timestamp,
				GamingInputDevice = gamepad,

                // Device identification - GamingInput API does not provide native device paths
                DeviceId = "",
				InterfacePath = GenerateGamingInputProductGuid().ToString(),
				HardwareIds = "",
				ParentDeviceId = "",
				
				// Version info
				DriverVersion = GAMING_INPUT_VERSION,
				HardwareRevision = 1,
				FirmwareRevision = 1,

                // Initial application profile state
                IsEnabled = false,
                AssignedToPad1 = false,
                AssignedToPad2 = false,
                AssignedToPad3 = false,
                AssignedToPad4 = false,
            };
		}

		#endregion

		#region Private Helper Methods - Validation

		/// <summary>
		/// Enhanced GamingInput availability check with detailed diagnostics.
		/// </summary>
		private bool IsGamingInputAvailable()
		{
			try
			{
				Debug.WriteLine($"GamingInputDevice: Detected OS version: {Environment.OSVersion.Version}");
				Debug.WriteLine($"GamingInputDevice: Platform: {Environment.OSVersion.Platform}");
				Debug.WriteLine($"GamingInputDevice: Is64BitOperatingSystem: {Environment.Is64BitOperatingSystem}");
				Debug.WriteLine($"GamingInputDevice: Is64BitProcess: {Environment.Is64BitProcess}");
				Debug.WriteLine($"GamingInputDevice: CLR Version: {Environment.Version}");
				Debug.WriteLine($"GamingInputDevice: Running as Administrator: {IsRunningAsAdministrator()}");
				Debug.WriteLine("GamingInputDevice: Testing Gaming Input API accessibility (this is the definitive test)...");
				
				// Test 1: Check if Windows.Gaming.Input namespace is available
				var gamingInputType = typeof(Windows.Gaming.Input.Gamepad);
				Debug.WriteLine($"GamingInputDevice: Gaming Input type loaded: {gamingInputType.FullName}");
				
				// Test 2: Try to access the Gamepads collection
				var gamepads = Windows.Gaming.Input.Gamepad.Gamepads;
				Debug.WriteLine($"GamingInputDevice: Gaming Input API is accessible! Found {gamepads.Count} gamepads");
				
				// Provide guidance if no gamepads found
				if (gamepads.Count == 0)
				{
					Debug.WriteLine("GamingInputDevice: No gamepads found in initial check.");
					if (!IsRunningAsAdministrator())
					{
						Debug.WriteLine("GamingInputDevice: WARNING - Running without Administrator privileges.");
						Debug.WriteLine("GamingInputDevice: GamingInput may require elevated privileges for device access.");
						Debug.WriteLine("GamingInputDevice: Consider running as Administrator if devices are not detected.");
					}
				}
				
				// Test 3: Try to register for gamepad events
				try
				{
					Windows.Gaming.Input.Gamepad.GamepadAdded += OnGamepadAdded;
					Windows.Gaming.Input.Gamepad.GamepadRemoved += OnGamepadRemoved;
					Debug.WriteLine("GamingInputDevice: Successfully registered for gamepad events");
					
					// Immediately unregister to avoid memory leaks
					Windows.Gaming.Input.Gamepad.GamepadAdded -= OnGamepadAdded;
					Windows.Gaming.Input.Gamepad.GamepadRemoved -= OnGamepadRemoved;
				}
				catch (Exception eventEx)
				{
					Debug.WriteLine($"GamingInputDevice: Could not register for events (may indicate limited access): {eventEx.Message}");
				}
				
				return true;
			}
			catch (System.TypeLoadException ex)
			{
				Debug.WriteLine($"GamingInputDevice: Gaming Input types not available: {ex.Message}");
				Debug.WriteLine("GamingInputDevice: This indicates Windows.Gaming.Input is not accessible");
				return false;
			}
			catch (System.IO.FileNotFoundException ex)
			{
				Debug.WriteLine($"GamingInputDevice: Gaming Input assemblies not found: {ex.Message}");
				Debug.WriteLine("GamingInputDevice: Required Windows Runtime components are missing");
				return false;
			}
			catch (System.PlatformNotSupportedException ex)
			{
				Debug.WriteLine($"GamingInputDevice: Platform not supported: {ex.Message}");
				Debug.WriteLine("GamingInputDevice: Gaming Input requires Windows 10 1607+ or Windows 11");
				return false;
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"GamingInputDevice: Gaming Input API is not accessible: {ex.Message}");
				Debug.WriteLine($"GamingInputDevice: Exception type: {ex.GetType().Name}");
				Debug.WriteLine("GamingInputDevice: This could mean:");
				Debug.WriteLine("  • Gaming Input is not available on this system");
				Debug.WriteLine("  • Windows version is too old (requires Windows 10 1607+)");
				Debug.WriteLine("  • UWP runtime components are missing");
				Debug.WriteLine("  • Application manifest doesn't declare Windows 10+ compatibility");
				Debug.WriteLine("  • Application is running in compatibility mode");
				Debug.WriteLine("  • Windows Gaming Input service is disabled");
				return false;
			}
		}

		/// <summary>
		/// Safely gets gamepad reading with proper error handling.
		/// </summary>
		private bool TryGetGamepadReading(Gamepad gamepad, out GamepadReading reading)
		{
			reading = new GamepadReading();
			
			try
			{
				reading = gamepad.GetCurrentReading();
				return true;
			}
			catch (System.ComponentModel.Win32Exception ex)
			{
				Debug.WriteLine($"GamingInputDevice: Win32Exception getting gamepad reading: {ex.Message}");
				return false;
			}
			catch (System.UnauthorizedAccessException ex)
			{
				Debug.WriteLine($"GamingInputDevice: UnauthorizedAccessException getting gamepad reading (UWP restriction): {ex.Message}");
				return false;
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"GamingInputDevice: Unexpected error getting gamepad reading: {ex.Message}");
				return false;
			}
		}

		/// <summary>
		/// Checks if the current process is running with Administrator privileges.
		/// </summary>
		private bool IsRunningAsAdministrator()
		{
			try
			{
				var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
				var principal = new System.Security.Principal.WindowsPrincipal(identity);
				return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"GamingInputDevice: Could not determine Administrator status: {ex.Message}");
				return false;
			}
		}

		#endregion

		#region Private Helper Methods - GUID Generation

		/// <summary>
		/// Generates a unique GUID for a Gaming Input gamepad.
		/// </summary>
		private Guid GenerateGamingInputGuid(int gamepadIndex)
		{
			// Generate consistent GUID based on Gaming Input gamepad index
			// Using a base GUID and modifying the last bytes with gamepad index
			var baseBytes = new byte[] { 0x47, 0x41, 0x4D, 0x49, 0x4E, 0x47, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
			baseBytes[15] = (byte)gamepadIndex;
			return new Guid(baseBytes);
		}

		/// <summary>
		/// Generates a product GUID for Gaming Input gamepads.
		/// </summary>
		private Guid GenerateGamingInputProductGuid()
		{
			// Standard Gaming Input gamepad product GUID
			return new Guid("47414D49-4E47-5052-4F44-000000000000"); // "GAMINGPROD"
		}

		/// <summary>
		/// Generates a class GUID for Gaming Input devices.
		/// </summary>
		private Guid GenerateGamingInputClassGuid()
		{
			// Gaming Input device class GUID
			return new Guid("47414D49-4E47-434C-4153-000000000000"); // "GAMINGCLAS"
		}
		
		#endregion

		#region Private Helper Methods - Logging

		/// <summary>
		/// Logs when Gaming Input is not available.
		/// </summary>
		private void LogNoGamingInputAvailable()
		{
			Debug.WriteLine("GamingInputDevice: Gaming Input is not available on this system");
			Debug.WriteLine("GamingInputDevice: Gaming Input requires Windows 10+ and is not available on this system");
			Debug.WriteLine("\nDevicesGamingInput: Gaming Input gamepads found: 0, Online: 0, Offline/Failed: 0\n");
		}

		/// <summary>
		/// Logs detection guidance based on privilege level.
		/// </summary>
		private void LogDetectionGuidance(bool isAdmin)
		{
			if (!isAdmin)
			{
				Debug.WriteLine("GamingInputDevice: No devices found in normal user mode.");
				Debug.WriteLine("GamingInputDevice: RECOMMENDATION: Run as Administrator for full GamingInput access.");
			}
			else
			{
				Debug.WriteLine("GamingInputDevice: No devices found with Administrator privileges.");
				Debug.WriteLine("GamingInputDevice: Controllers may only support DirectInput/XInput.");
			}
		}

		/// <summary>
		/// Logs detailed information about a detected device.
		/// </summary>
		private void LogDeviceInfo(GamingInputDeviceInfo deviceInfo, int deviceNumber)
		{
			// Note: GamingInput API does not provide DeviceId, InterfacePath, or HardwareIds - these are always empty
			Debug.WriteLine($"\n{deviceNumber}. DevicesGamingInputInfo: " +
				$"CommonIdentifier (generated): {deviceInfo.CommonIdentifier}, " +
				$"GamepadIndex: {deviceInfo.GamepadIndex}, " +
				$"InstanceGuid (generated): {deviceInfo.InstanceGuid}, " +
				$"ProductGuid (generated): {deviceInfo.ProductGuid}, " +
				$"InstanceName (generated): {deviceInfo.InstanceName}, " +
				$"ProductName (generated): {deviceInfo.ProductName}, " +
				$"DeviceType (generated): {deviceInfo.DeviceType}, " +
				$"DeviceTypeName (generated): {deviceInfo.DeviceTypeName}, " +
				$"Timestamp: {deviceInfo.LastTimestamp}");

			Debug.WriteLine($"DevicesGamingInputInfo Identification (generated): " +
				$"VidPidString: {deviceInfo.VidPidString}, " +
				$"VendorId: {deviceInfo.VendorId} (0x{deviceInfo.VendorId:X4}), " +
				$"ProductId: {deviceInfo.ProductId} (0x{deviceInfo.ProductId:X4})");

			Debug.WriteLine($"DevicesGamingInputInfo Capabilities (generated): " +
				$"AxeCount: {deviceInfo.AxeCount}, " +
				$"SliderCount: {deviceInfo.SliderCount}, " +
				$"ButtonCount: {deviceInfo.ButtonCount}, " +
				$"PovCount: {deviceInfo.PovCount}, " +
				$"HasForceFeedback: {deviceInfo.HasForceFeedback}, " +
				$"SupportsVibration: {deviceInfo.SupportsVibration}, " +
				$"SupportsTriggerRumble: {deviceInfo.SupportsTriggerRumble}");
			
			Debug.WriteLine($"DevicesGamingInputInfo Note: " +
				$"GamingInput API uses generic Microsoft VID/PID (045E:02FF) for all controllers - use DirectInput or RawInput for actual hardware identification");
		}

		/// <summary>
		/// Logs summary statistics for device enumeration results.
		/// </summary>
		private void LogDeviceListSummary(List<GamingInputDeviceInfo> deviceList, Stopwatch stopwatch)
		{
			var connectedCount = deviceList.Count;
			var offlineCount = deviceList.Count(d => !d.IsOnline);
			var vibrationCount = deviceList.Count(d => d.SupportsVibration);
			var triggerRumbleCount = deviceList.Count(d => d.SupportsTriggerRumble);

			stopwatch.Stop();

			Debug.WriteLine($"\nDevicesGamingInput: ({(int)Math.Round(stopwatch.Elapsed.TotalMilliseconds)} ms) " +
				$"Gaming Input gamepads found: {connectedCount}, " +
				$"Online: {connectedCount - offlineCount}, " +
				$"Offline/Failed: {offlineCount}, " +
				$"With Vibration: {vibrationCount}, " +
				$"With Trigger Rumble: {triggerRumbleCount}\n");
		}

		/// <summary>
		/// Appends gamepad diagnostics to the info string builder.
		/// </summary>
		private void AppendGamepadDiagnostics(System.Text.StringBuilder info)
		{
			try
			{
				var gamepads = Gamepad.Gamepads;
				info.AppendLine($"Connected Gamepads: {gamepads.Count}");
				info.AppendLine();
				
				for (int i = 0; i < gamepads.Count; i++)
				{
					var gamepad = gamepads[i];
					info.AppendLine($"Gamepad {i + 1}:");
					
					try
					{
						var reading = gamepad.GetCurrentReading();
						info.AppendLine($"  Status: Functional");
						info.AppendLine($"  Timestamp: {reading.Timestamp}");
						info.AppendLine($"  Buttons: {reading.Buttons}");
						info.AppendLine($"  Left Stick: ({reading.LeftThumbstickX:F2}, {reading.LeftThumbstickY:F2})");
						info.AppendLine($"  Right Stick: ({reading.RightThumbstickX:F2}, {reading.RightThumbstickY:F2})");
						info.AppendLine($"  Triggers: L={reading.LeftTrigger:F2}, R={reading.RightTrigger:F2}");
						
						try
						{
							var vibration = gamepad.Vibration;
							info.AppendLine($"  Vibration Support: Available");
							info.AppendLine($"  Current Vibration: L={vibration.LeftMotor:F2}, R={vibration.RightMotor:F2}, LT={vibration.LeftTrigger:F2}, RT={vibration.RightTrigger:F2}");
						}
						catch
						{
							info.AppendLine($"  Vibration Support: Unknown");
						}
					}
					catch (Exception ex)
					{
						info.AppendLine($"  Status: Error - {ex.Message}");
					}
					
					info.AppendLine();
				}
			}
			catch (Exception ex)
			{
				info.AppendLine($"Error enumerating gamepads: {ex.Message}");
			}
		}

		/// <summary>
		/// Appends reasons why Gaming Input is unavailable.
		/// </summary>
		private void AppendUnavailableReasons(System.Text.StringBuilder info)
		{
			info.AppendLine("Gaming Input is not available on this system.");
			info.AppendLine("Possible causes:");
			info.AppendLine("  • Windows version too old (requires Windows 10 1607+ or Windows 11)");
			info.AppendLine("  • Gaming Input API not accessible");
			info.AppendLine("  • UWP runtime components missing");
			info.AppendLine("  • Application manifest missing Gaming Input capability");
			info.AppendLine("  • System compatibility issues");
		}

		/// <summary>
		/// Appends Gaming Input features and limitations.
		/// </summary>
		private void AppendGamingInputFeatures(System.Text.StringBuilder info)
		{
			info.AppendLine("Gaming Input Features:");
			info.AppendLine("  • Modern Windows 10+ API");
			info.AppendLine("  • Advanced vibration (including trigger rumble)");
			info.AppendLine("  • Enhanced controller support");
			info.AppendLine("  • UWP and Win32 compatibility");
			info.AppendLine("  • Separate trigger axes");
			info.AppendLine();
			
			info.AppendLine("Gaming Input Limitations:");
			info.AppendLine("  • Windows 10+ required");
			info.AppendLine("  • No background access (UWP restriction)");
			info.AppendLine("  • No Guide button access");
			info.AppendLine("  • Limited to modern controllers");
		}

		#endregion

		#region Event Handlers

		/// <summary>
		/// Event handler for gamepad added events (used for testing event registration).
		/// </summary>
		private void OnGamepadAdded(object sender, Windows.Gaming.Input.Gamepad gamepad)
		{
			// This is just for testing event registration - actual event handling would go here
		}

		/// <summary>
		/// </summary>
		private void OnGamepadRemoved(object sender, Windows.Gaming.Input.Gamepad gamepad)
		{
			// This is just for testing event registration - actual event handling would go here
		}

		#endregion

		#region Virtual Device Filtering

		/// <summary>
		/// Determines if a device is a virtual/converted device that should be excluded.
		/// Checks for "ConvertedDevice" text in InterfacePath or DeviceId.
		/// </summary>
		/// <param name="deviceInfo">Device info to check</param>
		/// <returns>True if device is a virtual/converted device</returns>
		private bool IsVirtualConvertedDevice(GamingInputDeviceInfo deviceInfo)
		{
			if (deviceInfo == null)
				return false;

			// Use base class check for standard "ConvertedDevice" markers
			if (deviceInfo.IsVirtualConvertedDevice())
				return true;

			// Check for Input Configuration Devices and Portable Device Control
			var instanceName = (deviceInfo.InstanceName ?? "").ToLowerInvariant();
			var productName = (deviceInfo.ProductName ?? "").ToLowerInvariant();
			var combinedText = $"{instanceName} {productName}";
			
			if (combinedText.Contains("input configuration") ||
			    combinedText.Contains("input_config") ||
			    combinedText.Contains("inputconfig") ||
			    combinedText.Contains("portable device control") ||
			    combinedText.Contains("portable_device") ||
			    combinedText.Contains("portabledevice"))
			{
				return true;
			}
			
			// Check for Intel platform endpoints (HID Event Filter) - platform hotkey controllers
			// Examples: VID_494E54&PID_33D2 (INT33D2), VID_8087&PID_0000 (INTC816)
			var upperName = instanceName.ToUpperInvariant();
			if (upperName.Contains("INT33D2") || upperName.Contains("INTC816") ||
			    upperName.Contains("494E54") || upperName.Contains("8087"))
			{
				return true;
			}

			return false;
		}

		#endregion
	}
}
