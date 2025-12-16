using JocysCom.ClassLibrary.Controls.IssuesControl;
using Microsoft.Win32;
using System;
using System.Linq;

namespace x360ce.App.Issues
{
    public class CppX86RuntimeInstallIssue : IssueItem
    {
        public CppX86RuntimeInstallIssue() : base()
        {
            Name = "Software";
            FixName = "Download and Install";
            MoreInfo = new Uri("https://learn.microsoft.com/cpp/windows/latest-supported-vc-redists");
        }

        static readonly Version MinVcRuntimeVersion = new Version(14, 0, 23026, 0);

        public override void CheckTask()
        {
            Version installedVersion = GetVcRuntimeVersionX86();

            if (installedVersion == null || installedVersion < MinVcRuntimeVersion)
            {
                SetSeverity(
                    IssueSeverity.Critical,
                    1,
                    "Install or update Microsoft Visual C++ Redistributable (x86)"
                );
                return;
            }

            SetSeverity(IssueSeverity.None);
        }

        private static Version GetVcRuntimeVersionX86()
        {
            const string keyPath = @"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x86";

            using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
            using (var key = baseKey.OpenSubKey(keyPath))
            {
                if (key == null)
                    return null;

                if (Convert.ToInt32(key.GetValue("Installed", 0)) != 1)
                    return null;

                int major = Convert.ToInt32(key.GetValue("Major", 0));
                int minor = Convert.ToInt32(key.GetValue("Minor", 0));
                int bld   = Convert.ToInt32(key.GetValue("Bld",   0));
                int rbld  = Convert.ToInt32(key.GetValue("RBld",  0));

                return new Version(major, minor, bld, rbld);
            }
        }

        public override void FixTask()
        {
            var uri = new Uri("https://aka.ms/vs/17/release/vc_redist.x86.exe");
            var localPath = System.IO.Path.Combine(x360ce.Engine.EngineHelper.AppDataPath, "Temp", uri.Segments.Last());
            IssueHelper.DownloadAndInstall(uri, localPath, MoreInfo);
        }
    }
}
