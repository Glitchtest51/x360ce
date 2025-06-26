using JocysCom.ClassLibrary.IO;
using SharpDX.DirectInput;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Xml.Serialization;

namespace x360ce.Engine.Data
{
	public partial class UserDevice : IDisplayName, IUserRecord
	{

		public UserDevice()
		{
			DateCreated = DateTime.Now;
			DateUpdated = DateCreated;
			IsEnabled = true;
			ConnectionClass = Guid.Empty;
			// Set default input method to DirectInput for backward compatibility
			InputMethod = InputMethod.DirectInput;
		}

		[XmlIgnore]
		public string DisplayName
		{
			get
			{
				return string.Format("{0} - {1}", InstanceId, InstanceName);
			}
		}

		public void LoadInstance(DeviceInstance ins)
		{
			if (InstanceGuid != ins.InstanceGuid)
				InstanceGuid = ins.InstanceGuid;
			if (InstanceName != ins.InstanceName)
				InstanceName = ins.InstanceName;
			if (ProductGuid != ins.ProductGuid)
				ProductGuid = ins.ProductGuid;
			if (ProductName != ins.ProductName)
				ProductName = ins.ProductName;
		}

		public void LoadCapabilities(Capabilities cap)
		{
			// Check if value is same to reduce grid refresh.
			if (CapAxeCount != cap.AxeCount)
				CapAxeCount = cap.AxeCount;
			if (CapButtonCount != cap.ButtonCount)
				CapButtonCount = cap.ButtonCount;
			if (CapDriverVersion != cap.DriverVersion)
				CapDriverVersion = cap.DriverVersion;
			if (CapFirmwareRevision != cap.FirmwareRevision)
				CapFirmwareRevision = cap.FirmwareRevision;
			if (CapFlags != (int)cap.Flags)
				CapFlags = (int)cap.Flags;
			if (CapForceFeedbackMinimumTimeResolution != cap.ForceFeedbackMinimumTimeResolution)
				CapForceFeedbackMinimumTimeResolution = cap.ForceFeedbackMinimumTimeResolution;
			if (CapForceFeedbackSamplePeriod != cap.ForceFeedbackSamplePeriod)
				CapForceFeedbackSamplePeriod = cap.ForceFeedbackSamplePeriod;
			if (CapHardwareRevision != cap.HardwareRevision)
				CapHardwareRevision = cap.HardwareRevision;
			if (CapPovCount != cap.PovCount)
				CapPovCount = cap.PovCount;
			if (CapIsHumanInterfaceDevice != cap.IsHumanInterfaceDevice)
				CapIsHumanInterfaceDevice = cap.IsHumanInterfaceDevice;
			if (CapSubtype != cap.Subtype)
				CapSubtype = cap.Subtype;
			if (CapType != (int)cap.Type)
				CapType = (int)cap.Type;
		}

		public void LoadDevDeviceInfo(DeviceInfo info)
		{
			if (info == null)
			{
				DevManufacturer = "";
				DevVendorId = 0;
				DevProductId = 0;
				DevRevision = 0;
				DevDescription = "";
				DevDeviceId = "";
				DevHardwareIds = "";
				DevDevicePath = "";
				DevParentDeviceId = "";
				DevClassGuid = Guid.Empty;
				DevClassDescription = "";
			}
			else
			{
				// Check if value is same to reduce grid refresh.
				if (DevManufacturer != info.Manufacturer)
					DevManufacturer = info.Manufacturer;
				if (DevVendorId != (int)info.VendorId)
					DevVendorId = (int)info.VendorId;
				if (DevProductId != (int)info.ProductId)
					DevProductId = (int)info.ProductId;
				if (DevRevision != (int)info.Revision)
					DevRevision = (int)info.Revision;
				if (DevDescription != info.Description)
					DevDescription = info.Description;
				if (DevDeviceId != info.DeviceId)
					DevDeviceId = info.DeviceId;
				if (DevHardwareIds != info.HardwareIds)
					DevHardwareIds = info.HardwareIds;
				if (DevDevicePath != info.DevicePath)
					DevDevicePath = info.DevicePath;
				if (DevParentDeviceId != info.ParentDeviceId)
					DevParentDeviceId = info.ParentDeviceId;
				if (DevClassGuid != info.ClassGuid)
					DevClassGuid = info.ClassGuid;
				if (DevClassDescription != info.ClassDescription)
					DevClassDescription = info.ClassDescription;
			}
		}

		public void LoadHidDeviceInfo(DeviceInfo info)
		{
			if (info == null)
			{
				HidManufacturer = "";
				HidVendorId = 0;
				HidProductId = 0;
				HidRevision = 0;
				HidDescription = "";
				HidDeviceId = "";
				HidHardwareIds = "";
				HidDevicePath = "";
				HidParentDeviceId = "";
				HidClassGuid = Guid.Empty;
				HidClassDescription = "";
			}
			else
			{
				// Check if value is same to reduce grid refresh.
				if (HidManufacturer != info.Manufacturer)
					HidManufacturer = info.Manufacturer;
				if (HidVendorId != (int)info.VendorId)
					HidVendorId = (int)info.VendorId;
				if (HidProductId != (int)info.ProductId)
					HidProductId = (int)info.ProductId;
				if (HidRevision != (int)info.Revision)
					HidRevision = (int)info.Revision;
				if (HidDescription != info.Description)
					HidDescription = info.Description;
				if (HidDeviceId != info.DeviceId)
					HidDeviceId = info.DeviceId;
				if (HidHardwareIds != info.HardwareIds)
					HidHardwareIds = info.HardwareIds;
				if (HidDevicePath != info.DevicePath)
					HidDevicePath = info.DevicePath;
				if (HidParentDeviceId != info.ParentDeviceId)
					HidParentDeviceId = info.ParentDeviceId;
				if (HidClassGuid != info.ClassGuid)
					HidClassGuid = info.ClassGuid;
				if (HidClassDescription != info.ClassDescription)
					HidClassDescription = info.ClassDescription;
			}
		}

		#region Ignored properties used by application to store various device states.

		[XmlIgnore, NonSerialized]
		public bool DeviceChanged;

		/// <summary>DInput Device State.</summary>
		[XmlIgnore, NonSerialized]
		public Device Device;

		[XmlIgnore, NonSerialized]
		public DeviceObjectItem[] DeviceObjects;

		[XmlIgnore, NonSerialized]
		public DeviceEffectItem[] DeviceEffects;

		/// <summary>DInput JoystickState State.</summary>
		[XmlIgnore, NonSerialized]
		public object DeviceState;

		/// <summary>X360CE custom DirectInput state used for configuration.</summary>
		[XmlIgnore, NonSerialized]
		public CustomDeviceState DiState;

		[XmlIgnore, NonSerialized]
		public CustomDeviceUpdate[] DiUpdates;

		[XmlIgnore, NonSerialized]
		public long DiStateTime;

		[XmlIgnore, NonSerialized]
		public CustomDeviceState OldDiState;

		[XmlIgnore, NonSerialized]
		public CustomDeviceUpdate[] OldDiUpdates;

		[XmlIgnore, NonSerialized]
		public long OldDiStateTime;

		[XmlIgnore, NonSerialized]
		public CustomDeviceState OrgDiState;

		[XmlIgnore, NonSerialized]
		public long OrgDiStateTime;

		[XmlIgnore, NonSerialized]
		public ForceFeedbackState FFState;

		[XmlIgnore, NonSerialized]
		public bool? IsExclusiveMode;

		[XmlIgnore, NonSerialized]
		public string DevHardwareIds;

		[XmlIgnore, NonSerialized]
		public string HidHardwareIds;

		[XmlIgnore]
		public bool IsOnline
		{
			get { return _IsOnline; }
			set { _IsOnline = value; ReportPropertyChanged(x => x.IsOnline); }
		}
		bool _IsOnline;

		/// <summary>
		/// Gets or sets the input method used to read controller data from this device.
		/// Determines which API (DirectInput, XInput, Gaming Input, Raw Input) is used for input processing.
		/// </summary>
		/// <remarks>
		/// Each input method has specific capabilities and limitations:
		/// • DirectInput: All controllers, but Xbox controllers lose background access on Win10+
		/// • XInput: Xbox controllers only, max 4, works in background
		/// • Gaming Input: Windows 10+ only, no background access, best Xbox support
		/// • Raw Input: All controllers, works in background, complex setup
		/// 
		/// Default value is DirectInput for backward compatibility.
		/// User must manually select appropriate method based on their needs.
		/// </remarks>
		public InputMethod InputMethod
		{
			get { return _InputMethod; }
			set { _InputMethod = value; ReportPropertyChanged(x => x.InputMethod); }
		}
		InputMethod _InputMethod;

		[XmlIgnore]
		public string InstanceId
		{
			get
			{
				return EngineHelper.GetID(InstanceGuid);
			}
		}

		[XmlIgnore]
		public bool IsMouse => CapType == (int)SharpDX.DirectInput.DeviceType.Mouse;

		[XmlIgnore]
		public bool IsKeyboard => CapType == (int)SharpDX.DirectInput.DeviceType.Keyboard;

		[XmlIgnore]
		public bool AllowHide
		{
			get
			{
				return
					!IsKeyboard &&
					!IsMouse &&
					//ConnectionClass != JocysCom.ClassLibrary.Win32.DEVCLASS.SYSTEM &&
					// Device Id must be set.
					!string.IsNullOrEmpty(HidDeviceId);
			}
		}

		/// <summary>
		/// Gets whether this device is an Xbox-compatible controller that supports XInput.
		/// </summary>
		[XmlIgnore]
		public bool IsXboxCompatible
		{
			get
			{
				// Check for common Xbox controller identifiers
				var productName = ProductName?.ToLowerInvariant() ?? "";
				var instanceName = InstanceName?.ToLowerInvariant() ?? "";
				
				return productName.Contains("xbox") ||
					   productName.Contains("x360") ||
					   productName.Contains("gamepad") ||
					   instanceName.Contains("xbox") ||
					   instanceName.Contains("x360") ||
					   // Check VID/PID for known Xbox controllers
					   IsKnownXboxVidPid();
			}
		}

		/// <summary>
		/// Checks if the device has known Xbox controller VID/PID combinations.
		/// </summary>
		private bool IsKnownXboxVidPid()
		{
			// Microsoft VID is 0x045E
			if (DevVendorId == 0x045E || HidVendorId == 0x045E)
			{
				// Common Xbox controller PIDs
				var xboxPids = new int[]
				{
					0x028E, // Xbox 360 Controller
					0x028F, // Xbox 360 Wireless Controller
					0x02D1, // Xbox One Controller
					0x02DD, // Xbox One Controller (Firmware 2015)
					0x02E3, // Xbox One Elite Controller
					0x02EA, // Xbox One S Controller
					0x02FD, // Xbox One S Controller (Bluetooth)
					0x0719, // Xbox 360 Wireless Adapter
				};

				var devicePid = DevProductId != 0 ? DevProductId : HidProductId;
				return xboxPids.Contains(devicePid);
			}
			return false;
		}


		#endregion

		#region INotifyPropertyChanged

		/// <summary>
		/// Use: ReportPropertyChanged(x => x.PropertyName);
		/// </summary>
		void ReportPropertyChanged(Expression<Func<UserDevice, object>> selector)
		{
			var body = (MemberExpression)((UnaryExpression)selector.Body).Operand;
			var name = body.Member.Name;
			ReportPropertyChanged(name);
		}

		#endregion
	}
}
