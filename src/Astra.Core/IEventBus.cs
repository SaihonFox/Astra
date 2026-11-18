namespace Astra.Core;

/// <summary>
/// Publishes versioned events to subscribers inside the current process.
/// </summary>
/// <remarks>
/// Implementations must be thread-safe. Delivery is asynchronous and must not be treated as
/// durable; events are lost when the process exits.
/// </remarks>
public interface IEventBus
{
	/// <summary>
	/// Publishes an event envelope to all subscribers of <typeparamref name="TEvent"/>.
	/// </summary>
	/// <exception cref="ArgumentNullException"><paramref name="envelope"/> is <see langword="null"/>.</exception>
	/// <exception cref="OperationCanceledException">The operation was cancelled.</exception>
	ValueTask PublishAsync<TEvent>(
		EventEnvelope<TEvent> envelope,
		CancellationToken cancellationToken = default)
		where TEvent : notnull;

	/// <summary>
	/// Subscribes a handler to events of type <typeparamref name="TEvent"/>.
	/// </summary>
	/// <returns>A subscription that unsubscribes the handler when disposed.</returns>
	IDisposable Subscribe<TEvent>(
		Func<EventEnvelope<TEvent>, CancellationToken, ValueTask> handler)
		where TEvent : notnull;
}
