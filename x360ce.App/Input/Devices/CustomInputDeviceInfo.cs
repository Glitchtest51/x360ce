using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using x360ce.App.Input.States;

namespace x360ce.App.Input.Devices
{
	/// <summary>
	/// Custom device information structure containing properties common to all input types.
	/// Implements INotifyPropertyChanged to enable DataGrid reactive updates.
	/// Wraps an underlying IInputDeviceInfo to provide reference-based property access.
	/// </summary>
	public class CustomInputDeviceInfo : INotifyPropertyChanged
	{
		private InputDeviceInfo _inputDeviceInfo;

		public CustomInputDeviceInfo(InputDeviceInfo device)
		{
			_inputDeviceInfo = device ?? throw new ArgumentNullException(nameof(device));
		}

		public void SetDevice(InputDeviceInfo device)
		{
			if (_inputDeviceInfo != device)
			{
				_inputDeviceInfo = device ?? throw new ArgumentNullException(nameof(device));
				// Notify all properties changed since the underlying source changed
				OnPropertyChanged(null);
			}
		}

		public string InputType
		{
			get => _inputDeviceInfo.InputType;
			set { if (_inputDeviceInfo.InputType != value) { _inputDeviceInfo.InputType = value; OnPropertyChanged(); } }
		}

		public string CommonIdentifier
		{
			get => _inputDeviceInfo.CommonIdentifier;
			set { if (_inputDeviceInfo.CommonIdentifier != value) { _inputDeviceInfo.CommonIdentifier = value; OnPropertyChanged(); } }
		}

		public Guid InstanceGuid
		{
			get => _inputDeviceInfo.InstanceGuid;
			set { if (_inputDeviceInfo.InstanceGuid != value) { _inputDeviceInfo.InstanceGuid = value; OnPropertyChanged(); } }
		}

		public int AxeCount
		{
			get => _inputDeviceInfo.AxeCount;
			set { if (_inputDeviceInfo.AxeCount != value) { _inputDeviceInfo.AxeCount = value; OnPropertyChanged(); } }
		}

		public int SliderCount
		{
			get => _inputDeviceInfo.SliderCount;
			set { if (_inputDeviceInfo.SliderCount != value) { _inputDeviceInfo.SliderCount = value; OnPropertyChanged(); } }
		}

		public int ButtonCount
		{
			get => _inputDeviceInfo.ButtonCount;
			set { if (_inputDeviceInfo.ButtonCount != value) { _inputDeviceInfo.ButtonCount = value; OnPropertyChanged(); } }
		}

		public int PovCount
		{
			get => _inputDeviceInfo.PovCount;
			set { if (_inputDeviceInfo.PovCount != value) { _inputDeviceInfo.PovCount = value; OnPropertyChanged(); } }
		}

		public CustomInputState CustomInputState
		{
			get => _inputDeviceInfo.CustomInputState;
			set { if (_inputDeviceInfo.CustomInputState != value) { _inputDeviceInfo.CustomInputState = value; OnPropertyChanged(); } }
		}

		private string _displayName;

		public string ProductName
		{
			get => _displayName ?? _inputDeviceInfo.ProductName;
			set
			{
				if (_displayName != value)
				{
					_displayName = value;
					OnPropertyChanged();
				}
			}
		}

		public string InterfacePath
		{
			get => _inputDeviceInfo.InterfacePath;
			set { if (_inputDeviceInfo.InterfacePath != value) { _inputDeviceInfo.InterfacePath = value; OnPropertyChanged(); } }
		}

		public bool IsEnabled
		{
			get => _inputDeviceInfo.IsEnabled;
			set { if (_inputDeviceInfo.IsEnabled != value) { _inputDeviceInfo.IsEnabled = value; OnPropertyChanged(); } }
		}

		public string InstanceName
		{
			get => _inputDeviceInfo.InstanceName;
			set { if (_inputDeviceInfo.InstanceName != value) { _inputDeviceInfo.InstanceName = value; OnPropertyChanged(); } }
		}

		public Guid ProductGuid
		{
			get => _inputDeviceInfo.ProductGuid;
			set { if (_inputDeviceInfo.ProductGuid != value) { _inputDeviceInfo.ProductGuid = value; OnPropertyChanged(); } }
		}

		public int DeviceType
		{
			get => _inputDeviceInfo.DeviceType;
			set { if (_inputDeviceInfo.DeviceType != value) { _inputDeviceInfo.DeviceType = value; OnPropertyChanged(); } }
		}

		public int DeviceSubtype
		{
			get => _inputDeviceInfo.DeviceSubtype;
			set { if (_inputDeviceInfo.DeviceSubtype != value) { _inputDeviceInfo.DeviceSubtype = value; OnPropertyChanged(); } }
		}

		public string DeviceTypeName
		{
			get => _inputDeviceInfo.DeviceTypeName;
			set { if (_inputDeviceInfo.DeviceTypeName != value) { _inputDeviceInfo.DeviceTypeName = value; OnPropertyChanged(); } }
		}

		public int Usage
		{
			get => _inputDeviceInfo.Usage;
			set { if (_inputDeviceInfo.Usage != value) { _inputDeviceInfo.Usage = value; OnPropertyChanged(); } }
		}

		public int UsagePage
		{
			get => _inputDeviceInfo.UsagePage;
			set { if (_inputDeviceInfo.UsagePage != value) { _inputDeviceInfo.UsagePage = value; OnPropertyChanged(); } }
		}

		public bool HasForceFeedback
		{
			get => _inputDeviceInfo.HasForceFeedback;
			set { if (_inputDeviceInfo.HasForceFeedback != value) { _inputDeviceInfo.HasForceFeedback = value; OnPropertyChanged(); } }
		}

		public int VendorId
		{
			get => _inputDeviceInfo.VendorId;
			set { if (_inputDeviceInfo.VendorId != value) { _inputDeviceInfo.VendorId = value; OnPropertyChanged(); } }
		}

		public int ProductId
		{
			get => _inputDeviceInfo.ProductId;
			set { if (_inputDeviceInfo.ProductId != value) { _inputDeviceInfo.ProductId = value; OnPropertyChanged(); } }
		}

		public int DriverVersion
		{
			get => _inputDeviceInfo.DriverVersion;
			set { if (_inputDeviceInfo.DriverVersion != value) { _inputDeviceInfo.DriverVersion = value; OnPropertyChanged(); } }
		}

		public int HardwareRevision
		{
			get => _inputDeviceInfo.HardwareRevision;
			set { if (_inputDeviceInfo.HardwareRevision != value) { _inputDeviceInfo.HardwareRevision = value; OnPropertyChanged(); } }
		}

		public int FirmwareRevision
		{
			get => _inputDeviceInfo.FirmwareRevision;
			set { if (_inputDeviceInfo.FirmwareRevision != value) { _inputDeviceInfo.FirmwareRevision = value; OnPropertyChanged(); } }
		}

		public bool IsOnline
		{
			get => _inputDeviceInfo.IsOnline;
			set { if (_inputDeviceInfo.IsOnline != value) { _inputDeviceInfo.IsOnline = value; OnPropertyChanged(); } }
		}

		public Guid ClassGuid
		{
			get => _inputDeviceInfo.ClassGuid;
			set { if (_inputDeviceInfo.ClassGuid != value) { _inputDeviceInfo.ClassGuid = value; OnPropertyChanged(); } }
		}

		public string HardwareIds
		{
			get => _inputDeviceInfo.HardwareIds;
			set { if (_inputDeviceInfo.HardwareIds != value) { _inputDeviceInfo.HardwareIds = value; OnPropertyChanged(); } }
		}

		public string DeviceId
		{
			get => _inputDeviceInfo.DeviceId;
			set { if (_inputDeviceInfo.DeviceId != value) { _inputDeviceInfo.DeviceId = value; OnPropertyChanged(); } }
		}

		public string ParentDeviceId
		{
			get => _inputDeviceInfo.ParentDeviceId;
			set { if (_inputDeviceInfo.ParentDeviceId != value) { _inputDeviceInfo.ParentDeviceId = value; OnPropertyChanged(); } }
		}

		public string VidPidString => _inputDeviceInfo.VidPidString;

		public bool AssignedToPad1
		{
			get => _inputDeviceInfo.AssignedToPad1;
			set { if (_inputDeviceInfo.AssignedToPad1 != value) { _inputDeviceInfo.AssignedToPad1 = value; OnPropertyChanged(); } }
		}

		public bool AssignedToPad2
		{
			get => _inputDeviceInfo.AssignedToPad2;
			set { if (_inputDeviceInfo.AssignedToPad2 != value) { _inputDeviceInfo.AssignedToPad2 = value; OnPropertyChanged(); } }
		}

		public bool AssignedToPad3
		{
			get => _inputDeviceInfo.AssignedToPad3;
			set { if (_inputDeviceInfo.AssignedToPad3 != value) { _inputDeviceInfo.AssignedToPad3 = value; OnPropertyChanged(); } }
		}

		public bool AssignedToPad4
		{
			get => _inputDeviceInfo.AssignedToPad4;
			set { if (_inputDeviceInfo.AssignedToPad4 != value) { _inputDeviceInfo.AssignedToPad4 = value; OnPropertyChanged(); } }
		}

		private bool _axePressed;
		private bool _sliderPressed;
		private bool _buttonPressed;
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
		/// Gets or sets whether any button or key is currently pressed.
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
