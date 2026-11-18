using Astra.Core;

namespace Astra.Infrastructure.Time;

/// <summary>
/// Clock backed by the system UTC time.
/// </summary>
public sealed class SystemClock : IClock
{
	/// <inheritdoc />
	public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
