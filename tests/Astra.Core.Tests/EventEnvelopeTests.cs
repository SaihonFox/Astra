namespace Astra.Core.Tests;

public sealed class EventEnvelopeTests
{
	private sealed class FixedClock(DateTimeOffset utcNow) : IClock
	{
		public DateTimeOffset UtcNow { get; } = utcNow;
	}

	[Fact]
	public void CreatePopulatesMetadataAndNormalizesTimestamp()
	{
		var localTime = new DateTimeOffset(2026, 11, 18, 12, 30, 0, TimeSpan.FromHours(3));
		var taskId = Guid.NewGuid();

		var envelope = EventEnvelope.Create(
			"payload",
			"test.event",
			new FixedClock(localTime),
			taskId,
			2);

		Assert.Equal("test.event", envelope.EventType);
		Assert.Equal(2, envelope.SchemaVersion);
		Assert.Equal(taskId, envelope.TaskId);
		Assert.Equal("payload", envelope.Payload);
		Assert.Equal(localTime.ToUniversalTime(), envelope.Timestamp);
		Assert.Equal(TimeSpan.Zero, envelope.Timestamp.Offset);
		Assert.NotEqual(Guid.Empty, envelope.EventId);
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public void CreateRejectsEmptyEventType(string eventType)
	{
		var clock = new FixedClock(DateTimeOffset.UtcNow);

		Assert.Throws<ArgumentException>(
			() => EventEnvelope.Create("payload", eventType, clock));
	}

	[Fact]
	public void CreateRejectsNonPositiveSchemaVersion()
	{
		var clock = new FixedClock(DateTimeOffset.UtcNow);

		Assert.Throws<ArgumentOutOfRangeException>(
			() => EventEnvelope.Create("payload", "test.event", clock, schemaVersion: 0));
	}

	[Fact]
	public void CreateRejectsNullPayload()
	{
		var clock = new FixedClock(DateTimeOffset.UtcNow);

		Assert.Throws<ArgumentNullException>(
			() => EventEnvelope.Create<string>(null!, "test.event", clock));
	}
}
