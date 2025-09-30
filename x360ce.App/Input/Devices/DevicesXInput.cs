using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace x360ce.App.Input.Devices
{
	/// <summary>
	/// XInput device container with both device information and the actual XInput controller object.
	/// Contains comprehensive device metadata plus the live XInput controller for input reading.
	/// </summary>
	public class XInputDeviceInfo : IDisposable
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
		
		// XInput-specific properties
		public UserIndex XInputSlot { get; set; }
		public int SlotIndex { get; set; }
		public uint LastPacketNumber { get; set; }
		
		/// <summary>
		/// Note: XInput API does not provide native friendly names or manufacturer information.
		/// XInput abstracts hardware details and always reports generic "Microsoft" VID/PID values
		/// regardless of the actual controller manufacturer (Sony, Nintendo, etc.).
		/// Use DirectInput or RawInput for real manufacturer information.
		/// </summary>
		
		/// <summary>
		/// The actual XInput device object for reading input.
		/// </summary>
		public Controller XInputDevice { get; set; }
		
		/// <summary>
		/// Display name combining slot and name for easy identification.
		/// </summary>
		public string DisplayName => $"XInput Slot {SlotIndex + 1} - {InstanceName}";
		
		/// <summary>
		/// VID/PID string in standard format for hardware identification.
		/// </summary>
		public string VidPidString => $"VID_{VendorId:X4}&PID_{ProductId:X4}";
		
		/// <summary>
		/// Dispose the XInput controller when no longer needed.
		/// </summary>
		public void Dispose()
		{
			// XInput devices don't need explicit disposal, but we clear the reference
			XInputDevice = null;
		}
	}

	/// <summary>
	/// XInput device enumeration and management class.
	/// Self-contained implementation with minimal external dependencies.
	/// Provides functionality to discover and list XInput devices including Xbox controllers.
	/// Returns live XInput controller objects that can be used for input reading.
	/// </summary>
	internal class DevicesXInput
	{
		// XInput standard constants
		private const int MaxXInputControllers = 4;
		private const int XInputVendorId = 0x045E; // Microsoft
		private const int XInputProductId = 0x028E; // Xbox 360 Controller
		private const int XInputAxeCount = 6; // Left Stick X/Y, Right Stick X/Y, Left/Right Triggers
		private const int XInputButtonCount = 15; // A, B, X, Y, LB, RB, Back, Start, LS, RS, DPad (4), Guide
		private const int XInputPovCount = 0; // XInput maps DPad to buttons, not POV
		private const int XInputVersion = 0x0104; // Version 1.4
		private const int GameControlsUsage = 0x05;
		private const int GenericDesktopUsagePage = 0x01;
		
		// GUID constants
		private static readonly byte[] XInputSlotGuidBase = { 0x58, 0x49, 0x4E, 0x50, 0x55, 0x54, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
		private static readonly Guid XInputProductGuid = new Guid("58494E50-5554-5052-4F44-000000000000"); // "XINPUTPROD"

		/// <summary>
		/// Creates a public list of XInput devices (Xbox controllers) with live controller objects and logs their properties.
		/// This method enumerates all available XInput controllers and outputs detailed information for debugging.
		/// </summary>
		/// <returns>List of XInputDeviceInfo objects containing both device information and live XInput controller objects</returns>
		/// <remarks>
		/// This method performs comprehensive XInput device enumeration:
		/// • Discovers all XInput-compatible devices (Xbox 360/One controllers)
		/// • Creates XInputDeviceInfo objects with device information AND live XInput controller objects
		/// • Logs detailed device properties using Debug.WriteLine for diagnostics
		/// • Limited to 4 controllers maximum (XInput API limitation)
		/// • Provides device capability information for Xbox controllers
		/// • Keeps XInput controllers alive for immediate input reading
		/// • Is self-contained with minimal external dependencies
		///
		/// IMPORTANT: The returned XInputDeviceInfo objects contain live XInput controllers.
		/// Call Dispose() on each XInputDeviceInfo when no longer needed to free resources.
		/// </remarks>
		public List<XInputDeviceInfo> GetXInputDeviceList()
		{
			var stopwatch = Stopwatch.StartNew();
			var deviceList = new List<XInputDeviceInfo>();

			try
			{
				Debug.WriteLine("\n-----------------------------------------------------------------------------------------------------------------\n\n" +
					"DevicesXInput: Starting XInput device enumeration...");
				
				// Early exit if XInput library cannot be loaded
				if (!EnsureXInputLibraryLoaded())
				{
					Debug.WriteLine("DevicesXInput: XInput library not found or could not be loaded");
					Debug.WriteLine("\nDevicesXInput: XInput controllers found: 0/4, Online: 0, Offline/Failed: 0\n");
					return deviceList;
				}
				
				Debug.WriteLine("DevicesXInput: XInput library is loaded and available");
				
				// Enumerate all XInput controller slots (0-3)
				for (int slotIndex = 0; slotIndex < MaxXInputControllers; slotIndex++)
				{
					var deviceInfo = TryCreateXInputDevice(slotIndex);
					if (deviceInfo != null)
					{
						deviceList.Add(deviceInfo);
						LogDeviceInfo(deviceInfo, deviceList.Count);
					}
				}
				
				// Log summary
				stopwatch.Stop();
				var connectedCount = deviceList.Count;
				var offlineCount = deviceList.Count(d => !d.IsOnline);
				
				Debug.WriteLine($"\nDevicesXInput: ({(int)Math.Round(stopwatch.Elapsed.TotalMilliseconds)} ms) " +
					$"Input Devices found: {connectedCount}/{MaxXInputControllers}, " +
					$"Online: {connectedCount - offlineCount}, " +
					$"Offline/Failed: {offlineCount}\n");
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"DevicesXInput: Fatal error during XInput device enumeration: {ex.Message}");
				Debug.WriteLine($"DevicesXInput: Stack trace: {ex.StackTrace}");
			}

			return deviceList;
		}
		
		/// <summary>
		/// Attempts to create an XInputDeviceInfo for the specified slot.
		/// </summary>
		/// <param name="slotIndex">The XInput slot index (0-3)</param>
		/// <returns>XInputDeviceInfo if controller is connected, null otherwise</returns>
		private XInputDeviceInfo TryCreateXInputDevice(int slotIndex)
		{
			try
			{
				var userIndex = (UserIndex)slotIndex;
				var controller = new Controller(userIndex);
				
				// Test if controller is connected by attempting to get state
				if (!SafeGetControllerState(controller, out State controllerState))
				{
					Debug.WriteLine($"\n{slotIndex + 1}. DevicesXInput: No controller found in XInput slot {slotIndex}");
					return null;
				}
				
				// Create device info with all properties
				var slotGuidBytes = (byte[])XInputSlotGuidBase.Clone();
				slotGuidBytes[15] = (byte)slotIndex;
				
				return new XInputDeviceInfo
				{
					// Identity
					InstanceGuid = new Guid(slotGuidBytes),
					InstanceName = $"XInput Controller {slotIndex + 1}",
					ProductGuid = XInputProductGuid,
					ProductName = "XInput Controller",
					
					// Device type
					DeviceType = (int)SharpDX.DirectInput.DeviceType.Gamepad,
					DeviceSubtype = 1, // Standard gamepad
					DeviceTypeName = "XInput Controller",
					
					// HID information
					Usage = GameControlsUsage,
					UsagePage = GenericDesktopUsagePage,
					
					// Hardware identification (XInput standard values)
					VendorId = XInputVendorId,
					ProductId = XInputProductId,
					DeviceId = "", // XInput doesn't provide native device ID
					InterfacePath = "", // XInput doesn't provide native interface path
					HardwareIds = "", // XInput doesn't provide native hardware IDs
					CommonIdentifier = $"VID_{XInputVendorId:X4}&PID_{XInputProductId:X4}",
					
					// Capabilities
					AxeCount = XInputAxeCount,
					ButtonCount = XInputButtonCount,
					PovCount = XInputPovCount,
					HasForceFeedback = true,
					
					// Version information
					DriverVersion = XInputVersion,
					HardwareRevision = 1,
					FirmwareRevision = 1,
					
					// XInput-specific
					XInputSlot = userIndex,
					SlotIndex = slotIndex,
					XInputDevice = controller,
					IsOnline = true,
					LastPacketNumber = (uint)controllerState.PacketNumber
				};
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"DevicesXInput: Error checking XInput slot {slotIndex}: {ex.Message}");
				return null;
			}
		}
		
		/// <summary>
		/// Logs comprehensive device information for debugging.
		/// </summary>
		/// <param name="deviceInfo">The device to log</param>
		/// <param name="deviceIndex">The device index in the list</param>
		private void LogDeviceInfo(XInputDeviceInfo deviceInfo, int deviceIndex)
		{
			// Note: XInput API does not provide DeviceId, InterfacePath, or HardwareIds - these are always empty
			Debug.WriteLine($"\n{deviceIndex}. DevicesXInputInfo: " +
				$"CommonIdentifier: {deviceInfo.CommonIdentifier}, " +
				$"SlotIndex: {deviceInfo.SlotIndex}, " +
				$"XInputSlot: {deviceInfo.XInputSlot}, " +
				$"InstanceGuid: {deviceInfo.InstanceGuid}, " +
				$"ProductGuid: {deviceInfo.ProductGuid}, " +
				$"InstanceName: {deviceInfo.InstanceName}, " +
				$"ProductName: {deviceInfo.ProductName}, " +
				$"DeviceType: {deviceInfo.DeviceType}, " +
				$"DeviceTypeName: {deviceInfo.DeviceTypeName}, " +
				$"PacketNumber: {deviceInfo.LastPacketNumber}, " +
				$"VidPidString: {deviceInfo.VidPidString}, " +
				$"VendorId: {deviceInfo.VendorId} (0x{deviceInfo.VendorId:X4}), " +
				$"ProductId: {deviceInfo.ProductId} (0x{deviceInfo.ProductId:X4})");

			Debug.WriteLine($"DevicesXInputInfo Capabilities: " +
				$"AxeCount: {deviceInfo.AxeCount}, " +
				$"ButtonCount: {deviceInfo.ButtonCount}, " +
				$"PovCount: {deviceInfo.PovCount}, " +
				$"HasForceFeedback: {deviceInfo.HasForceFeedback}");
			
			Debug.WriteLine($"DevicesXInputInfo Note: " +
				$"XInput API uses generic Microsoft VID/PID (045E:028E) for all controllers - use DirectInput or RawInput for actual hardware identification");
		}
		
		/// <summary>
		/// Disposes all XInput devices in the provided list to free resources.
		/// Call this method when the device list is no longer needed.
		/// </summary>
		/// <param name="deviceList">List of XInputDeviceInfo objects to dispose</param>
		public static void DisposeDeviceList(List<XInputDeviceInfo> deviceList)
		{
			if (deviceList == null) return;
			
			Debug.WriteLine($"DevicesXInput: Disposing {deviceList.Count} XInput devices...");
			
			foreach (var deviceInfo in deviceList)
			{
				try
				{
					if (deviceInfo != null)
					{
						Debug.WriteLine($"DevicesXInput: Disposing device - {deviceInfo.InstanceName}");
						deviceInfo.Dispose();
					}
				}
				catch (Exception ex)
				{
					Debug.WriteLine($"DevicesXInput: Error disposing device {deviceInfo?.InstanceName}: {ex.Message}");
				}
			}
			
			Debug.WriteLine("DevicesXInput: All XInput devices disposed.");
		}
		
		/// <summary>
		/// Ensures XInput library is loaded and available for use.
		/// Uses the x360ce custom XInput library loading system.
		/// </summary>
		/// <returns>True if XInput library is loaded and functional</returns>
		private bool EnsureXInputLibraryLoaded()
		{
			try
			{
				// Check if XInput library is already loaded
				if (SharpDX.XInput.Controller.IsLoaded)
				{
					Debug.WriteLine("DevicesXInput: XInput library already loaded");
					return true;
				}
				
				Debug.WriteLine("DevicesXInput: XInput library not loaded, attempting to load...");
				
				// Try loading XInput libraries in order of preference
				var libraries = new[] { "xinput1_4.dll", "xinput1_3.dll", "xinput9_1_0.dll" };
				
				foreach (var library in libraries)
				{
					SharpDX.XInput.Controller.ReLoadLibrary(library, out Exception loadError);
					
					if (SharpDX.XInput.Controller.IsLoaded)
					{
						Debug.WriteLine($"DevicesXInput: Successfully loaded {library}");
						return true;
					}
					
					Debug.WriteLine($"DevicesXInput: Failed to load {library}: {loadError?.Message}");
				}
				
				Debug.WriteLine("DevicesXInput: All XInput library loading attempts failed");
				return false;
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"DevicesXInput: Exception during XInput library loading: {ex.Message}");
				return false;
			}
		}
		
		/// <summary>
		/// Safely gets controller state with proper error handling.
		/// </summary>
		/// <param name="controller">The controller to get state from</param>
		/// <param name="state">Output state</param>
		/// <returns>True if controller is connected and state was retrieved</returns>
		private bool SafeGetControllerState(Controller controller, out State state)
		{
			state = new State();
			
			try
			{
				return controller.GetState(out state);
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"DevicesXInput: Error getting controller state: {ex.Message}");
				return false;
			}
		}
	}
}
