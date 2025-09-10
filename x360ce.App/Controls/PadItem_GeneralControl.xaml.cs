using JocysCom.ClassLibrary.Controls;
using SharpDX.DirectInput;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using x360ce.Engine;
using x360ce.Engine.Data;
using x360ce.Engine.Common;

namespace x360ce.App.Controls
{
	// Half.
	public class ContainsKeywordConverterType : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			string text = value as string;
			if (!string.IsNullOrEmpty(text))
			{
				if (text.StartsWith("IButton"))
					return "IButton";
				else if (text.StartsWith("Button"))
					return "Button";
				else if (text.StartsWith("IAxis"))
					return "IAxis";
				else if (text.StartsWith("Axis"))
					return "Axis";
				else if (text.StartsWith("IHAxis"))
					return "IHAxis";
				else if (text.StartsWith("HAxis"))
					return "HAxis";
				else if (text.StartsWith("ISlider"))
					return "ISlider";
				else if (text.StartsWith("Slider"))
					return "Slider";
				else if (text.StartsWith("IHSlider"))
					return "IHSlider";
				else if (text.StartsWith("HSlider"))
					return "HSlider";
				else
					return "Empty";
			}
			return DependencyProperty.UnsetValue;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}

	public static class InversionHelper
	{
		public static readonly DependencyProperty InversionTargetProperty =
			DependencyProperty.RegisterAttached(
				"InversionTarget",
				typeof(TextBox),
				typeof(InversionHelper),
				new PropertyMetadata(null));

		public static void SetInversionTarget(UIElement element, TextBox value)
		{
			element.SetValue(InversionTargetProperty, value);
		}

		public static TextBox GetInversionTarget(UIElement element)
		{
			return (TextBox)element.GetValue(InversionTargetProperty);
		}
	}


	/// <summary>
	/// Interaction logic for PadControl_GeneralControl.xaml
	/// </summary>
	public partial class PadItem_GeneralControl : UserControl
	{
		public PadItem_GeneralControl()
		{
			InitHelper.InitTimer(this, InitializeComponent);
		}

		PadSetting _padSetting;
		MapTo _MappedTo;

		public void SetBinding(MapTo mappedTo, PadSetting ps, List<ImageInfo> imageInfo)
		{
			_MappedTo = mappedTo;
			//if (_padSetting != null) _padSetting.PropertyChanged -= _padSetting_PropertyChanged;

			// Unbind controls.
			foreach (var item in imageInfo) { SettingsManager.UnLoadMonitor(item.ControlBindedName as Control); }

			// Bind controls.
			if (ps == null) return;
			_padSetting = ps;
			var converter = new Converters.PaddSettingToText();


			foreach (var item in imageInfo) { SettingsManager.LoadAndMonitor(ps, item.Code.ToString(), item.ControlBindedName as Control, null, converter); }

			//_padSetting.PropertyChanged += _padSetting_PropertyChanged;
		}

		//private void _padSetting_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		//{
		// This event handler was originally empty.
		//}

		private void SetPresetButton_Click(object sender, RoutedEventArgs e) { }
		private void RemapAllButton_Click(object sender, RoutedEventArgs e) { }

		public void MapNameComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (!(sender is ComboBox box) || !(box.SelectedItem is Layout item)) return;

			// Update Trigger labels.
			TriggerLLabel.Content = item.LeftTrigger;
			TriggerRLabel.Content = item.RightTrigger;
			// Update Bumper labels.
			BumperLLabel.Content = item.LeftShoulder;
			BumperRLabel.Content = item.RightShoulder;
			// Update Menu labels.
			MenuBackLabel.Content = item.ButtonBack;
			MenuGuideLabel.Content = item.ButtonGuide;
			MenuStartLabel.Content = item.ButtonStart;
			// Update Action labels.
			ActionALabel.Content = item.ButtonA;
			ActionBLabel.Content = item.ButtonB;
			ActionXLabel.Content = item.ButtonX;
			ActionYLabel.Content = item.ButtonY;
			// Update D-Pad labels.
			DPadLabel.Content = item.DPad;
			DPadDownLabel.Content = item.DPadDown;
			DPadLeftLabel.Content = item.DPadLeft;
			DPadRightLabel.Content = item.DPadRight;
			DPadUpLabel.Content = item.DPadUp;
			// Update Stick Left labels.
			StickLButtonLabel.Content = item.LeftThumbButton;
			StickLAxisXLabel.Content = item.LeftThumbAxisX;
			StickLAxisYLabel.Content = item.LeftThumbAxisY;
			StickLDownLabel.Content = item.LeftThumbDown;
			StickLLeftLabel.Content = item.LeftThumbLeft;
			StickLRightLabel.Content = item.LeftThumbRight;
			StickLUpLabel.Content = item.LeftThumbUp;
			// Update Stick Right labels.
			StickRButtonLabel.Content = item.RightThumbButton;
			StickRAxisXLabel.Content = item.RightThumbAxisX;
			StickRAxisYLabel.Content = item.RightThumbAxisY;
			StickRDownLabel.Content = item.RightThumbDown;
			StickRLeftLabel.Content = item.RightThumbLeft;
			StickRRightLabel.Content = item.RightThumbRight;
			StickRUpLabel.Content = item.RightThumbUp;
		}

		#region Drag and Drop Menu

		/// <summary>
		/// Tracks the current drag state for UI operations. 
		/// Used to coordinate drag and drop behavior and prevent conflicting operations.
		/// </summary>
#pragma warning disable CS0414 // Field is assigned but never used - intentionally tracks drag state
		bool isDragging = false;
#pragma warning restore CS0414

        private void DragAndDropMenuLabel_Source_MouseDown(object sender, MouseEventArgs e)
        {
			if (sender is Label label && e.LeftButton == MouseButtonState.Pressed)
			{
                // Change dragging status.
                isDragging = true;
                Global.Orchestrator.StopDInputService();
				label.Background = colorRecord;

                try
				{
					// Start DragDrop with Label content.
					DragDrop.DoDragDrop(label, label.Tag?.ToString() ?? string.Empty, DragDropEffects.Copy);
				}
				finally
				{
                    // In any case, dragging ended or stopped (successful or not).
                    isDragging = false;
                    Global.Orchestrator.StartDInputService();
					label.Background = Brushes.Transparent;
                }
			}
        }

        // Drag and Drop Menu Drop event.
        private void DragAndDropMenu_Target_Drop(object sender, DragEventArgs e)
		{
			if (sender is TextBox textbox && e.Data.GetDataPresent(DataFormats.Text))
			{
				textbox.Clear();
				textbox.Text = (string)e.Data.GetData(DataFormats.Text);
			}
			e.Handled = true;
		}

		// Colors.
		SolidColorBrush colorActive = (SolidColorBrush)new BrushConverter().ConvertFrom("#FF42C765");
		SolidColorBrush colorLight = (SolidColorBrush)new BrushConverter().ConvertFrom("#FFF0F0F0");
		SolidColorBrush colorBackgroundDark = (SolidColorBrush)new BrushConverter().ConvertFrom("#FFDEDEDE");
		//SolidColorBrush colorNormalPath = (SolidColorBrush)new BrushConverter().ConvertFrom("#FF6699FF");
		//SolidColorBrush colorNormalTextBox = Brushes.White;
		//SolidColorBrush colorBlack = (SolidColorBrush)new BrushConverter().ConvertFrom("#11000000");
		SolidColorBrush colorNormal = (SolidColorBrush)new BrushConverter().ConvertFrom("#FF6699FF");
		//SolidColorBrush colorOver = (SolidColorBrush)new BrushConverter().ConvertFrom("#FFFFCC66");
		SolidColorBrush colorRecord = (SolidColorBrush)new BrushConverter().ConvertFrom("#FFFF6B66");

		Dictionary<int, (Label, Label, Label)> ButtonDictionary = new Dictionary<int, (Label, Label, Label)>();
		Dictionary<int, (Label, Label, Label)> PovDictionary = new Dictionary<int, (Label, Label, Label)>();
		Dictionary<int, (Label, Label, Label)> PovBDictionary = new Dictionary<int, (Label, Label, Label)>();
		Dictionary<int, (Label, Label, Label)> AxisDictionary = new Dictionary<int, (Label, Label, Label)>();
		Dictionary<int, (Label, Label, Label)> HAxisDictionary = new Dictionary<int, (Label, Label, Label)>();
		Dictionary<int, (Label, Label, Label)> SliderDictionary = new Dictionary<int, (Label, Label, Label)>();
		Dictionary<int, (Label, Label, Label)> HSliderDictionary = new Dictionary<int, (Label, Label, Label)>();

		object updateLock = new object();

		/// <summary>
		/// Gets the CustomDeviceState for the specified UserDevice.
		/// This method now uses the existing DiState property that should be populated 
		/// by all input methods (DirectInput, XInput, GamingInput, RawInput) rather than 
		/// trying to create a new CustomDeviceState from DirectInput-specific DeviceState.
		/// This ensures drag and drop buttons are visible regardless of InputMethod.
		/// </summary>
		/// <param name="ud">The UserDevice to get the CustomDeviceState from</param>
		/// <returns>The CustomDeviceState if available, null otherwise</returns>
		public CustomDeviceState GetCustomDeviceState(UserDevice ud)
		{
			// Use the existing DiState property that should be populated by all input methods
			// instead of trying to create a new CustomDeviceState from DirectInput-specific DeviceState
			return ud?.DeviceState;
		}

		UniformGrid PovUnifromGrid;

		// Create DragAndDrop menu labels.
		private void DragAndDropMenuLabels_Create(Dictionary<int, (Label, Label, Label)> dictionary, List<int> list, string itemName, string headerName, string iconName)
		{
			try
			{
				// GroupBox Header (icon and text).
				StackPanel headerStackPanel = new StackPanel { Orientation = Orientation.Horizontal };
				// Group icons.
				headerStackPanel.Children.Add(new ContentControl { Content = Application.Current.Resources[iconName] });
				if (!headerName.Contains("POV"))
				{
					headerStackPanel.Children.Add(new ContentControl { Content = Application.Current.Resources[iconName + "_Inverted"], Margin = new Thickness(3, 0, 0, 0) });
				}
				// Group title.
				headerStackPanel.Children.Add(new TextBlock { Text = headerName, Margin = new Thickness(3, 0, 0, 0) });
				if (headerName.Contains("HALF"))
				{
					headerStackPanel.Children.Add(new ContentControl { Content = Application.Current.Resources[iconName + "_2"], Margin = new Thickness(3, 0, 3, 0) });
					headerStackPanel.Children.Add(new ContentControl { Content = Application.Current.Resources[iconName + "_2"] });
				}
				// GroupBox Content (UniformGrid for Labels).
				UniformGrid buttonsUniformGrid = new UniformGrid { Columns = list.Last().ToString().Length > 2 ? 6 : 8 };
				// GroupBox.
				GroupBox buttonsGroupBox = new GroupBox { Header = headerStackPanel, Content = buttonsUniformGrid };

				// Put GroupBoxes into NORMAL and INVERTED tabs.
				if (iconName.Contains("Inverted"))
				{
					// DragAndDropStackPanelInverted.Children.Add(buttonsGroupBox);
				}
				else
				{
					DragAndDropStackPanel.Children.Add(buttonsGroupBox);
				}

				// Put POVB buttons inside POV GroupBox.
				if (itemName == "POV")
				{
					PovUnifromGrid = buttonsUniformGrid;
				}
				if (itemName == "POVB")
				{
					buttonsGroupBox.Visibility = Visibility.Collapsed;
				}

				// Create drag and drop buttons.
				dictionary.Clear();
				foreach (var i in list)
				{
					Label buttonLabel = new Label();
					if (itemName == "POVB")
					{
						var povNumber = (i / 4) + 1;
						// Name.
						var povNumberN = new[] { "U", "R", "D", "L" }[i % 4];
						buttonLabel.Content = povNumberN;
						// Drag and drop text.
						var povNumberB = new[] { "Up", "Right", "Down", "Left" }[i % 4];
						buttonLabel.Tag = $"POV {povNumber} {povNumberB}";
					}
					else
					{
						buttonLabel.Content = (i + 1).ToString();
						buttonLabel.Tag = $"{itemName} {buttonLabel.Content}";
					}

                    buttonLabel.MouseDown += DragAndDropMenuLabel_Source_MouseDown;

                    Label valueLabel = new Label
					{
						IsHitTestVisible = false,
						FontSize = 8,
						Padding = new Thickness(0),
						Background = colorLight
					};

					Label valueLabelInverted = new Label
					{
						IsHitTestVisible = false,
						FontSize = 8,
						Foreground = colorNormal,
						Padding = new Thickness(0),
						Background = colorLight
					};

					StackPanel stackPanel = new StackPanel();
					stackPanel.Children.Add(buttonLabel);
					stackPanel.Children.Add(valueLabel);
					stackPanel.Children.Add(valueLabelInverted);

					// Put POVB buttons inside POV GroupBox.
					if (itemName == "POVB")
					{
						PovUnifromGrid.Children.Add(stackPanel);
					}
					else
					{
						buttonsUniformGrid.Children.Add(stackPanel);
					}

					dictionary.Add(i, (buttonLabel, valueLabel, valueLabelInverted));
				}
			}
			catch (Exception ex)
			{
				// Simply ignore the exception by storing the message.
				var _ = ex.Message;
			}
		}

		List<int> buttons = new List<int>();
		List<int> povs = new List<int>();
		List<int> axes = new List<int>();
		List<int> sliders = new List<int>();

		// Runs every time a new DirectInput device becomes available / unaivalble.
		public void UpdateDragAndDropMenu(UserDevice ud)
		{
			// Clear drag and drop StackPanel children elements in XAML page.
			DragAndDropStackPanel.Children.Clear();
			// Clear dictionaries used to create drag and drop StackPanel content.
			ButtonDictionary.Clear();
			PovDictionary.Clear();
			PovBDictionary.Clear();
			AxisDictionary.Clear();
			HAxisDictionary.Clear();
			SliderDictionary.Clear();
			HSliderDictionary.Clear();

			// Clear lists with DirectInput device InstanceNumber's.
			buttons.Clear();
			povs.Clear();
			axes.Clear();
			sliders.Clear();

			// Update the input method status indicator first
			UpdateInputMethodStatus(ud);

			// Check if device is available and input method is supported
			if (ud == null || GetCustomDeviceState(ud) == null)
			{
				// Show "No Device" message in drag and drop area
				var noDeviceLabel = new Label
				{
					Content = "No device detected",
					HorizontalAlignment = HorizontalAlignment.Center,
					Opacity = 0.5,
					Margin = new Thickness(0, 20, 0, 20)
				};
				DragAndDropStackPanel.Children.Add(noDeviceLabel);
				return;
			}

			// Check if current input method is supported
			bool inputMethodSupported = IsInputMethodSupported(ud, ud.InputMethod);
			if (!inputMethodSupported)
			{
				// Show "Input Method Not Supported" message instead of drag and drop controls
				var unsupportedLabel = new Label
				{
					Content = $"{ud.InputMethod} not supported for this device",
					HorizontalAlignment = HorizontalAlignment.Center,
					Foreground = colorRecord,
					Margin = new Thickness(0, 20, 0, 0),
					FontWeight = FontWeights.Bold
				};
				
				var availableMethods = InputMethodDetector.GetAvailableInputMethodsText(ud);
				var suggestionLabel = new Label
				{
					Content = $"Available: {availableMethods}",
					HorizontalAlignment = HorizontalAlignment.Center,
					Opacity = 0.7,
					Margin = new Thickness(0, 5, 0, 0)
				};

				DragAndDropStackPanel.Children.Add(unsupportedLabel);
				DragAndDropStackPanel.Children.Add(suggestionLabel);
				return;
			}

			// Input method is supported - proceed with normal drag and drop menu creation
			UpdatePovsAxesButtonsSlidersLists(ud);

			buttons.Sort();
			povs.Sort();
			axes.Sort();
			sliders.Sort();

			// Buttons and Keys.
			if (buttons.Any())
			{
				DragAndDropMenuLabels_Create(ButtonDictionary, buttons, "Button", "BUTTON", "Icon_DragAndDrop_Button");
			}
			// Axes.
			if (axes.Any())
			{
				DragAndDropMenuLabels_Create(AxisDictionary, axes, "Axis", "AXIS", "Icon_DragAndDrop_Axis");
				DragAndDropMenuLabels_Create(HAxisDictionary, axes, "HAxis", "AXIS · HALF", "Icon_DragAndDrop_Axis_Half");
			}
			// Sliders.
			if (sliders.Any())
			{
				DragAndDropMenuLabels_Create(SliderDictionary, sliders, "Slider", "SLIDER", "Icon_DragAndDrop_Axis");
				DragAndDropMenuLabels_Create(HSliderDictionary, sliders, "HSlider", "SLIDER · HALF", "Icon_DragAndDrop_Axis_Half");
			}
			// POVs.
			if (povs.Any())
			{
				DragAndDropMenuLabels_Create(PovDictionary, povs, "POV", "POV", "Icon_DragAndDrop_POV");
				var povButtons = new List<int>();
				// Add 4 buttons (Up, Right, Bottom, Left) for each POV.
				for (int i = 0; i < povs.Count * 4; i++) { povButtons.Add(i); }
				DragAndDropMenuLabels_Create(PovBDictionary, povButtons, "POVB", "POV · BUTTON", "Icon_DragAndDrop_POV");
			}
		}

		/// <summary>
		/// Populates the lists of available device inputs (buttons, axes, POVs, sliders) 
		/// for creating drag and drop buttons. Now works with all input methods by using 
		/// device capabilities instead of DirectInput-specific device objects.
		/// </summary>
		/// <param name="ud">The UserDevice to analyze</param>
		/// <param name="usage">Optional usage parameter (currently unused)</param>
		private void UpdatePovsAxesButtonsSlidersLists(UserDevice ud, int usage = 0)
		{
			// Use device capabilities instead of DirectInput-specific device introspection
			// This approach works for all input methods (DirectInput, XInput, GamingInput, RawInput)

			// POVs - Use the capability count directly
			for (int i = 0; i < ud.CapPovCount; i++)
			{
				povs.Add(i);
			}

			// Buttons - Use the capability count directly  
			for (int i = 0; i < ud.CapButtonCount; i++)
			{
				buttons.Add(i);
			}

			// Axes - Use the capability count directly
			for (int i = 0; i < ud.CapAxeCount; i++)
			{
				axes.Add(i);
			}

			// Sliders - For non-DirectInput modes, we can assume standard slider availability
			// This provides a consistent interface regardless of input method
			if (ud.InputMethod == x360ce.Engine.InputMethod.DirectInput && ud.DirectInputDevice is Joystick device)
			{
				// For DirectInput, check actual slider state to see what's available
				try
				{
					var state = (JoystickState)ud.DirectInputDeviceState;
					if (state != null)
					{
						if (state.Sliders[0] != 0) sliders.Add(0);
						if (state.Sliders[1] != 0) sliders.Add(1);
						if (state.AccelerationSliders[0] != 0) sliders.Add(2);
						if (state.AccelerationSliders[1] != 0) sliders.Add(3);
						if (state.ForceSliders[0] != 0) sliders.Add(4);
						if (state.ForceSliders[1] != 0) sliders.Add(5);
						if (state.VelocitySliders[0] != 0) sliders.Add(6);
						if (state.VelocitySliders[1] != 0) sliders.Add(7);
					}
				}
				catch
				{
					// If DirectInput state access fails, fall back to basic slider assumption
					for (int i = 0; i < 2; i++) // Assume basic 2 sliders for most controllers
					{
						sliders.Add(i);
					}
				}
			}
			else
			{
				// For non-DirectInput input methods, handle sliders based on specific input method
				switch (ud.InputMethod)
				{
					case x360ce.Engine.InputMethod.XInput:
						// XInput doesn't use sliders - triggers are handled as separate axes (axes 4 and 5)
						// No sliders should be added for XInput to match the reported capabilities
						break;
					
					default:
						// For other non-DirectInput input methods, provide standard slider options
						// Most game controllers have at least 2 slider-like inputs (triggers)
						for (int i = 0; i < 2; i++)
						{
							sliders.Add(i);
						}
						break;
				}
			}

			Debug.WriteLine($"INFO: Device '{ud.InstanceName}' InputMethod: {ud.InputMethod}, " +
				$"Buttons: {buttons.Count}, Axes: {axes.Count}, POVs: {povs.Count}, Sliders: {sliders.Count}");
		}

		public static string GetObjectTypeName(Guid guid)
		{
			foreach (FieldInfo field in typeof(ObjectGuid).GetFields(BindingFlags.Public | BindingFlags.Static))
			{
				if (field.FieldType == typeof(Guid) && (Guid)field.GetValue(null) == guid)
					return field.Name;
			}
			return "Unknown";
		}

		//private void SetLabelDIContent(int axisLength, CustomDeviceState customDeviceState, TargetType targetType, Label label)
		//{
		//	Map map = _padSetting.Maps.FirstOrDefault(x => x.Target == targetType);

		//	if (map?.Index <= 0 || map.Index > axisLength)
		//		return;

		//	var i = map.Index - 1;
		//	if (map.IsAxis || map.IsHalf || map.IsInverted)
		//	{
		//		label.Content = customDeviceState.Axis[i];
		//	}
		//	else if (map.IsButton)
		//	{
		//		label.Content = customDeviceState.Buttons[i] ? 1 : 0;
		//	}
		//	else if (map.IsSlider)
		//	{
		//		label.Content = customDeviceState.Sliders[i];
		//	}
		//}

		// Update DragAndDrop menu labels.
		public void DragAndDropMenuLabels_Update(UserDevice ud)
		{
			// Update the input method status indicator
			UpdateInputMethodStatus(ud);

			// Return early if device state is not available
			if (ud?.DeviceState == null)
				return;

			// Buttons.
			foreach (var kvp in ButtonDictionary)
			{
				bool bDS = ud.DeviceState.Buttons[kvp.Key];

				ButtonDictionary[kvp.Key].Item1.Background = bDS ? colorActive : Brushes.Transparent;
				ButtonDictionary[kvp.Key].Item2.Content = bDS.ToString();
				ButtonDictionary[kvp.Key].Item3.Content = (bDS ? "True" : "False") == "True" ? "False" : "True";

				// Record active button.
				if (recordTextBox != null && bDS)
				{
					RecordAxisOrButton(ButtonDictionary[kvp.Key].Item1.Tag.ToString());
				}
			}

			// POVs.
			int[] povButtonValues = new[] { 0, 9000, 18000, 27000, 0, 9000, 18000, 27000, 0, 9000, 18000, 27000, 0, 9000, 18000, 27000 };
			foreach (var kvp in PovDictionary)
			{
				int pDS = ud.DeviceState.POVs[kvp.Key];
				PovDictionary[kvp.Key].Item1.Background = pDS > -1 ? colorActive : Brushes.Transparent;
				PovDictionary[kvp.Key].Item2.Content = pDS;
				// Up, Right, Down, Left.
				for (int b = 0; b < PovDictionary.Count * 4 && b < povButtonValues.Length; b++)
				{
					PovBDictionary[b].Item1.Background = pDS == povButtonValues[b] ? colorActive : Brushes.Transparent;
					PovBDictionary[b].Item2.Content = pDS == povButtonValues[b] ? pDS : -1;
				}

				// Record active POV.
				if (recordTextBox != null && pDS > -1)
				{
					var povName = PovDictionary[kvp.Key].Item1.Tag.ToString(); // "POV 1".
					var povDirection = povName;
					if (recordTextBox != DPadTextBox)
					{
						switch (pDS)
						{
							case 0: povName = povName + " Up"; break;
							case 9000: povName = povName + " Right"; break;
							case 18000: povName = povName + " Down"; break;
							case 27000: povName = povName + " Left"; break;
						}
					}
					RecordAxisOrButton(povName);
				}
			}

			// Stick axes.
			if (ud == null) return;
            const int DragAndDropAxisDeadzone = 8000;
			foreach (var kvp in AxisDictionary)
			{
				int aDS = ud.DeviceState.Axis[kvp.Key];
				bool active = (ud.InputMethod == x360ce.Engine.InputMethod.XInput) ? aDS > DragAndDropAxisDeadzone : aDS < 32767 - DragAndDropAxisDeadzone || aDS > 32767 + DragAndDropAxisDeadzone;
                AxisDictionary[kvp.Key].Item1.Background = active ? colorActive : Brushes.Transparent;
				HAxisDictionary[kvp.Key].Item1.Background = active ? colorActive : Brushes.Transparent;

				AxisDictionary[kvp.Key].Item2.Content = aDS;
				HAxisDictionary[kvp.Key].Item2.Content = Math.Max(0, Math.Min((aDS - 32767) * 2, 65535));
				AxisDictionary[kvp.Key].Item3.Content = Math.Abs(65535 - aDS);
				HAxisDictionary[kvp.Key].Item3.Content = Math.Max(0, Math.Min((Math.Abs(65535 - aDS) - 32767) * 2, 65535));

				// Record active axis.
				if (recordTextBox != null && active)
				{
					RecordAxisOrButton(AxisDictionary[kvp.Key].Item1.Tag.ToString());
				}
			}

			// Slider axes.
			const int DragAndDropSliderDeadzone = 8000;
			foreach (var kvp in SliderDictionary)
			{
				int sDS = ud.DeviceState.Sliders[kvp.Key];
				bool active = sDS > DragAndDropSliderDeadzone;
				SliderDictionary[kvp.Key].Item1.Background = active ? colorActive : Brushes.Transparent;
				HSliderDictionary[kvp.Key].Item1.Background = active ? colorActive : Brushes.Transparent;

				SliderDictionary[kvp.Key].Item2.Content = sDS;
				HSliderDictionary[kvp.Key].Item2.Content = Math.Max(0, Math.Min((sDS - 32767) * 2, 65535));

				SliderDictionary[kvp.Key].Item3.Content = Math.Abs(65535 - sDS);
				HSliderDictionary[kvp.Key].Item3.Content = Math.Max(0, Math.Min((Math.Abs(65535 - sDS) - 32767) * 2, 65535));

				// Record active axis.
				if (recordTextBox != null && active)
				{
					RecordAxisOrButton(SliderDictionary[kvp.Key].Item1.Tag.ToString());
				}
			}
		}

		/// <summary>
		/// Updates the input method status indicator to show whether the current input method is supported for the device.
		/// Uses InputMethodDetector to ensure same source of truth as the "Available Inputs" label.
		/// Provides clear feedback when unsupported input methods are selected.
		/// </summary>
		/// <param name="ud">The UserDevice to check compatibility for</param>
		public void UpdateInputMethodStatus(UserDevice ud)
		{
			if (ud == null)
			{
				InputMethodStatusLabel.Content = "No Device";
				InputMethodStatusBorder.Background = (SolidColorBrush)new BrushConverter().ConvertFrom("#FFCCCCCC");
				UpdateXILabelsVisibility(null);
				return;
			}

			var currentMethod = ud.InputMethod;
			var deviceState = ud.DeviceState;
			bool hasActiveInput = deviceState != null;
			bool isSupported = IsInputMethodSupported(ud, currentMethod);

			if (!isSupported)
			{
				// Input method is not supported - show error state
				InputMethodStatusLabel.Content = $"{currentMethod} Not Supported";
				InputMethodStatusBorder.Background = colorRecord;
				UpdateInputMethodTooltip(ud, currentMethod, false);
			}
			else
			{
				// Input method is supported - show status
				var statusText = hasActiveInput ? $"{currentMethod} - Active" : $"{currentMethod} - Ready";
				InputMethodStatusLabel.Content = statusText;
				InputMethodStatusBorder.Background = hasActiveInput ? colorActive : colorNormal;
				UpdateInputMethodTooltip(ud, currentMethod, true);
			}

			// Update XI labels visibility based on XInput support
			UpdateXILabelsVisibility(ud);
		}

		/// <summary>
		/// Checks if the specified input method is supported for the given device using InputMethodDetector.
		/// This ensures consistency with the "Available Inputs" label.
		/// </summary>
		/// <param name="ud">The UserDevice to check</param>
		/// <param name="inputMethod">The input method to check</param>
		/// <returns>True if the input method is supported</returns>
		private bool IsInputMethodSupported(UserDevice ud, Engine.InputMethod inputMethod)
		{
			if (ud == null)
				return false;

			switch (inputMethod)
			{
				case Engine.InputMethod.DirectInput:
					return InputMethodDetector.SupportsDirectInput(ud);
				case Engine.InputMethod.XInput:
					return InputMethodDetector.SupportsXInput(ud);
				case Engine.InputMethod.GamingInput:
					return InputMethodDetector.SupportsGamingInput(ud);
				case Engine.InputMethod.RawInput:
					return InputMethodDetector.SupportsRawInput(ud);
				default:
					return false;
			}
		}

		/// <summary>
		/// Updates the tooltip for the input method status label with appropriate information.
		/// </summary>
		/// <param name="ud">The UserDevice</param>
		/// <param name="inputMethod">The current input method</param>
		/// <param name="isSupported">Whether the input method is supported</param>
		private void UpdateInputMethodTooltip(UserDevice ud, Engine.InputMethod inputMethod, bool isSupported)
		{
			if (!isSupported)
			{
				var availableMethods = InputMethodDetector.GetAvailableInputMethodsText(ud);
				InputMethodStatusLabel.ToolTip = $"{inputMethod} is not supported for this device.\n\nAvailable methods: {availableMethods}\n\nSwitch to a supported input method for full functionality.";
				return;
			}

			switch (inputMethod)
			{
				case Engine.InputMethod.DirectInput:
					var diLimitation = ud.IsXboxCompatible && InputMethodDetector.IsWindows10OrLater()
						? "\n\nNote: Xbox controllers may lose background access on Windows 10+"
						: "";
					InputMethodStatusLabel.ToolTip = $"DirectInput provides universal controller support.{diLimitation}";
					break;

				case Engine.InputMethod.XInput:
					var slotsInfo = InputMethodDetector.GetAvailableXInputSlots() < 4
						? $"\n\n{4 - InputMethodDetector.GetAvailableXInputSlots()} XInput slots available"
						: "\n\nAll 4 XInput slots available";
					InputMethodStatusLabel.ToolTip = $"XInput provides Xbox controller support with background access.{slotsInfo}";
					break;

				case Engine.InputMethod.GamingInput:
					var support = ud.IsXboxCompatible
						? "full Xbox features including trigger rumble"
						: "basic gamepad support";
					InputMethodStatusLabel.ToolTip = $"GamingInput provides {support}.\n\nRequires Windows 10+. No background access.";
					break;

				case Engine.InputMethod.RawInput:
					InputMethodStatusLabel.ToolTip = "RawInput provides direct HID access with background support.\n\nMore complex setup but works with all controllers.";
					break;

				default:
					InputMethodStatusLabel.ToolTip = $"{inputMethod} input method is active.";
					break;
			}
		}

		/// <summary>
		/// Updates the visibility and styling of XInput value labels (XILabel controls) based on input method support.
		/// Uses InputMethodDetector for consistent validation with other UI elements.
		/// Grays out or hides XI labels when XInput is not supported to prevent user confusion.
		/// </summary>
		/// <param name="ud">The UserDevice to check XInput compatibility for</param>
		public void UpdateXILabelsVisibility(UserDevice ud)
		{
			bool showXILabels = ud != null &&
			                   ud.InputMethod == Engine.InputMethod.XInput &&
			                   InputMethodDetector.SupportsXInput(ud);
			var opacity = showXILabels ? 1.0 : 0.3;
			var foregroundBrush = showXILabels ? Brushes.Green : Brushes.Gray;

			// Update all XILabel controls to show appropriate visual feedback
			TriggerLXILabel.Opacity = opacity;
			TriggerLXILabel.Foreground = foregroundBrush;
			TriggerRXILabel.Opacity = opacity;
			TriggerRXILabel.Foreground = foregroundBrush;

			BumperLXILabel.Opacity = opacity;
			BumperLXILabel.Foreground = foregroundBrush;
			BumperRXILabel.Opacity = opacity;
			BumperRXILabel.Foreground = foregroundBrush;

			MenuBackXILabel.Opacity = opacity;
			MenuBackXILabel.Foreground = foregroundBrush;
			MenuStartXILabel.Opacity = opacity;
			MenuStartXILabel.Foreground = foregroundBrush;
			MenuGuideXILabel.Opacity = opacity;
			MenuGuideXILabel.Foreground = foregroundBrush;

			ActionAXILabel.Opacity = opacity;
			ActionAXILabel.Foreground = foregroundBrush;
			ActionBXILabel.Opacity = opacity;
			ActionBXILabel.Foreground = foregroundBrush;
			ActionXXILabel.Opacity = opacity;
			ActionXXILabel.Foreground = foregroundBrush;
			ActionYXILabel.Opacity = opacity;
			ActionYXILabel.Foreground = foregroundBrush;

			DPadXILabel.Opacity = opacity;
			DPadXILabel.Foreground = foregroundBrush;
			DPadUpXILabel.Opacity = opacity;
			DPadUpXILabel.Foreground = foregroundBrush;
			DPadDownXILabel.Opacity = opacity;
			DPadDownXILabel.Foreground = foregroundBrush;
			DPadLeftXILabel.Opacity = opacity;
			DPadLeftXILabel.Foreground = foregroundBrush;
			DPadRightXILabel.Opacity = opacity;
			DPadRightXILabel.Foreground = foregroundBrush;

			StickLAxisXXILabel.Opacity = opacity;
			StickLAxisXXILabel.Foreground = foregroundBrush;
			StickLAxisYXILabel.Opacity = opacity;
			StickLAxisYXILabel.Foreground = foregroundBrush;
			StickLButtonXILabel.Opacity = opacity;
			StickLButtonXILabel.Foreground = foregroundBrush;
			StickLUpXILabel.Opacity = opacity;
			StickLUpXILabel.Foreground = foregroundBrush;
			StickLDownXILabel.Opacity = opacity;
			StickLDownXILabel.Foreground = foregroundBrush;
			StickLLeftXILabel.Opacity = opacity;
			StickLLeftXILabel.Foreground = foregroundBrush;
			StickLRightXILabel.Opacity = opacity;
			StickLRightXILabel.Foreground = foregroundBrush;

			StickRAxisXXILabel.Opacity = opacity;
			StickRAxisXXILabel.Foreground = foregroundBrush;
			StickRAxisYXILabel.Opacity = opacity;
			StickRAxisYXILabel.Foreground = foregroundBrush;
			StickRButtonXILabel.Opacity = opacity;
			StickRButtonXILabel.Foreground = foregroundBrush;
			StickRUpXILabel.Opacity = opacity;
			StickRUpXILabel.Foreground = foregroundBrush;
			StickRDownXILabel.Opacity = opacity;
			StickRDownXILabel.Foreground = foregroundBrush;
			StickRLeftXILabel.Opacity = opacity;
			StickRLeftXILabel.Foreground = foregroundBrush;
			StickRRightXILabel.Opacity = opacity;
			StickRRightXILabel.Foreground = foregroundBrush;

			// Set content to indicate unavailable state when not supported
			if (!showXILabels && ud?.InputMethod == Engine.InputMethod.XInput)
			{
				var unavailableText = "-";
				TriggerLXILabel.Content = unavailableText;
				TriggerRXILabel.Content = unavailableText;
				BumperLXILabel.Content = unavailableText;
				BumperRXILabel.Content = unavailableText;
				MenuBackXILabel.Content = unavailableText;
				MenuStartXILabel.Content = unavailableText;
				MenuGuideXILabel.Content = unavailableText;
				ActionAXILabel.Content = unavailableText;
				ActionBXILabel.Content = unavailableText;
				ActionXXILabel.Content = unavailableText;
				ActionYXILabel.Content = unavailableText;
				DPadXILabel.Content = unavailableText;
				DPadUpXILabel.Content = unavailableText;
				DPadDownXILabel.Content = unavailableText;
				DPadLeftXILabel.Content = unavailableText;
				DPadRightXILabel.Content = unavailableText;
				StickLAxisXXILabel.Content = unavailableText;
				StickLAxisYXILabel.Content = unavailableText;
				StickLButtonXILabel.Content = unavailableText;
				StickLUpXILabel.Content = unavailableText;
				StickLDownXILabel.Content = unavailableText;
				StickLLeftXILabel.Content = unavailableText;
				StickLRightXILabel.Content = unavailableText;
				StickRAxisXXILabel.Content = unavailableText;
				StickRAxisYXILabel.Content = unavailableText;
				StickRButtonXILabel.Content = unavailableText;
				StickRUpXILabel.Content = unavailableText;
				StickRDownXILabel.Content = unavailableText;
				StickRLeftXILabel.Content = unavailableText;
				StickRRightXILabel.Content = unavailableText;
			}
		}

		#endregion

        #region ■ Direct Input Menu

        // Drag and Drop related commented code is preserved.
        //List<MenuItem> DiMenuStrip = new List<MenuItem>();
        //string cRecord = "[Record]";
        //string cEmpty = "<empty>";
        //string cPOVs = "povs";

        // Function is recreated as soon as new DirectInput Device is available.
        //public void ResetDiMenuStrip2(UserDevice ud)
        //{
        //	DiMenuStrip.Clear();
        //	MenuItem mi;
        //	mi = new MenuItem() { Header = cEmpty };
        //	mi.Foreground = SystemColors.ControlDarkBrush;
        //	mi.Click += DiMenuStrip_Click;
        //	DiMenuStrip.Add(mi);
        //	// Return if direct input device is not available.
        //	if (ud == null)
        //		return;
        //	// Add [Record] label.
        //	mi = new MenuItem() { Header = cRecord };
        //	//mi.Icon = new ContentControl();
        //	mi.Click += DiMenuStrip_Click;
        //	DiMenuStrip.Add(mi);

        //	// Do not add menu items for keyboard, because user interface will become too sluggish.
        //	// Recording feature is preferred way to map keyboard label.
        //	if (!ud.IsKeyboard)
        //	{
        //		// Add Buttons.
        //		mi = new MenuItem() { Header = "Buttons" };
        //		DiMenuStrip.Add(mi);
        //		CreateItems(mi, "Inverted", "IButton {0}", "-{0}", ud.CapButtonCount);
        //		CreateItems(mi, "Button {0}", "{0}", ud.CapButtonCount);
        // Add Axes.
        //		if (ud.DiAxeMask > 0)
        //		{
        //			mi = new MenuItem() { Header = "Axes" };
        //			DiMenuStrip.Add(mi);
        //			CreateItems(mi, "Inverted", "IAxis {0}", "a-{0}", CustomDeviceState.MaxAxis, ud.DiAxeMask);
        //			CreateItems(mi, "Inverted Half", "IHAxis {0}", "x-{0}", CustomDeviceState.MaxAxis, ud.DiAxeMask);
        //			CreateItems(mi, "Half", "HAxis {0}", "x{0}", CustomDeviceState.MaxAxis, ud.DiAxeMask);
        //			CreateItems(mi, "Axis {0}", "a{0}", CustomDeviceState.MaxAxis, ud.DiAxeMask);
        //		}
        //		Add Sliders. 
        //		if (ud.DiSliderMask > 0)
        //		{
        //			mi = new MenuItem() { Header = "Sliders" };
        //			DiMenuStrip.Add(mi);
        //			// 2 x Sliders, 2 x AccelerationSliders, 2 x bDS.ForceSliders, 2 x VelocitySliders
        //			CreateItems(mi, "Inverted", "ISlider {0}", "s-{0}", CustomDeviceState.MaxSliders, ud.DiSliderMask);
        //			CreateItems(mi, "Inverted Half", "IHSlider {0}", "h-{0}", CustomDeviceState.MaxSliders, ud.DiSliderMask);
        //			CreateItems(mi, "Half", "HSlider {0}", "h{0}", CustomDeviceState.MaxSliders, ud.DiSliderMask);
        //			CreateItems(mi, "Slider {0}", "s{0}", CustomDeviceState.MaxSliders, ud.DiSliderMask);
        //		}
        //		// Add D-Pads.
        //		if (ud.CapPovCount > 0)
        //		{
        //			// Add povs.
        //			mi = new MenuItem() { Header = cPOVs };
        //			DiMenuStrip.Add(mi);
        //			// Add D-Pad Top, Right, Bottom, Left label.
        //			var dPadNames = Enum.GetNames(typeof(DPadEnum));
        //			for (int dInputPolylineStepSize = 0; dInputPolylineStepSize < ud.CapPovCount; dInputPolylineStepSize++)
        //			{
        //				var dPadItem = CreateItem("POV {0}", "{1}{0}", dInputPolylineStepSize + 1, SettingName.SType.POV);
        //				mi.Items.Add(dPadItem);
        //				for (int d = 0; d < dPadNames.Length; d++)
        //				{
        //					var dPadButtonIndex = dInputPolylineStepSize * 4 + d + 1;
        //					var dPadButtonItem = CreateItem("POV {0} {1}", "{2}{3}", dInputPolylineStepSize + 1, dPadNames[d], SettingName.SType.POVButton, dPadButtonIndex);
        //					dPadItem.Items.Add(dPadButtonItem);
        //				}
        //			}
        //		}
        //	}
        //}

        //void CreateItems(MenuItem parent, string subMenu, string text, string tag, int count, int? mask = null)
        //{
        //	var smi = new MenuItem() { Header = subMenu };
        //	parent.Items.Add(smi);
        //	CreateItems(smi, text, tag, count, mask);
        //}

        /// <summary>Create menu item.</summary>
        /// <param name="mask">Mask contains information if item is present.</param>
        //void CreateItems(MenuItem parent, string text, string tag, int count, int? mask = null)
        //{
        //	var items = new List<MenuItem>();
        //	for (int i = 0; i < count; i++)
        //	{
        //		// If mask specified and item is not present then...
        //		if (mask.HasValue && i < 32 && (((int)Math.Pow(2, i) & mask) == 0))
        //			continue;
        //		var item = CreateItem(text, tag, i + 1);
        //		items.Add(item);
        //	}
        //	foreach (var item in items)
        //		parent.Items.Add(item);
        //}

        //MenuItem CreateItem(string text, string tag, params object[] args)
        //{
        //	var item = new MenuItem();
        //	item.Header = string.Format(text, args);
        //	item.Tag = string.Format(tag, args);
        //	item.Padding = new Thickness(0);
        //	item.Margin = new Thickness(0);
        //	item.Click += DiMenuStrip_Click;
        //	return item;
        //}

        //void DiMenuStrip_Closed(object sender, ToolStripDropDownClosedEventArgs e)
        //{
        //	EnableDPadMenu(false);
        //}

        //public void EnableDPadMenu(bool enable)
        //{
        //	foreach (ToolStripMenuItem item in DiMenuStrip.Items)
        //	{
        //		if (!item.Text.StartsWith(cRecord)
        //			&& !item.Text.StartsWith(cEmpty)
        //			&& !item.Text.StartsWith(cPOVs))
        //		{
        //			item.Visible = !enable;
        //		}
        //		if (item.Text.StartsWith(cPOVs))
        //		{
        //			if (item.HasDropDownItems)
        //			{
        //				foreach (ToolStripMenuItem l1 in item.DropDownItems)
        //				{
        //					foreach (ToolStripMenuItem l2 in l1.DropDownItems)
        //						l2.Visible = !enable;
        //				}
        //			}
        //		}
        //	}
        //}

        #endregion

        //MenuItem LastItem;

        //private TextBox CurrentTextBox;

        //private void MenuItem_Click(object sender, RoutedEventArgs e)
        //{
        //	var mi = (MenuItem)sender;
        //	var smi = (MenuItem)e.Source;
        //	if (mi != smi)
        //		return;

        //	LastItem?.Items.Clear();
        //	LastItem = mi;
        //	foreach (var item in DiMenuStrip)
        //		mi.Items.Add(item);

        //	var control = (Menu)mi.Parent;
        //	CurrentTextBox = (TextBox)control.Tag;

        //	ControlsHelper.BeginInvoke(() =>
        //	{
        //		mi.IsSubmenuOpen = true;
        //	});

        //}

        //void DiMenuStrip_Click(object sender, RoutedEventArgs e)
        //{
        //	var item = (MenuItem)sender;
        //	var fullValue = (string)item.Header;
        //	// If this DPad parent menu.
        //	if (fullValue == cRecord)
        //	{
        //		//var map = SettingsManager.Current.SettingsMap.First(x => x.Control == CurrentCbx);
        //		//StartRecording(map);
        //	}
        //	else
        //	{
        //		CurrentTextBox.Text = fullValue == cEmpty
        //			? ""
        //			: fullValue;
        //	}
        //}

        private void RecordClear_MouseEnterStackPanel(object sender, MouseEventArgs e)
        {
			var child = ((StackPanel)sender).Children.OfType<TextBox>().FirstOrDefault();
			if (child != null) { RecordClear_MouseEnterTextBox(child, null); };
        }

        private void RecordClear_MouseLeaveStackPanel(object sender, MouseEventArgs e)
        {
            RCStackPanel.Visibility = Visibility.Collapsed;
        }

        private void RecordClear_MouseEnterTextBox(object sender, MouseEventArgs e)
		{
			// If it was already hosted somewhere else, remove it first,
			if (RCStackPanel.Parent is StackPanel s1) { s1.Children.Remove(RCStackPanel); }

			// Act on TextBoxes inside a StackPanel.
			if (sender is TextBox t2 && t2.Parent is StackPanel s2)
			{
				if (s2.HorizontalAlignment == HorizontalAlignment.Left)
				{
					RCStackPanel.FlowDirection = FlowDirection.LeftToRight;
					ClearButton.FlowDirection = FlowDirection.LeftToRight;
					// Calculate the insertion index = just before the last element.
					int insertIndex = Math.Max(0, s2.Children.Count - 1);
					s2.Children.Insert(insertIndex, RCStackPanel);
				}
				else
				{
					RCStackPanel.FlowDirection = FlowDirection.RightToLeft;
					ClearButton.FlowDirection = FlowDirection.LeftToRight;
					// Calculate the insertion index = just before the last element.
					s2.Children.Insert(1, RCStackPanel);
				}

				RecordButton.Tag = ClearButton.Tag = t2;
				ClearButton.Visibility = (t2.Text.Length > 0) ? Visibility.Visible : Visibility.Collapsed;
				RCStackPanel.Visibility = Visibility.Visible;
			}
		}

		private void ClearButton_Click(object sender, RoutedEventArgs e)
		{
			if ((sender as Button)?.Tag is TextBox tb)
			{
				tb.Text = "";
			}
		}

		TextBox recordTextBox;

		private void RecordButton_Click(object sender, RoutedEventArgs e)
		{
			if (recordTextBox == null)
			{
				// Get TextBox from sender Tag value and set it as recordTextBox. If recordTextBox is not null (recording state) it will be filled with first detected button or axis.
				recordTextBox = (sender as Button)?.Tag as TextBox;
				if (recordTextBox != null)
				{
					recordTextBox.BorderBrush = colorRecord;
					recordTextBox.Text = "";
				}
			}
			else
			{
				recordTextBox.BorderBrush = colorBackgroundDark;
				recordTextBox = null;
			}
		}

		private void RecordAxisOrButton(string axisOrButtonName)
		{
			recordTextBox.Text = axisOrButtonName;
			recordTextBox.BorderBrush = colorBackgroundDark;
			recordTextBox = null;
		}

		private void UserControl_Loaded(object sender, RoutedEventArgs e)
		{
			if (!ControlsHelper.AllowLoad(this))
				return;
		}

		private void UserControl_Unloaded(object sender, RoutedEventArgs e)
		{
			if (!ControlsHelper.AllowUnload(this))
				return;
			// Moved to MainBodyControl_Unloaded().
		}

		public void ParentWindow_Unloaded()
		{
			SetBinding(MapTo.None, null, null);
			// DiMenuStrip.Clear();
		}

		private void InvertButton_Click(object sender, RoutedEventArgs e)
		{
			if (sender is Button button && button.Tag is TextBox textBox)
			{
				// Force a re-evaluation of the button’s data trigger by reassigning its Tag.
				// This tricks the DataTrigger that binds to Tag.Text into refreshing immediately.
				button.Tag = null;
				button.Tag = textBox;

				if (!string.IsNullOrEmpty(textBox.Text))
				{
					textBox.Text = textBox.Text.StartsWith("I")
						? textBox.Text.Substring(1)
						: "I" + textBox.Text;
				}
			}
		}
	}
}
