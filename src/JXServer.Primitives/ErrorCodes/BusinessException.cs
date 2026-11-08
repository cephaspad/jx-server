namespace JXServer.Primitives.ErrorCodes;

/// <summary>
/// Represents a domain or business rule violation identified by a structured error code from <see cref="JXServerErrorCodes"/>.
/// </summary>
public sealed class BusinessException : Exception
{
    /// <summary>
    /// Gets the structured error code (e.g., <c>JXServer:Account:0001</c>).
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Gets optional structured details associated with the business error.
    /// </summary>
    public string? Details { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessException"/> class.
    /// </summary>
    /// <param name="code">The structured error code.</param>
    /// <param name="message">The human-readable error message.</param>
    /// <param name="details">Optional diagnostic details.</param>
    /// <param name="innerException">Optional inner exception.</param>
    public BusinessException(
        string code,
        string? message = null,
        string? details = null,
        Exception? innerException = null)
        : base(message ?? $"Business error occurred with code '{code}'.", innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code;
        Details = details;
    }
}
