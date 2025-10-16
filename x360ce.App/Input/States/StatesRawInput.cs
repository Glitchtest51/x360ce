using System;
using System.Collections.Concurrent;
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
	
		[DllImport("kernel32.dll")]
		private static extern uint GetLastError();
	
		/// <summary>
		/// Gets the current state of a virtual key (including mouse buttons and keyboard keys).
		/// Returns non-zero if the key is currently pressed.
		/// </summary>
		[DllImport("user32.dll")]
		private static extern short GetAsyncKeyState(int vKey);
	
		// Virtual key codes for mouse buttons
		private const int VK_LBUTTON = 0x01;  // Left mouse button
		private const int VK_RBUTTON = 0x02;  // Right mouse button
		private const int VK_MBUTTON = 0x04;  // Middle mouse button
		private const int VK_XBUTTON1 = 0x05; // X1 mouse button
		private const int VK_XBUTTON2 = 0x06; // X2 mouse button

		private const uint RIM_TYPEMOUSE = 0;
		private const uint RIM_TYPEKEYBOARD = 1;
		private const uint RIM_TYPEHID = 2;
		private const uint RID_INPUT = 0x10000003;
		private const uint RIDEV_INPUTSINK = 0x00000100;
		private const ushort USAGE_PAGE_GENERIC_DESKTOP = 0x01;
		private const ushort USAGE_JOYSTICK = 0x04;
		private const ushort USAGE_GAMEPAD = 0x05;
		private const ushort USAGE_MULTI_AXIS = 0x08;
		// RAWHID_DATA_OFFSET removed - now computed dynamically using Marshal.SizeOf

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

		[StructLayout(LayoutKind.Sequential)]
		private struct RAWMOUSE
		{
			public ushort usFlags;
			public ushort usButtonFlags;
			public ushort usButtonData;
			public uint ulRawButtons;
			public int lLastX;
			public int lLastY;
			public uint ulExtraInformation;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct RAWKEYBOARD
		{
			public ushort MakeCode;
			public ushort Flags;
			public ushort Reserved;
			public ushort VKey;
			public uint Message;
			public uint ExtraInformation;
		}

		// Mouse button flags
		private const ushort RI_MOUSE_LEFT_BUTTON_DOWN = 0x0001;
		private const ushort RI_MOUSE_LEFT_BUTTON_UP = 0x0002;
		private const ushort RI_MOUSE_RIGHT_BUTTON_DOWN = 0x0004;
		private const ushort RI_MOUSE_RIGHT_BUTTON_UP = 0x0008;
		private const ushort RI_MOUSE_MIDDLE_BUTTON_DOWN = 0x0010;
		private const ushort RI_MOUSE_MIDDLE_BUTTON_UP = 0x0020;
		private const ushort RI_MOUSE_BUTTON_4_DOWN = 0x0040;
		private const ushort RI_MOUSE_BUTTON_4_UP = 0x0080;
		private const ushort RI_MOUSE_BUTTON_5_DOWN = 0x0100;
		private const ushort RI_MOUSE_BUTTON_5_UP = 0x0200;

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
		private readonly ConcurrentDictionary<string, byte[]> _cachedStates = new ConcurrentDictionary<string, byte[]>();
		private readonly ConcurrentDictionary<IntPtr, string> _handleToPath = new ConcurrentDictionary<IntPtr, string>();
		private readonly ConcurrentDictionary<IntPtr, byte> _mouseButtonStates = new ConcurrentDictionary<IntPtr, byte>();
		private readonly ConcurrentDictionary<IntPtr, byte[]> _keyboardKeyStates = new ConcurrentDictionary<IntPtr, byte[]>();
		private bool _isInitialized;
		private bool _disposed;
		
		// Cached struct sizes to avoid repeated Marshal.SizeOf calls
		private static readonly int s_rawinputDeviceSize = Marshal.SizeOf(typeof(RAWINPUTDEVICE));
		private static readonly int s_rawinputHeaderSize = Marshal.SizeOf(typeof(RAWINPUTHEADER));
		private static readonly int s_rawhidSize = Marshal.SizeOf(typeof(RAWHID));

		/// <summary>
		/// Initializes the RawInput message receiver (non-blocking).
		/// </summary>
		public StatesRawInput()
		{
			try
			{
				// Create hidden window for WM_INPUT messages
				_messageWindow = new RawInputMessageWindow(this);

				// Register for ALL input devices: Gaming devices, Keyboard, and Mouse
				// This ensures we receive WM_INPUT messages from all input device types
				var devices = new RAWINPUTDEVICE[5];
				
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
				
				// Keyboard (0x06) - All keyboard devices
				devices[3].usUsagePage = USAGE_PAGE_GENERIC_DESKTOP;
				devices[3].usUsage = 0x06;
				devices[3].dwFlags = RIDEV_INPUTSINK;
				devices[3].hwndTarget = _messageWindow.Handle;
				
				// Mouse (0x02) - All mouse devices
				devices[4].usUsagePage = USAGE_PAGE_GENERIC_DESKTOP;
				devices[4].usUsage = 0x02;
				devices[4].dwFlags = RIDEV_INPUTSINK;
				devices[4].hwndTarget = _messageWindow.Handle;
	
				bool success = RegisterRawInputDevices(devices, 5, (uint)s_rawinputDeviceSize);
				if (!success)
				{
					uint errorCode = GetLastError();
					System.Diagnostics.Debug.WriteLine($"StatesRawInput: RegisterRawInputDevices failed with error code: {errorCode}");
				}
				_isInitialized = success;
			}
			catch
			{
				_isInitialized = false;
			}
		}

		/// <summary>
		/// Processes WM_INPUT messages and caches device states (non-blocking).
		/// Handles HID devices (gamepads), mice, and keyboards.
		/// </summary>
		private void ProcessRawInputMessage(IntPtr lParam)
		{
			uint dwSize = 0;
			uint headerSize = (uint)s_rawinputHeaderSize;
			
			// First call: get required buffer size
			uint sizeResult = GetRawInputData(lParam, RID_INPUT, IntPtr.Zero, ref dwSize, headerSize);
			if (sizeResult == uint.MaxValue || dwSize == 0)
				return;
	
			IntPtr buffer = Marshal.AllocHGlobal((int)dwSize);
			try
			{
				// Second call: get actual data
				uint result = GetRawInputData(lParam, RID_INPUT, buffer, ref dwSize, headerSize);
				if (result == uint.MaxValue || result < headerSize)
					return;
	
				var rawInput = Marshal.PtrToStructure<RAWINPUT>(buffer);
				
				// Process based on device type
				if (rawInput.header.dwType == RIM_TYPEHID)
				{
					ProcessHidInput(buffer, dwSize, rawInput);
				}
				else if (rawInput.header.dwType == RIM_TYPEMOUSE)
				{
					ProcessMouseInput(buffer, rawInput);
				}
				else if (rawInput.header.dwType == RIM_TYPEKEYBOARD)
				{
					ProcessKeyboardInput(buffer, rawInput);
				}
			}
			finally
			{
				Marshal.FreeHGlobal(buffer);
			}
		}

		/// <summary>
		/// Processes HID device input (gamepads, joysticks).
		/// </summary>
		private void ProcessHidInput(IntPtr buffer, uint dwSize, RAWINPUT rawInput)
		{
			// Compute sizes dynamically to avoid hard-coded offsets
			int offset = s_rawinputHeaderSize + s_rawhidSize;
			int totalAvailable = (int)dwSize - offset;
			int hidReportSize = (int)rawInput.hid.dwSizeHid;
			int hidCount = Math.Max(1, (int)rawInput.hid.dwCount);
			int bytesToCopy = Math.Min(totalAvailable, hidReportSize * hidCount);

			if (bytesToCopy <= 0)
				return;

			// Copy HID report data (handles multiple reports if dwCount > 1)
			byte[] report = new byte[bytesToCopy];
			Marshal.Copy(IntPtr.Add(buffer, offset), report, 0, bytesToCopy);

			// Cache the report by device handle
			if (_handleToPath.TryGetValue(rawInput.header.hDevice, out string path))
			{
				_cachedStates[path] = report;
			}
		}

		/// <summary>
		/// Processes mouse input and creates a synthetic report with button states.
		/// Maintains accumulated button state across multiple WM_INPUT messages.
		/// CRITICAL FIX: Always caches report with current accumulated state, not just on button events.
		/// This ensures button hold detection works correctly.
		/// </summary>
		private void ProcessMouseInput(IntPtr buffer, RAWINPUT rawInput)
		{
			// Read RAWMOUSE structure from buffer
			int mouseOffset = s_rawinputHeaderSize;
			IntPtr mousePtr = IntPtr.Add(buffer, mouseOffset);
			var mouse = Marshal.PtrToStructure<RAWMOUSE>(mousePtr);
	
			// Get current button state for this device (or 0 if first time)
			byte buttonState = _mouseButtonStates.GetOrAdd(rawInput.header.hDevice, 0);
			
			// Update button state based on DOWN events (set bits)
			if ((mouse.usButtonFlags & RI_MOUSE_LEFT_BUTTON_DOWN) != 0)
				buttonState |= 0x01;
			if ((mouse.usButtonFlags & RI_MOUSE_RIGHT_BUTTON_DOWN) != 0)
				buttonState |= 0x02;
			if ((mouse.usButtonFlags & RI_MOUSE_MIDDLE_BUTTON_DOWN) != 0)
				buttonState |= 0x04;
			if ((mouse.usButtonFlags & RI_MOUSE_BUTTON_4_DOWN) != 0)
				buttonState |= 0x08;
			if ((mouse.usButtonFlags & RI_MOUSE_BUTTON_5_DOWN) != 0)
				buttonState |= 0x10;
	
			// Update button state based on UP events (clear bits)
			if ((mouse.usButtonFlags & RI_MOUSE_LEFT_BUTTON_UP) != 0)
				buttonState &= unchecked((byte)~0x01);
			if ((mouse.usButtonFlags & RI_MOUSE_RIGHT_BUTTON_UP) != 0)
				buttonState &= unchecked((byte)~0x02);
			if ((mouse.usButtonFlags & RI_MOUSE_MIDDLE_BUTTON_UP) != 0)
				buttonState &= unchecked((byte)~0x04);
			if ((mouse.usButtonFlags & RI_MOUSE_BUTTON_4_UP) != 0)
				buttonState &= unchecked((byte)~0x08);
			if ((mouse.usButtonFlags & RI_MOUSE_BUTTON_5_UP) != 0)
				buttonState &= unchecked((byte)~0x10);
	
			// Store updated button state for this device
			_mouseButtonStates[rawInput.header.hDevice] = buttonState;
	
			// CRITICAL FIX: Always create and cache report with current accumulated state
			// This ensures button hold detection works - the accumulated state persists
			// even when usButtonFlags is 0 (no new button events)
			byte[] report = new byte[1] { buttonState };
	
			// Get interface path from device handle (handles may change between enumeration and runtime)
			string path = GetDeviceInterfacePath(rawInput.header.hDevice);
			if (!string.IsNullOrEmpty(path))
			{
				// CRITICAL: Always update cached state, even if buttonState hasn't changed
				// This ensures GetRawInputDeviceState() always returns the current accumulated state
				_cachedStates[path] = report;
				// Also update handle-to-path mapping for future lookups
				_handleToPath[rawInput.header.hDevice] = path;
			}
		}

		/// <summary>
		/// Processes keyboard input and creates a synthetic report with key states.
		/// </summary>
		private void ProcessKeyboardInput(IntPtr buffer, RAWINPUT rawInput)
		{
			// Read RAWKEYBOARD structure from buffer
			int keyboardOffset = s_rawinputHeaderSize;
			IntPtr keyboardPtr = IntPtr.Add(buffer, keyboardOffset);
			var keyboard = Marshal.PtrToStructure<RAWKEYBOARD>(keyboardPtr);

			// Create a synthetic report with scan code
			// Byte 0: Reserved (0)
			// Byte 1: Reserved (0)
			// Byte 2: Scan code (if key is pressed)
			byte[] report = new byte[8];
			
			// Check if this is a key down event (bit 0 of Flags is 0 for key down)
			bool isKeyDown = (keyboard.Flags & 0x01) == 0;
			
			if (isKeyDown && keyboard.MakeCode != 0)
			{
				report[2] = (byte)keyboard.MakeCode; // Store scan code in byte 2
			}

			// Cache the report by device handle
			if (_handleToPath.TryGetValue(rawInput.header.hDevice, out string path))
			{
				_cachedStates[path] = report;
			}
		}

		/// <summary>
		/// Returns cached RawInput device state (non-blocking).
		/// For mouse and keyboard devices, polls ACTUAL current state using GetAsyncKeyState to ensure accuracy.
		/// </summary>
		public byte[] GetRawInputDeviceState(RawInputDeviceInfo deviceInfo)
		{
			if (deviceInfo?.InterfacePath == null)
				return null;
	
			// Register device handle to path mapping (thread-safe)
			if (deviceInfo.DeviceHandle != IntPtr.Zero)
			{
				_handleToPath.TryAdd(deviceInfo.DeviceHandle, deviceInfo.InterfacePath);
			}
	
			// For mouse devices, poll ACTUAL current button state directly
			// This ensures we detect button holds even if WM_INPUT messages are missed between polling intervals
			if (deviceInfo.RawInputDeviceType == RawInputDeviceType.Mouse)
			{
				// Poll actual current button state using GetAsyncKeyState
				byte currentButtonState = GetCurrentMouseButtonState();
				
				// Create report with current polled state
				byte[] report = new byte[1] { currentButtonState };
				
				// Update cache with current polled state
				_cachedStates[deviceInfo.InterfacePath] = report;
				
				// Also update accumulated state for consistency with WM_INPUT processing
				if (deviceInfo.DeviceHandle != IntPtr.Zero)
				{
					_mouseButtonStates[deviceInfo.DeviceHandle] = currentButtonState;
				}
				
				return report;
			}
	
			// For keyboard devices, poll ACTUAL current key state directly
			// This ensures we detect key holds even if WM_INPUT messages are missed between polling intervals
			if (deviceInfo.RawInputDeviceType == RawInputDeviceType.Keyboard)
			{
				// Poll actual current keyboard state using GetAsyncKeyState
				byte[] currentKeyState = GetCurrentKeyboardState();
				
				// Update cache with current polled state
				_cachedStates[deviceInfo.InterfacePath] = currentKeyState;
				
				// Also update accumulated state for consistency with WM_INPUT processing
				if (deviceInfo.DeviceHandle != IntPtr.Zero)
				{
					_keyboardKeyStates[deviceInfo.DeviceHandle] = currentKeyState;
				}
				
				return currentKeyState;
			}
	
			// For HID devices, return cached state (non-blocking, thread-safe)
			_cachedStates.TryGetValue(deviceInfo.InterfacePath, out byte[] cachedState);
			return cachedState;
		}
	
		/// <summary>
		/// Polls the ACTUAL current mouse button state using GetAsyncKeyState.
		/// This ensures we detect button holds even if WM_INPUT messages are missed.
		/// CRITICAL: This is called at high frequency (up to 1000Hz), so it must be ultra-fast.
		/// </summary>
		/// <returns>Byte with button state bits set (0x01=left, 0x02=right, 0x04=middle, 0x08=X1, 0x10=X2)</returns>
		private static byte GetCurrentMouseButtonState()
		{
			byte buttonState = 0;
	
			// Check each mouse button using GetAsyncKeyState
			// High bit (0x8000) indicates the key is currently pressed
			if ((GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0)
				buttonState |= 0x01;
			if ((GetAsyncKeyState(VK_RBUTTON) & 0x8000) != 0)
				buttonState |= 0x02;
			if ((GetAsyncKeyState(VK_MBUTTON) & 0x8000) != 0)
				buttonState |= 0x04;
			if ((GetAsyncKeyState(VK_XBUTTON1) & 0x8000) != 0)
				buttonState |= 0x08;
			if ((GetAsyncKeyState(VK_XBUTTON2) & 0x8000) != 0)
				buttonState |= 0x10;
	
			return buttonState;
		}
	
		/// <summary>
		/// Polls the ACTUAL current keyboard state using GetAsyncKeyState.
		/// This ensures we detect key holds even if WM_INPUT messages are missed.
		/// CRITICAL: This is called at high frequency (up to 1000Hz), so it must be ultra-fast.
		/// </summary>
		/// <returns>Byte array with keyboard report format (8 bytes: [0]=modifiers, [1]=reserved, [2-7]=scan codes)</returns>
		private static byte[] GetCurrentKeyboardState()
		{
			byte[] report = new byte[8];
			int keyIndex = 2; // Start at byte 2 for scan codes (bytes 0-1 are modifiers/reserved)
	
			// Scan all virtual key codes (0x08 to 0xFE) to find pressed keys
			// We skip 0x00-0x07 (undefined/mouse buttons) and 0xFF (reserved)
			// This is optimized to check only the most common key ranges
			for (int vKey = 0x08; vKey <= 0xFE && keyIndex < 8; vKey++)
			{
				// Skip mouse button virtual keys (already handled by mouse polling)
				if (vKey >= VK_LBUTTON && vKey <= VK_XBUTTON2)
					continue;
	
				// Check if key is currently pressed (high bit set)
				if ((GetAsyncKeyState(vKey) & 0x8000) != 0)
				{
					// Convert virtual key to scan code (simplified - stores vKey as scan code)
					// In a full implementation, you'd use MapVirtualKey, but that's too slow for 1000Hz
					report[keyIndex++] = (byte)vKey;
					
					// Stop after 6 keys (standard USB keyboard report limit)
					if (keyIndex >= 8)
						break;
				}
			}
	
			return report;
		}

		/// <summary>
		/// Returns cached RawInput device state and clears it (non-blocking).
		/// This ensures button detection only happens when NEW messages arrive between checks.
		/// </summary>
		public byte[] GetAndClearRawInputDeviceState(RawInputDeviceInfo deviceInfo)
		{
			if (deviceInfo?.InterfacePath == null)
				return null;

			// Register device handle to path mapping (thread-safe)
			if (deviceInfo.DeviceHandle != IntPtr.Zero)
			{
				_handleToPath.TryAdd(deviceInfo.DeviceHandle, deviceInfo.InterfacePath);
			}
	
			// Get and remove cached state in one operation (non-blocking, thread-safe)
			_cachedStates.TryRemove(deviceInfo.InterfacePath, out byte[] cachedState);
			return cachedState;
		}

		/// <summary>
		/// Returns cached state by interface path (non-blocking).
		/// </summary>
		public byte[] GetRawInputState(string interfacePath)
		{
			if (string.IsNullOrEmpty(interfacePath))
				return null;
			
			_cachedStates.TryGetValue(interfacePath, out byte[] cachedState);
			return cachedState;
		}

	/// <summary>
	/// Gets the device interface path from a device handle using GetRawInputDeviceInfo.
	/// This is necessary because device handles can change between enumeration and runtime.
	/// </summary>
	private string GetDeviceInterfacePath(IntPtr hDevice)
	{
		uint size = 0;
		GetRawInputDeviceInfo(hDevice, RIDI_DEVICENAME, IntPtr.Zero, ref size);

		if (size == 0)
			return null;

		IntPtr buffer = Marshal.AllocHGlobal((int)size * 2); // Unicode characters
		try
		{
			uint result = GetRawInputDeviceInfo(hDevice, RIDI_DEVICENAME, buffer, ref size);
			return result == uint.MaxValue ? null : Marshal.PtrToStringUni(buffer);
		}
		finally
		{
			Marshal.FreeHGlobal(buffer);
		}
	}

	[DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
	private static extern uint GetRawInputDeviceInfo(IntPtr hDevice, uint uiCommand, IntPtr pData, ref uint pcbSize);

	private const uint RIDI_DEVICENAME = 0x20000007;

		public void Dispose()
		{
			if (_disposed)
				return;
	
			_messageWindow?.Dispose();
			_cachedStates.Clear();
			_handleToPath.Clear();
			_mouseButtonStates.Clear();
			_keyboardKeyStates.Clear();
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
