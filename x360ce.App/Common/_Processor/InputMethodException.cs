using x360ce.Engine;
using x360ce.Engine.Data;

namespace x360ce.App.DInput
{
    /// <summary>
    /// Exception thrown when an input method encounters an error specific to its limitations.
    /// </summary>
    public class InputMethodException : System.Exception
    {
        /// <summary>
        /// Gets the input method that caused the exception.
        /// </summary>
        public InputMethod InputMethod { get; }

        /// <summary>
        /// Gets the device that was being processed when the error occurred.
        /// </summary>
        public UserDevice Device { get; }

        /// <summary>
        /// Initializes a new instance of the InputMethodException class.
        /// </summary>
        /// <param name="inputMethod">The input method that caused the exception</param>
        /// <param name="device">The device being processed</param>
        /// <param name="message">The error message</param>
        public InputMethodException(InputMethod inputMethod, UserDevice device, string message)
            : base(message)
        {
            InputMethod = inputMethod;
            Device = device;
        }

        /// <summary>
        /// Initializes a new instance of the InputMethodException class with an inner exception.
        /// </summary>
        /// <param name="inputMethod">The input method that caused the exception</param>
        /// <param name="device">The device being processed</param>
        /// <param name="message">The error message</param>
        /// <param name="innerException">The exception that caused this exception</param>
        public InputMethodException(InputMethod inputMethod, UserDevice device, string message, System.Exception innerException)
            : base(message, innerException)
        {
            InputMethod = inputMethod;
            Device = device;
        }
    }
}
