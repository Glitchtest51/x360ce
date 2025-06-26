namespace x360ce.App.DInput
{
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
