using SharpDX.XInput;
using System;
using System.Diagnostics;
using x360ce.App.Input.Devices;

namespace x360ce.App.Input.States
{
	/// <summary>
	/// Provides methods to retrieve XInput device states.
	/// Handles state reading for Xbox 360/One controllers using Microsoft XInput API.
	/// </summary>
	/// <remarks>
	/// XInput API Characteristics:
	/// • Uses slot-based indexing (0-3) instead of device objects
	/// • Maximum 4 controllers supported (hard API limit)
	/// • Only works with Xbox-compatible controllers
	/// • Provides direct access to Gamepad state structure
	/// • No device acquisition needed (unlike DirectInput)
	/// 
	/// XInput State Structure:
	/// • PacketNumber: Increments when controller state changes
	/// • Gamepad: Contains buttons, thumbsticks, and triggers
	///   - Buttons: GamepadButtonFlags enum (A, B, X, Y, shoulders, etc.)
	///   - LeftThumbX/Y: Left stick position (-32768 to 32767)
	///   - RightThumbX/Y: Right stick position (-32768 to 32767)
	///   - LeftTrigger/RightTrigger: Trigger pressure (0-255)
	/// </remarks>
	internal class StatesXInput
	{
		#region XInput Controller Management

		/// <summary>
		/// Maximum number of XInput controllers supported by the API.
		/// </summary>
		private const int MaxControllers = 4;

		/// <summary>
		/// Array of XInput controllers for the 4 possible slots.
		/// </summary>
		private readonly Controller[] _controllers;

		/// <summary>
		/// Initializes the XInput state reader with controllers for all 4 slots.
		/// </summary>
		public StatesXInput()
		{
			_controllers = new Controller[MaxControllers];
			for (int i = 0; i < MaxControllers; i++)
			{
				_controllers[i] = new Controller((UserIndex)i);
			}
		}

		#endregion

		#region State Retrieval Methods

		/// <summary>
		/// Returns the current state of an XInput device by slot index.
		/// </summary>
		/// <param name="slotIndex">XInput slot index (0-3)</param>
		/// <returns>XInput State structure or null if read failed or controller disconnected</returns>
		/// <remarks>
		/// This method reads the current input state from an XInput controller slot.
		/// 
		/// XInput Slot Mapping:
		/// • Slot 0 = UserIndex.One (Player 1)
		/// • Slot 1 = UserIndex.Two (Player 2)
		/// • Slot 2 = UserIndex.Three (Player 3)
		/// • Slot 3 = UserIndex.Four (Player 4)
		/// 
		/// Unlike DirectInput, XInput does not require device acquisition.
		/// The controller is automatically ready for reading if connected.
		/// 
		/// State Structure Contents:
		/// • PacketNumber: Increments when any input changes
		/// • Gamepad.Buttons: Button flags (A, B, X, Y, LB, RB, Back, Start, etc.)
		/// • Gamepad.LeftThumbX/Y: Left stick axes (-32768 to 32767)
		/// • Gamepad.RightThumbX/Y: Right stick axes (-32768 to 32767)
		/// • Gamepad.LeftTrigger: Left trigger (0-255)
		/// • Gamepad.RightTrigger: Right trigger (0-255)
		/// </remarks>
		public State? GetXInputDeviceState(XInputDeviceInfo xiDeviceInfo)
		{
			if (xiDeviceInfo == null)
				return null;

			int slotIndex = xiDeviceInfo.SlotIndex;

			if (slotIndex < 0 || slotIndex >= MaxControllers)
			{
				Debug.WriteLine($"StatesXInput: Invalid slot index {slotIndex}. Must be 0-3.");
				return null;
			}

			try
			{
				var controller = _controllers[slotIndex];

				// Read XInput state
				State state;
				bool isConnected = controller.GetState(out state);

				if (!isConnected)
				{
					// Controller is not connected to this slot
					return null;
				}

				return state;
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"StatesXInput: Error reading state for slot {slotIndex}: {ex.Message}");
				return null;
			}
		}

		#endregion

		#region Connection Status Methods

		/// <summary>
		/// Checks if a controller is connected to the specified slot.
		/// </summary>
		/// <param name="slotIndex">XInput slot index (0-3)</param>
		/// <returns>True if controller is connected, false otherwise</returns>
		/// <remarks>
		/// This method performs a lightweight connection check without reading full state.
		/// Useful for detecting controller presence before attempting state reads.
		/// </remarks>
		public bool IsControllerConnected(int slotIndex)
		{
			if (slotIndex < 0 || slotIndex >= MaxControllers)
				return false;

			try
			{
				var controller = _controllers[slotIndex];
				return controller.IsConnected;
			}
			catch
			{
				return false;
			}
		}

		/// <summary>
		/// Checks if a controller is connected to the specified UserIndex.
		/// </summary>
		/// <param name="userIndex">XInput UserIndex (One, Two, Three, Four)</param>
		/// <returns>True if controller is connected, false otherwise</returns>
		public bool IsControllerConnected(UserIndex userIndex)
		{
			return IsControllerConnected((int)userIndex);
		}

		/// <summary>
		/// Gets the number of currently connected XInput controllers.
		/// </summary>
		/// <returns>Number of connected controllers (0-4)</returns>
		public int GetConnectedControllerCount()
		{
			int count = 0;
			for (int i = 0; i < MaxControllers; i++)
			{
				if (IsControllerConnected(i))
					count++;
			}
			return count;
		}

		#endregion


		#region Diagnostic Methods

		/// <summary>
		/// Gets diagnostic information about all XInput slots.
		/// </summary>
		/// <returns>String containing connection status for all 4 slots</returns>
		/// <remarks>
		/// Provides a quick overview of which XInput slots have controllers connected.
		/// Useful for debugging and system diagnostics.
		/// </remarks>
		public string GetDiagnosticInfo()
		{
			var info = new System.Text.StringBuilder();
			info.AppendLine("=== XInput Slot Status ===");

			for (int i = 0; i < MaxControllers; i++)
			{
				bool connected = IsControllerConnected(i);
				string status = connected ? "Connected" : "Empty";
				info.AppendLine($"Slot {i + 1} (UserIndex.{(UserIndex)i}): {status}");

				if (connected)
				{
					// Create a temporary XInputDeviceInfo for diagnostic purposes
					var tempDevice = new XInputDeviceInfo { SlotIndex = i };
					var state = GetXInputDeviceState(tempDevice);
					if (state.HasValue)
					{
						info.AppendLine($"  PacketNumber: {state.Value.PacketNumber}");
					}
				}
			}

			info.AppendLine($"\nTotal Connected: {GetConnectedControllerCount()}/4");
			return info.ToString();
		}

		#endregion
	}
}
