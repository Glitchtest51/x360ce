using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace x360ce.App.Input.Devices
{
	/// <summary>
	/// Unified device information structure containing properties common to all input types.
	/// Implements INotifyPropertyChanged to enable DataGrid reactive updates.
	/// </summary>
	public class UnifiedInputDeviceInfo : INotifyPropertyChanged
	{
		public string InputType { get; set; }
		public string CommonIdentifier { get; set; }
		public int AxeCount { get; set; }
		public int SliderCount { get; set; }
		public int ButtonCount { get; set; }
		public int KeyCount { get; set; }
		public int PovCount { get; set; }
		public string ProductName { get; set; }
		public string InterfacePath { get; set; }

		private bool _axePressed;
		private bool _sliderPressed;
		private bool _buttonPressed;
		private bool _keyPressed;
		private bool _povPressed;

		/// <summary>
		/// Gets or sets whether any axis is currently pressed/moved.
		/// </summary>
		public bool AxePressed
		{
			get => _axePressed;
			set
			{
				if (_axePressed != value)
				{
					_axePressed = value;
					OnPropertyChanged();
				}
			}
		}

		/// <summary>
		/// Gets or sets whether any slider is currently pressed/moved.
		/// </summary>
		public bool SliderPressed
		{
			get => _sliderPressed;
			set
			{
				if (_sliderPressed != value)
				{
					_sliderPressed = value;
					OnPropertyChanged();
				}
			}
		}

		/// <summary>
		/// Gets or sets whether any button is currently pressed.
		/// </summary>
		public bool ButtonPressed
		{
			get => _buttonPressed;
			set
			{
				if (_buttonPressed != value)
				{
					_buttonPressed = value;
					OnPropertyChanged();
				}
			}
		}

		/// <summary>
		/// Gets or sets whether any key is currently pressed.
		/// </summary>
		public bool KeyPressed
		{
			get => _keyPressed;
			set
			{
				if (_keyPressed != value)
				{
					_keyPressed = value;
					OnPropertyChanged();
				}
			}
		}

		/// <summary>
		/// Gets or sets whether any POV (point of view) control is currently pressed.
		/// </summary>
		public bool PovPressed
		{
			get => _povPressed;
			set
			{
				if (_povPressed != value)
				{
					_povPressed = value;
					OnPropertyChanged();
				}
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		/// <summary>
		/// Raises the PropertyChanged event for data binding updates.
		/// </summary>
		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}