namespace StockSharp.Tests;

[TestClass]
public class AsyncEventExtensionsTests : BaseTestClass
{
	private static Message CreateMessage() => new TimeMessage();

	[TestMethod]
	public async Task Null_Completes()
	{
		Func<Message, CancellationToken, ValueTask> handler = null;

		await handler.InvokeAllAsync(CreateMessage(), CancellationToken);
	}

	[TestMethod]
	public async Task SingleHandler_Invoked()
	{
		var ran = false;

		Func<Message, CancellationToken, ValueTask> handler = (m, t) =>
		{
			ran = true;
			return default;
		};

		await handler.InvokeAllAsync(CreateMessage(), CancellationToken);

		ran.AssertTrue();
	}

	[TestMethod]
	public async Task FirstHandler_IsAwaited_NotOnlyLast()
	{
		var gate = AsyncHelper.CreateTaskCompletionSource<bool>();
		var firstDone = false;
		var secondRan = false;

		Func<Message, CancellationToken, ValueTask> handler = async (m, t) =>
		{
			await gate.Task;
			firstDone = true;
		};

		handler += (m, t) =>
		{
			secondRan = true;
			return default;
		};

		var invocation = handler.InvokeAllAsync(CreateMessage(), CancellationToken).AsTask();

		await Task.Delay(50, CancellationToken);
		invocation.IsCompleted.AssertFalse("must wait for the first handler, not only the last");
		secondRan.AssertFalse("second handler must not start before the first completes");

		gate.TrySetResult(true);
		await invocation.WithCancellation(CancellationToken);

		firstDone.AssertTrue();
		secondRan.AssertTrue();
	}

	[TestMethod]
	public async Task FirstHandlerException_Propagates()
	{
		Func<Message, CancellationToken, ValueTask> handler = async (m, t) =>
		{
			await Task.Yield();
			throw new InvalidOperationException("boom");
		};

		handler += (m, t) => default;

		var thrown = false;

		try
		{
			await handler.InvokeAllAsync(CreateMessage(), CancellationToken);
		}
		catch (InvalidOperationException)
		{
			thrown = true;
		}

		thrown.AssertTrue("exception from a non-last handler must propagate");
	}

	[TestMethod]
	public async Task Handlers_RunSequentially_InSubscriptionOrder()
	{
		var order = new List<int>();

		Func<Message, CancellationToken, ValueTask> handler = async (m, t) =>
		{
			await Task.Yield();
			lock (order)
				order.Add(1);
		};

		handler += async (m, t) =>
		{
			await Task.Yield();
			lock (order)
				order.Add(2);
		};

		handler += (m, t) =>
		{
			lock (order)
				order.Add(3);

			return default;
		};

		await handler.InvokeAllAsync(CreateMessage(), CancellationToken);

		order.Count.AssertEqual(3);
		order[0].AssertEqual(1);
		order[1].AssertEqual(2);
		order[2].AssertEqual(3);
	}

	[TestMethod]
	[Timeout(15000, CooperativeCancellation = true)]
	public async Task Channel_WaitsAllSubscribers_PerMessage()
	{
		using var channel = new InMemoryMessageChannel(new MessageByOrderQueue(), "TestChannel", _ => { });

		var gate = AsyncHelper.CreateTaskCompletionSource<bool>();
		var sub1Entered = 0;
		var sub2Seen = 0;

		channel.NewOutMessageAsync += async (m, t) =>
		{
			Interlocked.Increment(ref sub1Entered);
			await gate.Task;
		};

		channel.NewOutMessageAsync += (m, t) =>
		{
			Interlocked.Increment(ref sub2Seen);
			return default;
		};

		channel.Open();
		await channel.SendInMessageAsync(new TimeMessage(), CancellationToken);
		await channel.SendInMessageAsync(new TimeMessage(), CancellationToken);

		await Task.Delay(200, CancellationToken);
		Volatile.Read(ref sub1Entered).AssertEqual(1, "second message must not be dispatched while the first subscriber still processes the first");

		gate.TrySetResult(true);

		while (Volatile.Read(ref sub2Seen) < 2)
			await Task.Delay(10, CancellationToken);

		Volatile.Read(ref sub1Entered).AssertEqual(2);
	}
}
