using Astra.Core;
using Astra.Infrastructure.Events;

namespace Astra.Infrastructure.Tests;

public sealed class InProcessEventBusTests
{
	private sealed class FixedClock : IClock
	{
		public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
	}

	[Fact]
	public async Task PublishDeliversToAllSubscribersInSubscriptionOrder()
	{
		var bus = new InProcessEventBus();
		var received = new List<string>();

		using var first = bus.Subscribe<string>((envelope, _) =>
		{
			received.Add($"first:{envelope.Payload}");
			return ValueTask.CompletedTask;
		});
		using var second = bus.Subscribe<string>((envelope, _) =>
		{
			received.Add($"second:{envelope.Payload}");
			return ValueTask.CompletedTask;
		});

		await bus.PublishAsync(CreateEnvelope("value"));

		Assert.Equal(["first:value", "second:value"], received);
	}

	[Fact]
	public async Task DisposedSubscriptionStopsReceivingEvents()
	{
		var bus = new InProcessEventBus();
		var received = 0;

		var subscription = bus.Subscribe<string>((_, _) =>
		{
			received++;
			return ValueTask.CompletedTask;
		});
		subscription.Dispose();
		subscription.Dispose();

		await bus.PublishAsync(CreateEnvelope());

		Assert.Equal(0, received);
	}

	[Fact]
	public async Task FailingHandlerDoesNotBlockOtherHandlersAndFailuresAreAggregated()
	{
		var bus = new InProcessEventBus();
		var healthyHandlerCalled = false;

		using var failing = bus.Subscribe<string>(
			(_, _) => throw new InvalidOperationException("boom"));
		using var healthy = bus.Subscribe<string>((_, _) =>
		{
			healthyHandlerCalled = true;
			return ValueTask.CompletedTask;
		});

		var exception = await Assert.ThrowsAsync<AggregateException>(
			async () => await bus.PublishAsync(CreateEnvelope()));

		Assert.True(healthyHandlerCalled);
		Assert.Single(exception.InnerExceptions);
		Assert.IsType<InvalidOperationException>(exception.InnerExceptions[0]);
	}

	[Fact]
	public async Task PublishWithoutSubscribersCompletes()
	{
		var bus = new InProcessEventBus();

		await bus.PublishAsync(CreateEnvelope());
	}

	[Fact]
	public async Task PublishHonorsCancellationBeforeDelivery()
	{
		var bus = new InProcessEventBus();
		var called = false;

		using var subscription = bus.Subscribe<string>((_, _) =>
		{
			called = true;
			return ValueTask.CompletedTask;
		});

		using var cancellation = new CancellationTokenSource();
		await cancellation.CancelAsync();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			async () => await bus.PublishAsync(CreateEnvelope(), cancellation.Token));
		Assert.False(called);
	}

	private static EventEnvelope<string> CreateEnvelope(string payload = "payload") =>
		EventEnvelope.Create(payload, "test.event", new FixedClock());
}
