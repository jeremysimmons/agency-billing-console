namespace Aib.Application.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
