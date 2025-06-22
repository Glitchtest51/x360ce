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

	/// <summary>
	/// Represents the result of validating a device with an input method.
	/// </summary>
	public class ValidationResult
	{
		/// <summary>
		/// Gets the validation status.
		/// </summary>
		public ValidationStatus Status { get; private set; }

		/// <summary>
		/// Gets the validation message providing details about the result.
		/// </summary>
		public string Message { get; private set; }

		/// <summary>
		/// Gets whether the validation passed (Success or Warning).
		/// </summary>
		public bool IsValid => Status == ValidationStatus.Success || Status == ValidationStatus.Warning;

		private ValidationResult(ValidationStatus status, string message)
		{
			Status = status;
			Message = message ?? string.Empty;
		}

		/// <summary>
		/// Creates a successful validation result.
		/// </summary>
		/// <param name="message">Optional success message</param>
		/// <returns>Success validation result</returns>
		public static ValidationResult Success(string message = null)
		{
			return new ValidationResult(ValidationStatus.Success, message);
		}

		/// <summary>
		/// Creates a warning validation result.
		/// </summary>
		/// <param name="message">Warning message explaining the limitation</param>
		/// <returns>Warning validation result</returns>
		public static ValidationResult Warning(string message)
		{
			return new ValidationResult(ValidationStatus.Warning, message);
		}

		/// <summary>
		/// Creates an error validation result.
		/// </summary>
		/// <param name="message">Error message explaining why the device cannot be used</param>
		/// <returns>Error validation result</returns>
		public static ValidationResult Error(string message)
		{
			return new ValidationResult(ValidationStatus.Error, message);
		}
	}

	/// <summary>
	/// Validation status levels.
	/// </summary>
	public enum ValidationStatus
	{
		/// <summary>
		/// Device is fully compatible with the input method.
		/// </summary>
		Success,

		/// <summary>
		/// Device works but has limitations with this input method.
		/// </summary>
		Warning,

		/// <summary>
		/// Device cannot be used with this input method.
		/// </summary>
		Error
	}
}
