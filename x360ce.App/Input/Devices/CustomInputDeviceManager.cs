using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using x360ce.App.Input.States;

namespace x360ce.App.Input.Devices
{
    /// <summary>
    /// Combined device management class that orchestrates different input device types.
    /// Provides custom access to DirectInput, XInput, and other input methods.
    /// </summary>
    internal class CustomInputDeviceManager
    {
        private readonly PnPInputDevice pnPInputDevice = new PnPInputDevice();
        private readonly RawInputDevice rawInputDevice = new RawInputDevice();
        private readonly DirectInputDevice directInputDevice = new DirectInputDevice();
        private readonly XInputDevice xInputDevice = new XInputDevice();
        private readonly GamingInputDevice gamingInputDevice = new GamingInputDevice();

        public List<PnPInputDeviceInfo> PnPInputDeviceInfoList = new List<PnPInputDeviceInfo>();
        // RawInputDeviceInfoList is now a direct reference to the static authoritative list
        public List<RawInputDeviceInfo> RawInputDeviceInfoList => RawInputDevice.RawInputDeviceInfoList;
        public List<DirectInputDeviceInfo> DirectInputDeviceInfoList = new List<DirectInputDeviceInfo>();
        public List<XInputDeviceInfo> XInputDeviceInfoList = new List<XInputDeviceInfo>();
        public List<GamingInputDeviceInfo> GamingInputDeviceInfoList = new List<GamingInputDeviceInfo>();
        public ObservableCollection<CustomInputDeviceInfo> CustomInputDeviceInfoList = new ObservableCollection<CustomInputDeviceInfo>();

        // Cache for DirectInput product names to avoid repeated lookups
        private Dictionary<string, string> _directInputNameCache;

        // State collection manager for all device types
        private readonly CustomInputStateTimer _stateCollector = CustomInputStateTimer.Instance;

        // Dispatcher for UI thread synchronization
        private System.Windows.Threading.Dispatcher _dispatcher;

        /// <summary>
        /// Sets the dispatcher for UI thread synchronization.
        /// Must be called from the UI thread before device monitoring starts.
        /// </summary>
        public void SetDispatcher(System.Windows.Threading.Dispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }
        /// <summary>
        /// Creates and populates all input device lists from various input sources:
        /// A. On InputDevicesControl loaded.
        /// B. On Refresh button click.
        /// C. On Input device connection change.
        /// Updates lists incrementally: existing devices are updated in place, new devices are added, removed devices are deleted.
        /// </summary>
        public void GetCustomInputDeviceList()
        {
            // Retrieve current device lists from all input sources
            var currentPnPList = pnPInputDevice.GetPnPInputDeviceInfoList();
            // Populate the static RawInputDeviceInfoList (no return value, updates static list directly)
            rawInputDevice.GetRawInputDeviceInfoList();
            var currentDirectInputList = directInputDevice.GetDirectInputDeviceInfoList();
            var currentXInputList = xInputDevice.GetXInputDeviceInfoList();
            var currentGamingInputList = gamingInputDevice.GetGamingInputDeviceInfoList();

            // Update PnPInputDeviceInfoList incrementally
            UpdateDeviceList(PnPInputDeviceInfoList, currentPnPList, d => d.InstanceGuid);

            // RawInputDeviceInfoList is already updated by rawInputDevice.GetRawInputDeviceInfoList()
            // No need to call UpdateDeviceList - it's the authoritative static list that's directly modified

            // Update DirectInputDeviceInfoList incrementally
            UpdateDeviceList(DirectInputDeviceInfoList, currentDirectInputList, d => d.InstanceGuid);

            // Update XInputDeviceInfoList incrementally
            UpdateDeviceList(XInputDeviceInfoList, currentXInputList, d => d.InstanceGuid);

            // Update GamingInputDeviceInfoList incrementally
            UpdateDeviceList(GamingInputDeviceInfoList, currentGamingInputList, d => d.InstanceGuid);

            // Build DirectInput name cache for efficient lookups
            BuildDirectInputNameCache();
    
            // Update the custom list incrementally instead of clearing
            UpdateCustomInputDeviceList();
    
            // RawInputState now directly accesses RawInputDevice.RawInputDeviceInfoList
            // No SetDeviceList call needed - simplified architecture eliminates the method!
    
            // Collect and save states for all devices after enumeration
            // CollectDeviceStates();
        }

        /// <summary>
        /// Updates the custom device list incrementally by comparing with source lists.
        /// Removes devices no longer present, updates existing devices, and adds new devices.
        /// Ensures all ObservableCollection modifications happen on the UI thread.
        /// </summary>
        private void UpdateCustomInputDeviceList()
        {
            // Build a set of all current device identifiers from source lists
            var currentDeviceKeys = new HashSet<string>();

            // Collect all current device keys
            CollectDeviceKeys(DirectInputDeviceInfoList, currentDeviceKeys);
            CollectDeviceKeys(PnPInputDeviceInfoList, currentDeviceKeys);
            CollectDeviceKeys(RawInputDeviceInfoList, currentDeviceKeys);
            CollectDeviceKeys(XInputDeviceInfoList, currentDeviceKeys);
            CollectDeviceKeys(GamingInputDeviceInfoList, currentDeviceKeys);

            // Execute ObservableCollection modifications on UI thread
            var action = new Action(() =>
            {
                // Remove devices that are no longer present
                for (int i = CustomInputDeviceInfoList.Count - 1; i >= 0; i--)
                {
                    var device = CustomInputDeviceInfoList[i];
                    var key = GetDeviceKey(device.InputType, device.CommonIdentifier);
                    if (!currentDeviceKeys.Contains(key))
                    {
                        CustomInputDeviceInfoList.RemoveAt(i);
                    }
                }

                // Update existing devices and add new ones
                UpdateOrAddDevicesFromList(DirectInputDeviceInfoList, (item, _) => item.ProductName, item => item.InterfacePath);
                UpdateOrAddDevicesFromList(PnPInputDeviceInfoList, GetPrefixedProductName, item => item.HardwareIds);
                UpdateOrAddDevicesFromList(RawInputDeviceInfoList, GetPrefixedProductName, item => item.InterfacePath);
                UpdateOrAddDevicesFromList(XInputDeviceInfoList, GetPrefixedProductName, item => item.InterfacePath);
                UpdateOrAddDevicesFromList(GamingInputDeviceInfoList, GetPrefixedProductName, item => item.InterfacePath);
            });

            // If dispatcher is available and we're not on UI thread, invoke on UI thread
            if (_dispatcher != null && !_dispatcher.CheckAccess())
            {
                _dispatcher.Invoke(action);
            }
            else
            {
                // Already on UI thread or no dispatcher set, execute directly
                action();
            }
        }

        /// <summary>
        /// Collects device keys from a source list into the provided set.
        /// </summary>
        private void CollectDeviceKeys<T>(List<T> sourceList, HashSet<string> keySet) where T : class
        {
            if (sourceList == null)
                return;

            foreach (var item in sourceList)
            {
                dynamic device = item;
                var key = GetDeviceKey(device.InputType, device.CommonIdentifier);
                keySet.Add(key);
            }
        }

        /// <summary>
        /// Generates a unique key for a device based on input type and common identifier.
        /// </summary>
        private string GetDeviceKey(string inputType, string commonIdentifier)
        {
            return $"{inputType}|{commonIdentifier}";
        }

        /// <summary>
        /// Updates existing devices or adds new devices from a source list to the custom list.
        /// </summary>
        private void UpdateOrAddDevicesFromList<T>(
         List<T> sourceList,
         Func<T, string, string> getProductName,
         Func<T, string> getInterfacePath) where T : class
        {
            if (sourceList == null)
                return;

            foreach (var item in sourceList)
            {
                dynamic device = item;
                string commonId = device.CommonIdentifier;
                string inputType = device.InputType;
                var key = GetDeviceKey(inputType, commonId);

                // Find existing device in custom list
                var existingDevice = CustomInputDeviceInfoList.FirstOrDefault(d =>
                 GetDeviceKey(d.InputType, d.CommonIdentifier) == key);

                if (existingDevice != null)
                {
                	// Update existing device properties
                	   // Since CustomInputDeviceInfo wraps the source device by reference,
                	   // most properties are already up to date if the source object is the same.
                	   // However, computed properties like ProductName (with prefix) need explicit update.
                	   
                	   // CRITICAL: Update the underlying device reference.
                	   // Required for RawInput which creates new device instances on enumeration.
                	   existingDevice.SetDevice(item as InputDeviceInfo);

                	existingDevice.ProductName = getProductName(item, commonId);
                	existingDevice.InterfacePath = getInterfacePath(item);
                }
                else
                {
                	// Add new device
                	var newDevice = new CustomInputDeviceInfo(item as InputDeviceInfo)
                	{
                		AxePressed = false,
                		SliderPressed = false,
                		ButtonPressed = false,
                		PovPressed = false,
                		ProductName = getProductName(item, commonId),
                		InterfacePath = getInterfacePath(item),
                    };
                	CustomInputDeviceInfoList.Add(newDevice);
                }
            }
        }

        /// <summary>
        /// Updates a device list incrementally: existing devices are updated in place, new devices are added, removed devices are deleted.
        /// </summary>
        /// <typeparam name="T">Device info type</typeparam>
        /// <param name="existingList">Existing device list to update</param>
        /// <param name="currentList">Current device list from enumeration</param>
        /// <param name="keySelector">Function to extract unique identifier</param>
        private void UpdateDeviceList<T>(List<T> existingList, List<T> currentList, Func<T, Guid> keySelector) where T : class
        {
            if (currentList == null)
                return;

            // Create dictionaries for efficient lookup
            var existingDict = existingList.ToDictionary(keySelector);
            var currentDict = currentList.ToDictionary(keySelector);

            // CRITICAL FIX: Store ListInputState from devices that will be removed
            // This preserves state when devices are temporarily removed and re-added
            var preservedStates = new Dictionary<Guid, object>();
            for (int i = existingList.Count - 1; i >= 0; i--)
            {
                var device = existingList[i];
                var key = keySelector(device);
                if (!currentDict.ContainsKey(key))
                {
                    // Device is being removed - preserve its ListInputState if it has one
                    var listInputStateProp = device.GetType().GetProperty("CustomInputState");
                    if (listInputStateProp != null)
                    {
                        var state = listInputStateProp.GetValue(device);
                        if (state != null)
                        {
                            preservedStates[key] = state;
                            System.Diagnostics.Debug.WriteLine($"CustomInputDeviceManager.UpdateDeviceList: Preserving CustomInputState for device {key}");
                        }
                    }
                    existingList.RemoveAt(i);
                }
            }

            // Add new devices and update existing ones
            foreach (var currentDevice in currentList)
            {
                var key = keySelector(currentDevice);
                if (existingDict.ContainsKey(key))
                {
                    // CRITICAL FIX: Update existing device properties IN PLACE instead of replacing the object
                    // This preserves the ListInputState property that is updated by event-driven RawInput processing
                    var existingDevice = existingDict[key];

                    // Copy all properties from currentDevice to existingDevice
                    // This is done using reflection to handle all device types generically
                    var properties = currentDevice.GetType().GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    foreach (var prop in properties)
                    {
                        // Skip ListInputState property - it's updated by event-driven processing
                        if (prop.Name == "CustomInputState")
                            continue;

                        // Only copy writable properties
                        if (prop.CanWrite && prop.CanRead)
                        {
                            try
                            {
                                var value = prop.GetValue(currentDevice);
                                prop.SetValue(existingDevice, value);
                            }
                            catch
                            {
                                // Skip properties that can't be copied
                            }
                        }
                    }
                }
                else
                {
                    // Add new device
                    existingList.Add(currentDevice);
                    
                    // CRITICAL FIX: Restore preserved ListInputState if this device was temporarily removed
                    if (preservedStates.ContainsKey(key))
                    {
                        var listInputStateProp = currentDevice.GetType().GetProperty("CustomInputState");
                        if (listInputStateProp != null && listInputStateProp.CanWrite)
                        {
                            listInputStateProp.SetValue(currentDevice, preservedStates[key]);
                            System.Diagnostics.Debug.WriteLine($"CustomInputDeviceManager.UpdateDeviceList: Restored CustomInputState for device {key}");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Builds a cache of DirectInput product names indexed by truncated common identifier.
        /// This eliminates repeated LINQ queries during device processing.
        /// </summary>
        private void BuildDirectInputNameCache()
        {
            _directInputNameCache = new Dictionary<string, string>();

            if (DirectInputDeviceInfoList == null)
                return;

            foreach (var device in DirectInputDeviceInfoList)
            {
                if (string.IsNullOrEmpty(device.CommonIdentifier))
                    continue;

                var key = device.CommonIdentifier.Length > 17
                    ? device.CommonIdentifier.Substring(0, 17)
                    : device.CommonIdentifier;

                // Store first match only (consistent with original behavior)
                if (!_directInputNameCache.ContainsKey(key))
                    _directInputNameCache[key] = device.ProductName;
            }
        }

        /// <summary>
        /// Gets product name with DirectInput prefix for non-DirectInput devices.
        /// </summary>
        private string GetPrefixedProductName<T>(T item, string commonIdentifier) where T : class
        {
            var prefix = GetDirectInputProductNameFromCache(commonIdentifier);
            dynamic device = item;
            return prefix + device.ProductName;
        }

        /// <summary>
        /// Retrieves DirectInput product name from cache using truncated common identifier.
        /// Returns empty string if not found or cache is unavailable.
        /// </summary>
        /// <param name="commonIdentifier">The device's common identifier</param>
        /// <returns>DirectInput product name with separator, or empty string</returns>
        private string GetDirectInputProductNameFromCache(string commonIdentifier)
        {
            if (_directInputNameCache == null || string.IsNullOrEmpty(commonIdentifier))
                return string.Empty;

            var key = commonIdentifier.Length > 17
                ? commonIdentifier.Substring(0, 17)
                : commonIdentifier;

            return _directInputNameCache.TryGetValue(key, out var productName)
                ? productName + " • "
                : string.Empty;
        }

        /// <summary>
        /// Collects and saves current states for all enumerated devices.
        /// This populates the ListInputState property on each device info object.
        /// Note: RawInput states are updated event-driven (1000+ Hz) when WM_INPUT arrives,
        /// so we only need to collect initial states here for consistency.
        /// </summary>
        //private void CollectDeviceStates()
        //{
        //    // Collect initial states for RawInput (event-driven updates happen at 1000+ Hz via WM_INPUT)
        //    _stateCollector.GetAndSaveRawInputStates(RawInputDeviceInfoList);

        //    // Collect states for polling-based devices (updated at 20Hz by timer)
        //    _stateCollector.GetAndSaveDirectInputStates(DirectInputDeviceInfoList);
        //    _stateCollector.GetAndSaveXInputStates(XInputDeviceInfoList);
        //    _stateCollector.GetAndSaveGamingInputStates(GamingInputDeviceInfoList);
        //}

    }
}
