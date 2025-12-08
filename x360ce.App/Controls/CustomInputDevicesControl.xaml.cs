using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using x360ce.App.Input.Devices;
using x360ce.App.Input.States;
using x360ce.App.Input.Triggers;
using x360ce.App.Timers;

namespace x360ce.App.Controls
{
    /// <summary>
    /// Debug control for displaying and filtering input device information across multiple input methods.
    /// Supports text highlighting and real-time filtering of device properties.
    /// </summary>
    public partial class CustomInputDevicesControl : UserControl
    {
        private CollectionViewSource _viewSource;
        private string _highlightedText = string.Empty;
        private static readonly HashSet<string> ExcludedSearchProperties = new HashSet<string> { "AxeCount", "SliderCount", "ButtonCount", "PovCount" };
        private static readonly Brush HighlightBackground = Brushes.Yellow;
        private static readonly Brush HighlightForeground = Brushes.Black;

        // Performance optimization: Cache visual tree elements
        private readonly List<TextBox> _cachedTextBoxes = new List<TextBox>();
        private readonly List<DataGridRow> _cachedRows = new List<DataGridRow>();
        private readonly HashSet<TextBox> _attachedTextBoxes = new HashSet<TextBox>();

        // Performance optimization: Cache property info for filtering
        private PropertyInfo[] _cachedFilterProperties;

        // Debouncing for search
        private DispatcherTimer _searchDebounceTimer;
        private const int SearchDebounceMs = 150;

        // UI update timer for checking button states
        private readonly UserInterfaceUpdatingTimer _uiUpdateTimer = new UserInterfaceUpdatingTimer();

        public CustomInputDevicesControl()
        {
            InitializeComponent();
            InitializeSearchDebounce();
        }

        /// <summary>
        /// Handles click events for input type filter checkboxes.
        /// Updates settings and refreshes the view.
        /// </summary>
        private void FilterCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is CheckBox checkBox))
                return;

            // Update settings based on checkbox name
            // Uses dictionary-like mapping logic to avoid multiple if/else blocks
            bool isChecked = checkBox.IsChecked ?? true;

            if (checkBox == PnPInputDevicesCheckBox) SettingsManager.Options.ShowPnPDevices = isChecked;
            else if (checkBox == RawInputDevicesCheckBox) SettingsManager.Options.ShowRawInputDevices = isChecked;
            else if (checkBox == DirectInputDevicesCheckBox) SettingsManager.Options.ShowDirectInputDevices = isChecked;
            else if (checkBox == XInputDevicesCheckBox) SettingsManager.Options.ShowXInputDevices = isChecked;
            else if (checkBox == GamingInputDevicesCheckBox) SettingsManager.Options.ShowGamingInputDevices = isChecked;

            SettingsManager.Save();
            _viewSource.View.Refresh();
        }

        /// <summary>
        /// Initializes the search debounce timer to prevent excessive filtering operations.
        /// </summary>
        private void InitializeSearchDebounce()
        {
            _searchDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(SearchDebounceMs)
            };
            _searchDebounceTimer.Tick += (s, e) =>
            {
                _searchDebounceTimer.Stop();
                _viewSource?.View?.Refresh();
            };
        }

        // Input device management and state checking components
        private readonly CustomInputDeviceManager _customInputDeviceManager = new CustomInputDeviceManager();
        private readonly CustomInputDeviceConnection _customInputDeviceConnection = new CustomInputDeviceConnection();

        // Device selection handlers
        private DevicesTab_DeviceSelectedInfo _devicesTab_DeviceSelectedInfo;
        private DevicesTab_DeviceSelectedInput _devicesTab_DeviceSelectedInput;

        // Device connection triggers - monitor actual device connect/disconnect events
        private readonly PnPInputDeviceConnection _pnpTrigger = new PnPInputDeviceConnection();
        private readonly RawInputDeviceConnection _rawInputTrigger = new RawInputDeviceConnection();
        private readonly DirectInputDeviceConnection _directInputTrigger = new DirectInputDeviceConnection();
        private readonly XInputDeviceConnection _xinputTrigger = new XInputDeviceConnection();
        private readonly GamingInputDeviceConnection _gamingInputTrigger = new GamingInputDeviceConnection();

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            // Refresh device list
            _customInputDeviceManager.GetCustomInputDeviceList();
            _viewSource.Source = _customInputDeviceManager.CustomInputDeviceInfoList;

            // Clear visual cache when data source changes
            InvalidateVisualCache();

            _viewSource.View.Refresh();

            UpdateAllDevicesInformation();
        }

        private void InputDevicesControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Set dispatcher for UI thread synchronization
            _customInputDeviceManager.SetDispatcher(Dispatcher);

            // Create input devices lists: PnPInput, RawInput, DirectInput, XInput, GamingInput
            _customInputDeviceManager.GetCustomInputDeviceList();

            // Initialize checkbox states from settings
            PnPInputDevicesCheckBox.IsChecked = SettingsManager.Options.ShowPnPDevices;
            RawInputDevicesCheckBox.IsChecked = SettingsManager.Options.ShowRawInputDevices;
            DirectInputDevicesCheckBox.IsChecked = SettingsManager.Options.ShowDirectInputDevices;
            XInputDevicesCheckBox.IsChecked = SettingsManager.Options.ShowXInputDevices;
            GamingInputDevicesCheckBox.IsChecked = SettingsManager.Options.ShowGamingInputDevices;

            // Start populating ListInputStates:
            // Polling for DirectInput, XInput, GamingInput devices at 20Hz.
            // Event-Driven for RawInput devices.
            CustomInputStateTimer.Instance.StartStateCollection(_customInputDeviceManager);

            // Set up CollectionViewSource for filtering
            _viewSource = new CollectionViewSource { Source = _customInputDeviceManager.CustomInputDeviceInfoList };
            _viewSource.Filter += ViewSource_Filter;

            CustomInputDeviceInfoDataGrid.ItemsSource = _viewSource.View;

            // Initialize device selection handlers
            _devicesTab_DeviceSelectedInfo = new DevicesTab_DeviceSelectedInfo(_customInputDeviceManager);
            _devicesTab_DeviceSelectedInput = new DevicesTab_DeviceSelectedInput(_customInputDeviceManager);

            // Attach SelectionChanged event handler
            CustomInputDeviceInfoDataGrid.SelectionChanged += CustomInputDeviceInfoDataGrid_SelectionChanged;

            // Cache filter properties once
            if (_customInputDeviceManager.CustomInputDeviceInfoList?.Count > 0)
            {
                var firstItem = _customInputDeviceManager.CustomInputDeviceInfoList[0];
                _cachedFilterProperties = firstItem.GetType().GetProperties()
                 .Where(p => !ExcludedSearchProperties.Contains(p.Name))
                 .ToArray();
            }

            // Subscribe to device monitoring events for automatic updates
            _customInputDeviceConnection.CustomListUpdateRequired += DeviceMonitor_CustomListUpdateRequired;

            // Start device connection monitoring - these trigger only on actual device connect/disconnect
            StartDeviceConnectionMonitoring();

            // Attach handlers for row loading to support virtualization
            CustomInputDeviceInfoDataGrid.LoadingRow += DataGrid_LoadingRow;

            // Defer handler attachment until layout is complete
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(AttachTextBoxHandlers));

            // Select first device and display its information if available
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                if (CustomInputDeviceInfoDataGrid.Items.Count > 0)
                {
                    CustomInputDeviceInfoDataGrid.SelectedIndex = 0;
                    // Manually trigger the display since SelectionChanged might not fire for initial selection
                    DisplaySelectedDeviceInformation();
                }
                UpdateAllDevicesInformation();
            }));

            // Initialize button check timer (includes visibility handling)
            InitializeUIUpdateTimer();
        }

        /// <summary>
        /// Starts monitoring for actual device connection/disconnection events.
        /// These triggers use Windows messages and polling to detect when devices are added/removed.
        /// </summary>
        private void StartDeviceConnectionMonitoring()
        {
            // Helper to attach handler and start monitoring
            void Start(dynamic trigger)
            {
                trigger.DeviceChanged += new EventHandler<DeviceConnectionEventArgs>(OnDeviceConnectionChanged);
                trigger.StartMonitoring();
            }

            // Start monitoring all triggers
            Start(_pnpTrigger);
            Start(_rawInputTrigger);
            Start(_directInputTrigger);
            Start(_xinputTrigger);
            Start(_gamingInputTrigger);
        }

        /// <summary>
        /// Stops monitoring for device connection/disconnection events.
        /// </summary>
        private void StopDeviceConnectionMonitoring()
        {
            // Helper to stop monitoring and detach handler
            void Stop(dynamic trigger)
            {
                trigger.StopMonitoring();
                trigger.DeviceChanged -= new EventHandler<DeviceConnectionEventArgs>(OnDeviceConnectionChanged);
            }

            // Stop monitoring all triggers
            Stop(_pnpTrigger);
            Stop(_rawInputTrigger);
            Stop(_directInputTrigger);
            Stop(_xinputTrigger);
            Stop(_gamingInputTrigger);
        }

        /// <summary>
        /// Handles device connection/disconnection events from any trigger source.
        /// Refreshes device lists and updates the custom list when devices change.
        /// </summary>
        private void OnDeviceConnectionChanged(object sender, DeviceConnectionEventArgs e)
        {
            // Refresh all device lists when a connection change is detected
            _customInputDeviceManager.GetCustomInputDeviceList();

            // Monitor the refreshed lists to detect what changed
            _customInputDeviceConnection.MonitorPnPInputDeviceList(_customInputDeviceManager.PnPInputDeviceInfoList);
            _customInputDeviceConnection.MonitorRawInputDeviceList(_customInputDeviceManager.RawInputDeviceInfoList);
            _customInputDeviceConnection.MonitorDirectInputDeviceList(_customInputDeviceManager.DirectInputDeviceInfoList);
            _customInputDeviceConnection.MonitorXInputDeviceList(_customInputDeviceManager.XInputDeviceInfoList);
            _customInputDeviceConnection.MonitorGamingInputDeviceList(_customInputDeviceManager.GamingInputDeviceInfoList);
        }

        /// <summary>
        /// Handles device list changes from the monitoring system.
        /// Updates the custom device list incrementally when devices connect/disconnect.
        /// </summary>
        private void DeviceMonitor_CustomListUpdateRequired(object sender, CustomDeviceListUpdateEventArgs e)
        {
            // Invalidate visual cache when device list changes
            InvalidateVisualCache();

            // Refresh the view to reflect changes
            _viewSource?.View?.Refresh();

            UpdateAllDevicesInformation();
        }

        /// <summary>
        /// Initializes the timer that checks DirectInput button states.
        /// User interface timer. Always ticks if app is open, and updates only open-visible tabs.
        /// </summary>
        private void InitializeUIUpdateTimer()
        {
            _uiUpdateTimer.Initialize(this, () =>
            {
                // if "Devices" Tab is open, update Tab UI.
                if (DevicesTab.IsVisible)
                {
                    DevicesTab_UIUpdate(_customInputDeviceManager);
                }
            });
        }

        /// <summary>
        /// Invalidates the visual cache, forcing a rebuild on next access.
        /// </summary>
        private void InvalidateVisualCache()
        {
            _cachedTextBoxes.Clear();
            _cachedRows.Clear();
            _attachedTextBoxes.Clear();
        }

        /// <summary>
        /// Handles DataGrid row loading to attach event handlers to TextBoxes in virtualized rows.
        /// This ensures text selection and highlighting work correctly even for rows that were
        /// initially outside the viewport and became visible through scrolling.
        /// </summary>
        private void DataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            // Invalidate cache when new rows are loaded (virtualization)
            InvalidateVisualCache();

            // Attach handlers to TextBoxes in the newly loaded row
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                AttachTextBoxHandlersToRow(e.Row);

                // Reapply highlighting if there's active highlighted text
                if (!string.IsNullOrEmpty(_highlightedText))
                {
                    HighlightTextInRow(e.Row, _highlightedText);
                }
            }));
        }

        /// <summary>
        /// Attaches SelectionChanged event handlers to all TextBoxes in the DataGrid for text highlighting.
        /// Only attaches to new TextBoxes that haven't been processed yet.
        /// </summary>
        private void AttachTextBoxHandlers()
        {
            if (CustomInputDeviceInfoDataGrid.ItemsSource == null)
                return;

            CustomInputDeviceInfoDataGrid.UpdateLayout();

            // Build cache and attach handlers only to new TextBoxes
            foreach (var textBox in FindVisualChildren<TextBox>(CustomInputDeviceInfoDataGrid))
            {
                if (_attachedTextBoxes.Add(textBox))
                {
                    textBox.SelectionChanged += TextBox_SelectionChanged;
                }
            }
        }

        /// <summary>
        /// Attaches SelectionChanged event handlers to TextBoxes within a specific DataGrid row.
        /// Used for handling virtualized rows that become visible through scrolling.
        /// </summary>
        private void AttachTextBoxHandlersToRow(DataGridRow row)
        {
            if (row == null)
                return;

            foreach (var textBox in FindVisualChildren<TextBox>(row))
            {
                if (_attachedTextBoxes.Add(textBox))
                {
                    textBox.SelectionChanged += TextBox_SelectionChanged;
                }
            }
        }

        /// <summary>
        /// Applies highlighting to matching text within a specific DataGrid row.
        /// Used when rows are loaded through virtualization and there's active highlighted text.
        /// </summary>
        private void HighlightTextInRow(DataGridRow row, string textToHighlight)
        {
            if (row == null || string.IsNullOrEmpty(textToHighlight))
                return;

            bool rowHasMatch = false;

            foreach (var textBox in FindVisualChildren<TextBox>(row))
            {
                // Clear existing highlighting
                textBox.ClearValue(Control.BackgroundProperty);
                textBox.ClearValue(Control.ForegroundProperty);

                // Check for match and apply highlighting
                if (!string.IsNullOrEmpty(textBox.Text) && textBox.Text.Contains(textToHighlight))
                {
                    textBox.Background = HighlightBackground;
                    textBox.Foreground = HighlightForeground;
                    rowHasMatch = true;
                }
            }

            // Dim row if it doesn't have a match
            row.Opacity = rowHasMatch ? 1.0 : 0.2;
        }

        /// <summary>
        /// Handles text selection in DataGrid TextBoxes to highlight matching text across all cells.
        /// Automatically clears selection in other TextBoxes when a new selection is made.
        /// </summary>
        private void TextBox_SelectionChanged(object sender, RoutedEventArgs e)
        {
            if (!(sender is TextBox textBox))
                return;

            var selectedText = textBox.SelectedText;

            // Clear highlighting if no text is selected
            if (string.IsNullOrEmpty(selectedText))
            {
                if (!string.IsNullOrEmpty(_highlightedText))
                {
                    _highlightedText = string.Empty;
                    ClearAllHighlighting();
                }
                return;
            }

            // Only update if the selection actually changed
            if (_highlightedText == selectedText)
                return;

            // Clear selection in all other TextBoxes before applying new highlighting
            ClearAllTextBoxSelections(textBox);

            // Apply highlighting to matching text
            _highlightedText = selectedText;
            HighlightTextInAllTextBoxes(_highlightedText);
        }

        /// <summary>
        /// Clears all highlighting and resets row opacity.
        /// </summary>
        private void ClearAllHighlighting()
        {
            RebuildCacheIfNeeded();

            foreach (var textBox in _cachedTextBoxes)
            {
                textBox.ClearValue(Control.BackgroundProperty);
                textBox.ClearValue(Control.ForegroundProperty);
            }

            foreach (var row in _cachedRows)
            {
                row.Opacity = 1.0;
            }
        }

        /// <summary>
        /// Highlights matching text in all DataGrid TextBoxes and dims rows without matches.
        /// Optimized single-pass algorithm with cached visual elements.
        /// </summary>
        private void HighlightTextInAllTextBoxes(string textToHighlight)
        {
            if (string.IsNullOrEmpty(textToHighlight))
            {
                ClearAllHighlighting();
                return;
            }

            RebuildCacheIfNeeded();

            // Single pass: Clear highlighting, identify matches, and build row-to-match mapping
            var rowsWithMatches = new HashSet<DataGridRow>();

            foreach (var textBox in _cachedTextBoxes)
            {
                // Clear existing highlighting
                textBox.ClearValue(Control.BackgroundProperty);
                textBox.ClearValue(Control.ForegroundProperty);

                // Check for match and apply highlighting
                if (!string.IsNullOrEmpty(textBox.Text) && textBox.Text.Contains(textToHighlight))
                {
                    textBox.Background = HighlightBackground;
                    textBox.Foreground = HighlightForeground;

                    // Track which row has a match
                    var parentRow = FindVisualParent<DataGridRow>(textBox);
                    if (parentRow != null)
                        rowsWithMatches.Add(parentRow);
                }
            }

            // Update row opacity based on matches
            var hasMatches = rowsWithMatches.Count > 0;
            foreach (var row in _cachedRows)
            {
                row.Opacity = hasMatches && !rowsWithMatches.Contains(row) ? 0.5 : 1.0;
            }

            // Clear the highlighted text if no matches were found
            if (!hasMatches)
            {
                _highlightedText = string.Empty;
            }
        }

        /// <summary>
        /// Rebuilds the visual element cache if it's empty or stale.
        /// </summary>
        private void RebuildCacheIfNeeded()
        {
            // Rebuild cache if empty
            if (_cachedTextBoxes.Count == 0 || _cachedRows.Count == 0)
            {
                _cachedTextBoxes.Clear();
                _cachedRows.Clear();

                _cachedTextBoxes.AddRange(FindVisualChildren<TextBox>(CustomInputDeviceInfoDataGrid));
                _cachedRows.AddRange(FindVisualChildren<DataGridRow>(CustomInputDeviceInfoDataGrid));
            }
        }

        /// <summary>
        /// Clears text selection in all TextBoxes except the specified one.
        /// Uses cached TextBoxes for better performance.
        /// </summary>
        private void ClearAllTextBoxSelections(TextBox exceptTextBox)
        {
            RebuildCacheIfNeeded();

            foreach (var textBox in _cachedTextBoxes)
            {
                if (textBox != exceptTextBox && textBox.SelectionLength > 0)
                {
                    textBox.SelectionLength = 0;
                }
            }
        }

        /// <summary>
        /// Recursively finds all visual children of a specific type in the visual tree.
        /// Optimized to reduce allocations and improve traversal performance.
        /// </summary>
        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj == null)
                yield break;

            var childCount = VisualTreeHelper.GetChildrenCount(depObj);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(depObj, i);

                if (child is T typedChild)
                    yield return typedChild;

                foreach (var descendant in FindVisualChildren<T>(child))
                    yield return descendant;
            }
        }

        /// <summary>
        /// Finds the first visual parent of a specific type in the visual tree.
        /// </summary>
        private static T FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            var parentObject = VisualTreeHelper.GetParent(child);

            if (parentObject == null)
                return null;

            if (parentObject is T parent)
                return parent;

            return FindVisualParent<T>(parentObject);
        }

        private void InputDeviceSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Debounce search to prevent excessive filtering
            _searchDebounceTimer.Stop();
            _searchDebounceTimer.Start();
        }

        /// <summary>
        /// Filters device items based on search text, excluding numeric count properties.
        /// Uses cached property info for better performance.
        /// </summary>
        private void ViewSource_Filter(object sender, FilterEventArgs e)
        {
            // First check if the item type is visible based on checkbox settings
            if (e.Item is CustomInputDeviceInfo info)
            {
                if (info.InputType == "PnPInput" && !SettingsManager.Options.ShowPnPDevices) { e.Accepted = false; return; }
                if (info.InputType == "RawInput" && !SettingsManager.Options.ShowRawInputDevices) { e.Accepted = false; return; }
                if (info.InputType == "DirectInput" && !SettingsManager.Options.ShowDirectInputDevices) { e.Accepted = false; return; }
                if (info.InputType == "XInput" && !SettingsManager.Options.ShowXInputDevices) { e.Accepted = false; return; }
                if (info.InputType == "GamingInput" && !SettingsManager.Options.ShowGamingInputDevices) { e.Accepted = false; return; }
            }

            var searchText = InputDeviceSearch.Text?.Trim();

            if (string.IsNullOrEmpty(searchText))
            {
                e.Accepted = true;
                return;
            }

            e.Accepted = e.Item != null && ItemMatchesSearchText(e.Item, searchText.ToLower());
        }

        /// <summary>
        /// Checks if an item matches the search text in any of its filterable properties.
        /// </summary>
        private bool ItemMatchesSearchText(object item, string searchText)
        {
            var properties = _cachedFilterProperties ?? item.GetType().GetProperties()
                .Where(p => !ExcludedSearchProperties.Contains(p.Name))
                .ToArray();

            foreach (var property in properties)
            {
                var value = property.GetValue(item, null);
                if (value?.ToString().ToLower().Contains(searchText) == true)
                    return true;
            }

            return false;
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            // Stop and dispose UI update timer
            _uiUpdateTimer?.Dispose();

            // Stop continuous state collection
            CustomInputStateTimer.Instance.StopStateCollection();

            // Stop device connection monitoring
            StopDeviceConnectionMonitoring();

            // Unsubscribe from device monitoring events
            if (_customInputDeviceConnection != null)
                _customInputDeviceConnection.CustomListUpdateRequired -= DeviceMonitor_CustomListUpdateRequired;

            // Detach SelectionChanged event handler
            CustomInputDeviceInfoDataGrid.SelectionChanged -= CustomInputDeviceInfoDataGrid_SelectionChanged;
        }

        /// <summary>
        /// Handles device selection in the DataGrid and displays device information.
        /// </summary>
        private void CustomInputDeviceInfoDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            DisplaySelectedDeviceInformation();
        }


        /// <summary>
        /// Displays information for the currently selected device.
        /// </summary>
        private void DisplaySelectedDeviceInformation()
        {
            // Clear previous content
            SelectedDeviceInformationTextBox.Text = string.Empty;
            SelectedDeviceInputStackPanel.Children.Clear();

            // Get selected device
            if (!(CustomInputDeviceInfoDataGrid.SelectedItem is CustomInputDeviceInfo selectedDevice))
                return;

            // Set the selected device in the input handler so it knows which device to update
            _devicesTab_DeviceSelectedInput?.SetSelectedDevice(selectedDevice.InstanceGuid);

            var sb = new StringBuilder();
            sb.AppendLine("SELECTED INPUT DEVICE");
            sb.AppendLine(new string('-', 76));

            // Determine list index and device object
            var result = GetDeviceAndIndex(selectedDevice.InputType, selectedDevice.InstanceGuid);

            if (result.index > -1)
                sb.AppendLine($"ListIndex: {result.index}");

            if (result.device is InputDeviceInfo deviceObject)
            {
                var props = DevicesTab_DeviceSelectedInfo.ExtractDeviceProperties(deviceObject);
                foreach (var (name, value) in props)
                {
                    sb.AppendLine($"{name}: {value}");
                }

                sb.AppendLine(new string('-', 76));
                SelectedDeviceInformationTextBox.Text = sb.ToString();

                // Get current ListInputState directly from the device object
                var liState = deviceObject.CustomInputState;

                // Get device input as XAML elements
                var deviceInputElement = _devicesTab_DeviceSelectedInput?.CreateInputLayout(selectedDevice, liState);

                // Add to StackPanel if we got valid content
                if (deviceInputElement != null)
                    SelectedDeviceInputStackPanel.Children.Add(deviceInputElement);
            }
        }

        /// <summary>
        /// Helper method to find device index and object in the appropriate list.
        /// </summary>
        private (int index, object device) GetDeviceAndIndex(string inputType, Guid instanceGuid)
        {
            IList list = null;
            switch (inputType)
            {
                case "RawInput": list = _customInputDeviceManager.RawInputDeviceInfoList; break;
                case "DirectInput": list = _customInputDeviceManager.DirectInputDeviceInfoList; break;
                case "XInput": list = _customInputDeviceManager.XInputDeviceInfoList; break;
                case "GamingInput": list = _customInputDeviceManager.GamingInputDeviceInfoList; break;
                case "PnPInput": list = _customInputDeviceManager.PnPInputDeviceInfoList; break;
            }

            if (list != null)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i] is InputDeviceInfo info && info.InstanceGuid == instanceGuid)
                    {
                        return (i, info);
                    }
                }
            }
            return (-1, null);
        }

        private void UpdateAllDevicesInformation()
        {
            var sb = new StringBuilder();
            var selectedDevice = CustomInputDeviceInfoDataGrid.SelectedItem as CustomInputDeviceInfo;

            void ProcessList<T>(string header, List<T> list) where T : InputDeviceInfo
            {
                sb.AppendLine(header);
                sb.AppendLine(new string('-', 76));
                if (list != null)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        var item = list[i];
                        var isSelected = selectedDevice != null && item.InstanceGuid == selectedDevice.InstanceGuid;

                        sb.Append($"ListIndex: {i}");
                        if (isSelected)
                            sb.Append(" (SELECTED)");
                        sb.AppendLine();

                        var props = DevicesTab_DeviceSelectedInfo.ExtractDeviceProperties(item);
                        foreach (var (name, value) in props)
                        {
                            sb.AppendLine($"{name}: {value}");
                        }
                        sb.AppendLine(new string('-', 76));
                    }
                }
                sb.AppendLine();
            }

            ProcessList("RAW INPUT DEVICES", _customInputDeviceManager.RawInputDeviceInfoList);
            ProcessList("DIRECT INPUT DEVICES", _customInputDeviceManager.DirectInputDeviceInfoList);
            ProcessList("X INPUT DEVICES", _customInputDeviceManager.XInputDeviceInfoList);
            ProcessList("GAMING INPUT DEVICES", _customInputDeviceManager.GamingInputDeviceInfoList);

            AllDevicesInformationTextBox.Text = sb.ToString();
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            InputDeviceSearch.Text = string.Empty;
        }
        /// <summary>
        /// Checks all input methods for button presses and updates ButtonPressed properties.
        /// Gets current ListInputState directly from source device lists using InstanceGuid lookup.
        /// This ensures we always read the most current state without reference management complexity.
        /// </summary>
        /// <param name="customInputDeviceManager">The combined devices instance containing all device lists</param>
        private void DevicesTab_UIUpdate(CustomInputDeviceManager customInputDeviceManager)
        {
            if (customInputDeviceManager == null)
                return;

            // Loop through custom list and get current state
            foreach (var device in customInputDeviceManager.CustomInputDeviceInfoList)
            {
                var liState = device.CustomInputState;

                // Skip if no state available
                if (liState == null ||
                    liState.Buttons == null && liState.POVs == null && liState.Axes == null && liState.Sliders == null ||
                    liState.Buttons.Count == 0 && liState.POVs.Count == 0 && liState.Axes.Count == 0 && liState.Sliders.Count == 0)
                    continue;

                // Check if any button or POV is pressed and Update ButtonPressed property
                device.ButtonPressed = IsAnyButtonOrPovPressed(liState);

                // Update value labels if handler is set
                _devicesTab_DeviceSelectedInput?.UpdateValueLabels(device.InstanceGuid, liState);
            }
        }

        /// <summary>
        /// Checks if any button or POV is pressed in the given state.
        /// Optimized for high-frequency execution.
        /// </summary>
        /// <param name="listState">The device state to check</param>
        /// <returns>True if any button is pressed (value 1) or any POV is pressed (value > -1)</returns>
        private static bool IsAnyButtonOrPovPressed(CustomInputState listState)
        {
            if (listState == null)
                return false;

            // Check buttons.
            var buttons = listState.Buttons;
            if (buttons != null)
            {
                var count = buttons.Count;
                for (int i = 0; i < count; i++)
                {
                    if (buttons[i] != 0)
                        return true;
                }
            }

            // Check POVs.
            var povs = listState.POVs;
            if (povs != null)
            {
                var count = povs.Count;
                for (int i = 0; i < count; i++)
                {
                    if (povs[i] > -1)
                        return true;
                }
            }

            return false;
        }

        private void TabItemHeader_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender == SelectedDeviceTabItem.Header)
            {
                if (SelectedDeviceTabItem.IsSelected)
                {
                    // Toggle
                    SelectedDeviceInformationTextBox.Visibility = SelectedDeviceInformationTextBox.Visibility == Visibility.Visible
                        ? Visibility.Collapsed
                        : Visibility.Visible;
                }
                else
                {
                    // Make visible
                    SelectedDeviceInformationTextBox.Visibility = Visibility.Visible;
                }
            }
            else if (sender == AllDevicesTabItem.Header)
            {
                if (AllDevicesTabItem.IsSelected)
                {
                    // Toggle
                    AllDevicesInformationTextBox.Visibility = AllDevicesInformationTextBox.Visibility == Visibility.Visible
                        ? Visibility.Collapsed
                        : Visibility.Visible;
                }
                else
                {
                    // Make visible
                    AllDevicesInformationTextBox.Visibility = Visibility.Visible;
                }
            }
        }
    }
}
