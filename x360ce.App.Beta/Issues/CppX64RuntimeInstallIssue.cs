using JocysCom.ClassLibrary.Controls.IssuesControl;
using Microsoft.Win32;
using System;
using System.Linq;

namespace x360ce.App.Issues
{
    public class CppX64RuntimeInstallIssue : IssueItem
	{

		public CppX64RuntimeInstallIssue() : base()
		{
			Name = "Software";
			FixName = "Download and Install";
			MoreInfo = new Uri("https://learn.microsoft.com/cpp/windows/latest-supported-vc-redist");
		}
		
		static readonly Version MinVcRuntimeVersion = new Version(14, 0, 23026, 0);
		
		public override void CheckTask()
		{
			if (!Environment.Is64BitOperatingSystem)
			{
				SetSeverity(IssueSeverity.None);
				return;
			}

			Version installedVersion = GetVcRuntimeVersionX64();

			if (installedVersion == null || installedVersion < MinVcRuntimeVersion)
			{
				SetSeverity(
					IssueSeverity.Critical, 1,
					"Install or update Microsoft Visual C++ Redistributable (x64)"
				);
				return;
			}

			SetSeverity(IssueSeverity.None);
		}
		
		private static Version GetVcRuntimeVersionX64()
		{
			const string keyPath = @"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64";

			using (var key = Registry.LocalMachine.OpenSubKey(keyPath))
			{
				if (key == null)
					return null;
				
				var installed = key.GetValue("Installed") as int?;
				if (installed != 1)
					return null;

				int major = (int)(key.GetValue("Major") ?? 0);
				int minor = (int)(key.GetValue("Minor") ?? 0);
				int bld   = (int)(key.GetValue("Bld")   ?? 0);
				int rbld  = (int)(key.GetValue("RBld")  ?? 0);

				return new Version(major, minor, bld, rbld);
			}
		}

		public override void FixTask()
		{
			var uri = new Uri("https://aka.ms/vs/17/release/vc_redist.x64.exe");
			var localPath = System.IO.Path.Combine(x360ce.Engine.EngineHelper.AppDataPath, "Temp", uri.Segments.Last());
			IssueHelper.DownloadAndInstall(uri, localPath, MoreInfo);
		}
    }
}
