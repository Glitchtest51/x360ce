namespace x360ce.App.DInput
{
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
}
