namespace Astra.Core;

/// <summary>
/// Provides the current time in a way that can be controlled in tests.
/// </summary>
public interface IClock
{
	/// <summary>Gets the current UTC time.</summary>
	DateTimeOffset UtcNow { get; }
}
