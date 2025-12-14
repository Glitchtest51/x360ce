using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using x360ce.App.Input.Devices;
using x360ce.App.Input.States;

namespace x360ce.App.Input.Triggers
{
    /// <summary>
    /// Handles device selection events and generates device information display.
    /// Extracts detailed device inputs and formats them for display.
    /// </summary>
    internal class DevicesTab_DeviceSelectedInput
    {
        private readonly CustomInputDeviceManager _customInputDeviceInfoInternal;

        /// <summary>
        /// Initializes a new instance with reference to the custom device collection.
        /// </summary>
        /// <param name="customDeviceInfo">The custom device collection containing all device lists</param>
        public DevicesTab_DeviceSelectedInput(CustomInputDeviceManager customInputDevice)
        {
            _customInputDeviceInfoInternal = customInputDevice ?? throw new ArgumentNullException(nameof(customInputDevice));
        }

        /// <summary>
        /// Creates layout for device inputs using device capabilities as the source of truth.
        /// Displays live values from state if available, or default values if state is null.
        /// </summary>
        /// <param name="deviceInfo">Device information containing capability counts</param>
        /// <param name="inputStateAsList">Optional live state values (may be null if device offline)</param>
        /// <returns>UIElement containing the formatted layout</returns>
        public UIElement CreateInputLayout(CustomInputDeviceInfo deviceInfo, CustomInputState inputStateAsList)
        {
            // Check if device has any inputs
            if (deviceInfo.AxeCount == 0 && deviceInfo.SliderCount == 0 && deviceInfo.ButtonCount == 0 && deviceInfo.PovCount == 0)
                return null;

            var mainStackPanel = new StackPanel();

            // Create UI elements based on device capabilities, not state list length
            CreateInputGroup(mainStackPanel, "Axes", deviceInfo.AxeCount, inputStateAsList?.Axes);
            CreateInputGroup(mainStackPanel, "Sliders", deviceInfo.SliderCount, inputStateAsList?.Sliders);
            CreateInputGroup(mainStackPanel, "Buttons", deviceInfo.ButtonCount, inputStateAsList?.Buttons);
            CreateInputGroup(mainStackPanel, "POVs", deviceInfo.PovCount, inputStateAsList?.POVs);

            return mainStackPanel;
        }

        public List<(Label, Label)> SelectedDeviceAxisLabels = new List<(Label, Label)>();
        public List<(Label, Label)> SelectedDeviceSliderLabels = new List<(Label, Label)>();
        public List<(Label, Label)> SelectedDeviceButtonLabels = new List<(Label, Label)>();
        public List<(Label, Label)> SelectedDeviceKeyLabels = new List<(Label, Label)>();
        public List<(Label, Label)> SelectedDevicePovLabels = new List<(Label, Label)>();

        // Track the currently selected device instance GUID
        private Guid _currentSelectedDeviceInstanceGuid;

        /// <summary>
        /// Sets the currently selected device instance GUID.
        /// Called when a device is selected in the UI.
        /// </summary>
        /// <param name="instanceGuid">The instance GUID of the selected device</param>
        public void SetSelectedDevice(Guid instanceGuid)
        {
            _currentSelectedDeviceInstanceGuid = instanceGuid;
        }

        SolidColorBrush colorActive = (SolidColorBrush)new BrushConverter().ConvertFrom("#FF42C765");
        SolidColorBrush colorBackgroundDark = (SolidColorBrush)new BrushConverter().ConvertFrom("#FFDEDEDE");

        /// <summary>
        /// Updates the value labels for the selected device with current state values.
        /// Only updates if the device matches the currently selected device.
        /// IMPORTANT: This method is called at high frequency (10Hz) from CustomButtonPressed.
        /// </summary>
        /// <param name="deviceInstanceGuid">The instance GUID of the device being updated</param>
        /// <param name="listState">The current state values for the device</param>
        public void UpdateValueLabels(Guid deviceInstanceGuid, CustomInputState listState)
        {
            // Only update if this is the currently selected device
            if (_currentSelectedDeviceInstanceGuid == Guid.Empty ||
                _currentSelectedDeviceInstanceGuid != deviceInstanceGuid)
                return;

            if (listState == null)
                return;

            // Update axes values
            if (listState.Axes != null && SelectedDeviceAxisLabels.Count > 0)
            {
            	for (int i = 0; i < Math.Min(listState.Axes.Length, SelectedDeviceAxisLabels.Count); i++)
            	{
            		var currentValue = listState.Axes[i];
            		SelectedDeviceAxisLabels[i].Item2.Content = currentValue.ToString();
            		// Highlight axes that are not centered (for visual feedback)
            		SelectedDeviceAxisLabels[i].Item1.Background =
                        (currentValue > 1000 && currentValue < 31767) || (currentValue > 33767 && currentValue < 64535)
                        ? colorActive 
                        : colorBackgroundDark;
            	}
            }

            // Update slider values
            if (listState.Sliders != null && SelectedDeviceSliderLabels.Count > 0)
            {
                for (int i = 0; i < Math.Min(listState.Sliders.Length, SelectedDeviceSliderLabels.Count); i++)
                {
                    var currentValue = listState.Sliders[i];
                    SelectedDeviceSliderLabels[i].Item2.Content = currentValue.ToString();
                    SelectedDeviceSliderLabels[i].Item1.Background = currentValue > 10000 ? colorActive : colorBackgroundDark;
                }
            }

            // Update button values
            if (listState.Buttons != null && SelectedDeviceButtonLabels.Count > 0)
            {
                for (int i = 0; i < Math.Min(listState.Buttons.Length, SelectedDeviceButtonLabels.Count); i++)
                {
                    var currentValue = listState.Buttons[i];
                    SelectedDeviceButtonLabels[i].Item2.Content = currentValue.ToString();
                    SelectedDeviceButtonLabels[i].Item1.Background = currentValue > 0 ? colorActive : colorBackgroundDark;
                }
            }

            // Update key values (keys use the same button list)
            if (listState.Buttons != null && SelectedDeviceKeyLabels.Count > 0)
            {
                for (int i = 0; i < Math.Min(listState.Buttons.Length, SelectedDeviceKeyLabels.Count); i++)
                {
                    var currentValue = listState.Buttons[i];
                    SelectedDeviceKeyLabels[i].Item2.Content = currentValue.ToString();
                    SelectedDeviceKeyLabels[i].Item1.Background = currentValue > 0 ? colorActive : colorBackgroundDark;
                }
            }

            // Update POV values
            if (listState.POVs != null && SelectedDevicePovLabels.Count > 0)
            {
                for (int i = 0; i < Math.Min(listState.POVs.Length, SelectedDevicePovLabels.Count); i++)
                {
                    var currentValue = listState.POVs[i];
                    SelectedDevicePovLabels[i].Item2.Content = currentValue.ToString();
                    SelectedDevicePovLabels[i].Item1.Background = currentValue > -1 ? colorActive : colorBackgroundDark;
                }
            }
        }

        /// <summary>
        /// Creates a group of input elements (axes, sliders, buttons, or POVs).
        /// </summary>
        /// <param name="parentPanel">Parent panel to add the group to</param>
        /// <param name="groupName">Name of the input group</param>
        /// <param name="count">Number of inputs from device capabilities</param>
        /// <param name="values">Optional live values from device state</param>
        private void CreateInputGroup(StackPanel parentPanel, string groupName, int count, int[] values)
        {
            if (count == 0)
                return;

            // Clear the appropriate list before creating new labels
            switch (groupName)
            {
                case "Axes":
                    SelectedDeviceAxisLabels.Clear();
                    break;
                case "Sliders":
                    SelectedDeviceSliderLabels.Clear();
                    break;
                case "Buttons":
                    SelectedDeviceButtonLabels.Clear();
                    break;
                case "Keys":
                    SelectedDeviceKeyLabels.Clear();
                    break;
                case "POVs":
                    SelectedDevicePovLabels.Clear();
                    break;
            }

            // GroupBox
            var groupBox = new GroupBox { Header = groupName };

            // UniformGrid
            var uniformGrid = new UniformGrid();

            for (int i = 0; i < count; i++)
            {
                // StackPanel
                var stackPanel = new StackPanel();

                // Input Label - shows zero-based index (0,1,2,3,4,5...)
                var inputLabel = new Label
                {
                    Content = (i + 1).ToString(),
                };
                stackPanel.Children.Add(inputLabel);

                // Value Label - shows live value or "N/A" if state unavailable
                string displayValue = "N/A";
                if (values != null && i < values.Length)
                {
                    displayValue = values[i].ToString();
                }

                var valueLabel = new Label
                {
                    IsHitTestVisible = false,
                    FontSize = 8,
                    Margin = new Thickness(0),
                    Padding = new Thickness(0),
                    Content = displayValue,
                    Background = Brushes.Transparent,
                };

                if (groupName == "Buttons")
                { 
                    valueLabel.Visibility = Visibility.Collapsed;
                    groupBox.Padding = new Thickness(5,0,6,6);
                }
                else
                {
                    groupBox.Padding = new Thickness(5,0,6,4);
                }

                    stackPanel.Children.Add(valueLabel);
                uniformGrid.Children.Add(stackPanel);

                // Add the value label to the appropriate list
                switch (groupName)
                {
                    case "Axes":
                        SelectedDeviceAxisLabels.Add((inputLabel, valueLabel));
                        break;
                    case "Sliders":
                        SelectedDeviceSliderLabels.Add((inputLabel, valueLabel));
                        break;
                    case "Buttons":
                        SelectedDeviceButtonLabels.Add((inputLabel, valueLabel));
                        break;
                    case "Keys":
                        SelectedDeviceKeyLabels.Add((inputLabel, valueLabel));
                        break;
                    case "POVs":
                        SelectedDevicePovLabels.Add((inputLabel, valueLabel));
                        break;
                }
            }

            groupBox.Content = uniformGrid;
            parentPanel.Children.Add(groupBox);
        }
    }

}
