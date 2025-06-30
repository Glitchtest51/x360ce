using JocysCom.ClassLibrary.Controls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace x360ce.App.Controls
{
	/// <summary>
	/// Interaction logic for OptionsHidGuardianControl.xaml
	/// </summary>
	public partial class OptionsHidGuardianControl : UserControl
	{
		public OptionsHidGuardianControl()
		{
			InitHelper.InitTimer(this, InitializeComponent);
		}

		private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			var window = Global._MainWindow;
			if (window == null)
				return;
			var isSelected =
				window.MainBodyPanel.MainTabControl.SelectedItem == window.MainBodyPanel.OptionsTabPage &&
				window.OptionsPanel.MainTabControl.SelectedItem == window.OptionsPanel.HidGuardianTabPage;
			// If HidGuardian Tab was selected then refresh.
			if (isSelected)
				HidRefreshStatus();
		}

        private void HidHideInstallButton_Click(object sender, RoutedEventArgs e)
        {
            //ControlsHelper.BeginInvoke(() =>
            //{
            //    HidHideStatusTextBox.Text = "Installing. Please Wait...";
            //    Program.RunElevated(AdminCommand.InstallHidHide);
            //    //ViGEm.HidGuardianHelper.InsertCurrentProcessToWhiteList();
            //    HidRefreshStatus();
            //});
        }

        private void HidGuardianInstallButton_Click(object sender, RoutedEventArgs e)
		{
			ControlsHelper.BeginInvoke(() =>
			{
				HidGuardianStatusTextBox.Text = "Installing. Please Wait...";
				Program.RunElevated(AdminCommand.InstallHidGuardian);
				ViGEm.HidGuardianHelper.InsertCurrentProcessToWhiteList();
				HidRefreshStatus();
			});
		}

        private void HidHideRefreshButton_Click(object sender, RoutedEventArgs e)
        {
            HidRefreshStatus();
        }

        private void HidGuardianRefreshButton_Click(object sender, RoutedEventArgs e)
		{
			HidRefreshStatus();
		}

        private void HidHideUninstallButton_Click(object sender, RoutedEventArgs e)
        {
            //ControlsHelper.BeginInvoke(() =>
            //{
            //    HidHideStatusTextBox.Text = "Uninstalling. Please Wait...";
            //    Program.RunElevated(AdminCommand.UninstallHidHide);
            //    HidRefreshStatus();
            //});
        }

        private void HidGuardianUninstallButton_Click(object sender, RoutedEventArgs e)
		{
			ControlsHelper.BeginInvoke(() =>
			{
				HidGuardianStatusTextBox.Text = "Uninstalling. Please Wait...";
				Program.RunElevated(AdminCommand.UninstallHidGuardian);
				HidRefreshStatus();
			});
		}

		void HidRefreshStatus()
		{
			ControlsHelper.SetText(HidGuardianStatusTextBox, "Please wait...");
			// run in another thread, to make sure it is not freezing interface.
			var ts = new System.Threading.ThreadStart(delegate ()
			{
				// Get Virtual Bus, HidGuardian, and HidHide status.
				var hidGuardian = DInput.VirtualDriverInstaller.GetHidGuardianDriverInfo();
                var hidHide = DInput.VirtualDriverInstaller.GetHideDriverInfo();
                ControlsHelper.BeginInvoke(() =>
				{
					// Update HidGuardian status.
					var hidGuardianStatus = hidGuardian.DriverVersion == 0
						? "Not installed"
						: string.Format("{0} {1}", hidGuardian.Description, hidGuardian.GetVersion());
					ControlsHelper.SetText(HidGuardianStatusTextBox, hidGuardianStatus);
					HidGuardianInstallButton.IsEnabled = hidGuardian.DriverVersion == 0;
					HidGuardianUninstallButton.IsEnabled = hidGuardian.DriverVersion != 0;

                    // Update HidHide status.
					var hidHideStatus = hidHide.DriverVersion == 0
						? "Not installed"
						: string.Format("{0} {1}", hidHide.Description, hidHide.GetVersion());
                    ControlsHelper.SetText(HidHideStatusTextBox, hidHideStatus);
                    HidHideInstallButton.IsEnabled = hidHide.DriverVersion == 0;
                    HidHideUninstallButton.IsEnabled = hidHide.DriverVersion != 0;
                });
			});
			var t = new System.Threading.Thread(ts);
			t.Start();
		}

		private void UserControl_Loaded(object sender, RoutedEventArgs e)
		{
			if (!ControlsHelper.AllowLoad(this))
				return;
			Global._MainWindow.MainBodyPanel.MainTabControl.SelectionChanged += MainTabControl_SelectionChanged;
			Global._MainWindow.OptionsPanel.MainTabControl.SelectionChanged += MainTabControl_SelectionChanged;
            //var bytes1 = JocysCom.ClassLibrary.Helper.FindResource<byte[]>("Documents.Help_HidHide.rtf");
            //ControlsHelper.SetTextFromResource(HidHideRichTextBox, bytes1);
            var bytes2 = JocysCom.ClassLibrary.Helper.FindResource<byte[]>("Documents.Help_HidGuardian.rtf");
			ControlsHelper.SetTextFromResource(HidGuardianRichTextBox, bytes2);
			// Bind Controls.
			var o = SettingsManager.Options;
			SettingsManager.LoadAndMonitor(o, nameof(o.HidGuardianConfigureAutomatically), HidGuardianConfigureAutomaticallyCheckBox);
			HidRefreshStatus();
		}

		private void UserControl_Unloaded(object sender, RoutedEventArgs e)
		{
			if (!ControlsHelper.AllowUnload(this))
				return;
			// Moved to MainBodyControl_Unloaded().
		}

		public void ParentWindow_Unloaded()
		{
			TabControl tc;
			tc = Global._MainWindow?.MainBodyPanel?.MainTabControl;
			if (tc != null)
				tc.SelectionChanged -= MainTabControl_SelectionChanged;
			tc = Global._MainWindow?.OptionsPanel?.MainTabControl;
			if (tc != null)
				tc.SelectionChanged -= MainTabControl_SelectionChanged;
			SettingsManager.UnLoadMonitor(HidGuardianConfigureAutomaticallyCheckBox);
		}

        private void HyperLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            OpenUrl(e.Uri.AbsoluteUri);
        }

        public void OpenUrl(string url)
        {
            try
            {
                System.Diagnostics.Process.Start(url);
            }
            catch (System.ComponentModel.Win32Exception noBrowser)
            {
                if (noBrowser.ErrorCode == -2147467259)
                    MessageBox.Show(noBrowser.Message);
            }
            catch (System.Exception other)
            {
                MessageBox.Show(other.Message);
            }
        }

        private void HidHideOpenButton_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
