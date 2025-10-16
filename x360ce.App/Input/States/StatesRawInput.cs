using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using x360ce.App.Input.Devices;

namespace x360ce.App.Input.States
{
	/// <summary>
	/// Provides NON-BLOCKING RawInput device state reading using Windows WM_INPUT messages.
	/// This is the ONLY way to read RawInput without blocking other input methods.
	/// </summary>
	/// <remarks>
	/// NON-BLOCKING ARCHITECTURE:
	/// • Uses Windows Raw Input API (RegisterRawInputDevices + WM_INPUT messages)
	/// • Message-based system runs in background without blocking
	/// • Caches latest states from WM_INPUT messages
	/// • Never opens HID device handles directly
	/// • Safe for concurrent use with DirectInput, XInput, GamingInput
	/// 
	/// This is a lightweight, self-contained implementation that doesn't depend on
	/// RawInputProcessor or any other processor classes.
	/// </remarks>
	internal class StatesRawInput : IDisposable
	{
		#region Windows Raw Input API

		[DllImport("user32.dll", SetLastError = true)]
		private static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] pRawInputDevices, uint uiNumDevices, uint cbSize);

		[DllImport("user32.dll", SetLastError = true)]
		private static extern uint GetRawInputData(IntPtr hRawInput, uint uiCommand, IntPtr pData, ref uint pcbSize, uint cbSizeHeader);

		private const uint RIM_TYPEHID = 2;
		private const uint RID_INPUT = 0x10000003;
		private const uint RIDEV_INPUTSINK = 0x00000100;
		private const ushort USAGE_PAGE_GENERIC_DESKTOP = 0x01;
		private const ushort USAGE_JOYSTICK = 0x04;
		private const ushort USAGE_GAMEPAD = 0x05;
		private const ushort USAGE_MULTI_AXIS = 0x08;
		private const int RAWHID_DATA_OFFSET = 8; // Offset to HID data after RAWINPUTHEADER

		[StructLayout(LayoutKind.Sequential)]
		private struct RAWINPUTDEVICE
		{
			public ushort usUsagePage;
			public ushort usUsage;
			public uint dwFlags;
			public IntPtr hwndTarget;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct RAWINPUTHEADER
		{
			public uint dwType;
			public uint dwSize;
			public IntPtr hDevice;
			public IntPtr wParam;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct RAWINPUT
		{
			public RAWINPUTHEADER header;
			public RAWHID hid;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct RAWHID
		{
			public uint dwSizeHid;
			public uint dwCount;
			// Variable length data follows
		}

		#endregion

		#region Message Window for WM_INPUT

		/// <summary>
		/// Hidden window that receives WM_INPUT messages without blocking.
		/// </summary>
		private class RawInputMessageWindow : System.Windows.Forms.NativeWindow, IDisposable
		{
			private const int WM_INPUT = 0x00FF;
			private readonly StatesRawInput _parent;

			public RawInputMessageWindow(StatesRawInput parent)
			{
				_parent = parent;
				CreateHandle(new System.Windows.Forms.CreateParams
				{
					Caption = "RawInputStatesWindow",
					Style = 0,
					ExStyle = 0,
					ClassStyle = 0,
					X = 0,
					Y = 0,
					Width = 0,
					Height = 0,
					Parent = IntPtr.Zero
				});
			}

			protected override void WndProc(ref System.Windows.Forms.Message m)
			{
				if (m.Msg == WM_INPUT)
				{
					_parent.ProcessRawInputMessage(m.LParam);
				}
				base.WndProc(ref m);
			}

			public void Dispose()
			{
				if (Handle != IntPtr.Zero)
					DestroyHandle();
			}
		}

		#endregion

		private RawInputMessageWindow _messageWindow;
		private readonly Dictionary<string, byte[]> _cachedStates = new Dictionary<string, byte[]>();
		private readonly Dictionary<IntPtr, string> _handleToPath = new Dictionary<IntPtr, string>();
		private bool _isInitialized;
		private bool _disposed;
		
		// Cached struct sizes to avoid repeated Marshal.SizeOf calls
		private static readonly int s_rawinputDeviceSize = Marshal.SizeOf(typeof(RAWINPUTDEVICE));
		private static readonly int s_rawinputHeaderSize = Marshal.SizeOf(typeof(RAWINPUTHEADER));

		/// <summary>
		/// Initializes the RawInput message receiver (non-blocking).
		/// </summary>
		public StatesRawInput()
		{
			try
			{
				// Create hidden window for WM_INPUT messages
				_messageWindow = new RawInputMessageWindow(this);

				// Register for ALL gaming input devices: Joystick (0x04), Gamepad (0x05), Multi-axis (0x08)
				// This ensures we receive WM_INPUT messages from Xbox controllers and other gaming devices
				var devices = new RAWINPUTDEVICE[3];
				
				// Joystick (0x04) - Flight sticks, racing wheels, Xbox controllers often report as this
				devices[0].usUsagePage = USAGE_PAGE_GENERIC_DESKTOP;
				devices[0].usUsage = USAGE_JOYSTICK;
				devices[0].dwFlags = RIDEV_INPUTSINK;
				devices[0].hwndTarget = _messageWindow.Handle;
				
				// Gamepad (0x05) - Standard gamepads
				devices[1].usUsagePage = USAGE_PAGE_GENERIC_DESKTOP;
				devices[1].usUsage = USAGE_GAMEPAD;
				devices[1].dwFlags = RIDEV_INPUTSINK;
				devices[1].hwndTarget = _messageWindow.Handle;
				
				// Multi-axis Controller (0x08) - Complex controllers with many axes
				devices[2].usUsagePage = USAGE_PAGE_GENERIC_DESKTOP;
				devices[2].usUsage = USAGE_MULTI_AXIS;
				devices[2].dwFlags = RIDEV_INPUTSINK;
				devices[2].hwndTarget = _messageWindow.Handle;

				bool success = RegisterRawInputDevices(devices, 3, (uint)s_rawinputDeviceSize);
				_isInitialized = success;
			}
			catch
			{
				_isInitialized = false;
			}
		}

		/// <summary>
		/// Processes WM_INPUT messages and caches device states (non-blocking).
		/// </summary>
		private void ProcessRawInputMessage(IntPtr lParam)
		{
			uint dwSize = 0;
			GetRawInputData(lParam, RID_INPUT, IntPtr.Zero, ref dwSize, (uint)s_rawinputHeaderSize);

			if (dwSize == 0)
				return;

			IntPtr buffer = Marshal.AllocHGlobal((int)dwSize);
			try
			{
				uint result = GetRawInputData(lParam, RID_INPUT, buffer, ref dwSize, (uint)s_rawinputHeaderSize);
				if (result != dwSize)
					return;

				var rawInput = Marshal.PtrToStructure<RAWINPUT>(buffer);
				if (rawInput.header.dwType != RIM_TYPEHID)
					return;

				// Extract HID report data
				int reportSize = (int)rawInput.hid.dwSizeHid;
				if (reportSize <= 0)
					return;

				byte[] report = new byte[reportSize];
				
				// Copy report data from buffer
				IntPtr reportPtr = IntPtr.Add(buffer, s_rawinputHeaderSize + RAWHID_DATA_OFFSET);
				Marshal.Copy(reportPtr, report, 0, reportSize);

				// Cache the report by device handle
				if (_handleToPath.TryGetValue(rawInput.header.hDevice, out string path))
				{
					_cachedStates[path] = report;
				}
			}
			finally
			{
				Marshal.FreeHGlobal(buffer);
			}
		}

		/// <summary>
		/// Returns cached RawInput device state (non-blocking).
		/// </summary>
		public byte[] GetRawInputDeviceState(RawInputDeviceInfo deviceInfo)
		{
			if (deviceInfo?.InterfacePath == null)
				return null;

			// Register device handle to path mapping (single lookup optimization)
			if (deviceInfo.DeviceHandle != IntPtr.Zero && !_handleToPath.ContainsKey(deviceInfo.DeviceHandle))
			{
				_handleToPath[deviceInfo.DeviceHandle] = deviceInfo.InterfacePath;
			}

			// Return cached state (non-blocking)
			return _cachedStates.TryGetValue(deviceInfo.InterfacePath, out byte[] cachedState) ? cachedState : null;
		}

		/// <summary>
		/// Returns cached RawInput device state and clears it (non-blocking).
		/// This ensures button detection only happens when NEW messages arrive between checks.
		/// </summary>
		public byte[] GetAndClearRawInputDeviceState(RawInputDeviceInfo deviceInfo)
		{
			if (deviceInfo?.InterfacePath == null)
				return null;

			// Register device handle to path mapping (single lookup optimization)
			if (deviceInfo.DeviceHandle != IntPtr.Zero && !_handleToPath.ContainsKey(deviceInfo.DeviceHandle))
			{
				_handleToPath[deviceInfo.DeviceHandle] = deviceInfo.InterfacePath;
			}

			// Get and remove cached state in one operation (non-blocking)
			if (!_cachedStates.TryGetValue(deviceInfo.InterfacePath, out byte[] cachedState))
				return null;
			
			_cachedStates.Remove(deviceInfo.InterfacePath);
			return cachedState;
		}

		/// <summary>
		/// Returns cached state by interface path (non-blocking).
		/// </summary>
		public byte[] GetRawInputState(string interfacePath)
		{
			return string.IsNullOrEmpty(interfacePath) ? null :
				(_cachedStates.TryGetValue(interfacePath, out byte[] cachedState) ? cachedState : null);
		}

		public void Dispose()
		{
			if (_disposed)
				return;

			_messageWindow?.Dispose();
			_cachedStates.Clear();
			_handleToPath.Clear();
			_disposed = true;
		}

		public string GetDiagnosticInfo()
		{
			var info = new System.Text.StringBuilder(256); // Pre-allocate capacity
			info.AppendLine("=== RawInput State Reader (Non-Blocking) ===");
			info.AppendLine($"Initialized: {_isInitialized}");
			info.AppendLine($"Cached States: {_cachedStates.Count}");
			info.AppendLine($"Tracked Devices: {_handleToPath.Count}");
			info.AppendLine();
			info.AppendLine("Implementation: WM_INPUT message-based (non-blocking)");
			info.AppendLine("✅ Safe for concurrent use with all input methods");
			return info.ToString();
		}
	}
}
