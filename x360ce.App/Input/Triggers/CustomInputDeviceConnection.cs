using System;
using System.Collections.Generic;
using System.Linq;
using x360ce.App.Input.Devices;

namespace x360ce.App.Input.Triggers
{
    /// <summary>
    /// Monitors device list changes and maintains a custom input device list.
    /// Triggers CustomInputDeviceInfoList updates when any source list changes.
    /// Updates are incremental - only affected items are modified (existing devices updated in place, new devices added, removed devices deleted).
    /// </summary>
    internal class CustomInputDeviceConnection
    {
        /// <summary>
        /// Event triggered when the custom device list needs to be updated.
        /// </summary>
        public event EventHandler<CustomDeviceListUpdateEventArgs> CustomListUpdateRequired;

        private List<PnPInputDeviceInfo> _lastPnPList = new List<PnPInputDeviceInfo>();
        private List<RawInputDeviceInfo> _lastRawInputList = new List<RawInputDeviceInfo>();
        private List<DirectInputDeviceInfo> _lastDirectInputList = new List<DirectInputDeviceInfo>();
        private List<XInputDeviceInfo> _lastXInputList = new List<XInputDeviceInfo>();
        private List<GamingInputDeviceInfo> _lastGamingInputList = new List<GamingInputDeviceInfo>();

        /// <summary>
        /// Monitors PnPInputDeviceInfoList for changes and triggers custom list update for PnPInput items only.
        /// </summary>
        /// <param name="currentList">Current PnP device list</param>
        public void MonitorPnPInputDeviceList(List<PnPInputDeviceInfo> currentList)
        {
            if (currentList == null)
                currentList = new List<PnPInputDeviceInfo>();

            var changes = DetectChanges(_lastPnPList, currentList, d => d.InstanceGuid);
            
            if (changes.HasChanges)
            {
                _lastPnPList = new List<PnPInputDeviceInfo>(currentList);
                OnCustomListUpdateRequired(new CustomDeviceListUpdateEventArgs
                {
                    InputType = "PnPInput",
                    AddedDevices = changes.Added.Cast<object>().ToList(),
                    RemovedDevices = changes.Removed.Cast<object>().ToList(),
                    UpdatedDevices = changes.Updated.Cast<object>().ToList()
                });
            }
        }

        /// <summary>
        /// Monitors RawInputDeviceInfoList for changes and triggers custom list update for RawInput items only.
        /// </summary>
        /// <param name="currentList">Current RawInput device list</param>
        public void MonitorRawInputDeviceList(List<RawInputDeviceInfo> currentList)
        {
            if (currentList == null)
                currentList = new List<RawInputDeviceInfo>();

            var changes = DetectChanges(_lastRawInputList, currentList, d => d.InstanceGuid);
            
            if (changes.HasChanges)
            {
                _lastRawInputList = new List<RawInputDeviceInfo>(currentList);
                OnCustomListUpdateRequired(new CustomDeviceListUpdateEventArgs
                {
                    InputType = "RawInput",
                    AddedDevices = changes.Added.Cast<object>().ToList(),
                    RemovedDevices = changes.Removed.Cast<object>().ToList(),
                    UpdatedDevices = changes.Updated.Cast<object>().ToList()
                });
            }
        }

        /// <summary>
        /// Monitors DirectInputDeviceInfoList for changes and triggers custom list update for DirectInput items only.
        /// </summary>
        /// <param name="currentList">Current DirectInput device list</param>
        public void MonitorDirectInputDeviceList(List<DirectInputDeviceInfo> currentList)
        {
            if (currentList == null)
                currentList = new List<DirectInputDeviceInfo>();

            var changes = DetectChanges(_lastDirectInputList, currentList, d => d.InstanceGuid);
            
            if (changes.HasChanges)
            {
                _lastDirectInputList = new List<DirectInputDeviceInfo>(currentList);
                OnCustomListUpdateRequired(new CustomDeviceListUpdateEventArgs
                {
                    InputType = "DirectInput",
                    AddedDevices = changes.Added.Cast<object>().ToList(),
                    RemovedDevices = changes.Removed.Cast<object>().ToList(),
                    UpdatedDevices = changes.Updated.Cast<object>().ToList()
                });
            }
        }

        /// <summary>
        /// Monitors XInputDeviceInfoList for changes and triggers custom list update for XInput items only.
        /// </summary>
        /// <param name="currentList">Current XInput device list</param>
        public void MonitorXInputDeviceList(List<XInputDeviceInfo> currentList)
        {
            if (currentList == null)
                currentList = new List<XInputDeviceInfo>();

            var changes = DetectChanges(_lastXInputList, currentList, d => d.InstanceGuid);
            
            if (changes.HasChanges)
            {
                _lastXInputList = new List<XInputDeviceInfo>(currentList);
                OnCustomListUpdateRequired(new CustomDeviceListUpdateEventArgs
                {
                    InputType = "XInput",
                    AddedDevices = changes.Added.Cast<object>().ToList(),
                    RemovedDevices = changes.Removed.Cast<object>().ToList(),
                    UpdatedDevices = changes.Updated.Cast<object>().ToList()
                });
            }
        }

        /// <summary>
        /// Monitors GamingInputDeviceInfoList for changes and triggers custom list update for GamingInput items only.
        /// </summary>
        /// <param name="currentList">Current GamingInput device list</param>
        public void MonitorGamingInputDeviceList(List<GamingInputDeviceInfo> currentList)
        {
            if (currentList == null)
                currentList = new List<GamingInputDeviceInfo>();

            var changes = DetectChanges(_lastGamingInputList, currentList, d => d.InstanceGuid);
            
            if (changes.HasChanges)
            {
                _lastGamingInputList = new List<GamingInputDeviceInfo>(currentList);
                OnCustomListUpdateRequired(new CustomDeviceListUpdateEventArgs
                {
                    InputType = "GamingInput",
                    AddedDevices = changes.Added.Cast<object>().ToList(),
                    RemovedDevices = changes.Removed.Cast<object>().ToList(),
                    UpdatedDevices = changes.Updated.Cast<object>().ToList()
                });
            }
        }

        /// <summary>
        /// Detects changes between previous and current device lists.
        /// </summary>
        /// <typeparam name="T">Device info type</typeparam>
        /// <param name="previousList">Previous device list</param>
        /// <param name="currentList">Current device list</param>
        /// <param name="keySelector">Function to extract unique identifier</param>
        /// <returns>Change detection result</returns>
        private DeviceListChanges<T> DetectChanges<T>(List<T> previousList, List<T> currentList, Func<T, Guid> keySelector)
        {
            var previousDict = previousList.ToDictionary(keySelector);
            var currentDict = currentList.ToDictionary(keySelector);

            var added = currentList.Where(d => !previousDict.ContainsKey(keySelector(d))).ToList();
            var removed = previousList.Where(d => !currentDict.ContainsKey(keySelector(d))).ToList();
            var updated = currentList.Where(d => previousDict.ContainsKey(keySelector(d))).ToList();

            return new DeviceListChanges<T>
            {
                Added = added,
                Removed = removed,
                Updated = updated,
                HasChanges = added.Any() || removed.Any() || updated.Any()
            };
        }

        /// <summary>
        /// Raises the CustomListUpdateRequired event.
        /// </summary>
        /// <param name="e">Event arguments</param>
        protected virtual void OnCustomListUpdateRequired(CustomDeviceListUpdateEventArgs e)
        {
            CustomListUpdateRequired?.Invoke(this, e);
        }

        /// <summary>
        /// Resets all monitored lists to empty state.
        /// </summary>
        public void Reset()
        {
            _lastPnPList.Clear();
            _lastRawInputList.Clear();
            _lastDirectInputList.Clear();
            _lastXInputList.Clear();
            _lastGamingInputList.Clear();
        }
    }

    /// <summary>
    /// Event arguments for custom device list update events.
    /// </summary>
    internal class CustomDeviceListUpdateEventArgs : EventArgs
    {
        /// <summary>
        /// Input type that triggered the update (PnPInput, RawInput, DirectInput, XInput, GamingInput).
        /// </summary>
        public string InputType { get; set; }

        /// <summary>
        /// Devices that were added to the source list.
        /// </summary>
        public List<object> AddedDevices { get; set; } = new List<object>();

        /// <summary>
        /// Devices that were removed from the source list.
        /// </summary>
        public List<object> RemovedDevices { get; set; } = new List<object>();

        /// <summary>
        /// Devices that exist in both lists (should be updated in place).
        /// </summary>
        public List<object> UpdatedDevices { get; set; } = new List<object>();
    }

    /// <summary>
    /// Result of device list change detection.
    /// </summary>
    /// <typeparam name="T">Device info type</typeparam>
    internal class DeviceListChanges<T>
    {
        /// <summary>
        /// Devices that were added.
        /// </summary>
        public List<T> Added { get; set; } = new List<T>();

        /// <summary>
        /// Devices that were removed.
        /// </summary>
        public List<T> Removed { get; set; } = new List<T>();

        /// <summary>
        /// Devices that were updated (exist in both lists).
        /// </summary>
        public List<T> Updated { get; set; } = new List<T>();

        /// <summary>
        /// Indicates whether any changes were detected.
        /// </summary>
        public bool HasChanges { get; set; }
    }
}
