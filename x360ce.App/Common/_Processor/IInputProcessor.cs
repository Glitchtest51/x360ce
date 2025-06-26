using x360ce.Engine;
//using x360ce.Engine.Data;

namespace x360ce.App.DInput
{
    /// <summary>
    /// Interface for input processors that handle different input methods (DirectInput, XInput, RawInput, GamingInput).
    /// </summary>
    public interface IInputProcessor
    {
        /// <summary>
        /// Gets the input method supported by this processor.
        /// </summary>
        InputMethod SupportedMethod { get; }

        /*

        /// <summary>
        /// Determines if this processor can handle the specified device.
        /// </summary>
        /// <param name="device">The user device to check</param>
        /// <returns>True if the processor can handle this device, false otherwise</returns>
        bool CanProcess(UserDevice device);

        /// <summary>
        /// Reads the current state from the device using the processor's input method.
        /// </summary>
        /// <param name="device">The device to read from</param>
        /// <returns>CustomDiState representing the current controller state</returns>
        /// <exception cref="InputMethodException">Thrown when the input method encounters errors</exception>
        CustomDiState ReadState(UserDevice device);

        /// <summary>
        /// Handles force feedback for the specified device.
        /// </summary>
        /// <param name="device">The device to send force feedback to</param>
        /// <param name="ffState">The force feedback state to apply</param>
        void HandleForceFeedback(UserDevice device, Engine.ForceFeedbackState ffState);

        /// <summary>
        /// Validates if the device can be used with this input method.
        /// </summary>
        /// <param name="device">The device to validate</param>
        /// <returns>ValidationResult indicating compatibility and any limitations</returns>
        ValidationResult ValidateDevice(UserDevice device);

        */
    }
}
