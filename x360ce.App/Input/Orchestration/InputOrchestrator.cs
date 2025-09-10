using JocysCom.ClassLibrary.IO;
using SharpDX.DirectInput;
using SharpDX.XInput;
using System;
using System.Diagnostics;
using System.Threading;
using x360ce.App.Input.Processors;
using x360ce.Engine;
using x360ce.Engine.Data;

namespace x360ce.App.Input.Orchestration
{
	public partial class InputOrchestrator : IDisposable
	{

        // --------------------------------------------------------------------------------------------
        // DESCRIPTION - 8-STEP INPUT ORCHESTRATION ARCHITECTURE
        // --------------------------------------------------------------------------------------------
        // Monitor (WM_DEVICECHANGE) device (HID, Keyboard, Mouse) interface events (DEV_BROADCAST_DEVICEINTERFACE).
        // On detection, set DevicesNeedUpdating = true (also, set 'true' on 'InputLost' error during state reading).
        // Build a list of SharpDX.DirectInput.DeviceInstance objects (DeviceClass.GameControl, DeviceClass.Keyboard, DeviceClass.Pointer).
        // On DEV_BROADCAST_DEVICEINTERFACE event, PID and VID is extracted from detected Win32_PnPEntity.DeviceID.
        // For example: HID\VID_046D&PID_C216\7&1D9AEBE3&0&0000 > c216046d.
        // This PID and VID is used to check if Win32_PnPEntity exists as SharpDX.DirectInput.DeviceInstance.
        // For example: c216046d will find SharpDX.DirectInput.DeviceInstance.ProductGuid: c216046d-0000-0000-0000-504944564944.
        // if Win32_PnPEntity exists as SharpDX.DirectInput.DeviceInstance, this will trigger next steps.

        // 8-STEP SERIAL EXECUTION ORDER (1000Hz main loop):
        //
        // Step 1: UpdateDevices - Device detection and initialization
        // Step 2: LoadCapabilities - Flag-based capability loading
        // Step 3: ReadDeviceStates - Raw input state reading (4 input methods)
        // Step 4: ConvertToCustomStates - Convert to unified CustomDeviceState format
        // Step 5: UpdateXiStates - Convert CustomDeviceState to XInput states
        // Step 6: CombineXiStates - Combine multiple controller states
        // Step 7: UpdateVirtualDevices - Update ViGEm virtual devices
        // Step 8: RetrieveXiStates - Retrieve XInput controller states
        //
        // Process 1 is limited to [125, 250, 500, 1000Hz] - Main orchestration loop
        // Lock { All 8 steps execute serially to ensure thread safety }
        //
        // Process 2 is limited to [30Hz] (only when visible) - UI updates
        // Lock { Read orchestration results for UI display }

        // ⚠️⚠️⚠️ CRITICAL PERFORMANCE WARNING - TWO EXECUTION PATHS ⚠️⚠️⚠️
        //
        // THIS CODE HAS TWO DISTINCT EXECUTION PATHS WITH DIFFERENT PERFORMANCE RULES:
        //
        // 🐌 **DEVICE DETECTION PATH** (Lines 304-322) - SLOW OPERATIONS ALLOWED:
        //    - Runs ONLY when DevicesNeedUpdating=true (new device connected/disconnected)
        //    - ✅ WMI queries, device enumeration, file I/O are acceptable here
        //    - ✅ Debug.WriteLine, logging, complex operations allowed
        //    - ✅ UpdateDiDevices(), DeviceDetector.GetDevices(), GetInterfaces()
        //    - Purpose: Gather complete device information during hardware changes
        //
        // ⚡ **HIGH-FREQUENCY PATH** (Lines 325-347) - ULTRA-FAST REQUIRED:
        //    - Runs at 1000Hz+ continuously during normal operation
        //    - ❌ Debug.WriteLine() - Will flood output and destroy performance
        //    - ❌ Console.WriteLine() - Will kill console performance
        //    - ❌ String.Format() or string interpolation
        //    - ❌ File I/O operations, network calls, database operations
        //    - ❌ Exception logging, Thread.Sleep(), blocking operations
        //    - ❌ Large object allocations, complex string operations
        //    - ❌ LINQ in hot paths, reflection, heavy computations
        //    - Covers: Step 2-8 (LoadCapabilities through RetrieveXiStates)
        //
        // **RULE FOR AI AGENTS**:
        // - Device detection path (`UpdateDiDevices`) = Slow operations are allowed as an exception.
        // - High-frequency path (Steps 2-8) = Must be ultra-lightweight microsecond execution.
        // - Check which execution path before adding any expensive operations!
        // ⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️


        // Constructor
        public InputOrchestrator()
		{
			CombinedXiConnected = new bool[4];
			LiveXiConnected = new bool[4];
			CombinedXiStates = new State[4];
			LiveXiStates = new State[4];
			LiveXiControllers = new Controller[4];

			for (int i = 0; i < 4; i++)
			{
				CombinedXiStates[i] = new State();
				LiveXiStates[i] = new State();
				LiveXiControllers[i] = new Controller((UserIndex)i);
			}

			// Set current instance for processors to access
			SetCurrent(this);
		}

		#region Input Processor Registry

		/// <summary>
		/// Input method processors for the 4 supported input APIs.
		/// These processors handle method-specific operations like state reading, capability loading, and validation.
		/// </summary>
		public DirectInputProcessor directInputProcessor = new DirectInputProcessor();
		public XInputProcessor xInputProcessor = new XInputProcessor();
		public GamingInputProcessor gamingInputProcessor = new GamingInputProcessor();
		public RawInputProcessor rawInputProcessor = new RawInputProcessor();

		#endregion

		#region Shared Fields for All Input Methods

		UserDevice[] mappedDevices = new UserDevice[0];
		UserGame currentGame = SettingsManager.CurrentGame;
		Options options = SettingsManager.Options;
		public bool isVirtual = false;

		#endregion

		#region Shared Properties for Input Method Processors

		/// <summary>
		/// Gets whether the current game uses virtual emulation.
		/// Used by all input method processors for force feedback decisions.
		/// </summary>
		public bool IsVirtual => isVirtual;

		/// <summary>
		/// Gets the current InputOrchestrator instance for processors that need access to helper methods.
		/// </summary>
		public static InputOrchestrator Current { get; private set; }

		/// <summary>
		/// Sets the current InputOrchestrator instance.
		/// </summary>
		/// <param name="helper">The helper instance to set</param>
		public static void SetCurrent(InputOrchestrator helper)
		{
			Current = helper;
		}

		#endregion

		//===============================================================================================

		#region ■ Device Detector

		// DevicesNeedUpdating can be set (true = update device list as soon as possible) from multiple threads.
		public bool DevicesNeedUpdating = false;
		// DevicesAreUpdating property ensures parameter remains unchanged during RefreshAll(manager, detector) action.
		// CheckAndUnloadXInputLibrary(*) > UpdateDiDevices(*) > CheckAndLoadXInputLibrary(*).
		private bool DevicesAreUpdating = false;

		#endregion

		/// <summary>
		/// _ResetEvent with _Timer is used to limit update refresh frequency.
		/// ms1_1000Hz = 1, ms2_500Hz = 2, ms4_250Hz = 4, ms8_125Hz = 8.
		/// </summary>
		/// 
		ManualResetEvent _ResetEvent = new ManualResetEvent(false);
		JocysCom.ClassLibrary.HiResTimer _Timer;
		UpdateFrequency _Frequency = UpdateFrequency.ms1_1000Hz;

		public UpdateFrequency Frequency
		{
			get => _Frequency;
			set
			{
				_Frequency = value;
				if (_Timer?.Interval != (int)value)
					_Timer.Interval = (int)value;
			}
		}

		/// <summary>
		/// _Stopwatch time is used to calculate the actual update frequency in Hz per second.
		/// </summary>
		public Stopwatch _Stopwatch = new Stopwatch();
		private object timerLock = new object();
		private bool _AllowThreadToRun;

		// Start DInput Service.
		public void StartDInputService()
		{
			lock (timerLock)
			{
				if (_Timer != null)
					return;
				_Stopwatch.Restart();
				_Timer = new JocysCom.ClassLibrary.HiResTimer((int)Frequency, "InputOrchestratorTimer");
				_Timer.Elapsed += Timer_Elapsed;
				_Timer.Start();
				_AllowThreadToRun = true;
				RefreshAllAsync();
			}
		}

		// Stop DInput Service.
		public void StopDInputService()
		{
			lock (timerLock)
			{
				if (_Timer == null)
					return;
				_AllowThreadToRun = false;
				_Timer.Stop();
				_Timer.Elapsed -= Timer_Elapsed;
				_Timer.Dispose();
				_Timer = null;
				_ResetEvent.Set();
				// Wait for the thread to stop.
				_Thread.Join();
			}
		}

		/// <summary>
		/// Method which will create a separate thread for all DInput and XInput updates.
		/// This thread will run a function which will update the BindingList, which will use synchronous Invoke() on the main form running on the main thread.
		/// It can freeze because the main thread is not getting attention to process Invoke() (because attention is on this thread)
		/// and this thread is frozen because it is waiting for Invoke() to finish.
		/// Control when the event can continue.
		/// </summary>
		ThreadStart _ThreadStart;
		Thread _Thread;
		void RefreshAllAsync()
		{
			_ThreadStart = new ThreadStart(ThreadAction);
			_Thread = new Thread(_ThreadStart)
			{
				IsBackground = true
			};
			_Thread.Start();
		}

		public Exception LastException = null;
		private void Timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
		{
			try
			{
				// Sets the state of the event to signaled, allowing one or more waiting threads to proceed.
				_ResetEvent.Set();
			}
			catch (Exception ex)
			{
				JocysCom.ClassLibrary.Runtime.LogHelper.Current.WriteException(ex);
				LastException = ex;
			}
		}

		DirectInput directInput = new DirectInput();
		// Suspended is used during re-loading of the XInput library.
		public bool Suspended;
		void ThreadAction()
		{
			Thread.CurrentThread.Name = "RefreshAllThread";
			// DIrect input device querying and force feedback updates will run on a separate thread from MainForm, therefore
			// a separate windows form must be created on the same thread as the process which will access and update the device.
			// detector.DetectorForm will be used to acquire devices.
			// Main job of the detector is to fire an event on device connection (power on) and removal (power off).
			directInput = new DirectInput();
			var detector = new DeviceDetector(false);
			do
			{
				// Sets the state of the event to non-signaled, causing threads to block.
				_ResetEvent.Reset();
				// Perform all updates if not suspended.
				if (!Suspended)
					RefreshAll(directInput, detector);
				// Blocks the current thread until the current WaitHandle receives a signal.
				// The thread will be released by the timer. Do not wait longer than 50ms.
				_ResetEvent.WaitOne(50);
			}
			// Loop until allowed to run.
			while (_AllowThreadToRun);
			detector.Dispose();
			directInput.Dispose();
		}

		// Events.
		public event EventHandler<DInputEventArgs> DevicesUpdated;
		public event EventHandler<DInputEventArgs> StatesUpdated;
		public event EventHandler<DInputEventArgs> StatesRetrieved;
		public event EventHandler<DInputEventArgs> XInputReloaded;
		public event EventHandler<DInputEventArgs> UpdateCompleted;

		private readonly object DiUpdatesLock = new object();

		private void RefreshAll(DirectInput manager, DeviceDetector detector)
		{
			lock (DiUpdatesLock)
			{
				var game = SettingsManager.CurrentGame;
				// If the game is not selected.
				if (game != null || !Program.IsClosing)
				{
					// Note: Getting XInput states is not required in order to do emulation.
					// Get states only when the form is maximized in order to reduce CPU usage.
					var getXInputStates = SettingsManager.Options.GetXInputStates && Global._MainWindow.FormEventsEnabled;
					// Update hardware.
					if ((DevicesNeedUpdating && !DevicesAreUpdating) || DeviceDetector.DiDevices == null)
					{
						DevicesAreUpdating = true;
						try
						{
							// The best place to unload the XInput DLL is at the start, because UpdateDiStates(...) function
							// will try to acquire new devices exclusively for force feedback information and control.
							CheckAndUnloadXInputLibrary(game, getXInputStates);
							// Update information about connected devices.
							_ = UpdateDiDevices(manager);
							// Load the XInput library before retrieving XInput states.
							CheckAndLoadXInputLibrary(game, getXInputStates);
						}
						finally
						{
							DevicesNeedUpdating = false;
							DevicesAreUpdating = false;
						}
					}
					else
					{
						// NEW 8-STEP ARCHITECTURE - Execute steps in serial order
						
						// Step 2: Load device capabilities (flag-based)
						LoadDeviceCapabilities(game);
						
						// Step 3: Read raw device states
						ReadDeviceStates(game, detector);
						
						// Step 4: Convert raw states to CustomDeviceState
						ConvertToCustomStates(game);
						
						// Step 5: Update XInput states from Custom DirectInput states
						UpdateXiStates(game);
						
						// Step 6: Combine XInput states of controllers
						CombineXiStates();
						
						// Step 7: Update virtual devices from combined states
						UpdateVirtualDevices(game);
						
						// Step 8: Retrieve XInput states from XInput controllers
						RetrieveXiStates(getXInputStates);
					}
				}
				// Count DInput updates per second to show in the app's status bar as Hz: #.
				UpdateDelayFrequency();
				// Fire update completed event.
				UpdateCompleted?.Invoke(this, new DInputEventArgs());
			}
		}

		// Count DInput updates per second to show in the app's status bar as Hz: #.
		public event EventHandler<DInputEventArgs> FrequencyUpdated;
		private int executionCount = 0;
		private long lastTime = 0;
		public long CurrentUpdateFrequency;
		private void UpdateDelayFrequency()
		{
			var currentTime = _Stopwatch.ElapsedMilliseconds;
			// If one second has elapsed then...
			if ((currentTime - lastTime) > 1000)
			{
				CurrentUpdateFrequency = Interlocked.Exchange(ref executionCount, 0);
				FrequencyUpdated?.Invoke(this, new DInputEventArgs());
				lastTime = currentTime;
			}
			Interlocked.Increment(ref executionCount);
		}

		#region 

		EmulationType CurrentEmulation;

		public void CheckAndUnloadXInputLibrary(UserGame game, bool getXInputStates)
		{
			lock (Controller.XInputLock)
			{
				var (unloadLoad, emType) = UnloadLoad(game, getXInputStates);
				if (!Controller.IsLoaded || !unloadLoad || emType != CurrentEmulation)
					return;
				Controller.FreeLibrary();
				XInputReloaded?.Invoke(this, new DInputEventArgs());
			}
		}

		private (bool, EmulationType) UnloadLoad(UserGame game, bool getXInputStates)
		{
			var emType = (EmulationType)(game?.EmulationType ?? (int)EmulationType.None);
			var unloadLoad =
				// No emulation or
				emType == EmulationType.None ||
				// If no actual XInput states are required for Virtual emulation.
				emType == EmulationType.Virtual && !getXInputStates ||
				// New device was detected so exclusive lock is necessary to retrieve force feedback information.
				// Don't load until device list was not refreshed.
				// DevicesAreUpdating ||
				// This will also release exclusive lock if another game/application must use it.
				// No actual XInput states are required for Library emulation when minimized.
				emType == EmulationType.Library && !Global._MainWindow.FormEventsEnabled;
			return (unloadLoad, emType);
		}

		public void CheckAndLoadXInputLibrary(UserGame game, bool getXInputStates)
		{
			lock (Controller.XInputLock)
			{
				var (unloadLoad, emType) = UnloadLoad(game, getXInputStates);

				if (Controller.IsLoaded || !unloadLoad)
					return;

				//MainForm.Current.Save();
				var e = new DInputEventArgs();
				CurrentEmulation = emType;
				Program.ReloadCount++;
				// Always load Microsoft XInput DLL by default.
				var dllInfo = EngineHelper.GetDefaultDll(emType == EmulationType.Virtual);
				if (dllInfo != null && dllInfo.Exists)
				{
					e.XInputVersionInfo = FileVersionInfo.GetVersionInfo(dllInfo.FullName);
					e.XInputFileInfo = dllInfo;
					// If fast reload of settings is supported then...
					if (Controller.IsLoaded && Controller.IsResetSupported)
					{
						IAsyncResult result;
						Action action = () =>
						{
							Controller.Reset();
						};
						result = action.BeginInvoke(null, null);
						// var timeout = !result.AsyncWaitHandle.WaitOne(1000);
						var caption = string.Format("Failed to Reset() controller. '{0}'", dllInfo.FullName);
						e.Error = new Exception(caption);
					}
					// Slow: Reload whole x360ce.dll.
					else
					{
						Exception error;
						Controller.ReLoadLibrary(dllInfo.FullName, out error);
						if (!Controller.IsLoaded)
						{
							var caption = string.Format("Failed to load '{0}'", dllInfo.FullName);
							e.Error = new Exception(caption);
						}
					}
				}
				// If x360ce DLL loaded and settings changed then...
				var IsLibrary = game != null && game.IsLibrary;
				if (Controller.IsLoaded && IsLibrary && SettingsChanged)
				{
					// Reset configuration.
					Controller.Reset();
					SettingsChanged = false;
				}
				XInputReloaded?.Invoke(this, e);
			}
		}

		#endregion

		#region ■ IDisposable

		private bool IsDisposing;
		private bool disposed = false;

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
			// PnPDeviceWatcher?.Dispose();
			directInput?.Dispose();
		}

		protected virtual void Dispose(bool disposing)
		{
			if (disposed)
				return;

			if (disposing)
			{
				// Do not dispose twice.
				if (IsDisposing)
					return;
				IsDisposing = true;

				StopDInputService();
				Nefarius.ViGEm.Client.ViGEmClient.DisposeCurrent();
				_ResetEvent?.Dispose();

				// Nullify managed resources after disposal.
				_Timer = null;
				_Thread = null;
				_ResetEvent = null;
			}

			disposed = true;
		}

		~InputOrchestrator()
		{
			Dispose(false);
		}

		#endregion

	}
}
