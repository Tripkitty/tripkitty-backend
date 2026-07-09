namespace Tripkitty.Domain.Exceptions;

public class DomainException(string code, string message, string? field = null, object? details = null) : Exception(message)
{
    public string Code { get; } = code;
    public string? Field { get; } = field;
    public object? Details { get; } = details;
}
