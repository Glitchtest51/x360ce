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
	public class GamingInputDeviceInfo : IDisposable
	{
		public Guid InstanceGuid { get; set; }
		public string InstanceName { get; set; }
		public Guid ProductGuid { get; set; }
		public string ProductName { get; set; }
		public int DeviceType { get; set; }
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
		
		// Gaming Input-specific properties
		public int GamepadIndex { get; set; }
		public uint LastTimestamp { get; set; }
		public bool SupportsVibration { get; set; }
		public bool SupportsTriggerRumble { get; set; }
		
		/// <summary>
		/// The actual Gaming Input gamepad object for reading input.
		/// </summary>
		public Gamepad GamingInputDevice { get; set; }
		
		/// <summary>
		/// Display name combining index and name for easy identification.
		/// </summary>
		public string DisplayName => $"Gaming Input {GamepadIndex + 1} - {InstanceName}";
		
		/// <summary>
		/// VID/PID string in standard format for hardware identification.
		/// </summary>
		public string VidPidString => $"VID_{VendorId:X4}&PID_{ProductId:X4}";
		
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
	internal class DevicesGamingInput
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
		public List<GamingInputDeviceInfo> GetGamingInputDeviceList()
		{
			var stopwatch = Stopwatch.StartNew();
			var deviceList = new List<GamingInputDeviceInfo>();

			try
			{
				Debug.WriteLine("\n-----------------------------------------------------------------------------------------------------------------\n\n" +
					"DevicesGamingInput: Starting Gaming Input device enumeration...");
				
				// Early exit if Gaming Input is not available
				if (!IsGamingInputAvailable())
				{
					LogNoGamingInputAvailable();
					return deviceList;
				}
				
				Debug.WriteLine("DevicesGamingInput: Gaming Input API is available");
				
				// Use privilege-aware detection strategy
				bool isAdmin = IsRunningAsAdministrator();
				Debug.WriteLine($"DevicesGamingInput: Running as Administrator: {isAdmin}");
				Debug.WriteLine($"DevicesGamingInput: Using {(isAdmin ? "Administrator" : "normal-user")} detection mode");
				
				var gamepads = DetectGamepadsWithRetry(isAdmin);
				Debug.WriteLine($"DevicesGamingInput: Detection found {gamepads.Count} gamepads");
				
				// Process all detected gamepads
				ProcessDetectedGamepads(gamepads, deviceList);
				
				// Log guidance if no devices found
				LogDetectionGuidance(deviceList.Count, isAdmin);
				
				// Log summary
				LogDeviceListSummary(deviceList, stopwatch);
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"DevicesGamingInput: Fatal error during Gaming Input device enumeration: {ex.Message}");
				Debug.WriteLine($"DevicesGamingInput: Stack trace: {ex.StackTrace}");
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
			
			Debug.WriteLine($"DevicesGamingInput: Disposing {deviceList.Count} Gaming Input devices...");
			
			foreach (var deviceInfo in deviceList)
			{
				try
				{
					if (deviceInfo != null)
					{
						Debug.WriteLine($"DevicesGamingInput: Disposing device - {deviceInfo.InstanceName}");
						deviceInfo.Dispose();
					}
				}
				catch (Exception ex)
				{
					Debug.WriteLine($"DevicesGamingInput: Error disposing device {deviceInfo?.InstanceName}: {ex.Message}");
				}
			}
			
			Debug.WriteLine("DevicesGamingInput: All Gaming Input devices disposed.");
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
		/// Detects gamepads with retry logic based on privilege level.
		/// </summary>
		private IReadOnlyList<Gamepad> DetectGamepadsWithRetry(bool isAdmin)
		{
			var gamepads = Gamepad.Gamepads;
			Debug.WriteLine($"DevicesGamingInput: Immediate access found {gamepads.Count} gamepads");
			
			if (gamepads.Count > 0)
				return gamepads;
			
			// Apply privilege-specific retry strategy
			if (isAdmin)
			{
				return RetryDetectionWithDelays(
					new[] { ADMIN_INITIAL_DELAY, ADMIN_RETRY_DELAY, ADMIN_RETRY_DELAY },
					"Admin"
				);
			}
			else
			{
				return RetryDetectionWithDelays(
					new[] { USER_INITIAL_DELAY, USER_RETRY_DELAY },
					"User"
				);
			}
		}

		/// <summary>
		/// Retries gamepad detection with specified delays.
		/// </summary>
		private IReadOnlyList<Gamepad> RetryDetectionWithDelays(int[] delays, string mode)
		{
			var gamepads = Gamepad.Gamepads;
			int totalDelay = 0;
			
			foreach (var delay in delays)
			{
				if (gamepads.Count > 0)
					break;
					
				Debug.WriteLine($"DevicesGamingInput: [{mode}] No devices found, trying with {delay}ms delay...");
				System.Threading.Thread.Sleep(delay);
				totalDelay += delay;
				gamepads = Gamepad.Gamepads;
				Debug.WriteLine($"DevicesGamingInput: [{mode}] After {totalDelay}ms total delay found {gamepads.Count} gamepads");
			}
			
			return gamepads;
		}

		/// <summary>
		/// Processes all detected gamepads and adds them to the device list.
		/// </summary>
		private void ProcessDetectedGamepads(IReadOnlyList<Gamepad> gamepads, List<GamingInputDeviceInfo> deviceList)
		{
			for (int i = 0; i < gamepads.Count; i++)
			{
				try
				{
					var deviceInfo = CreateDeviceInfo(gamepads[i], i);
					if (deviceInfo != null)
					{
						deviceList.Add(deviceInfo);
						LogDeviceInfo(deviceInfo, deviceList.Count);
					}
				}
				catch (Exception ex)
				{
					Debug.WriteLine($"DevicesGamingInput: Error processing gamepad {i}: {ex.Message}");
				}
			}
		}

		/// <summary>
		/// Creates a GamingInputDeviceInfo object from a gamepad.
		/// Returns null if the gamepad is not functional.
		/// </summary>
		private GamingInputDeviceInfo CreateDeviceInfo(Gamepad gamepad, int index)
		{
			// Test if gamepad is functional
			if (!TryGetGamepadReading(gamepad, out var reading))
			{
				Debug.WriteLine($"DevicesGamingInput: Gamepad {index} is not responding");
				return null;
			}
			
			var deviceInfo = new GamingInputDeviceInfo
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
				
				// Hardware identification - GamingInput uses generic Microsoft VID/PID
				VendorId = MICROSOFT_VENDOR_ID,
				ProductId = GAMING_INPUT_PRODUCT_ID,
				ClassGuid = GenerateGamingInputClassGuid(),
				
				// Capabilities
				AxeCount = AXES_COUNT,
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
				InterfacePath = "",
				HardwareIds = "",
				ParentDeviceId = "",
				
				// Version info
				DriverVersion = GAMING_INPUT_VERSION,
				HardwareRevision = 1,
				FirmwareRevision = 1
			};
			
			// Generate CommonIdentifier for device grouping
			GenerateCommonIdentifier(deviceInfo);
			
			return deviceInfo;
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
				Debug.WriteLine($"DevicesGamingInput: Detected OS version: {Environment.OSVersion.Version}");
				Debug.WriteLine($"DevicesGamingInput: Platform: {Environment.OSVersion.Platform}");
				Debug.WriteLine($"DevicesGamingInput: Is64BitOperatingSystem: {Environment.Is64BitOperatingSystem}");
				Debug.WriteLine($"DevicesGamingInput: Is64BitProcess: {Environment.Is64BitProcess}");
				Debug.WriteLine($"DevicesGamingInput: CLR Version: {Environment.Version}");
				Debug.WriteLine($"DevicesGamingInput: Running as Administrator: {IsRunningAsAdministrator()}");
				Debug.WriteLine("DevicesGamingInput: Testing Gaming Input API accessibility (this is the definitive test)...");
				
				// Test 1: Check if Windows.Gaming.Input namespace is available
				var gamingInputType = typeof(Windows.Gaming.Input.Gamepad);
				Debug.WriteLine($"DevicesGamingInput: Gaming Input type loaded: {gamingInputType.FullName}");
				
				// Test 2: Try to access the Gamepads collection
				var gamepads = Windows.Gaming.Input.Gamepad.Gamepads;
				Debug.WriteLine($"DevicesGamingInput: Gaming Input API is accessible! Found {gamepads.Count} gamepads");
				
				// Provide guidance if no gamepads found
				if (gamepads.Count == 0)
				{
					Debug.WriteLine("DevicesGamingInput: No gamepads found in initial check.");
					if (!IsRunningAsAdministrator())
					{
						Debug.WriteLine("DevicesGamingInput: WARNING - Running without Administrator privileges.");
						Debug.WriteLine("DevicesGamingInput: GamingInput may require elevated privileges for device access.");
						Debug.WriteLine("DevicesGamingInput: Consider running as Administrator if devices are not detected.");
					}
				}
				
				// Test 3: Try to register for gamepad events
				try
				{
					Windows.Gaming.Input.Gamepad.GamepadAdded += OnGamepadAdded;
					Windows.Gaming.Input.Gamepad.GamepadRemoved += OnGamepadRemoved;
					Debug.WriteLine("DevicesGamingInput: Successfully registered for gamepad events");
					
					// Immediately unregister to avoid memory leaks
					Windows.Gaming.Input.Gamepad.GamepadAdded -= OnGamepadAdded;
					Windows.Gaming.Input.Gamepad.GamepadRemoved -= OnGamepadRemoved;
				}
				catch (Exception eventEx)
				{
					Debug.WriteLine($"DevicesGamingInput: Could not register for events (may indicate limited access): {eventEx.Message}");
				}
				
				return true;
			}
			catch (System.TypeLoadException ex)
			{
				Debug.WriteLine($"DevicesGamingInput: Gaming Input types not available: {ex.Message}");
				Debug.WriteLine("DevicesGamingInput: This indicates Windows.Gaming.Input is not accessible");
				return false;
			}
			catch (System.IO.FileNotFoundException ex)
			{
				Debug.WriteLine($"DevicesGamingInput: Gaming Input assemblies not found: {ex.Message}");
				Debug.WriteLine("DevicesGamingInput: Required Windows Runtime components are missing");
				return false;
			}
			catch (System.PlatformNotSupportedException ex)
			{
				Debug.WriteLine($"DevicesGamingInput: Platform not supported: {ex.Message}");
				Debug.WriteLine("DevicesGamingInput: Gaming Input requires Windows 10 1607+ or Windows 11");
				return false;
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"DevicesGamingInput: Gaming Input API is not accessible: {ex.Message}");
				Debug.WriteLine($"DevicesGamingInput: Exception type: {ex.GetType().Name}");
				Debug.WriteLine("DevicesGamingInput: This could mean:");
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
				Debug.WriteLine($"DevicesGamingInput: Win32Exception getting gamepad reading: {ex.Message}");
				return false;
			}
			catch (System.UnauthorizedAccessException ex)
			{
				Debug.WriteLine($"DevicesGamingInput: UnauthorizedAccessException getting gamepad reading (UWP restriction): {ex.Message}");
				return false;
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"DevicesGamingInput: Unexpected error getting gamepad reading: {ex.Message}");
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
				Debug.WriteLine($"DevicesGamingInput: Could not determine Administrator status: {ex.Message}");
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
		
		/// <summary>
		/// Generates CommonIdentifier for the device.
		/// GamingInput uses generic Microsoft VID/PID, so CommonIdentifier is VID/PID only.
		/// </summary>
		/// <param name="deviceInfo">Device information to process</param>
		private void GenerateCommonIdentifier(GamingInputDeviceInfo deviceInfo)
		{
			try
			{
				var vid = deviceInfo.VendorId > 0 ? $"{deviceInfo.VendorId:X4}" : "0000";
				var pid = deviceInfo.ProductId > 0 ? $"{deviceInfo.ProductId:X4}" : "0000";
				
				// GamingInput uses generic Microsoft VID/PID for all devices
				// No MI or COL information available from GamingInput API
				deviceInfo.CommonIdentifier = $"VID_{vid}&PID_{pid}";
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"DevicesGamingInput: Error generating CommonIdentifier: {ex.Message}");
				deviceInfo.CommonIdentifier = "VID_0000&PID_0000";
			}
		}

		#endregion

		#region Private Helper Methods - Logging

		/// <summary>
		/// Logs when Gaming Input is not available.
		/// </summary>
		private void LogNoGamingInputAvailable()
		{
			Debug.WriteLine("DevicesGamingInput: Gaming Input is not available on this system");
			Debug.WriteLine("DevicesGamingInput: Gaming Input requires Windows 10+ and is not available on this system");
			Debug.WriteLine("\nDevicesGamingInput: Gaming Input gamepads found: 0, Online: 0, Offline/Failed: 0\n");
		}

		/// <summary>
		/// Logs detection guidance based on results and privilege level.
		/// </summary>
		private void LogDetectionGuidance(int deviceCount, bool isAdmin)
		{
			if (deviceCount > 0)
				return;
				
			if (!isAdmin)
			{
				Debug.WriteLine("DevicesGamingInput: No devices found in normal user mode.");
				Debug.WriteLine("DevicesGamingInput: RECOMMENDATION: Try running as Administrator for full GamingInput access.");
				Debug.WriteLine("DevicesGamingInput: Right-click the application and select 'Run as administrator'.");
				Debug.WriteLine("DevicesGamingInput: No GamingInput devices detected in normal user mode.");
				Debug.WriteLine("DevicesGamingInput: For full GamingInput support, run as Administrator.");
			}
			else
			{
				Debug.WriteLine("DevicesGamingInput: No devices found even with Administrator privileges.");
				Debug.WriteLine("DevicesGamingInput: This indicates no GamingInput-compatible controllers are connected.");
				Debug.WriteLine("DevicesGamingInput: No GamingInput devices found (Administrator mode).");
				Debug.WriteLine("DevicesGamingInput: Controllers may only support DirectInput/XInput.");
			}
		}

		/// <summary>
		/// Logs detailed information about a detected device.
		/// </summary>
		private void LogDeviceInfo(GamingInputDeviceInfo deviceInfo, int deviceNumber)
		{
			// Note: GamingInput API does not provide DeviceId, InterfacePath, or HardwareIds - these are always empty
			Debug.WriteLine($"\n{deviceNumber}. DevicesGamingInputInfo: " +
				$"CommonIdentifier: {deviceInfo.CommonIdentifier}, " +
				$"GamepadIndex: {deviceInfo.GamepadIndex}, " +
				$"InstanceGuid: {deviceInfo.InstanceGuid}, " +
				$"ProductGuid: {deviceInfo.ProductGuid}, " +
				$"InstanceName: {deviceInfo.InstanceName}, " +
				$"ProductName: {deviceInfo.ProductName}, " +
				$"DeviceType: {deviceInfo.DeviceType}, " +
				$"DeviceTypeName: {deviceInfo.DeviceTypeName}, " +
				$"Timestamp: {deviceInfo.LastTimestamp}");

			Debug.WriteLine($"DevicesGamingInputInfo Identification: " +
				$"VidPidString: {deviceInfo.VidPidString}, " +
				$"VendorId: {deviceInfo.VendorId} (0x{deviceInfo.VendorId:X4}), " +
				$"ProductId: {deviceInfo.ProductId} (0x{deviceInfo.ProductId:X4})");

			Debug.WriteLine($"DevicesGamingInputInfo Capabilities: " +
				$"AxeCount: {deviceInfo.AxeCount}, " +
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
	}
}
		/// Event handler for gamepad removed events (used for testing event registration).
		///
