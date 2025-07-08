using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Diagnostics;
using x360ce.Engine;
using x360ce.Engine.Data;

namespace x360ce.App.Input.Processors
{
	/// <summary>
	/// Real-time polling support for RawInputProcessor.
	/// This partial class implements direct HID device polling to eliminate
	/// the 2-3 second delay caused by Windows message queue buffering.
	/// </summary>
	public partial class RawInputProcessor
	{
		#region HID Device Management for Real-Time Polling

		// Cache of opened HID device handles for real-time polling
		private Dictionary<Guid, IntPtr> _hidDeviceHandles = new Dictionary<Guid, IntPtr>();

		[DllImport("hid.dll", SetLastError = true)]
		private static extern bool HidD_GetInputReport(IntPtr HidDeviceObject, IntPtr ReportBuffer, uint ReportBufferLength);

		[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
		private static extern IntPtr CreateFile(
			string lpFileName,
			uint dwDesiredAccess,
			uint dwShareMode,
			IntPtr lpSecurityAttributes,
			uint dwCreationDisposition,
			uint dwFlagsAndAttributes,
			IntPtr hTemplateFile);

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern bool CloseHandle(IntPtr hObject);

		private const uint GENERIC_READ = 0x80000000;
		private const uint FILE_SHARE_READ = 0x00000001;
		private const uint FILE_SHARE_WRITE = 0x00000002;
		private const uint OPEN_EXISTING = 3;

		/// <summary>
		/// Opens HID device handle for real-time polling
		/// </summary>
		private IntPtr OpenHidDeviceForPolling(UserDevice device)
		{
			// Check cache first
			if (_hidDeviceHandles.TryGetValue(device.InstanceGuid, out var cachedHandle))
			{
				return cachedHandle;
			}

			// Open new HID device handle
			if (!string.IsNullOrEmpty(device.HidDeviceId))
			{
				var handle = CreateFile(
					device.HidDeviceId,
					GENERIC_READ,
					FILE_SHARE_READ | FILE_SHARE_WRITE,
					IntPtr.Zero,
					OPEN_EXISTING,
					0,
					IntPtr.Zero);

				if (handle != IntPtr.Zero && handle.ToInt32() != -1)
				{
					_hidDeviceHandles[device.InstanceGuid] = handle;
					Debug.WriteLine($"Raw Input: Opened HID device for real-time polling: {device.DisplayName}");
					return handle;
				}
			}

			return IntPtr.Zero;
		}

		/// <summary>
		/// Reads current input report directly from HID device
		/// </summary>
		private byte[] ReadCurrentHidInputReport(IntPtr hidHandle, UserDevice device)
		{
			// Get report length from device capabilities
			var reportLength = GetInputReportLength(device);
			if (reportLength == 0)
				return null;

			var buffer = new byte[reportLength];
			var bufferPtr = Marshal.AllocHGlobal(buffer.Length);
			try
			{
				Marshal.Copy(buffer, 0, bufferPtr, buffer.Length);
				
				if (HidD_GetInputReport(hidHandle, bufferPtr, (uint)buffer.Length))
				{
					Marshal.Copy(bufferPtr, buffer, 0, buffer.Length);
					return buffer;
				}
			}
			finally
			{
				Marshal.FreeHGlobal(bufferPtr);
			}

			return null;
		}

		/// <summary>
		/// Parses HID report directly to CustomDeviceState (real-time)
		/// </summary>
		private CustomDeviceState ParseHidReportToCustomDeviceState(byte[] report, UserDevice device)
		{
			var state = new CustomDeviceState();
			
			// Get device HID capabilities for parsing
			var rawInputHandle = GetOrCreateRawInputMapping(device);
			if (rawInputHandle != IntPtr.Zero && _trackedDevices.TryGetValue(rawInputHandle, out var deviceInfo))
			{
				if (deviceInfo.HidCapabilities != null)
				{
					// Use existing HID API parsing but with current report data
					var bufferPtr = Marshal.AllocHGlobal(report.Length);
					try
					{
						Marshal.Copy(report, 0, bufferPtr, report.Length);
						return ReadHidStateWithApi(deviceInfo, bufferPtr, (uint)report.Length);
					}
					finally
					{
						Marshal.FreeHGlobal(bufferPtr);
					}
				}
			}

			// Fallback to basic parsing if HID capabilities not available
			return ReadHidStateFallback(report);
		}

		/// <summary>
		/// Gets input report length for a device
		/// </summary>
		private int GetInputReportLength(UserDevice device)
		{
			var rawInputHandle = GetOrCreateRawInputMapping(device);
			if (rawInputHandle != IntPtr.Zero && _trackedDevices.TryGetValue(rawInputHandle, out var deviceInfo))
			{
				return deviceInfo.HidCapabilities?.RawCaps.InputReportByteLength ?? 0;
			}
			return 0;
		}

		/// <summary>
		/// Fallback HID report parsing for real-time reading
		/// </summary>
		private CustomDeviceState ReadHidStateFallback(byte[] report)
		{
			// Use basic parsing for common controller layouts
			var state = new CustomDeviceState();
			
			// Basic parsing for common controller layouts
			if (report != null && report.Length >= 8)
			{
				// Example basic parsing - this would need to be expanded
				// based on actual device HID descriptors
				state.Axis[0] = (report[1] - 128) * 256; // X axis
				state.Axis[1] = (report[2] - 128) * 256; // Y axis
				state.Axis[2] = (report[3] - 128) * 256; // Z axis
				state.Axis[3] = (report[4] - 128) * 256; // RZ axis

				// Buttons
				for (int i = 0; i < Math.Min(16, state.Buttons.Length); i++)
				{
					state.Buttons[i] = (report[5] & (1 << i)) != 0;
				}
			}

			return state;
		}

		/// <summary>
		/// Cleans up HID device handles when disposing
		/// </summary>
		private void CleanupHidDeviceHandles()
		{
			foreach (var handle in _hidDeviceHandles.Values)
			{
				try
				{
					CloseHandle(handle);
				}
				catch (Exception ex)
				{
					Debug.WriteLine($"Error closing HID device handle: {ex.Message}");
				}
			}
			_hidDeviceHandles.Clear();
		}

		#endregion
	}
}