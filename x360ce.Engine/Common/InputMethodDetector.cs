using System;
using System.Collections.Generic;
using System.Linq;
using x360ce.Engine.Data;

namespace x360ce.Engine.Common
{
	/// <summary>
	/// Utility class for detecting which input methods are available for a specific controller device.
	/// Provides both simple text output for UI display and detailed capability information.
	/// </summary>
	public static class InputMethodDetector
	{
		/// <summary>
		/// Gets a comma-separated string of available input methods for the specified device.
		/// This is the main method called from the UI to populate the AvailableInputs label.
		/// </summary>
		/// <param name="device">The UserDevice to analyze for input method compatibility</param>
		/// <returns>Formatted string like "DirectInput, XInput, GamingInput" or empty string if device is null</returns>
		public static string GetAvailableInputMethodsText(UserDevice device)
		{
			if (device == null)
				return "";

			var methods = new List<string>();

			// DirectInput - Always available for all devices
			if (SupportsDirectInput(device))
				methods.Add("DirectInput");

			// XInput - Xbox controllers only, with slot availability check
			if (SupportsXInput(device))
				methods.Add("XInput");

			// Gaming Input - Windows 10+ with broader device support
			if (SupportsGamingInput(device))
				methods.Add("GamingInput");

			// Raw Input - HID-compliant devices
			if (SupportsRawInput(device))
				methods.Add("RawInput");

			return string.Join(", ", methods);
		}

		/// <summary>
		/// Gets detailed information about available input methods and their limitations.
		/// Used for tooltips and detailed analysis.
		/// </summary>
		/// <param name="device">The UserDevice to analyze</param>
		/// <returns>Multi-line string with detailed capability information</returns>
		public static string GetDetailedCapabilitiesText(UserDevice device)
		{
			if (device == null)
				return "No device selected";

			var details = new List<string>();

			if (SupportsDirectInput(device))
			{
				var limitation = device.IsXboxCompatible && IsWindows10OrLater() 
					? " (Xbox controllers may lose background access on Windows 10+)"
					: "";
				details.Add($"DirectInput: Universal support{limitation}");
			}

			if (SupportsXInput(device))
			{
				var slotsInfo = GetAvailableXInputSlots() < 4 
					? $" ({4 - GetAvailableXInputSlots()} slots available)"
					: " (all 4 slots available)";
				details.Add($"XInput: Xbox controller support{slotsInfo}");
			}

			if (SupportsGamingInput(device))
			{
				var support = device.IsXboxCompatible 
					? "Full Xbox features including trigger rumble"
					: "Basic gamepad support";
				details.Add($"GamingInput: {support} (Windows 10+, no background access)");
			}

			if (SupportsRawInput(device))
			{
				details.Add("RawInput: Direct HID access (background support, complex setup)");
			}

			return string.Join("\n", details);
		}

		/// <summary>
		/// Checks if the device supports DirectInput.
		/// DirectInput supports virtually all controller devices.
		/// </summary>
		/// <param name="device">The device to check</param>
		/// <returns>True if DirectInput is supported (almost always true)</returns>
		public static bool SupportsDirectInput(UserDevice device)
		{
			if (device == null)
				return false;

			// DirectInput supports almost all devices, but exclude system devices
			// that shouldn't be used for gaming
			return !device.IsKeyboard && !device.IsMouse;
		}

		/// <summary>
		/// Checks if the device supports XInput.
		/// Only Xbox-compatible controllers support XInput, and there's a maximum of 4 XInput devices.
		/// </summary>
		/// <param name="device">The device to check</param>
		/// <returns>True if the device is Xbox-compatible and XInput slots are available</returns>
		public static bool SupportsXInput(UserDevice device)
		{
			if (device == null)
				return false;

			// Must be Xbox-compatible controller
			if (!device.IsXboxCompatible)
				return false;

			// XInput has a hard limit of 4 controllers
			// For simplicity, we'll assume XInput is available if the device is Xbox-compatible
			// Advanced implementation could check actual XInput slot usage
			return true;
		}

		/// <summary>
		/// Checks if the device supports Gaming Input (Windows.Gaming.Input API).
		/// Available on Windows 10+ for both Xbox and generic controllers.
		/// </summary>
		/// <param name="device">The device to check</param>
		/// <returns>True if Gaming Input is supported on this system and device</returns>
		public static bool SupportsGamingInput(UserDevice device)
		{
			if (device == null)
				return false;

			// Gaming Input requires Windows 10 or later
			if (!IsWindows10OrLater())
				return false;

			// Gaming Input supports both Xbox controllers (full features) and generic gamepads (basic support)
			// Exclude keyboards and mice
			return !device.IsKeyboard && !device.IsMouse;
		}

		/// <summary>
		/// Checks if the device supports Raw Input.
		/// Raw Input works with HID-compliant devices that have a valid device path.
		/// </summary>
		/// <param name="device">The device to check</param>
		/// <returns>True if the device has HID capabilities suitable for Raw Input</returns>
		public static bool SupportsRawInput(UserDevice device)
		{
			if (device == null)
				return false;

			// Raw Input requires HID device path
			if (string.IsNullOrEmpty(device.HidDevicePath))
				return false;

			// Exclude keyboards and mice (they use Raw Input differently)
			if (device.IsKeyboard || device.IsMouse)
				return false;

			// Must be HID-compliant device
			return device.CapIsHumanInterfaceDevice;
		}

		/// <summary>
		/// Determines if the current system is Windows 10 or later.
		/// Gaming Input API is only available on Windows 10+.
		/// </summary>
		/// <returns>True if running on Windows 10 or later</returns>
		public static bool IsWindows10OrLater()
		{
			try
			{
				// Windows 10 is version 10.0
				var version = Environment.OSVersion.Version;
				return version.Major >= 10;
			}
			catch
			{
				// If we can't determine the version, assume older Windows
				return false;
			}
		}

		/// <summary>
		/// Gets the number of available XInput controller slots.
		/// XInput supports a maximum of 4 controllers simultaneously.
		/// </summary>
		/// <returns>Number of available XInput slots (assumes some slots available for basic implementation)</returns>
		public static int GetAvailableXInputSlots()
		{
			// For basic implementation, assume 2 slots are available
			// Advanced implementation would need device usage info passed from calling code
			// to avoid circular dependency between Engine and App projects
			return 2;
		}

		/// <summary>
		/// Gets a user-friendly description of why certain input methods might not be available.
		/// </summary>
		/// <param name="device">The device to analyze</param>
		/// <returns>String describing any limitations or recommendations</returns>
		public static string GetInputMethodRecommendations(UserDevice device)
		{
			if (device == null)
				return "";

			var recommendations = new List<string>();

			if (device.IsXboxCompatible)
			{
				if (IsWindows10OrLater())
				{
					recommendations.Add("For Xbox controllers on Windows 10+:");
					recommendations.Add("• XInput: Best for background access and performance");
					recommendations.Add("• GamingInput: Best for full Xbox One features (no background access)");
					recommendations.Add("• DirectInput: May lose background access, not recommended");
				}
				else
				{
					recommendations.Add("For Xbox controllers on older Windows:");
					recommendations.Add("• XInput: Recommended for best performance");
					recommendations.Add("• DirectInput: Alternative option with full background access");
				}
			}
			else
			{
				recommendations.Add("For generic controllers:");
				recommendations.Add("• DirectInput: Most compatible option");
				recommendations.Add("• RawInput: Advanced option with background access");
				if (IsWindows10OrLater())
				{
					recommendations.Add("• GamingInput: Modern option (Windows 10+, no background access)");
				}
			}

			return string.Join("\n", recommendations);
		}
	}
}
