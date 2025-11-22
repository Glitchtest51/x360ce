using System;
using System.Linq;
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
    /// SELF-CONTAINED DESIGN:
    /// • Manages its own internal device list to avoid circular dependencies
    /// • Does NOT depend on UnifiedInputDeviceManager or InputStateManager
    /// • External code can register device lists for state updates
    /// • This is a lightweight, self-contained implementation
    /// </remarks>
    internal class RawInputState : IDisposable
    {
        #region Singleton Pattern

        private static readonly object _lock = new object();
        private static RawInputState _rawInputState;

        /// <summary>
        /// Gets the singleton instance of RawInputState.
        /// CRITICAL: Only ONE instance can exist to prevent WM_INPUT message routing conflicts.
        /// </summary>
        public static RawInputState rawInputState
        {
            get
            {
                if (_rawInputState == null)
                {
                    lock (_lock)
                    {
                        if (_rawInputState == null)
                        {
                            _rawInputState = new RawInputState();
                        }
                    }
                }
                return _rawInputState;
            }
        }

        #endregion

        #region Windows Raw Input API

        [DllImport("user32.dll", SetLastError = true, EntryPoint = "RegisterRawInputDevices")]
        private static extern bool RegisterRawInputDevicesNative(RAWINPUTDEVICE[] pRawInputDevices, uint uiNumDevices, uint cbSize);

        /// <summary>
        /// Wrapper for RegisterRawInputDevices that logs all calls for debugging.
        /// </summary>
        private static bool RegisterRawInputDevices(RAWINPUTDEVICE[] pRawInputDevices, uint uiNumDevices, uint cbSize)
        {
            // Log what devices are being registered
            var deviceTypes = new System.Text.StringBuilder();
            deviceTypes.Append("RegisterRawInputDevices called with devices: ");
            for (int i = 0; i < uiNumDevices; i++)
            {
                var device = pRawInputDevices[i];
                string deviceType = device.usUsage == 0x02 ? "Mouse" :
                                   device.usUsage == 0x06 ? "Keyboard" :
                                   device.usUsage == 0x04 ? "Joystick" :
                                   device.usUsage == 0x05 ? "Gamepad" :
                                   device.usUsage == 0x08 ? "MultiAxis" :
                                   $"Unknown(0x{device.usUsage:X2})";
                deviceTypes.Append($"{deviceType}(Page:0x{device.usUsagePage:X2}, Flags:0x{device.dwFlags:X8}) ");
            }
            System.Diagnostics.Debug.WriteLine(deviceTypes.ToString());
            System.Diagnostics.Debug.WriteLine($"  Stack trace: {new System.Diagnostics.StackTrace(1, true).ToString().Split('\n')[0]}");
            
            return RegisterRawInputDevicesNative(pRawInputDevices, uiNumDevices, cbSize);
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetRawInputData(IntPtr hRawInput, uint uiCommand, IntPtr pData, ref uint pcbSize, uint cbSizeHeader);

        [DllImport("kernel32.dll")]
        private static extern uint GetLastError();

        /// <summary>
        /// Gets the current state of a virtual key (used for keyboard polling).
        /// Returns non-zero if the key is currently pressed.
        /// </summary>
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        /// <summary>
        /// Gets a usage value from a HID input report.
        /// </summary>
        [DllImport("hid.dll", SetLastError = true)]
        private static extern int HidP_GetUsageValue(
            HIDP_REPORT_TYPE ReportType,
            ushort UsagePage,
            ushort LinkCollection,
            ushort Usage,
            out int UsageValue,
            IntPtr PreparsedData,
            IntPtr Report,
            uint ReportLength);

        /// <summary>
        /// Gets all button usages that are currently pressed.
        /// </summary>
        [DllImport("hid.dll", SetLastError = true)]
        private static extern int HidP_GetUsages(
            HIDP_REPORT_TYPE ReportType,
            ushort UsagePage,
            ushort LinkCollection,
            [Out] ushort[] UsageList,
            ref ushort UsageLength,
            IntPtr PreparsedData,
            IntPtr Report,
            uint ReportLength);

        // HID report types
        private enum HIDP_REPORT_TYPE
        {
            HidP_Input = 0,
            HidP_Output = 1,
            HidP_Feature = 2
        }

        // HID status codes
        private const int HIDP_STATUS_SUCCESS = 0x00110000;

        // HID Usage Pages
        private const ushort HID_USAGE_PAGE_GENERIC = 0x01;
        private const ushort HID_USAGE_PAGE_BUTTON = 0x09;

        private const uint RIM_TYPEMOUSE = 0;
        private const uint RIM_TYPEKEYBOARD = 1;
        private const uint RIM_TYPEHID = 2;
        private const uint RID_INPUT = 0x10000003;

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
        private struct RAWHID
        {
            public uint dwSizeHid;
            public uint dwCount;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct RAWMOUSE
        {
            [FieldOffset(0)]
            public ushort usFlags;           // Mouse movement flags
            [FieldOffset(4)]
            public ushort usButtonFlags;     // Button transition flags (press/release events)
            [FieldOffset(6)]
            public ushort usButtonData;      // Wheel delta OR extra button data
            [FieldOffset(8)]
            public uint ulRawButtons;        // Raw button state (rarely used)
            [FieldOffset(12)]
            public int lLastX;               // X-axis movement delta
            [FieldOffset(16)]
            public int lLastY;               // Y-axis movement delta
            [FieldOffset(20)]
            public uint ulExtraInformation;  // Extra device-specific info
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
        private const ushort RI_MOUSE_WHEEL = 0x0400;
        private const ushort RI_MOUSE_HWHEEL = 0x0800;

        #endregion

        #region Message Window for WM_INPUT

        /// <summary>
        /// Hidden window that receives WM_INPUT messages without blocking.
        /// </summary>
        private class RawInputMessageWindow : System.Windows.Forms.NativeWindow, IDisposable
        {
            private const int WM_INPUT = 0x00FF;
            private readonly RawInputState _parent;

            public RawInputMessageWindow(RawInputState parent)
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
                else if (m.Msg == 0x0002) // WM_DESTROY
                {
                    System.Diagnostics.Debug.WriteLine($"RawInputMessageWindow: WM_DESTROY received for handle 0x{Handle:X}");
                }
                else if (m.Msg == 0x0082) // WM_NCDESTROY
                {
                    System.Diagnostics.Debug.WriteLine($"RawInputMessageWindow: WM_NCDESTROY received for handle 0x{Handle:X}");
                }
                base.WndProc(ref m);
            }

            public void Dispose()
            {
                if (Handle != IntPtr.Zero)
                {
                    System.Diagnostics.Debug.WriteLine($"RawInputMessageWindow.Dispose: Destroying handle 0x{Handle:X}");
                    DestroyHandle();
                }
            }
        }

        #endregion

        private RawInputMessageWindow _messageWindow;
        private bool _isInitialized;
        private bool _disposed;

        // Optimization: Reuse buffer for WM_INPUT messages to avoid AllocHGlobal/FreeHGlobal overhead
        private IntPtr _buffer;
        private int _bufferSize;

        // Cached struct sizes (used multiple times in hot path)
        private static readonly int s_rawinputHeaderSize = Marshal.SizeOf(typeof(RAWINPUTHEADER));
        private static readonly int s_rawhidSize = Marshal.SizeOf(typeof(RAWHID));

        // HID Usage Page and Usage constants (Static Readonly for performance)
        private static readonly ushort[] _axisUsages = { 0x30, 0x31, 0x32, 0x33, 0x34, 0x35 };
        private static readonly ushort[] _sliderUsages = { 0x36, 0x37, 0x38 };

        // Reusable buffer for button usages to avoid allocation in hot path
        private ushort[] _buttonUsagesBuffer = new ushort[256];

        // HID Usage Page and Usage constants
        private const ushort USAGE_PAGE_GENERIC_DESKTOP = 0x01;
        private const ushort USAGE_JOYSTICK = 0x04;
        private const ushort USAGE_GAMEPAD = 0x05;
        private const ushort USAGE_MULTI_AXIS = 0x08;
        private const uint RIDEV_INPUTSINK = 0x00000100;

        /// <summary>
        /// Initializes the RawInput message receiver (non-blocking).
        /// </summary>
        private RawInputState()
        {
            try
            {
                // Create hidden window for WM_INPUT messages
                _messageWindow = new RawInputMessageWindow(this);

                // Perform initial device registration
                RegisterDevices();
            }
            catch
            {
                _isInitialized = false;
            }
        }

        /// <summary>
        /// Registers (or re-registers) Raw Input devices.
        /// CRITICAL: This must be called whenever device lists are recreated to maintain mouse input.
        /// Windows Raw Input registration is per-process and can be overwritten by subsequent calls.
        /// </summary>
        public void RegisterDevices()
        {
            // CRITICAL: Ensure message window exists and has valid handle before registration
            // This prevents registration failures that cause mouse messages to stop
            if (_messageWindow == null || _messageWindow.Handle == IntPtr.Zero)
            {
                System.Diagnostics.Debug.WriteLine($"RawInputState.RegisterDevices: Message window invalid (null={_messageWindow == null}, handle={_messageWindow?.Handle ?? IntPtr.Zero:X}), recreating...");
                try
                {
                    // Dispose old window if it exists
                    if (_messageWindow != null)
                    {
                        try { _messageWindow.Dispose(); } catch { }
                        _messageWindow = null;
                    }
                    
                    // Create new message window
                    _messageWindow = new RawInputMessageWindow(this);
                    
                    // Verify handle was created successfully
                    if (_messageWindow.Handle == IntPtr.Zero)
                    {
                        System.Diagnostics.Debug.WriteLine("RawInputState.RegisterDevices: CRITICAL - Failed to create valid window handle!");
                        return;
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"RawInputState.RegisterDevices: Created new message window with handle: 0x{_messageWindow.Handle:X}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"RawInputState.RegisterDevices: CRITICAL - Failed to create message window: {ex.Message}");
                    _messageWindow = null;
                    return;
                }
            }

            try
            {
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
                // NOTE: RIDEV_NOLEGACY causes thread termination issues - use RIDEV_INPUTSINK only
                devices[4].usUsagePage = USAGE_PAGE_GENERIC_DESKTOP;
                devices[4].usUsage = 0x02;
                devices[4].dwFlags = RIDEV_INPUTSINK;
                devices[4].hwndTarget = _messageWindow.Handle;

                bool success = RegisterRawInputDevices(devices, 5, (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICE)));
                if (!success)
                {
                    uint errorCode = GetLastError();
                    System.Diagnostics.Debug.WriteLine($"RawInputState.RegisterDevices: CRITICAL - RegisterRawInputDevices failed with error code: {errorCode}");
                    System.Diagnostics.Debug.WriteLine($"RawInputState.RegisterDevices: This will cause mouse/keyboard/HID messages to stop arriving!");
                    _isInitialized = false;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"RawInputState.RegisterDevices: ✅ Successfully registered all input devices (HID, Keyboard, Mouse) for window handle: 0x{_messageWindow.Handle:X}");
                    System.Diagnostics.Debug.WriteLine($"RawInputState.RegisterDevices: Mouse messages should now arrive continuously");
                    _isInitialized = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RawInputState.RegisterDevices: Exception during registration: {ex.Message}");
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

            // Optimization: Optimistically try to read with existing buffer first.
            // This saves a "Get Size" P/Invoke call for every message in the common case.
            if (_buffer != IntPtr.Zero && _bufferSize > 0)
            {
                dwSize = (uint)_bufferSize;
                uint bytesRead = GetRawInputData(lParam, RID_INPUT, _buffer, ref dwSize, (uint)s_rawinputHeaderSize);
                if (bytesRead != uint.MaxValue)
                {
                    // Use bytesRead (actual size) instead of dwSize (buffer size)
                    ProcessBuffer(_buffer, bytesRead);
                    return;
                }
            }

            // Fallback: Get required buffer size
            if (GetRawInputData(lParam, RID_INPUT, IntPtr.Zero, ref dwSize, (uint)s_rawinputHeaderSize) == uint.MaxValue)
                return;

            // Resize buffer if necessary
            if (dwSize > _bufferSize)
            {
                if (_buffer != IntPtr.Zero)
                    Marshal.FreeHGlobal(_buffer);
                _buffer = Marshal.AllocHGlobal((int)dwSize);
                _bufferSize = (int)dwSize;
            }

            // Read actual data
            uint finalBytesRead = GetRawInputData(lParam, RID_INPUT, _buffer, ref dwSize, (uint)s_rawinputHeaderSize);
            if (finalBytesRead != uint.MaxValue)
            {
                ProcessBuffer(_buffer, finalBytesRead);
            }
        }

        private void ProcessBuffer(IntPtr buffer, uint dwSize)
        {
            // Optimization: Only marshal the header first to determine type
            // This avoids marshaling the wrong union member or extra data
            var header = Marshal.PtrToStructure<RAWINPUTHEADER>(buffer);

            // Process based on device type
            if (header.dwType == RIM_TYPEHID)
                ProcessHidInput(buffer, dwSize, header);
            else if (header.dwType == RIM_TYPEMOUSE)
                ProcessMouseInput(buffer, header);
            else if (header.dwType == RIM_TYPEKEYBOARD)
                ProcessKeyboardInput(buffer, header);
        }

        private RawInputDeviceInfo GetDeviceInfo(IntPtr hDevice, RawInputDeviceType rawInputDeviceType)
        {
            // Get device interface path
            string devicePath = GetDeviceInterfacePath(hDevice);
            if (string.IsNullOrEmpty(devicePath)) return null;

            // Direct access to the authoritative static device list
            var device = RawInputDevice.RawInputDeviceInfoList
                .FirstOrDefault(d => d.InterfacePath == devicePath && d.RawInputDeviceType == rawInputDeviceType);

            // If device not found in list (e.g., during list recreation), create a temporary device info
            // This ensures WM_INPUT messages continue to be processed even when the device list is being updated
            if (device == null)
            {
                // Create a minimal temporary device info to keep processing messages
                device = new RawInputDeviceInfo
                {
                    DeviceHandle = hDevice,
                    InterfacePath = devicePath,
                    RawInputDeviceType = rawInputDeviceType,
                    IsOnline = true,
                    InputType = "RawInput"
                };

                // Set appropriate defaults based on device type
                if (rawInputDeviceType == RawInputDeviceType.Mouse)
                {
                    device.ButtonCount = 5; // Standard 5-button mouse
                    device.AxeCount = 4; // X, Y, Z (wheel), W (hwheel)
                    device.ProductName = "Mouse (Temporary)";
                    device.DeviceTypeName = "RawInput Mouse";
                }
                else if (rawInputDeviceType == RawInputDeviceType.Keyboard)
                {
                    device.ButtonCount = 256; // Standard keyboard key count
                    device.ProductName = "Keyboard (Temporary)";
                    device.DeviceTypeName = "RawInput Keyboard";
                }
                else if (rawInputDeviceType == RawInputDeviceType.HID)
                {
                    device.ProductName = "HID Device (Temporary)";
                    device.DeviceTypeName = "RawInput HID";
                }
            }

            return device;
        }

        /// <summary>
        /// Processes HID device input (gamepads, joysticks).
        /// Updates the device's ListInputState property directly from RawInput state messages.
        /// </summary>
        private void ProcessHidInput(IntPtr buffer, uint dwSize, RAWINPUTHEADER header)
        {
            //System.Diagnostics.Debug.WriteLine("RawInputState.ProcessHidInput: HID input received.");

            var device = GetDeviceInfo(header.hDevice, RawInputDeviceType.HID);
            if (device == null) return;

            // Ensure ListInputState is initialized
            if (device.ListInputState == null)
            {
                device.ListInputState = new ListInputState();
                // Initialize with device capabilities
                for (int i = 0; i < device.AxeCount; i++) device.ListInputState.Axes.Add(32767);
                for (int i = 0; i < device.SliderCount; i++) device.ListInputState.Sliders.Add(0);
                for (int i = 0; i < device.ButtonCount; i++) device.ListInputState.Buttons.Add(0);
                for (int i = 0; i < device.PovCount; i++) device.ListInputState.POVs.Add(-1);
            }

            var deviceState = device.ListInputState;

            // Calculate HID report offset and size
            // CRITICAL: offset points to the START of the HID report (including Report ID if present)
            int offset = s_rawinputHeaderSize + s_rawhidSize;
            int reportLength = (int)dwSize - offset;

            if (reportLength <= 0)
                return;

            // Get pointer to HID report data (this is the START of the HID report)
            IntPtr reportPtr = IntPtr.Add(buffer, offset);

            // Parse axes using HID API if preparsed data is available
            if (device.PreparsedData != IntPtr.Zero && device.AxeCount > 0)
            {
                // Standard axis usages: X(0x30), Y(0x31), Z(0x32), Rx(0x33), Ry(0x34), Rz(0x35)
                // CRITICAL: Map each successfully read axis to the next available index
                // because devices may not report all axes in sequential order
                int axisIndex = 0; // Track actual axis position in deviceState.Axes
                
                foreach (var usage in _axisUsages)
                {
                    if (axisIndex >= device.AxeCount)
                        break; // No more axes to read
                    
                    int value;
                    int status = HidP_GetUsageValue(
                        HIDP_REPORT_TYPE.HidP_Input,
                        HID_USAGE_PAGE_GENERIC,
                        0, // LinkCollection
                        usage,
                        out value,
                        device.PreparsedData,
                        reportPtr,
                        (uint)reportLength);

                    if (status == HIDP_STATUS_SUCCESS && axisIndex < deviceState.Axes.Count)
                    {
                        // Successfully read this axis - store it at the current index
                        deviceState.Axes[axisIndex] = value;
                        axisIndex++; // Move to next axis position
                    }
                }
            }

            // Parse sliders using HID API if preparsed data is available
            if (device.PreparsedData != IntPtr.Zero && device.SliderCount > 0)
            {
                // Slider usages: Slider(0x36), Dial(0x37), Wheel(0x38)
                for (int i = 0; i < Math.Min(device.SliderCount, _sliderUsages.Length); i++)
                {
                    int value;
                    int status = HidP_GetUsageValue(
                        HIDP_REPORT_TYPE.HidP_Input,
                        HID_USAGE_PAGE_GENERIC,
                        0, // LinkCollection
                        _sliderUsages[i],
                        out value,
                        device.PreparsedData,
                        reportPtr,
                        (uint)reportLength);

                    if (status == HIDP_STATUS_SUCCESS && i < deviceState.Sliders.Count)
                    {
                        deviceState.Sliders[i] = value;
                    }
                }
            }

            // Parse POVs using HID API if preparsed data is available
            if (device.PreparsedData != IntPtr.Zero && device.PovCount > 0)
            {
                // POV Hat Switch usage: 0x39
                for (int i = 0; i < device.PovCount; i++)
                {
                    int value;
                    int status = HidP_GetUsageValue(
                        HIDP_REPORT_TYPE.HidP_Input,
                        HID_USAGE_PAGE_GENERIC,
                        0, // LinkCollection
                        0x39, // POV Hat Switch
                        out value,
                        device.PreparsedData,
                        reportPtr,
                        (uint)reportLength);

                    if (status == HIDP_STATUS_SUCCESS && i < deviceState.POVs.Count)
                    {
                        // HID POV values are typically 0-7 for 8 directions, or 0-15 for 16 directions
                        // Convert to centidegrees: -1 = neutral, 0 = North, 9000 = East, 18000 = South, 27000 = West
                        if (value == 0x0F || value == 0xFF || value == -1)
                        {
                            // Neutral position
                            deviceState.POVs[i] = -1;
                        }
                        else if (value >= 0 && value <= 15)
                        {
                            // 16-direction POV: convert to centidegrees (0-15 range)
                            deviceState.POVs[i] = value * 2250; // 22.5° = 2250 centidegrees
                        }
                        else if (value >= 0 && value <= 7)
                        {
                            // 8-direction POV: convert to centidegrees (0-7 range, fallback if not 16-direction)
                            deviceState.POVs[i] = value * 4500; // 45° = 4500 centidegrees
                        }
                        else
                        {
                            // Unknown value, treat as neutral
                            deviceState.POVs[i] = -1;
                        }
                    }
                }
            }

            // Parse buttons using HID API instead of raw byte reading
            // This is the ONLY reliable way to read buttons because HID reports can have
            // buttons, POVs, and axes interleaved in complex ways
            if (device.PreparsedData != IntPtr.Zero && device.ButtonCount > 0)
            {
                // Optimization: Resize shared buffer if needed (rare)
                if (_buttonUsagesBuffer.Length < device.ButtonCount)
                    _buttonUsagesBuffer = new ushort[device.ButtonCount];

                // Use HidP_GetUsages to get all pressed buttons
                // This API correctly interprets the HID report descriptor
                ushort usageLength = (ushort)device.ButtonCount;
                
                int status = HidP_GetUsages(
                    HIDP_REPORT_TYPE.HidP_Input,
                    HID_USAGE_PAGE_BUTTON,
                    0, // LinkCollection
                    _buttonUsagesBuffer,
                    ref usageLength,
                    device.PreparsedData,
                    reportPtr,
                    (uint)reportLength);
                
                if (status == HIDP_STATUS_SUCCESS)
                {
                    // Clear all buttons first
                    for (int i = 0; i < deviceState.Buttons.Count; i++)
                        deviceState.Buttons[i] = 0;
                    
                    // Set pressed buttons (usages are 1-based, button indices are 0-based)
                    for (int i = 0; i < usageLength; i++)
                    {
                        int buttonIndex = _buttonUsagesBuffer[i] - 1; // Convert 1-based to 0-based
                        if (buttonIndex >= 0 && buttonIndex < deviceState.Buttons.Count)
                            deviceState.Buttons[buttonIndex] = 1;
                    }
                }
            }
        }

        /// <summary>
        /// Processes mouse input and creates a raw report with button states and RAW axis deltas.
        /// Report format: [0]=buttons, [1-4]=X delta (int), [5-8]=Y delta (int), [9-12]=Z delta (int)
        /// </summary>
        private void ProcessMouseInput(IntPtr buffer, RAWINPUTHEADER header)
        {
            
            // Get mouse interface path
            var device = GetDeviceInfo(header.hDevice, RawInputDeviceType.Mouse);
            if (device == null) return;

            //System.Diagnostics.Debug.WriteLine($"RawInputState.ProcessMouseInput: MOUSE input received {device.InterfacePath}.");

            // Ensure ListInputState is initialized
            if (device.ListInputState == null)
            {
                device.ListInputState = new ListInputState();
                // Initialize with device.ButtonCount buttons and device.AxeCount axes (X, Y, Z-wheel, W-hwheel)
                for (int i = 0; i < device.ButtonCount; i++) device.ListInputState.Buttons.Add(0);
                for (int i = 0; i < device.AxeCount; i++) device.ListInputState.Axes.Add(i < 2 ? 32767 : 0);
            }

            var deviceState = device.ListInputState;

            // Read RAWMOUSE structure
            var mouse = Marshal.PtrToStructure<RAWMOUSE>(IntPtr.Add(buffer, s_rawinputHeaderSize));

            // Update persistent button states based on DOWN/UP flags
            // Note: WM_INPUT only sends transition events, so we track state ourselves
            UpdateMouseButton(deviceState, 0, mouse.usButtonFlags, RI_MOUSE_LEFT_BUTTON_DOWN, RI_MOUSE_LEFT_BUTTON_UP);
            UpdateMouseButton(deviceState, 1, mouse.usButtonFlags, RI_MOUSE_RIGHT_BUTTON_DOWN, RI_MOUSE_RIGHT_BUTTON_UP);
            UpdateMouseButton(deviceState, 2, mouse.usButtonFlags, RI_MOUSE_MIDDLE_BUTTON_DOWN, RI_MOUSE_MIDDLE_BUTTON_UP);
            UpdateMouseButton(deviceState, 3, mouse.usButtonFlags, RI_MOUSE_BUTTON_4_DOWN, RI_MOUSE_BUTTON_4_UP);
            UpdateMouseButton(deviceState, 4, mouse.usButtonFlags, RI_MOUSE_BUTTON_5_DOWN, RI_MOUSE_BUTTON_5_UP);

            // Update axes from movement deltas (relative movement converted to joystick range)
            // Accumulate deltas and clamp to 0-65535 to simulate joystick analog stick behavior
            if (deviceState.Axes.Count > 0)
                deviceState.Axes[0] = Math.Max(0, Math.Min(65535, deviceState.Axes[0] + mouse.lLastX * device.MouseAxisSensitivity[0]));
            if (deviceState.Axes.Count > 1)
                deviceState.Axes[1] = Math.Max(0, Math.Min(65535, deviceState.Axes[1] + mouse.lLastY * device.MouseAxisSensitivity[1]));

            // Process vertical wheel delta (vertical wheel axis)
            // Accumulate deltas and clamp to 0-65535 to simulate joystick analog stick behavior
            if (deviceState.Axes.Count > 2 && (mouse.usButtonFlags & RI_MOUSE_WHEEL) != 0)
            {
                short wheelDelta = (short)mouse.usButtonData;               
                deviceState.Axes[2] = Math.Max(0, Math.Min(65535, deviceState.Axes[2] + wheelDelta * device.MouseAxisSensitivity[2]));
            }
            
            // Process horizontal wheel delta (horizontal wheel axis)
            if (deviceState.Axes.Count > 3 && (mouse.usButtonFlags & RI_MOUSE_HWHEEL) != 0)
            {
                short hwheelDelta = (short)mouse.usButtonData;                
                deviceState.Axes[3] = Math.Max(0, Math.Min(65535, deviceState.Axes[3] + hwheelDelta * device.MouseAxisSensitivity[3]));
            }
        }

        /// <summary>
        /// Helper method to update mouse button state based on DOWN/UP flags.
        /// </summary>
        private void UpdateMouseButton(ListInputState state, int buttonIndex, ushort flags, ushort downFlag, ushort upFlag)
        {
            if (state.Buttons.Count > buttonIndex)
            {
                if ((flags & downFlag) != 0)
                    state.Buttons[buttonIndex] = 1;
                else if ((flags & upFlag) != 0)
                    state.Buttons[buttonIndex] = 0;
            }
        }

        /// <summary>
        /// Processes keyboard input and creates a synthetic report with key states.
        /// </summary>
        private void ProcessKeyboardInput(IntPtr buffer, RAWINPUTHEADER header)
        {
            //System.Diagnostics.Debug.WriteLine("RawInputState.ProcessKeyboardInput: KEYBOARD input received.");

            // Get device from
            var device = GetDeviceInfo(header.hDevice, RawInputDeviceType.Keyboard);
            if (device == null) return;

            // Ensure ListInputState is initialized
            if (device.ListInputState == null)
            {
                device.ListInputState = new ListInputState();
                // Initialize with device.ButtonCount buttons.
                for (int i = 0; i < device.ButtonCount; i++) device.ListInputState.Buttons.Add(0);
            }

            var deviceState = device.ListInputState;

            // Read RAWKEYBOARD structure and update key state
            var keyboard = Marshal.PtrToStructure<RAWKEYBOARD>(IntPtr.Add(buffer, s_rawinputHeaderSize));
            deviceState.Buttons[keyboard.VKey] = (keyboard.Flags & 0x01) == 0 ? 1 : 0;
        }

        /// <summary>
        /// Gets the device interface path from a device handle using GetRawInputDeviceInfo.
        /// This is necessary because device handles can change between enumeration and runtime.
        /// </summary>
        private string GetDeviceInterfacePath(IntPtr hDevice)
        {
            uint size = 0;
            if (GetRawInputDeviceInfo(hDevice, RIDI_DEVICENAME, IntPtr.Zero, ref size) == uint.MaxValue || size == 0)
                return null;

            IntPtr buffer = Marshal.AllocHGlobal((int)size * 2);
            try
            {
                return GetRawInputDeviceInfo(hDevice, RIDI_DEVICENAME, buffer, ref size) == uint.MaxValue
                    ? null
                    : Marshal.PtrToStringUni(buffer);
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

            if (_buffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_buffer);
                _buffer = IntPtr.Zero;
            }

            _disposed = true;
        }
    }
}
