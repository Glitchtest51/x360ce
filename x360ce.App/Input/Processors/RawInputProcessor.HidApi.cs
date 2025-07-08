using System;
using System.Runtime.InteropServices;

namespace x360ce.App.Input.Processors
{
	/// <summary>
	/// Windows HID API declarations and structures for Raw Input processing.
	/// This partial class contains all Windows API interop for HID functionality.
	/// Follows HID Usage Tables v1.3 specification.
	/// </summary>
	public partial class RawInputProcessor
	{
		#region Windows HID API Functions

		[DllImport("hid.dll", SetLastError = true)]
		private static extern int HidP_GetCaps(IntPtr PreparsedData, out HIDP_CAPS Capabilities);

		[DllImport("hid.dll", SetLastError = true)]
		private static extern int HidP_GetButtonCaps(HIDP_REPORT_TYPE ReportType, [Out] HIDP_BUTTON_CAPS[] ButtonCaps, ref int ButtonCapsLength, IntPtr PreparsedData);

		[DllImport("hid.dll", SetLastError = true)]
		private static extern int HidP_GetValueCaps(HIDP_REPORT_TYPE ReportType, [Out] HIDP_VALUE_CAPS[] ValueCaps, ref int ValueCapsLength, IntPtr PreparsedData);

		[DllImport("hid.dll", SetLastError = true)]
		private static extern int HidP_GetUsages(HIDP_REPORT_TYPE ReportType, ushort UsagePage, ushort LinkCollection, [Out] ushort[] UsageList, ref int UsageLength, IntPtr PreparsedData, IntPtr Report, int ReportLength);

		[DllImport("hid.dll", SetLastError = true)]
		private static extern int HidP_GetUsageValue(HIDP_REPORT_TYPE ReportType, ushort UsagePage, ushort LinkCollection, ushort Usage, out int UsageValue, IntPtr PreparsedData, IntPtr Report, int ReportLength);

		#endregion

		#region HID Constants

		// HID Report Types
		private enum HIDP_REPORT_TYPE : ushort
		{
			HidP_Input = 0,
			HidP_Output = 1,
			HidP_Feature = 2
		}

		// HID Status Codes
		private const int HIDP_STATUS_SUCCESS = unchecked((int)0x110000);
		private const int HIDP_STATUS_NULL = unchecked((int)0x80110001);
		private const int HIDP_STATUS_INVALID_PREPARSED_DATA = unchecked((int)0xC0110001);
		private const int HIDP_STATUS_INVALID_REPORT_TYPE = unchecked((int)0xC0110002);
		private const int HIDP_STATUS_INVALID_REPORT_LENGTH = unchecked((int)0xC0110003);
		private const int HIDP_STATUS_USAGE_NOT_FOUND = unchecked((int)0xC0110004);
		private const int HIDP_STATUS_BUFFER_TOO_SMALL = unchecked((int)0xC0110007);

		// HID Usage Pages (from HID Usage Tables specification v1.3)
		private const ushort HID_USAGE_PAGE_GENERIC_DESKTOP = 0x01;
		private const ushort HID_USAGE_PAGE_SIMULATION = 0x02;
		private const ushort HID_USAGE_PAGE_VR = 0x03;
		private const ushort HID_USAGE_PAGE_SPORT = 0x04;
		private const ushort HID_USAGE_PAGE_GAME = 0x05;
		private const ushort HID_USAGE_PAGE_GENERIC_DEVICE = 0x06;
		private const ushort HID_USAGE_PAGE_KEYBOARD = 0x07;
		private const ushort HID_USAGE_PAGE_LED = 0x08;
		private const ushort HID_USAGE_PAGE_BUTTON = 0x09;
		private const ushort HID_USAGE_PAGE_ORDINAL = 0x0A;
		private const ushort HID_USAGE_PAGE_TELEPHONY = 0x0B;
		private const ushort HID_USAGE_PAGE_CONSUMER = 0x0C;

		// Generic Desktop Usage IDs (HID Usage Tables v1.3, page 28)
		private const ushort HID_USAGE_GENERIC_POINTER = 0x01;
		private const ushort HID_USAGE_GENERIC_MOUSE = 0x02;
		private const ushort HID_USAGE_GENERIC_JOYSTICK = 0x04;
		private const ushort HID_USAGE_GENERIC_GAMEPAD = 0x05;
		private const ushort HID_USAGE_GENERIC_KEYBOARD = 0x06;
		private const ushort HID_USAGE_GENERIC_KEYPAD = 0x07;
		private const ushort HID_USAGE_GENERIC_MULTI_AXIS = 0x08;
		private const ushort HID_USAGE_GENERIC_TABLET_PC = 0x09;

		// Generic Desktop Axis Usage IDs (HID Usage Tables v1.3, page 29)
		private const ushort HID_USAGE_GENERIC_X = 0x30;
		private const ushort HID_USAGE_GENERIC_Y = 0x31;
		private const ushort HID_USAGE_GENERIC_Z = 0x32;
		private const ushort HID_USAGE_GENERIC_RX = 0x33;
		private const ushort HID_USAGE_GENERIC_RY = 0x34;
		private const ushort HID_USAGE_GENERIC_RZ = 0x35;
		private const ushort HID_USAGE_GENERIC_SLIDER = 0x36;
		private const ushort HID_USAGE_GENERIC_DIAL = 0x37;
		private const ushort HID_USAGE_GENERIC_WHEEL = 0x38;
		private const ushort HID_USAGE_GENERIC_HATSWITCH = 0x39;
		private const ushort HID_USAGE_GENERIC_COUNTED_BUFFER = 0x3A;
		private const ushort HID_USAGE_GENERIC_BYTE_COUNT = 0x3B;
		private const ushort HID_USAGE_GENERIC_MOTION_WAKEUP = 0x3C;
		private const ushort HID_USAGE_GENERIC_START = 0x3D;
		private const ushort HID_USAGE_GENERIC_SELECT = 0x3E;

		// Generic Desktop Vector Usage IDs (HID Usage Tables v1.3, page 30)
		private const ushort HID_USAGE_GENERIC_VX = 0x40;
		private const ushort HID_USAGE_GENERIC_VY = 0x41;
		private const ushort HID_USAGE_GENERIC_VZ = 0x42;
		private const ushort HID_USAGE_GENERIC_VBRX = 0x43;
		private const ushort HID_USAGE_GENERIC_VBRY = 0x44;
		private const ushort HID_USAGE_GENERIC_VBRZ = 0x45;
		private const ushort HID_USAGE_GENERIC_VNO = 0x46;

		// Game Controls Usage IDs (HID Usage Tables v1.3, page 51)
		private const ushort HID_USAGE_GAME_3D_GAME_CONTROLLER = 0x01;
		private const ushort HID_USAGE_GAME_PINBALL_DEVICE = 0x02;
		private const ushort HID_USAGE_GAME_GUN_DEVICE = 0x03;
		private const ushort HID_USAGE_GAME_POINT_OF_VIEW = 0x20;
		private const ushort HID_USAGE_GAME_TURN_RIGHT_LEFT = 0x21;
		private const ushort HID_USAGE_GAME_PITCH_FORWARD_BACKWARD = 0x22;
		private const ushort HID_USAGE_GAME_ROLL_RIGHT_LEFT = 0x23;
		private const ushort HID_USAGE_GAME_MOVE_RIGHT_LEFT = 0x24;
		private const ushort HID_USAGE_GAME_MOVE_FORWARD_BACKWARD = 0x25;
		private const ushort HID_USAGE_GAME_MOVE_UP_DOWN = 0x26;
		private const ushort HID_USAGE_GAME_LEAN_RIGHT_LEFT = 0x27;
		private const ushort HID_USAGE_GAME_LEAN_FORWARD_BACKWARD = 0x28;
		private const ushort HID_USAGE_GAME_HEIGHT_OF_POV = 0x29;
		private const ushort HID_USAGE_GAME_FLIPPER = 0x2A;
		private const ushort HID_USAGE_GAME_SECONDARY_FLIPPER = 0x2B;
		private const ushort HID_USAGE_GAME_BUMP = 0x2C;
		private const ushort HID_USAGE_GAME_NEW_GAME = 0x2D;
		private const ushort HID_USAGE_GAME_SHOOT_BALL = 0x2E;
		private const ushort HID_USAGE_GAME_PLAYER = 0x2F;
		private const ushort HID_USAGE_GAME_GUN_BOLT = 0x30;
		private const ushort HID_USAGE_GAME_GUN_CLIP = 0x31;
		private const ushort HID_USAGE_GAME_GUN_SELECTOR = 0x32;
		private const ushort HID_USAGE_GAME_GUN_SINGLE_SHOT = 0x33;
		private const ushort HID_USAGE_GAME_GUN_BURST = 0x34;
		private const ushort HID_USAGE_GAME_GUN_AUTOMATIC = 0x35;
		private const ushort HID_USAGE_GAME_GUN_SAFETY = 0x36;

		#endregion

		#region HID Structures

		[StructLayout(LayoutKind.Sequential)]
		public struct HIDP_CAPS
		{
			public ushort Usage;
			public ushort UsagePage;
			public ushort InputReportByteLength;
			public ushort OutputReportByteLength;
			public ushort FeatureReportByteLength;
			[MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
			public ushort[] Reserved;
			public ushort NumberLinkCollectionNodes;
			public ushort NumberInputButtonCaps;
			public ushort NumberInputValueCaps;
			public ushort NumberInputDataIndices;
			public ushort NumberOutputButtonCaps;
			public ushort NumberOutputValueCaps;
			public ushort NumberOutputDataIndices;
			public ushort NumberFeatureButtonCaps;
			public ushort NumberFeatureValueCaps;
			public ushort NumberFeatureDataIndices;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct HIDP_BUTTON_CAPS
		{
			public ushort UsagePage;
			public byte ReportID;
			public byte IsAlias;
			public ushort BitField;
			public ushort LinkCollection;
			public ushort LinkUsage;
			public ushort LinkUsagePage;
			public byte IsRange;
			public byte IsStringRange;
			public byte IsDesignatorRange;
			public byte IsAbsolute;
			[MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
			public uint[] Reserved;
			public HIDP_BUTTON_CAPS_UNION Union;
		}

		[StructLayout(LayoutKind.Explicit)]
		private struct HIDP_BUTTON_CAPS_UNION
		{
			[FieldOffset(0)]
			public HIDP_BUTTON_CAPS_RANGE Range;
			[FieldOffset(0)]
			public HIDP_BUTTON_CAPS_NOT_RANGE NotRange;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct HIDP_BUTTON_CAPS_RANGE
		{
			public ushort UsageMin;
			public ushort UsageMax;
			public ushort StringMin;
			public ushort StringMax;
			public ushort DesignatorMin;
			public ushort DesignatorMax;
			public ushort DataIndexMin;
			public ushort DataIndexMax;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct HIDP_BUTTON_CAPS_NOT_RANGE
		{
			public ushort Usage;
			public ushort Reserved1;
			public ushort StringIndex;
			public ushort Reserved2;
			public ushort DesignatorIndex;
			public ushort Reserved3;
			public ushort DataIndex;
			public ushort Reserved4;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct HIDP_VALUE_CAPS
		{
			public ushort UsagePage;
			public byte ReportID;
			public byte IsAlias;
			public ushort BitField;
			public ushort LinkCollection;
			public ushort LinkUsage;
			public ushort LinkUsagePage;
			public byte IsRange;
			public byte IsStringRange;
			public byte IsDesignatorRange;
			public byte IsAbsolute;
			public byte HasNull;
			public byte Reserved;
			public ushort BitSize;
			public ushort ReportCount;
			[MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
			public ushort[] Reserved2;
			public uint UnitsExp;
			public uint Units;
			public int LogicalMin;
			public int LogicalMax;
			public int PhysicalMin;
			public int PhysicalMax;
			public HIDP_VALUE_CAPS_UNION Union;
		}

		[StructLayout(LayoutKind.Explicit)]
		private struct HIDP_VALUE_CAPS_UNION
		{
			[FieldOffset(0)]
			public HIDP_VALUE_CAPS_RANGE Range;
			[FieldOffset(0)]
			public HIDP_VALUE_CAPS_NOT_RANGE NotRange;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct HIDP_VALUE_CAPS_RANGE
		{
			public ushort UsageMin;
			public ushort UsageMax;
			public ushort StringMin;
			public ushort StringMax;
			public ushort DesignatorMin;
			public ushort DesignatorMax;
			public ushort DataIndexMin;
			public ushort DataIndexMax;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct HIDP_VALUE_CAPS_NOT_RANGE
		{
			public ushort Usage;
			public ushort Reserved1;
			public ushort StringIndex;
			public ushort Reserved2;
			public ushort DesignatorIndex;
			public ushort Reserved3;
			public ushort DataIndex;
			public ushort Reserved4;
		}

		#endregion
	}
}