using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace x360ce.App.Input.Devices
{
	/// <summary>
	/// Combined device management class that orchestrates different input device types.
	/// Provides unified access to DirectInput, XInput, and other input methods.
	/// </summary>
	internal class UnifiedInputDeviceManager
	{
		private readonly PnPInputDevice pnPInputDevice = new PnPInputDevice();
		private readonly RawInputDevice rawInputDevice = new RawInputDevice();
		private readonly DirectInputDevice directInputDevice = new DirectInputDevice();
		private readonly XInputDevice xInputDevice = new XInputDevice();
		private readonly GamingInputDevice gamingInputDevice = new GamingInputDevice();

		public List<PnPInputDeviceInfo> PnPInputDeviceInfoList = new List<PnPInputDeviceInfo>();
		public List<RawInputDeviceInfo> RawInputDeviceInfoList = new List<RawInputDeviceInfo>();
		public List<DirectInputDeviceInfo> DirectInputDeviceInfoList = new List<DirectInputDeviceInfo>();
		public List<XInputDeviceInfo> XInputDeviceInfoList = new List<XInputDeviceInfo>();
		public List<GamingInputDeviceInfo> GamingInputDeviceInfoList = new List<GamingInputDeviceInfo>();
		public ObservableCollection<UnifiedInputDeviceInfo> UnifiedInputDeviceInfoList = new ObservableCollection<UnifiedInputDeviceInfo>();

        // Cache for DirectInput product names to avoid repeated lookups
        private Dictionary<string, string> _directInputNameCache;
		
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
        /// Creates and populates all input device lists from various input sources.
        /// Updates lists incrementally: existing devices are updated in place, new devices are added, removed devices are deleted.
        /// </summary>
        public void GetUnifiedInputDeviceList()
  {
   // Retrieve current device lists from all input sources
   var currentPnPList = pnPInputDevice.GetPnPInputDeviceInfoList();
   var currentRawInputList = rawInputDevice.GetRawInputDeviceInfoList();
   var currentDirectInputList = directInputDevice.GetDirectInputDeviceInfoList();
   var currentXInputList = xInputDevice.GetXInputDeviceInfoList();
   var currentGamingInputList = gamingInputDevice.GetGamingInputDeviceInfoList();

   // Update PnPInputDeviceInfoList incrementally
   UpdateDeviceList(PnPInputDeviceInfoList, currentPnPList, d => d.InstanceGuid);
   
   // Update RawInputDeviceInfoList incrementally
   UpdateDeviceList(RawInputDeviceInfoList, currentRawInputList, d => d.InstanceGuid);
   
   // Update DirectInputDeviceInfoList incrementally
   UpdateDeviceList(DirectInputDeviceInfoList, currentDirectInputList, d => d.InstanceGuid);
   
   // Update XInputDeviceInfoList incrementally
   UpdateDeviceList(XInputDeviceInfoList, currentXInputList, d => d.InstanceGuid);
   
   // Update GamingInputDeviceInfoList incrementally
   UpdateDeviceList(GamingInputDeviceInfoList, currentGamingInputList, d => d.InstanceGuid);

   // Build DirectInput name cache for efficient lookups
   BuildDirectInputNameCache();

   // Update the unified list incrementally instead of clearing
   UpdateUnifiedInputDeviceList();
  }

  /// <summary>
  /// Updates the unified device list incrementally by comparing with source lists.
  /// Removes devices no longer present, updates existing devices, and adds new devices.
  /// Ensures all ObservableCollection modifications happen on the UI thread.
  /// </summary>
  private void UpdateUnifiedInputDeviceList()
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
    for (int i = UnifiedInputDeviceInfoList.Count - 1; i >= 0; i--)
    {
     var device = UnifiedInputDeviceInfoList[i];
     var key = GetDeviceKey(device.InputType, device.CommonIdentifier);
     if (!currentDeviceKeys.Contains(key))
     {
      UnifiedInputDeviceInfoList.RemoveAt(i);
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
  /// Updates existing devices or adds new devices from a source list to the unified list.
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

    // Find existing device in unified list
    var existingDevice = UnifiedInputDeviceInfoList.FirstOrDefault(d =>
     GetDeviceKey(d.InputType, d.CommonIdentifier) == key);

    if (existingDevice != null)
    {
     // Update existing device properties
     existingDevice.AxeCount = device.AxeCount;
     existingDevice.SliderCount = device.SliderCount;
     existingDevice.ButtonCount = device.ButtonCount;
     existingDevice.KeyCount = device.KeyCount;
     existingDevice.PovCount = device.PovCount;
     existingDevice.ProductName = getProductName(item, commonId);
     existingDevice.InterfacePath = getInterfacePath(item);
     // Note: Pressed states are preserved and updated separately by button check logic
    }
    else
    {
     // Add new device
     var newDevice = new UnifiedInputDeviceInfo
     {
      InputType = inputType,
      CommonIdentifier = commonId,
      AxeCount = device.AxeCount,
      SliderCount = device.SliderCount,
      ButtonCount = device.ButtonCount,
      KeyCount = device.KeyCount,
      PovCount = device.PovCount,
      AxePressed = false,
      SliderPressed = false,
      ButtonPressed = false,
      KeyPressed = false,
      PovPressed = false,
      ProductName = getProductName(item, commonId),
      InterfacePath = getInterfacePath(item)
     };
     UnifiedInputDeviceInfoList.Add(newDevice);
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

   // Remove devices that are no longer present
   for (int i = existingList.Count - 1; i >= 0; i--)
   {
    var key = keySelector(existingList[i]);
    if (!currentDict.ContainsKey(key))
    {
     existingList.RemoveAt(i);
    }
   }

   // Add new devices and update existing ones
   foreach (var currentDevice in currentList)
   {
    var key = keySelector(currentDevice);
    if (existingDict.ContainsKey(key))
    {
     // Update existing device in place
     var index = existingList.FindIndex(d => keySelector(d).Equals(key));
     if (index >= 0)
     {
      existingList[index] = currentDevice;
     }
    }
    else
    {
     // Add new device
     existingList.Add(currentDevice);
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

	}
}
