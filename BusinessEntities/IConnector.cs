namespace StockSharp.BusinessEntities;

/// <summary>
/// The main interface providing the connection to the trading systems.
/// </summary>
public interface IConnector : IMessageTransport, IPersistable, ILogReceiver,
	IMarketDataProvider, ITransactionProvider, ISecurityProvider,
	ISubscriptionProvider, ITimeProvider,
	IPortfolioProvider, IPositionProvider
{
	/// <summary>
	/// Connected.
	/// </summary>
	event Action Connected;

	/// <summary>
	/// Disconnected.
	/// </summary>
	event Action Disconnected;

	/// <summary>
	/// Connection error (for example, the connection was aborted by server).
	/// </summary>
	event Action<Exception> ConnectionError;

	/// <summary>
	/// Connected.
	/// </summary>
	event Action<IMessageAdapter> ConnectedEx;

	/// <summary>
	/// Disconnected.
	/// </summary>
	event Action<IMessageAdapter> DisconnectedEx;

	/// <summary>
	/// Connection error (for example, the connection was aborted by server).
	/// </summary>
	event Action<IMessageAdapter, Exception> ConnectionErrorEx;

	/// <summary>
	/// Connection lost.
	/// </summary>
	event Action<IMessageAdapter> ConnectionLost;

	/// <summary>
	/// Connection restored.
	/// </summary>
	event Action<IMessageAdapter> ConnectionRestored;

	/// <summary>
	/// Data process error.
	/// </summary>
	event Action<Exception> Error;

	/// <summary>
	/// Change password result.
	/// </summary>
	event Action<long, Exception> ChangePasswordResult;

	/// <summary>
	/// List of all exchange boards, for which instruments are loaded <see cref="Securities"/>.
	/// </summary>
	IEnumerable<ExchangeBoard> ExchangeBoards { get; }

	/// <summary>
	/// List of all loaded instruments. It should be called after event <see cref="ISubscriptionProvider.SecurityReceived"/> arisen. Otherwise the empty set will be returned.
	/// </summary>
	IEnumerable<Security> Securities { get; }

	/// <summary>
	/// Determines this connector is ready for establish connection.
	/// </summary>
	bool CanConnect { get; }

	/// <summary>
	/// Connection state.
	/// </summary>
	ConnectionStates ConnectionState { get; }

	/// <summary>
	/// Transactional adapter.
	/// </summary>
	IMessageAdapter TransactionAdapter { get; }

	/// <summary>
	/// Market-data adapter.
	/// </summary>
	IMessageAdapter MarketDataAdapter { get; }

	/// <summary>
	/// Connect to trading system.
	/// </summary>
	[Obsolete("Use ConnectAsync instead.")]
	void Connect();

	/// <summary>
	/// Disconnect from trading system.
	/// </summary>
	[Obsolete("Use DisconnectAsync instead.")]
	void Disconnect();

	/// <summary>
	/// Connect to trading system and await the result: completes when
	/// <see cref="Connected"/> or fails when <see cref="ConnectionError"/> is raised.
	/// </summary>
	/// <param name="cancellationToken"><see cref="CancellationToken"/>.</param>
	/// <returns><see cref="ValueTask"/>.</returns>
	async ValueTask ConnectAsync(CancellationToken cancellationToken)
	{
		if (ConnectionState != ConnectionStates.Disconnected)
			throw new InvalidOperationException($"State is {ConnectionState}.");

		var tcs = AsyncHelper.CreateTaskCompletionSource<bool>();

		using var _ = cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));

		void onConnected() => tcs.TrySetResult(true);
		void onError(Exception ex) => tcs.TrySetException(ex);

		Connected += onConnected;
		ConnectionError += onError;

		try
		{
#pragma warning disable CS0618 // fallback via the deprecated sync method
			Connect();
#pragma warning restore CS0618

			await tcs.Task;
		}
		finally
		{
			Connected -= onConnected;
			ConnectionError -= onError;
		}
	}

	/// <summary>
	/// Disconnect from trading system and await the result: completes when
	/// <see cref="Disconnected"/> or fails when <see cref="ConnectionError"/> is raised.
	/// </summary>
	/// <param name="cancellationToken"><see cref="CancellationToken"/>.</param>
	/// <returns><see cref="ValueTask"/>.</returns>
	async ValueTask DisconnectAsync(CancellationToken cancellationToken)
	{
		if (ConnectionState != ConnectionStates.Connected)
			throw new InvalidOperationException($"State is {ConnectionState}.");

		var tcs = AsyncHelper.CreateTaskCompletionSource<bool>();

		using var _ = cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));

		void onDisconnected() => tcs.TrySetResult(true);
		void onError(Exception ex) => tcs.TrySetException(ex);

		Disconnected += onDisconnected;
		ConnectionError += onError;

		try
		{
#pragma warning disable CS0618 // fallback via the deprecated sync method
			Disconnect();
#pragma warning restore CS0618

			await tcs.Task;
		}
		finally
		{
			Disconnected -= onDisconnected;
			ConnectionError -= onError;
		}
	}

	/// <summary>
	/// Get <see cref="SecurityId"/>.
	/// </summary>
	/// <param name="security">Security.</param>
	/// <returns>Security ID.</returns>
	SecurityId GetSecurityId(Security security);

	/// <summary>
	/// Get security by identifier.
	/// </summary>
	/// <param name="securityId">Security ID.</param>
	/// <returns>Security.</returns>
	[Obsolete("Use GetSecurityAsync instead.")]
	Security GetSecurity(SecurityId securityId);

	/// <summary>
	/// Get security by identifier.
	/// </summary>
	/// <param name="securityId">Security ID.</param>
	/// <param name="cancellationToken"><see cref="CancellationToken"/></param>
	/// <returns>Security.</returns>
	ValueTask<Security> GetSecurityAsync(SecurityId securityId, CancellationToken cancellationToken);

	/// <summary>
	/// Send outgoing message.
	/// </summary>
	/// <param name="message">Message.</param>
	[Obsolete("Use SendOutMessageAsync instead.")]
	void SendOutMessage(Message message);

	/// <summary>
	/// Send outgoing message.
	/// </summary>
	/// <param name="message">Message.</param>
	/// <param name="cancellationToken"><see cref="CancellationToken"/></param>
	ValueTask SendOutMessageAsync(Message message, CancellationToken cancellationToken);
}