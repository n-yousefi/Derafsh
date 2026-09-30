namespace Derafsh;

/// <summary>Thrown when a graph-persistence operation cannot be completed safely.</summary>
public sealed class DerafshPersistenceException : InvalidOperationException
{
    /// <summary>Initializes a new persistence exception.</summary>
    public DerafshPersistenceException(string message) : base(message) { }

    /// <summary>Initializes a new persistence exception with an inner exception.</summary>
    public DerafshPersistenceException(string message, Exception innerException) : base(message, innerException) { }
}
