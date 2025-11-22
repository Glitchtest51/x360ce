using SharpDX;
using SharpDX.DirectInput;
using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using x360ce.App.Input.Devices;

namespace x360ce.App.Input.States
{
	/// <summary>
	/// Provides methods to retrieve DirectInput device states.
	/// Handles state reading for Joystick, Keyboard, and Mouse devices.
	/// For keyboard and mouse, uses GetAsyncKeyState polling (same as RawInput) for reliable button detection.
	/// </summary>
	internal class DirectInputState
	{
		#region Windows API for Keyboard/Mouse Polling
		
		/// <summary>
		/// Gets the current state of a virtual key (including mouse buttons and keyboard keys).
		/// Returns non-zero if the key is currently pressed.
		/// </summary>
		[DllImport("user32.dll")]
		private static extern short GetAsyncKeyState(int vKey);
		
		// Virtual key codes for mouse buttons
		private const int VK_XBUTTON2 = 0x06; // X2 mouse button
		
		#endregion
		
		#region State Caching for Keyboard/Mouse
		
		// Cache for keyboard and mouse states (keyed by device InterfacePath)
		private readonly ConcurrentDictionary<string, CachedKeyboardMouseState> _cachedStates = new ConcurrentDictionary<string, CachedKeyboardMouseState>();
		
		/// <summary>
		/// Cached state for keyboard or mouse device.
		/// Stores the polled state as InputStateAsList for direct use.
		/// </summary>
		private class CachedKeyboardMouseState
		{
			public ListInputState State { get; set; }
			public DateTime LastUpdate { get; set; }
		}
		
		#endregion
		
		/// <summary>
		/// Returns the current state of a DirectInput device.
		/// For joysticks, returns JoystickState. For keyboard/mouse, returns cached InputStateAsList.
		/// </summary>
		/// <param name="diDeviceInfo">DirectInputDeviceInfo containing the device to read</param>
		/// <returns>Device state object (JoystickState for joysticks, or InputStateAsList for keyboard/mouse) or null if failed</returns>
		/// <remarks>
		/// This method reads the current input state from a DirectInput device.
		/// The returned object type depends on the device type:
		/// - Joystick/Gamepad: JoystickState (axes, sliders, buttons, POVs)
		/// - Keyboard: InputStateAsList (polled using GetAsyncKeyState for reliability)
		/// - Mouse: InputStateAsList (polled using GetAsyncKeyState for reliability)
		///
		/// IMPORTANT: For keyboard and mouse devices, we bypass DirectInput's GetCurrentState()
		/// and return InputStateAsList directly from GetAsyncKeyState polling (same as RawInput).
		/// This ensures button presses are detected correctly.
		/// </remarks>
		public object GetDirectInputState(DirectInputDeviceInfo diDeviceInfo)
		{
			if (diDeviceInfo?.DirectInputDevice == null)
				return null;

			var device = diDeviceInfo.DirectInputDevice;

			try
			{
				// Read state based on device type
				switch (device)
				{
					case Joystick joystick:
						// Joysticks use standard DirectInput polling
						try
						{
							joystick.Acquire();
						}
						catch (SharpDXException)
						{
							// Device may already be acquired
						}
						joystick.Poll();
						return joystick.GetCurrentState();
						
					case Keyboard keyboard:
						// For keyboards, return InputStateAsList directly from polling
						// This bypasses DirectInput's unreliable GetCurrentState()
						return GetCurrentKeyboardStateAsListPolled(diDeviceInfo.InterfacePath);

					case Mouse mouse:
						// For mice, return InputStateAsList directly from polling
						// This bypasses DirectInput's Acquire()/Poll() which kills RawInput messages
						return GetCurrentMouseStateAsListPolled(diDeviceInfo.InterfacePath);

					default:
						return null;
				}
			}
			catch (SharpDXException ex)
			{
				// Device may be unplugged or access lost
				System.Diagnostics.Debug.WriteLine($"DirectInputState: Error reading state for {diDeviceInfo.InstanceName}: {ex.Message}");
				return null;
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"DirectInputState: Unexpected error reading state for {diDeviceInfo.InstanceName}: {ex.Message}");
				return null;
			}
		}
		
		/// <summary>
		/// Polls the ACTUAL current mouse state using GetAsyncKeyState and returns InputStateAsList.
		/// This ensures we don't block RawInput messages by Acquiring the device in DirectInput.
		/// </summary>
		private ListInputState GetCurrentMouseStateAsListPolled(string interfacePath)
		{
			// Reuse existing ListInputState object if it exists
			ListInputState result;
			if (_cachedStates.TryGetValue(interfacePath, out var cached))
			{
				result = cached.State;
				// Clear existing button states
				for (int i = 0; i < result.Buttons.Count; i++)
					result.Buttons[i] = 0;
			}
			else
			{
				result = new ListInputState();
				// Initialize standard mouse buttons (Left, Right, Middle, X1, X2)
				for (int i = 0; i < 5; i++)
					result.Buttons.Add(0);
				// Initialize axes (X, Y, Wheel) - placeholder as we can't easily poll relative deltas without DI
				for (int i = 0; i < 3; i++)
					result.Axes.Add(i < 2 ? 32767 : 0);
			}

			// Poll standard mouse buttons
			// VK_LBUTTON (0x01), VK_RBUTTON (0x02), VK_MBUTTON (0x04), VK_XBUTTON1 (0x05), VK_XBUTTON2 (0x06)
			if ((GetAsyncKeyState(0x01) & 0x8000) != 0) result.Buttons[0] = 1; // Left
			if ((GetAsyncKeyState(0x02) & 0x8000) != 0) result.Buttons[1] = 1; // Right
			if ((GetAsyncKeyState(0x04) & 0x8000) != 0) result.Buttons[2] = 1; // Middle
			if ((GetAsyncKeyState(0x05) & 0x8000) != 0) result.Buttons[3] = 1; // X1
			if ((GetAsyncKeyState(0x06) & 0x8000) != 0) result.Buttons[4] = 1; // X2

			// Cache the state
			_cachedStates[interfacePath] = new CachedKeyboardMouseState
			{
				State = result,
				LastUpdate = DateTime.Now
			};

			return result;
		}

		/// <summary>
		/// Polls the ACTUAL current keyboard state using GetAsyncKeyState and returns InputStateAsList.
		/// This ensures we detect key holds reliably, same approach as RawInput.
		/// CRITICAL: Reuses existing ListInputState object to maintain reference consistency.
		/// </summary>
		private ListInputState GetCurrentKeyboardStateAsListPolled(string interfacePath)
		{
			// CRITICAL FIX: Reuse existing ListInputState object if it exists
			// This maintains the reference in UnifiedInputDeviceInfo.ListInputState
			ListInputState result;
			if (_cachedStates.TryGetValue(interfacePath, out var cached))
			{
				result = cached.State;
				// Clear existing button states
				for (int i = 0; i < result.Buttons.Count; i++)
				{
					result.Buttons[i] = 0;
				}
			}
			else
			{
				// First time - create new ListInputState
				result = new ListInputState();
				// Initialize all 256 buttons as released (0)
				for (int i = 0; i < 256; i++)
				{
					result.Buttons.Add(0);
				}
			}
			
			// Scan virtual key codes to find pressed keys
			// Skip 0x00-0x07 (undefined/mouse buttons) and 0xFF (reserved)
			for (int vKey = 0x08; vKey <= 0xFE; vKey++)
			{
				// Skip mouse button virtual keys
				if (vKey <= VK_XBUTTON2)
					continue;
				
				// Check if key is currently pressed (high bit set)
				if ((GetAsyncKeyState(vKey) & 0x8000) != 0)
				{
					// Set button state to 1 (pressed)
					if (vKey < 256)
					{
						result.Buttons[vKey] = 1;
					}
				}
			}
			
			// Cache the state (or update cache timestamp)
			_cachedStates[interfacePath] = new CachedKeyboardMouseState
			{
				State = result,
				LastUpdate = DateTime.Now
			};
			
			return result;
		}
	}
}
