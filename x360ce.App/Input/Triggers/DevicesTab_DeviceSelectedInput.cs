using System;
using System.Linq;
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
        private readonly UnifiedInputDeviceManager _unifiedInputDeviceInfoInternal;

        /// <summary>
        /// Initializes a new instance with reference to the unified device collection.
        /// </summary>
        /// <param name="unifiedDeviceInfo">The unified device collection containing all device lists</param>
		public DevicesTab_DeviceSelectedInput(UnifiedInputDeviceManager unifiedInputDevice)
        {
            _unifiedInputDeviceInfoInternal = unifiedInputDevice ?? throw new ArgumentNullException(nameof(unifiedInputDevice));
        }

        /// <summary>
        /// Gets axe, slider, button and pov information from device state as XAML elements for display.
        /// Extracts properties from the appropriate device list based on input type.
        /// </summary>
        /// <param name="inputType">The input type (DirectInput, RawInput, etc.)</param>
        /// <param name="interfacePath">The device interface path for identification</param>
        /// <returns>UIElement containing formatted device information or null if device not found</returns>
        public UIElement GetDeviceInputAsXamlElements(UnifiedInputDeviceInfo unifiedInputDeviceInfo)
        {

            // Get device information list based on input type
            var deviceStateAsList = GetDeviceStateAsList(unifiedInputDeviceInfo);
            if (deviceStateAsList == null)
                return null;

            // Create layout
            return CreateInputLayout(deviceStateAsList);
        }

        private readonly RawInputState _statesRawInput = new RawInputState();
        private readonly DirectInputState _statesDirectInput = new DirectInputState();
        private readonly XInputState _statesXInput = new XInputState();
        private readonly GamingInputState _statesGamingInput = new GamingInputState();


        /// <summary>
		/// Retrieves device information properties from the appropriate device list.
		/// </summary>
		/// <param name="inputType">The input type identifier</param>
		/// <param name="interfacePath">The device interface path</param>
		/// <returns>List of property name-value pairs</returns>
		private InputStateAsList GetDeviceStateAsList(UnifiedInputDeviceInfo unifiedInputDeviceInfo)
        {
            var inputType = unifiedInputDeviceInfo.InputType;
            var interfacePath = unifiedInputDeviceInfo.InterfacePath;

            InputStateAsList listState = null;

            switch (inputType)
            {
                //case "PnPInput":
                //    PnPInputDeviceInfo pnpInputDeviceInfo = _unifiedInputDeviceInfo.PnPInputDeviceInfoList?
                //        .FirstOrDefault(d => string.Equals(d.HardwareIds, interfacePath, StringComparison.OrdinalIgnoreCase) ||
                //                             string.Equals(d.DeviceInstanceId, interfacePath, StringComparison.OrdinalIgnoreCase));
                //    break;
                case "RawInput":
                    RawInputDeviceInfo riInfo = _unifiedInputDeviceInfoInternal.RawInputDeviceInfoList?
                        .FirstOrDefault(d => string.Equals(d.InterfacePath, interfacePath, StringComparison.OrdinalIgnoreCase));
                    // Get the latest RawInput device state (non-blocking)
                    var riState = _statesRawInput.GetRawInputState(riInfo);
                    // Convert RawInput state to ListTypeState format (non-blocking)
                    listState = RawInputStateToList.ConvertRawInputStateToList(riState, riInfo);
                    break;

                case "DirectInput":
                    DirectInputDeviceInfo directInputDeviceInfo = _unifiedInputDeviceInfoInternal.DirectInputDeviceInfoList?
                        .FirstOrDefault(d => string.Equals(d.InterfacePath, interfacePath, StringComparison.OrdinalIgnoreCase));
                    // Get the latest DirectInput device state (non-blocking)
                    var diState = _statesDirectInput.GetDirectInputState(directInputDeviceInfo);
                    // Convert DirectInput state to ListTypeState format (non-blocking)
                    listState = DirectInputStateToList.ConvertDirectInputStateToList(diState);
                    break;

                case "XInput":
                    XInputDeviceInfo xInputDeviceInfo = _unifiedInputDeviceInfoInternal.XInputDeviceInfoList?
                        .FirstOrDefault(d => string.Equals(d.InterfacePath, interfacePath, StringComparison.OrdinalIgnoreCase));
                    // Get the latest XInput device state (non-blocking)
                    var xiState = _statesXInput.GetXInputState(xInputDeviceInfo);
                    // Convert XInput state to ListTypeState format (non-blocking)
                    if (xiState.HasValue)
                        listState = XInputStateToList.ConvertXInputStateToList(xiState.Value);
                    break;

                case "GamingInput":
                    GamingInputDeviceInfo gamingInputDeviceInfo = _unifiedInputDeviceInfoInternal.GamingInputDeviceInfoList?
                        .FirstOrDefault(d => string.Equals(d.InterfacePath, interfacePath, StringComparison.OrdinalIgnoreCase));
                    // Get the latest GamingInput device state (non-blocking)
                    var giState = _statesGamingInput.GetGamingInputState(gamingInputDeviceInfo);
                    // Convert GamingInput state to ListTypeState format (non-blocking)
                    listState = GamingInputStateToList.ConvertGamingInputStateToList(giState);
                    break;
            }

            return listState;
        }

        /// <summary>
        /// Creates layout for device properties using UniformGrid.
        /// </summary>
        /// <param name="inputStateAsList">List of property name-value pairs</param>
        /// <returns>UIElement containing the formatted layout</returns>
        private UIElement CreateInputLayout(InputStateAsList inputStateAsList)
        {
            if (inputStateAsList == null || (inputStateAsList.Axes.Count == 0 && inputStateAsList.Sliders.Count == 0 && inputStateAsList.Buttons.Count == 0 && inputStateAsList.POVs.Count == 0))
                return null;

            var Axes = inputStateAsList.Axes;
            var Sliders = inputStateAsList.Sliders;
            var Buttons = inputStateAsList.Buttons;
            var POVs = inputStateAsList.POVs;

            var mainStackPanel = new StackPanel();

            foreach (var currentList in new[] { Axes, Sliders, Buttons, POVs })
            {
                if (currentList.Count > 0)
                {
                    var groupBoxHeader = "Unknown";

                    switch (currentList)
                    {
                        case var x when x == Axes:
                            groupBoxHeader = "Axes";
                            break;
                        case var x when x == Sliders:
                            groupBoxHeader = "Sliders";
                            break;
                        case var x when x == Buttons:
                            groupBoxHeader = "Buttons";
                            break;
                        case var x when x == POVs:
                            groupBoxHeader = "POVs";
                            break;
                    }
                    // GroupBox
                    var groupBox = new GroupBox();
                    groupBox.Header = groupBoxHeader;
                    // UniformGrid
                    var uniformGrid = new UniformGrid();

                    for (int i = 0; i < currentList.Count; i++)
                    {
                        var item = currentList[i];

                        // StackPanel
                        var stackPanel = new StackPanel();

                        // Input Label - now shows actual index (0,1,2,3,4,5...)
                        var inputLabel = new Label
                        {
                            Content = i.ToString(),
                        };
                        stackPanel.Children.Add(inputLabel);

                        // Value Label.
                        var valueLabel = new Label
                        {
                            IsHitTestVisible = false,
                            FontSize = 8,
                            Margin = new Thickness(0),
                            Padding = new Thickness(0),
                            Content = item.ToString(),
                            Background = Brushes.Transparent,
                        };
                        stackPanel.Children.Add(valueLabel);
                        uniformGrid.Children.Add(stackPanel);
                    }

                    groupBox.Content = uniformGrid;
                    mainStackPanel.Children.Add(groupBox);
                }
            }

            return mainStackPanel;

        }
    }

}

