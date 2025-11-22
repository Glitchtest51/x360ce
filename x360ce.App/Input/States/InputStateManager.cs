using System;
using System.Diagnostics;
using System.Windows.Threading;
using x360ce.App.Input.Devices;

namespace x360ce.App.Input.States
{
    /// <summary>
    /// Gets current states from input devices and saves them to device info ListInputState property.
    /// </summary>
    /// <remarks>
    /// State Collection Strategy:
    ///
    /// A. Polling (20 Hz) - for devices which support polling:
    ///    States are retrieved at rate 20 times per second via internal timer.
    ///    Used by: DirectInput (joysticks), XInput, GamingInput
    ///
    /// B. Event Driven - for devices which report changes via events or messages:
    ///    States are retrieved from messages which report changes immediately.
    ///    Used by: RawInput (WM_INPUT messages at 1000+ Hz) - NO POLLING, purely event-driven
    ///
    /// State Processing:
    /// • If full state is received from device, convert to ListInputState and save in device info ListInputState property
    /// • If only changes are received, analyze last saved state and changes to form and save new state
    ///
    /// Device Lists:
    /// • RawInputDeviceInfoList - x360ce.App/Input/Devices/RawInputDeviceInfo.cs
    /// • DirectInputDeviceInfoList - x360ce.App/Input/Devices/DirectInputDeviceInfo.cs
    /// • XInputDeviceInfoList - x360ce.App/Input/Devices/XInputDeviceInfo.cs
    /// • GamingInputDeviceInfoList - x360ce.App/Input/Devices/GamingInputDeviceInfo.cs
    /// </remarks>
    internal class InputStateManager
    {
        #region Singleton Pattern

        private static readonly object _lock = new object();
        private static InputStateManager _instance;

        /// <summary>
        /// Gets the singleton instance of GetAndSaveStates.
        /// </summary>
        public static InputStateManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new InputStateManager();
                        }
                    }
                }
                return _instance;
            }
        }

        #endregion

        #region State Collection Timer

        private DispatcherTimer _statePollingTimer;
        private const int StateCollectionIntervalMs = 50; // 20 Hz (50ms interval) for polling-based devices
        private UnifiedInputDeviceManager _deviceManager;

        /// <summary>
        /// Starts the continuous state collection timer at 20Hz.
        /// Must be called from UI thread with valid device manager reference.
        /// </summary>
        /// <param name="deviceManager">Device manager containing device lists to monitor</param>
        public void StartStateCollection(UnifiedInputDeviceManager deviceManager)
        {
            if (deviceManager == null)
                throw new ArgumentNullException(nameof(deviceManager));

            // Start polling DirectInput, XInput, GamingInput devices for states at 20Hz.
            // RawInput is purely event-driven via WM_INPUT messages - no polling needed.

            _deviceManager = deviceManager;

            if (_statePollingTimer == null)
            {
                _statePollingTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(StateCollectionIntervalMs)
                };
                _statePollingTimer.Tick += StateCollectionTimer_Tick;
            }

            if (!_statePollingTimer.IsEnabled)
                _statePollingTimer.Start();
        }

        /// <summary>
        /// Stops the continuous state collection timer.
        /// </summary>
        public void StopStateCollection()
        {
            // Stop polling DirectInput, XInput, GamingInput devices for states at 20Hz.
            _statePollingTimer?.Stop();
        }

        /// <summary>
        /// Timer tick handler that collects states from polling-based devices only (20Hz).
        /// RawInput is event-driven and updates states immediately when WM_INPUT messages arrive.
        /// </summary>
        private void StateCollectionTimer_Tick(object sender, EventArgs e)
        {
            if (_deviceManager == null)
                return;

            try
            {
                // Collect states for POLLING-BASED devices only (20Hz).
                GetAndSaveDirectInputStates(_deviceManager.DirectInputDeviceInfoList);
                GetAndSaveXInputStates(_deviceManager.XInputDeviceInfoList);
                GetAndSaveGamingInputStates(_deviceManager.GamingInputDeviceInfoList);

                // REMOVED: RawInput polling - it's purely event-driven via WM_INPUT messages
                // GetAndSaveRawInputStates(_deviceManager.RawInputDeviceInfoList);
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
        private readonly XInputState _xInputState;
        private readonly GamingInputState _gamingInputState;

        #endregion

        /// <summary>
        /// Initializes the state reader with singleton state readers for each input method.
        /// </summary>
        private InputStateManager()
        {
            _rawInputState = RawInputState.rawInputState;
            _directInputState = new DirectInputState();
            _xInputState = new XInputState();
            _gamingInputState = new GamingInputState();
        }

        #region DirectInput State Collection (Polling)

        /// <summary>
        /// Gets and saves DirectInput device state as ListInputState to DirectInputDeviceInfo ListInputState property.
        /// <param name="diDeviceInfo">DirectInput device information</param>
        /// </summary>
        /// <returns>True if state was successfully retrieved and saved</returns>
        public bool GetDirectInputStateAndSaveAsListInputState(DirectInputDeviceInfo diDeviceInfo)
        {          
            if (diDeviceInfo == null)
                return false;

            try
            {
                // Get DirectInput device state.
                object diState = _directInputState.GetDirectInputState(diDeviceInfo);

                if (diState == null)
                    return false;

                // Convert DirectInput state to ListInputState.
                var liState = DirectInputStateToListInputState.ConvertDirectInputStateToListInputState(diState, diDeviceInfo);

                if (liState == null)
                    return false;

                // Save ListInputState to DirectInputDeviceInfo ListInputState property.
                diDeviceInfo.ListInputState = liState;
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetAndSaveStates: Error getting DirectInput state for {diDeviceInfo.InstanceName}: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region XInput State Collection (Polling)

        /// <summary>
        /// Gets and saves XInput device state as ListInputState to XInputDeviceInfo ListInputState property.
        /// </summary>
        /// <param name="xiDeviceInfo">XInput device information</param>
        /// <returns>True if state was successfully retrieved and saved</returns>
        public bool GetAndSaveXInputState(XInputDeviceInfo xiDeviceInfo)
        {
            if (xiDeviceInfo == null)
                return false;

            try
            {
                // Get DirectInput device state.
                var xiState = _xInputState.GetXInputState(xiDeviceInfo);

                if (!xiState.HasValue)
                    return false;

                // Convert XInput state to ListInputState.
                var liState = XInputStateToListInputState.ConvertXInputStateToListInputState(xiState.Value);

                if (liState == null)
                    return false;

                // Save XInputState to XInputDeviceInfo ListInputState property.
                xiDeviceInfo.ListInputState = liState;
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetAndSaveStates: Error getting XInput state for {xiDeviceInfo.InstanceName}: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region GamingInput State Collection (Polling)

        /// <summary>
        /// Gets and saves GamingInput device state as ListInputState to GamingInputDeviceInfo ListInputState property.
        /// </summary>
        /// <param name="giDeviceInfo">GamingInput device information</param>
        /// <returns>True if state was successfully retrieved and saved</returns>
        public bool GetAndSaveGamingInputState(GamingInputDeviceInfo giDeviceInfo)
        {
            if (giDeviceInfo == null)
                return false;

            try
            {
                // Poll Gaming Input gamepad for current state
                var giState = _gamingInputState.GetGamingInputState(giDeviceInfo);

                if (!giState.HasValue)
                    return false;

                // Convert Gaming Input reading to InputStateAsList format
                var liState = GamingInputStateToListInputState.ConvertGamingInputStateToListInputState(giState);

                if (liState == null)
                    return false;

                // Save to device info ListInputState property
                giDeviceInfo.ListInputState = liState;
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetAndSaveStates: Error getting GamingInput state for {giDeviceInfo.InstanceName}: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Batch State Collection

        /// <summary>
        /// Gets and saves states for all DirectInput devices in the list.
        /// </summary>
        /// <param name="diDeviceList">List of DirectInput devices</param>
        /// <returns>Number of devices successfully updated</returns>
        public int GetAndSaveDirectInputStates(System.Collections.Generic.List<DirectInputDeviceInfo> diDeviceList)
        {
            if (diDeviceList == null)
                return 0;

            int successCount = 0;
            foreach (var device in diDeviceList)
            {
                if (GetDirectInputStateAndSaveAsListInputState(device))
                    successCount++;
            }
            return successCount;
        }

        /// <summary>
        /// Gets and saves states for all XInput devices in the list.
        /// </summary>
        /// <param name="xiDeviceList">List of XInput devices</param>
        /// <returns>Number of devices successfully updated</returns>
        public int GetAndSaveXInputStates(System.Collections.Generic.List<XInputDeviceInfo> xiDeviceList)
        {
            if (xiDeviceList == null)
                return 0;

            int successCount = 0;
            foreach (var device in xiDeviceList)
            {
                if (GetAndSaveXInputState(device))
                    successCount++;
            }
            return successCount;
        }

        /// <summary>
        /// Gets and saves states for all GamingInput devices in the list.
        /// </summary>
        /// <param name="giDeviceList">List of GamingInput devices</param>
        /// <returns>Number of devices successfully updated</returns>
        public int GetAndSaveGamingInputStates(System.Collections.Generic.List<GamingInputDeviceInfo> giDeviceList)
        {
            if (giDeviceList == null)
                return 0;

            int successCount = 0;
            foreach (var device in giDeviceList)
            {
                if (GetAndSaveGamingInputState(device))
                    successCount++;
            }
            return successCount;
        }

        #endregion
    }
}
