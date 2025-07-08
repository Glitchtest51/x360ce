using System;
using System.Linq;
using x360ce.Engine;
using x360ce.Engine.Data;

namespace x360ce.App.Input.Orchestration
{
	public partial class InputOrchestrator
	{
		/// <summary>
		/// Step 2: Load device capabilities for devices that need capability loading.
		/// This step processes capability loading flags set during device initialization or input method changes.
		/// Executes in serial order within the main orchestrator loop to ensure thread safety.
		/// </summary>
		/// <param name="game">The current game configuration</param>
		/// <remarks>
		/// FLAG-BASED CAPABILITY LOADING:
		/// • CapabilitiesNeedLoading: Set during device initialization
		/// • InputMethodChanged: Set when user changes input method
		/// • Serial execution ensures no threading conflicts with device access
		/// • Capabilities loaded once per device initialization + when input method changes
		/// </remarks>
		void LoadDeviceCapabilities(UserGame game)
		{
			// Get all mapped user devices for the specified game
			var devicesToCheck = SettingsManager.GetMappedDevices(game?.FileName)
				.Where(x => x != null && x.IsOnline)
				.ToArray();

			foreach (var device in devicesToCheck)
			{
				// Check if capabilities need loading
				if (device.CapabilitiesNeedLoading || device.InputMethodChanged)
				{
					try
					{
						// Load capabilities using the centralized processor method
						LoadDeviceCapabilities(device);

						// Clear the flags after successful loading
						device.CapabilitiesNeedLoading = false;
						device.InputMethodChanged = false;

						System.Diagnostics.Debug.WriteLine($"Step2: Loaded capabilities for {device.DisplayName} - Method: {device.InputMethod}, Buttons: {device.CapButtonCount}, Axes: {device.CapAxeCount}");
					}
					catch (Exception ex)
					{
						// Log the error but don't stop processing other devices
						System.Diagnostics.Debug.WriteLine($"Step2: Failed to load capabilities for {device.DisplayName} ({device.InputMethod}): {ex.Message}");
						JocysCom.ClassLibrary.Runtime.LogHelper.Current.WriteException(ex);

						// Clear flags even on failure to prevent infinite retry
						device.CapabilitiesNeedLoading = false;
						device.InputMethodChanged = false;
					}
				}
			}
		}

		/// <summary>
		/// Loads device capabilities using the appropriate processor based on the device's input method.
		/// This is the centralized entry point for capability loading across all input methods.
		/// </summary>
		/// <param name="device">The device to load capabilities for</param>
		/// <remarks>
		/// This method ensures capabilities are loaded consistently across all input methods:
		/// • DirectInput: Real hardware detection via DirectInput API (default method)
		/// • XInput: Standard Xbox controller capabilities (15 buttons, 6 axes)
		/// • Gaming Input: Gaming Input API capabilities (16 buttons, 6 axes)
		/// • Raw Input: HID descriptor-based capabilities with reasonable defaults
		///
		/// Called during device initialization and when input method changes.
		/// Handles capability loading failures gracefully with appropriate fallbacks.
		/// </remarks>
		public void LoadDeviceCapabilities(UserDevice device)
		{
			if (device == null)
				return;

			try
			{
				switch (device.InputMethod)
				{
					case InputMethod.DirectInput:
						directInputProcessor.LoadCapabilities(device);
						break;
					case InputMethod.XInput:
						xInputProcessor.LoadCapabilities(device);
						break;
					case InputMethod.GamingInput:
						gamingInputProcessor.LoadCapabilities(device);
						break;
					case InputMethod.RawInput:
						rawInputProcessor.LoadCapabilities(device);
						break;
					default:
						throw new ArgumentException($"Invalid InputMethod: {device.InputMethod}");
				}

				System.Diagnostics.Debug.WriteLine($"Loaded {device.InputMethod} capabilities for {device.DisplayName} - Buttons: {device.CapButtonCount}, Axes: {device.CapAxeCount}, POVs: {device.CapPovCount}");
			}
			catch (Exception ex)
			{
				// Log error and clear capability values
				System.Diagnostics.Debug.WriteLine($"Capability loading failed for {device.DisplayName} ({device.InputMethod}): {ex.Message}");

				// Clear capability values instead of setting fake ones
				device.CapButtonCount = 0;
				device.CapAxeCount = 0;
				device.CapPovCount = 0;
				device.DeviceObjects = new DeviceObjectItem[0];
				device.DeviceEffects = new DeviceEffectItem[0];
			}
		}

		/// <summary>
		/// Gets detailed capability information using the appropriate processor.
		/// </summary>
		/// <param name="device">The device to get capability information for</param>
		/// <returns>String containing detailed capability information</returns>
		public string GetDeviceCapabilitiesInfo(UserDevice device)
		{
			if (device == null)
				return "Device is null";

			try
			{
				switch (device.InputMethod)
				{
					case InputMethod.DirectInput:
						return directInputProcessor.GetCapabilitiesInfo(device);
					case InputMethod.XInput:
						return xInputProcessor.GetCapabilitiesInfo(device);
					case InputMethod.GamingInput:
						return gamingInputProcessor.GetCapabilitiesInfo(device);
					case InputMethod.RawInput:
						return rawInputProcessor.GetCapabilitiesInfo(device);
					default:
						return $"Unknown InputMethod: {device.InputMethod}";
				}
			}
			catch (Exception ex)
			{
				return $"Error getting capability info: {ex.Message}";
			}
		}
	}
}
