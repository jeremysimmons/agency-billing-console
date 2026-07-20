namespace Aib.Domain;

/// <summary>Raised when a domain invariant is violated (maps to HTTP 400/409).</summary>
public sealed class DomainException(string message) : Exception(message);
