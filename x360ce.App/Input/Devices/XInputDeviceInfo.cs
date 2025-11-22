using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using x360ce.App.Input.States;

namespace x360ce.App.Input.Devices
{
	/// <summary>
	/// XInput device container with device metadata and live XInput controller object.
	/// Note: XInput API abstracts hardware details and reports generic Microsoft VID/PID (045E:028E)
	/// regardless of actual manufacturer. Use DirectInput or RawInput for real hardware identification.
	/// </summary>
	public class XInputDeviceInfo : IDisposable
	{
		// Identity
		public Guid InstanceGuid { get; set; }
		public string InstanceName { get; set; }
		public Guid ProductGuid { get; set; }
		public string ProductName { get; set; }
		
		// Device classification
		public int DeviceType { get; set; }
		public int DeviceSubtype { get; set; }
		public string DeviceTypeName { get; set; }
		
		// HID information
		public int Usage { get; set; }
		public int UsagePage { get; set; }
		
		// Hardware identification (XInput uses generic Microsoft values)
		public int VendorId { get; set; }
		public int ProductId { get; set; }
		public string CommonIdentifier { get; set; }
		public string InputType { get; set; }
        public ListInputState ListInputState { get; set; }

        // Capabilities
        public int AxeCount { get; set; }
		public int SliderCount { get; set; }
		public int ButtonCount { get; set; }
		public int PovCount { get; set; }
		public bool HasForceFeedback { get; set; }
		
		// Version information
		public int DriverVersion { get; set; }
		public int HardwareRevision { get; set; }
		public int FirmwareRevision { get; set; }
		
		// XInput-specific
		public UserIndex XInputSlot { get; set; }
		public int SlotIndex { get; set; }
		public uint LastPacketNumber { get; set; }
		public bool IsOnline { get; set; }
		
		// Unused properties (XInput doesn't provide these)
		public Guid ClassGuid { get; set; }
		public string HardwareIds { get; set; }
		public string DeviceId { get; set; }
		public string ParentDeviceId { get; set; }
		public string InterfacePath { get; set; }
		
		/// <summary>
		/// The actual XInput controller object for reading input.
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
		
		public void Dispose()
		{
			XInputDevice = null;
		}
	}

	/// <summary>
	/// Self-contained XInput device enumeration with minimal external dependencies.
	/// Discovers XInput-compatible controllers and returns live controller objects for input reading.
	/// </summary>
	internal class XInputDevice
	{
		// XInput API constants
		private const int MaxXInputControllers = 4;
		private const int XInputVendorId = 0x045E;
		private const int XInputProductId = 0x028E;
		private const int XInputAxeCount = 6;
		private const int XInputSliderCount = 0;
		private const int XInputButtonCount = 15;
		private const int XInputPovCount = 0;
		private const int XInputVersion = 0x0104;
		private const int GameControlsUsage = 0x05;
		private const int GenericDesktopUsagePage = 0x01;
		
		private static readonly byte[] XInputSlotGuidBase = { 0x58, 0x49, 0x4E, 0x50, 0x55, 0x54, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
		private static readonly Guid XInputProductGuid = new Guid("58494E50-5554-5052-4F44-000000000000");

		/// <summary>
		/// Enumerates all XInput devices and returns list with live controller objects.
		/// Call Dispose() on each XInputDeviceInfo when no longer needed.
		/// </summary>
		public List<XInputDeviceInfo> GetXInputDeviceInfoList()
		{
			var stopwatch = Stopwatch.StartNew();
			var deviceList = new List<XInputDeviceInfo>();

			try
			{
				Debug.WriteLine("\n" + new string('-', 109) + "\n");
				Debug.WriteLine("XInputDevice: Starting XInput device enumeration...");
				
				if (!EnsureXInputLibraryLoaded())
				{
					Debug.WriteLine("XInputDevice: XInput library not available");
					LogSummary(deviceList, stopwatch);
					return deviceList;
				}
				
				Debug.WriteLine("XInputDevice: XInput library loaded");
				
				for (int slotIndex = 0; slotIndex < MaxXInputControllers; slotIndex++)
				{
					var deviceInfo = TryCreateXInputDevice(slotIndex);
					if (deviceInfo != null)
					{
						deviceList.Add(deviceInfo);
						LogDeviceInfo(deviceInfo, deviceList.Count);
					}
				}
				
				LogSummary(deviceList, stopwatch);
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"XInputDevice: Fatal error: {ex.Message}");
			}

			return deviceList;
		}
		
		/// <summary>
		/// Attempts to create XInputDeviceInfo for specified slot.
		/// Returns null if no controller connected or error occurs.
		/// </summary>
		private XInputDeviceInfo TryCreateXInputDevice(int slotIndex)
		{
			try
			{
				var userIndex = (UserIndex)slotIndex;
				var controller = new Controller(userIndex);
				
				// Early exit if controller not connected
				if (!controller.GetState(out State controllerState))
				{
					Debug.WriteLine($"\n{slotIndex + 1}. XInputDevice: No controller in slot {slotIndex}");
					return null;
				}
				
				var slotGuidBytes = (byte[])XInputSlotGuidBase.Clone();
				slotGuidBytes[15] = (byte)slotIndex;
				
				return new XInputDeviceInfo
				{
					InstanceGuid = new Guid(slotGuidBytes),
					InstanceName = $"XInput Controller {slotIndex + 1}",
					ProductGuid = XInputProductGuid,
					ProductName = "XInput Controller",
					DeviceType = (int)SharpDX.DirectInput.DeviceType.Gamepad,
					DeviceSubtype = 1,
					DeviceTypeName = "XInput Controller",
					Usage = GameControlsUsage,
					UsagePage = GenericDesktopUsagePage,
					VendorId = XInputVendorId,
					ProductId = XInputProductId,
					CommonIdentifier = $"VID_{XInputVendorId:X4}&PID_{XInputProductId:X4}",
					InputType = "XInput",
					AxeCount = XInputAxeCount,
					SliderCount = XInputSliderCount,
					ButtonCount = XInputButtonCount,
					PovCount = XInputPovCount,
					HasForceFeedback = true,
					DriverVersion = XInputVersion,
					HardwareRevision = 1,
					FirmwareRevision = 1,
					XInputSlot = userIndex,
					SlotIndex = slotIndex,
					XInputDevice = controller,
					IsOnline = true,
					LastPacketNumber = (uint)controllerState.PacketNumber,
					DeviceId = "",
					InterfacePath = XInputProductGuid.ToString(),
					HardwareIds = ""
				};
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"XInputDevice: Error in slot {slotIndex}: {ex.Message}");
				return null;
			}
		}
		
		/// <summary>
		/// Logs device information for debugging.
		/// </summary>
		private void LogDeviceInfo(XInputDeviceInfo deviceInfo, int deviceIndex)
		{
			Debug.WriteLine($"\n{deviceIndex}. DevicesXInputInfo: " +
				$"CommonIdentifier (generated): {deviceInfo.CommonIdentifier}, " +
				$"SlotIndex: {deviceInfo.SlotIndex}, " +
				$"XInputSlot: {deviceInfo.XInputSlot}, " +
				$"InstanceGuid (generated): {deviceInfo.InstanceGuid}, " +
				$"ProductGuid (generated): {deviceInfo.ProductGuid}, " +
				$"InstanceName (generated): {deviceInfo.InstanceName}, " +
				$"ProductName (generated): {deviceInfo.ProductName}, " +
				$"DeviceType (generated): {deviceInfo.DeviceType}, " +
				$"DeviceTypeName (generated): {deviceInfo.DeviceTypeName}, " +
				$"PacketNumber: {deviceInfo.LastPacketNumber}, " +
				$"VidPidString (generated): {deviceInfo.VidPidString}, " +
				$"VendorId (generated): {deviceInfo.VendorId} (0x{deviceInfo.VendorId:X4}), " +
				$"ProductId (generated): {deviceInfo.ProductId} (0x{deviceInfo.ProductId:X4})");

			Debug.WriteLine($"DevicesXInputInfo Capabilities (generated): " +
				$"AxeCount: {deviceInfo.AxeCount}, " +
				$"SliderCount: {deviceInfo.SliderCount}, " +
				$"ButtonCount: {deviceInfo.ButtonCount}, " +
				$"PovCount: {deviceInfo.PovCount}, " +
				$"HasForceFeedback: {deviceInfo.HasForceFeedback}");
			
			Debug.WriteLine($"DevicesXInputInfo Note: " +
				$"XInput API uses generic Microsoft VID/PID (045E:028E) for all controllers - use DirectInput or RawInput for actual hardware identification");
		}
		
		/// <summary>
		/// Logs enumeration summary.
		/// </summary>
		private void LogSummary(List<XInputDeviceInfo> deviceList, Stopwatch stopwatch)
		{
			stopwatch.Stop();
			var connectedCount = deviceList.Count;
			var offlineCount = deviceList.Count(d => !d.IsOnline);
			
			Debug.WriteLine($"\nDevicesXInput: ({stopwatch.ElapsedMilliseconds} ms) " +
				$"Found: {connectedCount}/{MaxXInputControllers}, " +
				$"Online: {connectedCount - offlineCount}, " +
				$"Offline: {offlineCount}\n");
		}
		
		/// <summary>
		/// Disposes all XInput devices in the list.
		/// </summary>
		public static void DisposeDeviceList(List<XInputDeviceInfo> deviceList)
		{
			if (deviceList == null) return;
			
			Debug.WriteLine($"XInputDevice: Disposing {deviceList.Count} devices");
			
			foreach (var deviceInfo in deviceList)
			{
				try
				{
					deviceInfo?.Dispose();
				}
				catch (Exception ex)
				{
					Debug.WriteLine($"XInputDevice: Error disposing {deviceInfo?.InstanceName}: {ex.Message}");
				}
			}
		}
		
		/// <summary>
		/// Ensures XInput library is loaded and available.
		/// </summary>
		private bool EnsureXInputLibraryLoaded()
		{
			try
			{
				if (SharpDX.XInput.Controller.IsLoaded)
					return true;
				
				Debug.WriteLine("XInputDevice: Loading XInput library...");
				
				var libraries = new[] { "xinput1_4.dll", "xinput1_3.dll", "xinput9_1_0.dll" };
				
				foreach (var library in libraries)
				{
					SharpDX.XInput.Controller.ReLoadLibrary(library, out Exception loadError);
					
					if (SharpDX.XInput.Controller.IsLoaded)
					{
						Debug.WriteLine($"XInputDevice: Loaded {library}");
						return true;
					}
					
					Debug.WriteLine($"XInputDevice: Failed {library}: {loadError?.Message}");
				}
				
				return false;
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"XInputDevice: Library load exception: {ex.Message}");
				return false;
			}
		}
	}
}
