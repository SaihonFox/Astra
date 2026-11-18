using Astra.Core;

namespace Astra.Infrastructure.Events;

/// <summary>
/// Thread-safe in-process event bus with sequential, failure-isolated delivery.
/// </summary>
public sealed class InProcessEventBus : IEventBus
{
	private readonly object _gate = new();
	private readonly Dictionary<Type, List<object>> _handlers = [];

	/// <inheritdoc />
	public IDisposable Subscribe<TEvent>(
		Func<EventEnvelope<TEvent>, CancellationToken, ValueTask> handler)
		where TEvent : notnull
	{
		ArgumentNullException.ThrowIfNull(handler);

		lock (_gate)
		{
			if (!_handlers.TryGetValue(typeof(TEvent), out var handlers))
			{
				handlers = [];
				_handlers[typeof(TEvent)] = handlers;
			}

			handlers.Add(handler);
		}

		return new Subscription(this, typeof(TEvent), handler);
	}

	/// <inheritdoc />
	public async ValueTask PublishAsync<TEvent>(
		EventEnvelope<TEvent> envelope,
		CancellationToken cancellationToken = default)
		where TEvent : notnull
	{
		ArgumentNullException.ThrowIfNull(envelope);
		cancellationToken.ThrowIfCancellationRequested();

		Func<EventEnvelope<TEvent>, CancellationToken, ValueTask>[] snapshot;
		lock (_gate)
		{
			snapshot = _handlers.TryGetValue(typeof(TEvent), out var handlers)
				? [.. handlers.Cast<Func<EventEnvelope<TEvent>, CancellationToken, ValueTask>>()]
				: [];
		}

		if (snapshot.Length == 0)
		{
			return;
		}

		List<Exception>? failures = null;
		foreach (var handler in snapshot)
		{
			try
			{
				await handler(envelope, cancellationToken);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				throw;
			}
#pragma warning disable CA1031 // A failing subscriber must not prevent other subscribers from receiving the event.
			catch (Exception exception)
#pragma warning restore CA1031
			{
				(failures ??= []).Add(exception);
			}
		}

		if (failures is not null)
		{
			throw new AggregateException("One or more event handlers failed.", failures);
		}
	}

	private void Unsubscribe(Type eventType, object handler)
	{
		lock (_gate)
		{
			if (!_handlers.TryGetValue(eventType, out var handlers))
			{
				return;
			}

			handlers.Remove(handler);

			if (handlers.Count == 0)
			{
				_handlers.Remove(eventType);
			}
		}
	}

	private sealed class Subscription : IDisposable
	{
		private readonly object _handler;
		private readonly Type _eventType;
		private InProcessEventBus? _bus;

		public Subscription(InProcessEventBus bus, Type eventType, object handler)
		{
			_bus = bus;
			_eventType = eventType;
			_handler = handler;
		}

		public void Dispose()
		{
			var bus = Interlocked.Exchange(ref _bus, null);
			bus?.Unsubscribe(_eventType, _handler);
		}
	}
}
