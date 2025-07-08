using System.Windows.Forms;

namespace x360ce.App.Input.Processors
{
	/// <summary>
	/// Hidden window for receiving Raw Input messages.
	/// </summary>
	internal class RawInputWindow : Form
	{
		private const int WM_INPUT = 0x00FF;

		public RawInputWindow()
		{
			// Create hidden window
			WindowState = FormWindowState.Minimized;
			ShowInTaskbar = false;
			Visible = false;
			CreateHandle();
		}

		protected override void WndProc(ref Message m)
		{
			if (m.Msg == WM_INPUT)
			{
				RawInputProcessor.ProcessRawInput(this.Handle, m.LParam);
			}
			base.WndProc(ref m);
		}

		protected override CreateParams CreateParams
		{
			get
			{
				CreateParams cp = base.CreateParams;
				cp.ExStyle |= 0x80; // WS_EX_TOOLWINDOW
				return cp;
			}
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				// Notify the processor that this window is being disposed
				RawInputProcessor.UnregisterWindow(this.Handle);
			}
			base.Dispose(disposing);
		}
	}
}
