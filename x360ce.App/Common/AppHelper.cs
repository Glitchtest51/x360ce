using SharpDX.DirectInput;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using x360ce.Engine;
using System.Security.AccessControl;
using System.Security.Principal;
using x360ce.Engine.Data;
using SharpDX.XInput;
using JocysCom.ClassLibrary.Win32;
using x360ce.App.ViGEm;
// using System.Diagnostics;
//using System.CodeDom;
//using System.Windows.Controls;
//using System.Windows.Data;

namespace x360ce.App
{
	public static class AppHelper
	{
		#region ■ DLL Functions

		static void Elevate()
		{
			// If this is Vista/7 and is not elevated then elevate.
			if (WinAPI.IsVista && !WinAPI.IsElevated())
				WinAPI.RunElevated();
		}

		public static bool WriteFile(string resourceName, string destinationFileName)
		{
			var assembly = Assembly.GetExecutingAssembly();
			var sr = assembly.GetManifestResourceStream(resourceName);
			FileStream sw;
			try
			{
				sw = new FileStream(destinationFileName, FileMode.Create, FileAccess.Write);
			}
			catch (Exception)
			{
				Elevate();
				return false;
			}
			var buffer = new byte[1024];
			while (true)
			{
				var count = sr.Read(buffer, 0, buffer.Length);
				if (count == 0)
					break;
				sw.Write(buffer, 0, count);
			}
			sr.Close();
			sw.Close();
			return true;
		}

		public static bool CopyFile(string sourceFileName, string destFileName)
		{
			try
			{
				File.Copy(sourceFileName, destFileName, true);
			}
			catch (Exception)
			{
				Elevate();
				return false;
			}
			return true;
		}



		#endregion

		// Use cache so same image won't processed multiple times.
		public static Dictionary<Bitmap, Bitmap> DisabledImageCache = new Dictionary<Bitmap, Bitmap>();
		//static object DisabledImageLock = new object();

		/// <summary>
		/// Generates disabled Image. Images are cached so do not use method for random images.
		/// </summary>
		//public static Bitmap GetDisabledImage(Bitmap image)
		//{
		//	lock (DisabledImageLock)
		//	{
		//		if (!DisabledImageCache.ContainsKey(image))
		//		{
		//			var newImage = (Bitmap)image.Clone();
		//			JocysCom.ClassLibrary.Drawing.Effects.GrayScale(newImage);
		//			JocysCom.ClassLibrary.Drawing.Effects.Transparent(newImage, 50);
		//			DisabledImageCache.Add(image, newImage);
		//		}
		//		return DisabledImageCache[image];
		//	}
		//}

		/// <summary>
		/// Remove explicit file rules and leave inherited rules only.
		/// Allow built-in users to write and modify file.
		/// </summary>
		public static bool CheckExplicitAccessRulesAndAllowToModify(string fileName, bool applyFix)
		{
			var fileInfo = new FileInfo(fileName);
			var fileSecurity = fileInfo.GetAccessControl();
			fileSecurity.SetAccessRuleProtection(false, false);
			var identity = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
			// Get explicit file rules of FileSystemAccessRule type.
			var rules = fileSecurity.GetAccessRules(true, true, typeof(NTAccount)).OfType<FileSystemAccessRule>();
			var referenceValue = ((NTAccount)identity.Translate(typeof(NTAccount))).Value;
			// Remove explicit permission.
			var allowsWrite = false;
			var allowsModify = false;
			var rulesChanged = false;
			foreach (var rule in rules)
			{
				if (rule.AccessControlType == AccessControlType.Allow && rule.IdentityReference.Value == referenceValue)
				{
					if (rule.FileSystemRights.HasFlag(FileSystemRights.Write))
					{
						allowsWrite = true;
						continue;
					}
					if (rule.FileSystemRights.HasFlag(FileSystemRights.Modify))
					{
						allowsModify = true;
						continue;
					}
				}
				// If rule is not inherited from parent directory then...
				if (!rule.IsInherited)
				{
					// Remove rules.
					fileSecurity.RemoveAccessRule(rule);
					rulesChanged = true;
				}
			}
			if (applyFix)
			{
				if (!allowsWrite)
				{
					fileSecurity.AddAccessRule(new FileSystemAccessRule(identity, FileSystemRights.Write, AccessControlType.Allow));
					rulesChanged = true;
				}
				if (!allowsModify)
				{
					fileSecurity.AddAccessRule(new FileSystemAccessRule(identity, FileSystemRights.Modify, AccessControlType.Allow));
					rulesChanged = true;
				}
				if (rulesChanged)
				{
					fileInfo.SetAccessControl(fileSecurity);
				}
			}
			return rulesChanged;
		}

		/// <summary>
		/// Update (wipe all old records) DataGridView is such way that it won't loose selection.
		/// </summary>
		public static void UpdateList<T>(IList<T> source, IList<T> destination)
		{
			if (source == null)
				source = new List<T>();
			var sCount = source.Count;
			var dCount = destination.Count;
			var length = Math.Min(sCount, dCount);
			for (int i = 0; i < length; i++)
				destination[i] = source[i];
			// Add extra rows.
			if (sCount > dCount)
			{
				for (int i = dCount; i < sCount; i++)
					destination.Add(source[i]);
			}
			else if (dCount > sCount)
			{
				for (int i = dCount - 1; i >= sCount; i--)
					destination.RemoveAt(i);
			}
		}

		public static Engine.Data.UserSetting GetNewSetting(UserDevice device, Engine.Data.UserGame game, MapTo mapTo)
		{
			// Create new setting for game/device.
			var newSetting = new Engine.Data.UserSetting();
			newSetting.InstanceGuid = device.InstanceGuid;
			newSetting.InstanceName = device.InstanceName;
			newSetting.ProductGuid = device.ProductGuid;
			newSetting.ProductName = device.ProductName;
			newSetting.DeviceType = device.CapType;
			newSetting.FileName = game.FileName;
			newSetting.FileProductName = game.FileProductName;
			newSetting.DateCreated = DateTime.Now;
			newSetting.IsEnabled = true;
			newSetting.MapTo = (int)mapTo;
			return newSetting;
		}

		public static MapToMask GetMapFlag(MapTo mapTo)
		{
			switch (mapTo)
			{
				case MapTo.Controller1:
					return MapToMask.Controller1;
				case MapTo.Controller2:
					return MapToMask.Controller2;
				case MapTo.Controller3:
					return MapToMask.Controller3;
				case MapTo.Controller4:
					return MapToMask.Controller4;
				default:
					return MapToMask.None;
			}
		}

		#region ■ HID Guardian

		public static void InitializeHidGuardian()
		{
			// If can't fix and modify registry then return.
			if (!ViGEm.HidGuardianHelper.CanModifyParameters(true))
				return;
			ViGEm.HidGuardianHelper.InsertCurrentProcessToWhiteList();
			ViGEm.HidGuardianHelper.ClearWhiteList(true, true);
		}

		public static void UnInitializeHidGuardian()
		{
			// If can't modify registry then return.
			if (!ViGEm.HidGuardianHelper.CanModifyParameters())
				return;
			if (SettingsManager.Options.HidGuardianConfigureAutomatically)
				UnhideAllDevices();
			ViGEm.HidGuardianHelper.RemoveCurrentProcessFromWhiteList();
		}

		/// <summary>
		/// Must be executed before program close.
		/// </summary>
		/// <returns></returns>
		public static bool UnhideAllDevices()
		{
			var affected = ViGEm.HidGuardianHelper.GetAffected();
			// Clear list of hidden devices.
			ViGEm.HidGuardianHelper.ClearAffected();
			var devices = SettingsManager.UserDevices.ItemsToArraySynchronized();
			// Unhide all devices.
			for (int i = 0; i < devices.Length; i++)
				devices[i].IsHidden = false;
			HidGuardianHelper.ResetDevices(affected);
			return true;
		}

		public static bool SynchronizeToHidGuardian(params Guid[] instanceGuids)
		{
			var game = SettingsManager.CurrentGame;
			// Affected devices.
			UserDevice[] devices;
			lock (SettingsManager.UserDevices.SyncRoot)
			{
				devices = instanceGuids == null || instanceGuids.Length == 0
					? SettingsManager.UserDevices.Items.ToArray()
					: SettingsManager.UserDevices.Items.Where(x => instanceGuids.Contains(x.InstanceGuid)).ToArray();
			}
			// Get all Ids.
			var idsToHide = new List<string>();
			var idsToShow = new List<string>();
			foreach (var ud in devices)
			{
				var hardwareId = (ud.HidHardwareIds ?? "")
					// Split lines into arraty and exclude empty ones.
					.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
					// Get all Hardware IDs with vendor code and product code.
					.Where(x => HidGuardianHelper.HardwareIdRegex.IsMatch(x)).ToList()
					// Put longest ID on top.
					.OrderByDescending(x => x)
					// Take most detail Hardware ID.
					.FirstOrDefault();
				// If hardware is not available then create from device id.
				if (string.IsNullOrEmpty(hardwareId) && !string.IsNullOrEmpty(ud.DevDeviceId))
					hardwareId = HidGuardianHelper.ConvertToHidVidPid(ud.DevDeviceId).FirstOrDefault();
				if (string.IsNullOrEmpty(hardwareId))
					continue;
				// If must hide and device is not keyboard or mouse.
				if (ud.IsHidden && !ud.IsKeyboard && !ud.IsMouse)
					idsToHide.Add(hardwareId);
				else if (!ud.IsHidden)
					idsToShow.Add(hardwareId);
			}
			var canModify = ViGEm.HidGuardianHelper.CanModifyParameters(true);
			if (canModify)
			{
				var idsToHide2 = idsToHide.Distinct().ToArray();
				var idsToShow2 = idsToShow.Distinct().ToArray();
				ViGEm.HidGuardianHelper.RemoveFromAffected(idsToShow2);
				ViGEm.HidGuardianHelper.InsertToAffected(idsToHide2);
			}
			return canModify;
		}

		#endregion

	}
}
