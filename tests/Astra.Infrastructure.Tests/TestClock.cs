using Astra.Core;

namespace Astra.Infrastructure.Tests;

internal sealed class TestClock(DateTimeOffset utcNow) : IClock
{
	public DateTimeOffset UtcNow { get; set; } = utcNow;

	public void Advance(TimeSpan delta) => UtcNow = UtcNow.Add(delta);
}
