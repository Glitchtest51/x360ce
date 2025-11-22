using System;
using System.Windows;
using System.Windows.Threading;

namespace x360ce.App.Timers
{
	/// <summary>
	/// Manages UI update timer for input device controls.
	/// Provides periodic UI updates at configurable intervals with automatic start/stop based on visibility.
	/// </summary>
	public class UserInterfaceUpdatingTimer : IDisposable
	{
		private DispatcherTimer _dispatcherTimer;
		private const int DefaultUpdateIntervalMs = 100; // Run 10 times per second
		private UIElement _targetControl;
		private Action _tickAction;

		/// <summary>
		/// Gets or sets the update interval in milliseconds.
		/// Default is 100ms (10Hz update rate).
		/// </summary>
		public int UpdateIntervalMs
		{
			get => _updateIntervalMs;
			set
			{
				_updateIntervalMs = value;
				if (_dispatcherTimer != null)
					_dispatcherTimer.Interval = TimeSpan.FromMilliseconds(value);
			}
		}
		private int _updateIntervalMs = DefaultUpdateIntervalMs;

		/// <summary>
		/// Gets whether the timer is currently running.
		/// </summary>
		public bool IsRunning => _dispatcherTimer?.IsEnabled ?? false;

		/// <summary>
		/// Initializes the UI update timer with the specified target control and tick action.
		/// </summary>
		/// <param name="targetControl">The UI element whose visibility controls timer start/stop.</param>
		/// <param name="tickAction">The action to execute on each timer tick.</param>
		public void Initialize(UIElement targetControl, Action tickAction)
		{
			if (targetControl == null)
				throw new ArgumentNullException(nameof(targetControl));
			if (tickAction == null)
				throw new ArgumentNullException(nameof(tickAction));
			// Prevent double initialization
			if (_dispatcherTimer != null)
				return;

			_targetControl = targetControl;
			_tickAction = tickAction;

			// Create the dispatcher timer
			_dispatcherTimer = new DispatcherTimer
			{
				Interval = TimeSpan.FromMilliseconds(UpdateIntervalMs)
			};
			_dispatcherTimer.Tick += (s, e) => _tickAction?.Invoke();

			// Set up visibility-based start/stop
			InitializeVisibilityHandling();

			// Start timer if control is already visible
			if (_targetControl.IsVisible)
				Start();
		}

		/// <summary>
		/// Initializes visibility handling to automatically start/stop timer based on control visibility.
		/// If control is visible, UI timer starts; if not, UI timer stops.
		/// </summary>
		private void InitializeVisibilityHandling()
		{
			_targetControl.IsVisibleChanged += TargetControl_IsVisibleChanged;
		}

		private void TargetControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
		{
			if (_targetControl.IsVisible)
				Start();
			else
				Stop();
		}

		/// <summary>
		/// Starts the UI update timer.
		/// </summary>
		public void Start()
		{
			_dispatcherTimer?.Start();
		}

		/// <summary>
		/// Stops the UI update timer.
		/// </summary>
		public void Stop()
		{
			_dispatcherTimer?.Stop();
		}

		/// <summary>
		/// Updates the timer interval. Timer will be restarted if it was running.
		/// </summary>
		/// <param name="intervalMs">New interval in milliseconds.</param>
		public void UpdateInterval(int intervalMs)
		{
			if (intervalMs <= 0)
				throw new ArgumentOutOfRangeException(nameof(intervalMs), "Interval must be greater than zero.");

			UpdateIntervalMs = intervalMs;
		}

		/// <summary>
		/// Disposes the timer and cleans up resources.
		/// </summary>
		public void Dispose()
		{
			Stop();
			if (_targetControl != null)
			{
				_targetControl.IsVisibleChanged -= TargetControl_IsVisibleChanged;
				_targetControl = null;
			}
			_dispatcherTimer = null;
			_tickAction = null;
		}
	}
}
