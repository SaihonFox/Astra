namespace Astra.Core;

/// <summary>
/// Immutable event envelope shared by the in-process bus and future serialized boundaries.
/// </summary>
/// <typeparam name="TEvent">Type of the event payload.</typeparam>
public sealed record EventEnvelope<TEvent>
	where TEvent : notnull
{
	/// <summary>Gets the unique, sortable event identifier.</summary>
	public required Guid EventId { get; init; }

	/// <summary>Gets the related task identifier, when the event belongs to a task.</summary>
	public Guid? TaskId { get; init; }

	/// <summary>Gets the UTC time at which the event was created.</summary>
	public required DateTimeOffset Timestamp { get; init; }

	/// <summary>Gets the stable event type name.</summary>
	public required string EventType { get; init; }

	/// <summary>Gets the payload schema version; starts at 1.</summary>
	public required int SchemaVersion { get; init; }

	/// <summary>Gets the event payload.</summary>
	public required TEvent Payload { get; init; }
}

/// <summary>
/// Factory for typed event envelopes.
/// </summary>
public static class EventEnvelope
{
	/// <summary>
	/// Creates an envelope with a generated event ID and the current clock time.
	/// </summary>
	/// <param name="payload">Event payload. Must not be <see langword="null"/>.</param>
	/// <param name="eventType">Stable event type name. Must not be empty.</param>
	/// <param name="clock">Clock used for the timestamp.</param>
	/// <param name="taskId">Optional related task identifier.</param>
	/// <param name="schemaVersion">Payload schema version. Must be positive.</param>
	/// <exception cref="ArgumentException"><paramref name="eventType"/> is empty or whitespace.</exception>
	/// <exception cref="ArgumentNullException"><paramref name="payload"/> or <paramref name="clock"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="schemaVersion"/> is not positive.</exception>
	public static EventEnvelope<TEvent> Create<TEvent>(
		TEvent payload,
		string eventType,
		IClock clock,
		Guid? taskId = null,
		int schemaVersion = 1)
		where TEvent : notnull
	{
		ArgumentNullException.ThrowIfNull(payload);
		ArgumentNullException.ThrowIfNull(clock);
		ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(schemaVersion);

		return new EventEnvelope<TEvent>
		{
			EventId = Guid.CreateVersion7(),
			TaskId = taskId,
			Timestamp = clock.UtcNow.ToUniversalTime(),
			EventType = eventType,
			SchemaVersion = schemaVersion,
			Payload = payload,
		};
	}
}
