using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using x360ce.App.Input.States;
namespace x360ce.App.Input.Devices
{
	/// <summary>
	/// XInput device container with device metadata and live XInput controller object.
	/// Note: XInput API abstracts hardware details and reports generic Microsoft VID/PID (045E:028E)
	/// regardless of actual manufacturer. Use DirectInput or RawInput for real hardware identification.
	/// </summary>
	public class XInputDeviceInfo : InputDeviceInfo, IDisposable
	{
		// XInput-specific
		public UserIndex XInputSlot { get; set; }
		public int SlotIndex { get; set; }
		public uint LastPacketNumber { get; set; }

		/// <summary>
		/// Display name combining slot and name for easy identification.
		/// </summary>
		public string DisplayName => $"XInput Slot {SlotIndex + 1} - {InstanceName}";

        /// <summary>
        /// The actual XInput controller object for reading input.
        /// </summary>
        public Controller XInputDevice { get; set; }

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
		private const int XInputPovCount = 1;
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
			var deviceList = new List<XInputDeviceInfo>();

			try
			{
				if (!EnsureXInputLibraryLoaded())
				{
					Debug.WriteLine("XInputDevice: XInput library not available");
					return deviceList;
				}
				
				for (int slotIndex = 0; slotIndex < MaxXInputControllers; slotIndex++)
				{
					var deviceInfo = TryCreateXInputDevice(slotIndex);
					if (deviceInfo != null)
					{
						deviceList.Add(deviceInfo);
					}
				}
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
					return null;
				}

				            // Convert initial state to ListInputState for capability checks
				            var listInputState = CustomInputState.ConvertXInputStateToListInputState(controllerState);

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
					HardwareIds = "",
					// Initial application profile state
                    IsEnabled = false,
                    AssignedToPad1 = false,
                    AssignedToPad2 = false,
                    AssignedToPad3 = false,
                    AssignedToPad4 = false,
                    CustomInputState = listInputState
                };
   }
   catch (Exception ex)
			{
				Debug.WriteLine($"XInputDevice: Error in slot {slotIndex}: {ex.Message}");
				return null;
			}
		}
		
		/// <summary>
		/// Disposes all XInput devices in the list.
		/// </summary>
		public static void DisposeDeviceList(List<XInputDeviceInfo> deviceList)
		{
			if (deviceList == null) return;
			
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
				
				var libraries = new[] { "xinput1_4.dll", "xinput1_3.dll", "xinput9_1_0.dll" };
				
				foreach (var library in libraries)
				{
					SharpDX.XInput.Controller.ReLoadLibrary(library, out Exception loadError);
					
					if (SharpDX.XInput.Controller.IsLoaded)
					{
						return true;
					}
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
