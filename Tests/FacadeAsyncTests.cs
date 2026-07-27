namespace StockSharp.Tests;

[TestClass]
public class FacadeAsyncTests : BaseTestClass
{
	[TestMethod]
	[Timeout(15000, CooperativeCancellation = true)]
	public async Task ConnectSubscribeUnsubscribeDisconnect_AsyncFacade()
	{
		var secId = new SecurityId { SecurityCode = "BTCUSDT", BoardCode = "BINANCE" };

		var connector = new Connector();
		var adapter = new LiveFeedCryptoAdapter("Binance", [secId], connector.TransactionIdGenerator);
		connector.Adapter.InnerAdapters.Add(adapter);
		connector.Adapter.SecurityAdapterProvider.SetAdapter((secId, null), adapter);

		await connector.ConnectAsync(CancellationToken);
		connector.ConnectionState.AssertEqual(ConnectionStates.Connected);

		var security = new Security { Id = secId.ToStringId() };
		await connector.SendOutMessageAsync(security.ToMessage(), CancellationToken);

		var started = AsyncHelper.CreateTaskCompletionSource<bool>();
		var stopped = AsyncHelper.CreateTaskCompletionSource<bool>();

		var sub = new Subscription(DataType.Ticks, security);

		connector.SubscriptionStarted += s =>
		{
			if (ReferenceEquals(s, sub))
				started.TrySetResult(true);
		};

		connector.SubscriptionStopped += (s, _) =>
		{
			if (ReferenceEquals(s, sub))
				stopped.TrySetResult(true);
		};

		await connector.SubscribeAsync(sub, CancellationToken);
		await started.Task.WithCancellation(CancellationToken);

		await connector.UnSubscribeAsync(sub, CancellationToken);
		await stopped.Task.WithCancellation(CancellationToken);

		await connector.DisconnectAsync(CancellationToken);
		connector.ConnectionState.AssertEqual(ConnectionStates.Disconnected);
	}

	[TestMethod]
	[Timeout(15000, CooperativeCancellation = true)]
	public async Task ConnectAsync_WrongState_Throws()
	{
		var connector = new Connector();
		var adapter = new LiveFeedCryptoAdapter("Binance", [], connector.TransactionIdGenerator);
		connector.Adapter.InnerAdapters.Add(adapter);

		await connector.ConnectAsync(CancellationToken);

		var thrown = false;

		try
		{
			await connector.ConnectAsync(CancellationToken);
		}
		catch (InvalidOperationException)
		{
			thrown = true;
		}

		thrown.AssertTrue("second ConnectAsync in Connected state must throw");

		await connector.DisconnectAsync(CancellationToken);
	}
}
