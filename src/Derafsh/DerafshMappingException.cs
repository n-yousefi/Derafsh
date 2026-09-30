namespace Derafsh;

/// <summary>Thrown when an object graph contains an invalid or unsupported Derafsh mapping.</summary>
public sealed class DerafshMappingException : InvalidOperationException
{
    /// <summary>Initializes a new mapping exception.</summary>
    public DerafshMappingException(string message) : base(message) { }

    /// <summary>Initializes a new mapping exception with an inner exception.</summary>
    public DerafshMappingException(string message, Exception innerException) : base(message, innerException) { }
}
