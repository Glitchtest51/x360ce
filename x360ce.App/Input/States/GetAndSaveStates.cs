using System;
using System.Diagnostics;
using System.Windows.Threading;
using x360ce.App.Input.Devices;

namespace x360ce.App.Input.States
{
	/// <summary>
	/// Gets current states from input devices and saves them to device info StateList property.
	/// Handles both polling-based and event-driven state collection.
	/// Runs a 10Hz timer for continuous state collection from polling-based devices.
	/// </summary>
	/// <remarks>
	/// State Collection Strategy:
	///
	/// A. Polling (10 Hz) - for devices which support polling:
	///    States are retrieved at rate 10 times per second via internal timer.
	///    Used by: DirectInput (joysticks), XInput, GamingInput
	///
	/// B. Event Driven - for devices which report changes via events or messages:
	///    States are retrieved from messages which report changes immediately.
	///    Used by: RawInput (WM_INPUT messages at 1000+ Hz)
	///
	/// State Processing:
	/// • If full state is received from device, convert to InputStateAsList and save in device info StateList property
	/// • If only changes are received, analyze last saved state and changes to form and save new state
	///
	/// Device Lists:
	/// • RawInputDeviceInfoList - x360ce.App/Input/Devices/RawInputDeviceInfo.cs
	/// • DirectInputDeviceInfoList - x360ce.App/Input/Devices/DirectInputDeviceInfo.cs
	/// • XInputDeviceInfoList - x360ce.App/Input/Devices/XInputDeviceInfo.cs
	/// • GamingInputDeviceInfoList - x360ce.App/Input/Devices/GamingInputDeviceInfo.cs
	/// </remarks>
	internal class GetAndSaveStates
	{
		#region Singleton Pattern

		private static readonly object _lock = new object();
		private static GetAndSaveStates _instance;

		/// <summary>
		/// Gets the singleton instance of GetAndSaveStates.
		/// </summary>
		public static GetAndSaveStates Instance
		{
			get
			{
				if (_instance == null)
				{
					lock (_lock)
					{
						if (_instance == null)
						{
							_instance = new GetAndSaveStates();
						}
					}
				}
				return _instance;
			}
		}

		#endregion

		#region State Collection Timer

		private DispatcherTimer _stateCollectionTimer;
		private const int StateCollectionIntervalMs = 100; // 10 Hz (100ms interval)
		private UnifiedInputDeviceManager _deviceManager;

		/// <summary>
		/// Starts the continuous state collection timer at 10Hz.
		/// Must be called from UI thread with valid device manager reference.
		/// </summary>
		/// <param name="deviceManager">Device manager containing device lists to monitor</param>
		public void StartStateCollection(UnifiedInputDeviceManager deviceManager)
		{
			if (deviceManager == null)
				throw new ArgumentNullException(nameof(deviceManager));

			_deviceManager = deviceManager;

			if (_stateCollectionTimer == null)
			{
				_stateCollectionTimer = new DispatcherTimer
				{
					Interval = TimeSpan.FromMilliseconds(StateCollectionIntervalMs)
				};
				_stateCollectionTimer.Tick += StateCollectionTimer_Tick;
			}

			if (!_stateCollectionTimer.IsEnabled)
				_stateCollectionTimer.Start();
		}

		/// <summary>
		/// Stops the continuous state collection timer.
		/// </summary>
		public void StopStateCollection()
		{
			_stateCollectionTimer?.Stop();
		}

		/// <summary>
		/// Timer tick handler that collects states from all device types at 10Hz.
		/// </summary>
		private void StateCollectionTimer_Tick(object sender, EventArgs e)
		{
			if (_deviceManager == null)
				return;

			try
			{
				// Collect states for all device types (10Hz for polling-based devices)
				// RawInput states are event-driven but we still poll to ensure StateList is current
				GetAndSaveRawInputStates(_deviceManager.RawInputDeviceInfoList);
				GetAndSaveDirectInputStates(_deviceManager.DirectInputDeviceInfoList);
				GetAndSaveXInputStates(_deviceManager.XInputDeviceInfoList);
				GetAndSaveGamingInputStates(_deviceManager.GamingInputDeviceInfoList);
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"GetAndSaveStates: Error in state collection timer: {ex.Message}");
			}
		}

		#endregion

		#region State Readers

		private readonly RawInputState _rawInputState;
		private readonly DirectInputState _directInputState;
		private readonly DirectInputStateToList _directInputStateToList;
		private readonly XInputState _xInputState;
		private readonly GamingInputState _gamingInputState;

		#endregion

		/// <summary>
		/// Initializes the state reader with singleton state readers for each input method.
		/// </summary>
		private GetAndSaveStates()
		{
			_rawInputState = RawInputState.Instance;
			_directInputState = new DirectInputState();
			_directInputStateToList = new DirectInputStateToList();
			_xInputState = new XInputState();
			_gamingInputState = new GamingInputState();
		}

		#region RawInput State Collection (Event-Driven)

		/// <summary>
		/// Gets and saves RawInput device state (event-driven via WM_INPUT messages).
		/// RawInput states are cached from WM_INPUT messages and retrieved here.
		/// </summary>
		/// <param name="deviceInfo">RawInput device information</param>
		/// <returns>True if state was successfully retrieved and saved</returns>
		public bool GetAndSaveRawInputState(RawInputDeviceInfo deviceInfo)
		{
			if (deviceInfo == null)
				return false;

			try
			{
				// Get cached state from RawInput message handler (non-blocking)
				byte[] rawReport = _rawInputState.GetRawInputState(deviceInfo);
				
				if (rawReport == null || rawReport.Length == 0)
					return false;

				// Convert raw HID report to InputStateAsList format
				var stateList = RawInputStateToList.ConvertRawInputStateToList(rawReport, deviceInfo);
				
				if (stateList == null)
					return false;

				// Save to device info StateList property
				deviceInfo.StateList = stateList;
				return true;
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"GetAndSaveStates: Error getting RawInput state for {deviceInfo.InstanceName}: {ex.Message}");
				return false;
			}
		}

		#endregion

		#region DirectInput State Collection (Polling)

		/// <summary>
		/// Gets and saves DirectInput device state (polling-based).
		/// Polls the device for current state at 10 Hz.
		/// </summary>
		/// <param name="deviceInfo">DirectInput device information</param>
		/// <returns>True if state was successfully retrieved and saved</returns>
		public bool GetAndSaveDirectInputState(DirectInputDeviceInfo deviceInfo)
		{
			if (deviceInfo == null)
				return false;

			try
			{
				// Poll device for current state
				object diState = _directInputState.GetDirectInputState(deviceInfo);
				
				if (diState == null)
					return false;

				// Convert DirectInput state to InputStateAsList format with sensitivity control
				var stateList = _directInputStateToList.ConvertDirectInputStateToList(diState, deviceInfo);
				
				if (stateList == null)
					return false;

				// Save to device info StateList property
				deviceInfo.StateList = stateList;
				return true;
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"GetAndSaveStates: Error getting DirectInput state for {deviceInfo.InstanceName}: {ex.Message}");
				return false;
			}
		}

		#endregion

		#region XInput State Collection (Polling)

		/// <summary>
		/// Gets and saves XInput device state (polling-based).
		/// Polls the XInput controller for current state at 10 Hz.
		/// </summary>
		/// <param name="deviceInfo">XInput device information</param>
		/// <returns>True if state was successfully retrieved and saved</returns>
		public bool GetAndSaveXInputState(XInputDeviceInfo deviceInfo)
		{
			if (deviceInfo == null)
				return false;

			try
			{
				// Poll XInput controller for current state
				var xiState = _xInputState.GetXInputState(deviceInfo);
				
				if (!xiState.HasValue)
					return false;

				// Convert XInput state to InputStateAsList format
				var stateList = XInputStateToList.ConvertXInputStateToList(xiState.Value);
				
				if (stateList == null)
					return false;

				// Save to device info StateList property
				deviceInfo.StateList = stateList;
				return true;
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"GetAndSaveStates: Error getting XInput state for {deviceInfo.InstanceName}: {ex.Message}");
				return false;
			}
		}

		#endregion

		#region GamingInput State Collection (Polling)

		/// <summary>
		/// Gets and saves GamingInput device state (polling-based).
		/// Polls the Gaming Input gamepad for current state at 10 Hz.
		/// </summary>
		/// <param name="deviceInfo">GamingInput device information</param>
		/// <returns>True if state was successfully retrieved and saved</returns>
		public bool GetAndSaveGamingInputState(GamingInputDeviceInfo deviceInfo)
		{
			if (deviceInfo == null)
				return false;

			try
			{
				// Poll Gaming Input gamepad for current state
				var giReading = _gamingInputState.GetGamingInputState(deviceInfo);
				
				if (!giReading.HasValue)
					return false;

				// Convert Gaming Input reading to InputStateAsList format
				var stateList = GamingInputStateToList.ConvertGamingInputStateToList(giReading);
				
				if (stateList == null)
					return false;

				// Save to device info StateList property
				deviceInfo.StateList = stateList;
				return true;
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"GetAndSaveStates: Error getting GamingInput state for {deviceInfo.InstanceName}: {ex.Message}");
				return false;
			}
		}

		#endregion

		#region Batch State Collection

		/// <summary>
		/// Gets and saves states for all RawInput devices in the list.
		/// </summary>
		/// <param name="deviceList">List of RawInput devices</param>
		/// <returns>Number of devices successfully updated</returns>
		public int GetAndSaveRawInputStates(System.Collections.Generic.List<RawInputDeviceInfo> deviceList)
		{
			if (deviceList == null)
				return 0;

			int successCount = 0;
			foreach (var device in deviceList)
			{
				if (GetAndSaveRawInputState(device))
					successCount++;
			}
			return successCount;
		}

		/// <summary>
		/// Gets and saves states for all DirectInput devices in the list.
		/// </summary>
		/// <param name="deviceList">List of DirectInput devices</param>
		/// <returns>Number of devices successfully updated</returns>
		public int GetAndSaveDirectInputStates(System.Collections.Generic.List<DirectInputDeviceInfo> deviceList)
		{
			if (deviceList == null)
				return 0;

			int successCount = 0;
			foreach (var device in deviceList)
			{
				if (GetAndSaveDirectInputState(device))
					successCount++;
			}
			return successCount;
		}

		/// <summary>
		/// Gets and saves states for all XInput devices in the list.
		/// </summary>
		/// <param name="deviceList">List of XInput devices</param>
		/// <returns>Number of devices successfully updated</returns>
		public int GetAndSaveXInputStates(System.Collections.Generic.List<XInputDeviceInfo> deviceList)
		{
			if (deviceList == null)
				return 0;

			int successCount = 0;
			foreach (var device in deviceList)
			{
				if (GetAndSaveXInputState(device))
					successCount++;
			}
			return successCount;
		}

		/// <summary>
		/// Gets and saves states for all GamingInput devices in the list.
		/// </summary>
		/// <param name="deviceList">List of GamingInput devices</param>
		/// <returns>Number of devices successfully updated</returns>
		public int GetAndSaveGamingInputStates(System.Collections.Generic.List<GamingInputDeviceInfo> deviceList)
		{
			if (deviceList == null)
				return 0;

			int successCount = 0;
			foreach (var device in deviceList)
			{
				if (GetAndSaveGamingInputState(device))
					successCount++;
			}
			return successCount;
		}

		#endregion

		#region Diagnostic Methods

		/// <summary>
		/// Gets diagnostic information about the state collection system.
		/// </summary>
		/// <returns>String containing diagnostic information</returns>
		public string GetDiagnosticInfo()
		{
			var info = new System.Text.StringBuilder();
			info.AppendLine("=== GetAndSaveStates Diagnostic Information ===");
			info.AppendLine();
			info.AppendLine("State Collection Methods:");
			info.AppendLine("  • RawInput: Event-driven (WM_INPUT messages at 1000+ Hz)");
			info.AppendLine("  • DirectInput: Polling-based (10 Hz)");
			info.AppendLine("  • XInput: Polling-based (10 Hz)");
			info.AppendLine("  • GamingInput: Polling-based (10 Hz)");
			info.AppendLine();
			info.AppendLine("State Format: InputStateAsList");
			info.AppendLine("  • Axes: 0-65535 range");
			info.AppendLine("  • Sliders: 0-65535 range");
			info.AppendLine("  • Buttons: 0=released, 1=pressed");
			info.AppendLine("  • POVs: -1=neutral, 0-27000 centidegrees");
			
			return info.ToString();
		}

		#endregion
	}
}
