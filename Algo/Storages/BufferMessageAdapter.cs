namespace StockSharp.Algo.Storages;

/// <summary>
/// Buffered message adapter.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="BufferMessageAdapter"/>.
/// </remarks>
/// <param name="innerAdapter">Underlying adapter.</param>
/// <param name="settings">Storage settings.</param>
/// <param name="buffer">Storage buffer.</param>
/// <param name="snapshotRegistry">Snapshot storage registry.</param>
public class BufferMessageAdapter(IMessageAdapter innerAdapter, StorageCoreSettings settings, IStorageBuffer buffer, ISnapshotRegistry snapshotRegistry) : MessageAdapterWrapper(innerAdapter)
{
	private readonly SynchronizedSet<long> _orderStatusIds = [];
	private readonly SynchronizedDictionary<long, long> _cancellationTransactions = [];
	private readonly SynchronizedDictionary<long, long> _replaceTransactions = [];
	private readonly SynchronizedDictionary<long, long> _replaceTransactionsByTransId = [];

	/// <summary>
	/// Storage buffer.
	/// </summary>
	public IStorageBuffer Buffer { get; } = buffer ?? throw new ArgumentNullException(nameof(buffer));

	/// <summary>
	/// Snapshot storage registry.
	/// </summary>
	public ISnapshotRegistry SnapshotRegistry { get; } = snapshotRegistry;// ?? throw new ArgumentNullException(nameof(snapshotRegistry));

	/// <summary>
	/// Storage settings.
	/// </summary>
	public StorageCoreSettings Settings { get; } = settings ?? throw new ArgumentNullException(nameof(settings));

	/// <summary>
	/// To reset the state.
	/// </summary>
	private void Reset()
	{
		_orderStatusIds.Clear();
		_cancellationTransactions.Clear();
		_replaceTransactions.Clear();
		_replaceTransactionsByTransId.Clear();
		StopStorageTimer();
	}

	private ISnapshotStorage<TKey, TMessage> GetSnapshotStorage<TKey, TMessage>(DataType dataType)
		where TMessage : Message
		=> (ISnapshotStorage<TKey, TMessage>)SnapshotRegistry.GetSnapshotStorage(dataType);

	private ISnapshotStorage<SecurityId, TMessage> GetSnapshotStorage<TMessage>(DataType dataType)
		where TMessage : Message
		=> GetSnapshotStorage<SecurityId, TMessage>(dataType);

	/// <inheritdoc />
	protected override async ValueTask OnSendInMessageAsync(Message message, CancellationToken cancellationToken)
	{
		switch (message.Type)
		{
			case MessageTypes.Reset:
				Reset();
				Buffer.ProcessInMessage(message);
				break;

			case MessageTypes.Connect:
				//Buffer.Enabled = CanAutoStorage && (_storageProcessor.StorageRegistry != null || SupportBuffer);
				StartStorageTimer();
				break;

			case MessageTypes.Disconnect:
				StopStorageTimer();
				break;

			case MessageTypes.OrderStatus:
			{
				if (message.Adapter != null && message.Adapter != this)
					break;

				if (Buffer.EnabledTransactions)
					message = await ProcessOrderStatusAsync((OrderStatusMessage)message, cancellationToken);

				break;
			}

			case MessageTypes.OrderRegister:
			{
				if (Buffer.EnabledTransactions)
					Buffer.ProcessInMessage(message);

				break;
			}
			case MessageTypes.OrderReplace:
			{
				if (Buffer.EnabledTransactions)
				{
					var replaceMsg = (OrderReplaceMessage)message;

					// can be looped back from offline
					_replaceTransactions.TryAdd(replaceMsg.TransactionId, replaceMsg.OriginalTransactionId);

					Buffer.ProcessInMessage(replaceMsg);
				}

				break;
			}
			case MessageTypes.OrderCancel:
			{
				if (Buffer.EnabledTransactions)
				{
					var cancelMsg = (OrderCancelMessage)message;

					// can be looped back from offline
					_cancellationTransactions.TryAdd(cancelMsg.TransactionId, cancelMsg.OriginalTransactionId);
				}

				break;
			}
			case MessageTypes.MarketData:
				await ProcessMarketDataAsync((MarketDataMessage)message, cancellationToken);
				break;
		}

		if (message == null)
			return;

		await base.OnSendInMessageAsync(message, cancellationToken);
	}

	private async ValueTask ProcessMarketDataAsync(MarketDataMessage message, CancellationToken cancellationToken)
	{
		if (message is null)
			throw new ArgumentNullException(nameof(message));

		Buffer.ProcessInMessage(message);

		if (message.IsSubscribe && message.From == null && message.To == null && Settings.IsMode(StorageModes.Snapshot))
		{
			async ValueTask SendSnapshotAsync<TMessage>(TMessage msg)
				where TMessage : Message, ISubscriptionIdMessage
			{
				msg.SetSubscriptionIds(subscriptionId: message.TransactionId);
				await RaiseNewOutMessageAsync(msg, cancellationToken);
			}

			if (message.DataType2 == DataType.Level1)
			{
				var l1Storage = GetSnapshotStorage<Level1ChangeMessage>(message.DataType2);

				if (message.SecurityId == default)
				{
					foreach (var msg in l1Storage.GetAll())
						await SendSnapshotAsync(msg);
				}
				else
				{
					var level1Msg = l1Storage.Get(message.SecurityId);

					if (level1Msg != null)
					{
						//SendReply();
						await SendSnapshotAsync(level1Msg);
					}
				}
			}
			else if (message.DataType2 == DataType.MarketDepth)
			{
				var quotesStorage = GetSnapshotStorage<QuoteChangeMessage>(message.DataType2);

				if (message.SecurityId == default)
				{
					foreach (var msg in quotesStorage.GetAll())
						await SendSnapshotAsync(msg);
				}
				else
				{
					var quotesMsg = quotesStorage.Get(message.SecurityId);

					if (quotesMsg != null)
					{
						//SendReply();
						await SendSnapshotAsync(quotesMsg);
					}
				}
			}
		}
	}

	/// <summary>
	/// Process <see cref="OrderStatusMessage"/>.
	/// </summary>
	/// <param name="message">A message requesting current registered orders and trades.</param>
	/// <param name="cancellationToken"><see cref="CancellationToken"/></param>
	/// <returns>A message requesting current registered orders and trades.</returns>
	private async ValueTask<OrderStatusMessage> ProcessOrderStatusAsync(OrderStatusMessage message, CancellationToken cancellationToken)
	{
		if (message == null)
			throw new ArgumentNullException(nameof(message));

		if (!message.IsSubscribe)
			return message;

		var transId = message.TransactionId;

		_orderStatusIds.Add(transId);

		if (!message.HasOrderId() && message.OriginalTransactionId == 0 /*&& Settings.DaysLoad > TimeSpan.Zero*/)
		{
			var from = message.From ?? CurrentTime.Date/* - Settings.DaysLoad*/;
			var to = message.To;

			if (Settings.IsMode(StorageModes.Snapshot))
			{
				var states = message.States.ToHashSet();

				var ordersIds = new HashSet<long>();

				var storage = GetSnapshotStorage<string, ExecutionMessage>(DataType.Transactions);

				foreach (var snapshot in storage.GetAll(from, to))
				{
					if (snapshot.HasOrderInfo)
					{
						if (!snapshot.IsMatch(message, states))
							continue;

						ordersIds.Add(snapshot.TransactionId);
					}
					else if (!ordersIds.Contains(snapshot.TransactionId))
						continue;

					snapshot.OriginalTransactionId = transId;
					snapshot.SetSubscriptionIds(subscriptionId: transId);
					await RaiseNewOutMessageAsync(snapshot, cancellationToken);

					from = snapshot.ServerTime;
				}

				if (from >= to)
					return null;

				// do not fill From field to avoid muptiple requests
				// in SubscriptionOnlineMessageAdapter
				//
				//message.From = from;
			}
			else if (Settings.IsMode(StorageModes.Incremental))
			{
				if (message.SecurityId != default)
				{
					// TODO restore last actual state from incremental messages

					//GetStorage<ExecutionMessage>(msg.SecurityId, ExecutionTypes.Transaction)
					//	.Load(from, to)
					//	.ForEach(RaiseStorageMessage);
				}
			}
		}

		return message;
	}

	/// <inheritdoc />
	protected override ValueTask OnInnerAdapterNewOutMessageAsync(Message message, CancellationToken cancellationToken)
	{
		Buffer.ProcessOutMessage(message);

		return base.OnInnerAdapterNewOutMessageAsync(message, cancellationToken);
	}

	private CancellationTokenSource _cts;
	private Task _storageTask;
	private readonly Lock _timerSync = new();

	// Batches taken from the buffer and not yet persisted. Buffer.GetXxx() reads and
	// clears in one step, so a save that throws leaves its messages owned by nobody:
	// they are already out of the buffer and never reached storage. These carry them
	// to the next cycle instead. Only the storage task touches them.
	private readonly Dictionary<SecurityId, List<ExecutionMessage>> _unsavedTicks = [];
	private readonly Dictionary<SecurityId, List<ExecutionMessage>> _unsavedOrderLog = [];
	private readonly Dictionary<SecurityId, List<QuoteChangeMessage>> _unsavedOrderBooks = [];
	private readonly Dictionary<SecurityId, List<Level1ChangeMessage>> _unsavedLevel1 = [];
	private readonly Dictionary<SecurityId, List<PositionChangeMessage>> _unsavedPositions = [];
	private readonly Dictionary<(SecurityId secId, DataType dataType), List<CandleMessage>> _unsavedCandles = [];
	private readonly List<NewsMessage> _unsavedNews = [];
	private readonly List<BoardStateMessage> _unsavedBoardStates = [];

	// A backlog that never drains would grow until the process dies, which is a worse
	// failure than the one this retry prevents. Past this many messages for one key the
	// oldest go, and the drop is logged rather than silent.
	private const int _maxUnsavedPerKey = 100_000;

	/// <summary>
	/// Merges what the previous cycle could not persist with what the buffer just handed
	/// over, then saves each key on its own.
	/// </summary>
	/// <remarks>
	/// A key whose save throws keeps its batch for the next cycle and the remaining keys
	/// still go out, where one throw previously abandoned every key behind it.
	/// </remarks>
	private async ValueTask SaveEachAsync<TKey, TMessage>(
		Dictionary<TKey, List<TMessage>> unsaved,
		IDictionary<TKey, IEnumerable<TMessage>> fresh,
		Func<TKey, IEnumerable<TMessage>, ValueTask> save,
		CancellationToken token)
		where TMessage : Message
	{
		foreach (var pair in fresh)
			Append(unsaved.SafeAdd(pair.Key), pair.Value, pair.Key);

		foreach (var key in unsaved.Keys.ToArray())
		{
			token.ThrowIfCancellationRequested();

			var batch = unsaved[key];

			if (batch.Count == 0)
			{
				unsaved.Remove(key);
				continue;
			}

			try
			{
				await save(key, batch);
				unsaved.Remove(key);
			}
			catch (Exception ex)
			{
				if (token.IsCancellationRequested)
					throw;

				// The batch stays in unsaved, so the next cycle retries it.
				LogError("Saving {0} failed, {1} message(s) held for retry: {2}", key, batch.Count, ex.Message);
			}
		}
	}

	private void Append<TMessage>(List<TMessage> batch, IEnumerable<TMessage> messages, object key)
	{
		batch.AddRange(messages);

		var excess = batch.Count - _maxUnsavedPerKey;

		if (excess <= 0)
			return;

		batch.RemoveRange(0, excess);
		LogWarning("Storage backlog for {0} passed {1}; dropped {2} oldest message(s).", key, _maxUnsavedPerKey, excess);
	}

	/// <summary>
	/// Start storage auto-save thread.
	/// </summary>
	private void StartStorageTimer()
	{
		using (_timerSync.EnterScope())
		{
			if (_cts != null || !Buffer.Enabled || Buffer.DisableStorageTimer)
				return;

			_cts = new();
			var token = _cts.Token;
			var interval = TimeSpan.FromSeconds(10);

			_storageTask = Task.Run(async () =>
			{
				while (!token.IsCancellationRequested)
				{
					try
					{
						var incremental = Settings.IsMode(StorageModes.Incremental);
						var snapshot = Settings.IsMode(StorageModes.Snapshot);

						await SaveEachAsync(_unsavedTicks, Buffer.GetTicks(), async (secId, messages) =>
						{
							if (incremental)
								await Settings.GetStorage<ExecutionMessage>(secId, DataType.Ticks).SaveAsync(messages, token);
						}, token);

						await SaveEachAsync(_unsavedOrderLog, Buffer.GetOrderLog(), async (secId, messages) =>
						{
							if (incremental)
								await Settings.GetStorage<ExecutionMessage>(secId, DataType.OrderLog).SaveAsync(messages, token);
						}, token);

						foreach (var pair in Buffer.GetTransactions())
						{
							var secId = pair.Key;

							// failed order's response doesn't contain sec id
							if (secId == default)
								continue;

							try
							{
								if (incremental)
									await Settings.GetStorage<ExecutionMessage>(secId, DataType.Transactions).SaveAsync(pair.Value, token);

								if (snapshot)
								{
									var snapshotStorage = GetSnapshotStorage<string, ExecutionMessage>(DataType.Transactions);

									foreach (var message in pair.Value)
									{
										// do not store cancellation commands into snapshot
										if (message.IsCancellation)
										{
											LogWarning("Cancellation transaction: {0}", message);
											continue;
										}

										var originTransId = message.OriginalTransactionId;

										if (originTransId == 0)
											continue;

										if (_cancellationTransactions.TryGetValue(originTransId, out var cancelledId))
										{
											// do not store cancellation errors
											if (!message.IsOk())
												continue;

											// override cancel trans id by original order's registration trans id
											originTransId = cancelledId;
										}
										else if (_orderStatusIds.Contains(originTransId))
										{
											// override status request trans id by original order's registration trans id
											originTransId = message.TransactionId;
										}
										else if (_replaceTransactions.TryGetAndRemove(originTransId, out var replacedId))
										{
											if (message.IsOk())
											{
												var replaced = (ExecutionMessage)snapshotStorage.Get(replacedId.To<string>());

												if (replaced == null)
													LogWarning("Replaced order {0} not found.", replacedId);
												else
												{
													if (replaced.OrderState != OrderStates.Done)
														replaced.OrderState = OrderStates.Done;
												}
											}
										}

										message.SecurityId = secId;

										if (message.TransactionId == 0)
											message.TransactionId = originTransId;

										message.OriginalTransactionId = 0;

										if (message.TransactionId != 0)
											SaveTransaction(snapshotStorage, message);
									}
								}
							}
							catch (Exception ex)
							{
								if (token.IsCancellationRequested)
									throw;

								// Not held for retry, unlike the other kinds: the snapshot path
								// consumes _replaceTransactions entries as it goes, so a second
								// pass over the same batch would take a different branch. What
								// this guard buys is that one security's failure costs its own
								// batch instead of abandoning every security behind it.
								LogError("Saving transactions for {0} failed, batch lost: {1}", secId, ex.Message);
							}
						}

						await SaveEachAsync(_unsavedOrderBooks, Buffer.GetOrderBooks(), async (secId, messages) =>
						{
							if (incremental)
								await Settings.GetStorage<QuoteChangeMessage>(secId, DataType.MarketDepth).SaveAsync(messages, token);

							if (snapshot)
							{
								var snapshotStorage = GetSnapshotStorage<QuoteChangeMessage>(DataType.MarketDepth);

								foreach (var message in messages)
									snapshotStorage.Update(message);
							}
						}, token);

						await SaveEachAsync(_unsavedLevel1, Buffer.GetLevel1(), async (secId, buffered) =>
						{
							var messages = buffered.Where(m => m.HasChanges()).ToArray();

							if (incremental)
								await Settings.GetStorage<Level1ChangeMessage>(secId, DataType.Level1).SaveAsync(messages, token);

							if (Settings.IsMode(StorageModes.Snapshot))
							{
								var snapshotStorage = GetSnapshotStorage<Level1ChangeMessage>(DataType.Level1);

								foreach (var message in messages)
									snapshotStorage.Update(message);
							}
						}, token);

						await SaveEachAsync(_unsavedCandles, Buffer.GetCandles(), async (key, messages) =>
						{
							await Settings.GetStorage(key.secId, key.dataType).SaveAsync(messages, token);
						}, token);

						await SaveEachAsync(_unsavedPositions, Buffer.GetPositionChanges(), async (secId, buffered) =>
						{
							var messages = buffered.Where(m => m.HasChanges()).ToArray();

							if (incremental)
								await Settings.GetStorage<PositionChangeMessage>(secId, DataType.PositionChanges).SaveAsync(messages, token);

							if (snapshot)
							{
								var snapshotStorage = GetSnapshotStorage<(SecurityId, string, string), PositionChangeMessage>(DataType.PositionChanges);

								foreach (var message in messages)
									snapshotStorage.Update(message);
							}
						}, token);

						Append(_unsavedNews, Buffer.GetNews(), DataType.News);

						if (_unsavedNews.Count > 0)
						{
							await Settings.GetStorage<NewsMessage>(default, DataType.News).SaveAsync(_unsavedNews, token);
							_unsavedNews.Clear();
						}

						Append(_unsavedBoardStates, Buffer.GetBoardStates(), DataType.BoardState);

						if (_unsavedBoardStates.Count > 0)
						{
							await Settings.GetStorage<BoardStateMessage>(default, DataType.BoardState).SaveAsync(_unsavedBoardStates, token);
							_unsavedBoardStates.Clear();
						}

					}
					catch (Exception ex)
					{
						if (!token.IsCancellationRequested)
							this.AddErrorLog(ex);
					}

					// Outside the try on purpose. The delay used to sit at the end of the
					// cycle, so a cycle that threw skipped it and the loop spun at full
					// speed for as long as storage kept failing.
					try
					{
						await interval.Delay(token);
					}
					catch (OperationCanceledException)
					{
						break;
					}
				}
			}, token);
		}
	}

	private void StopStorageTimer()
	{
		using (_timerSync.EnterScope())
		{
			var cts = _cts;

			if (cts == null)
				return;

			_cts = null;

			try
			{
				cts.Cancel();
			}
			catch (Exception ex)
			{
				this.AddErrorLog(ex);
			}

			try
			{
				// A cycle mid-save keeps running past this wait; the token it holds is
				// already cancelled, so it unwinds on its own.
				_storageTask?.Wait(TimeSpan.FromSeconds(1));
			}
			catch (AggregateException ex) when (ex.InnerExceptions.All(e => e is OperationCanceledException))
			{
				// Expected: the cycle observed the cancellation.
			}
			catch (Exception ex)
			{
				this.AddErrorLog(ex);
			}

			try
			{
				cts.Dispose();
			}
			catch (Exception ex)
			{
				this.AddErrorLog(ex);
			}

			_storageTask = null;
		}
	}

	private static void SaveTransaction(ISnapshotStorage snapshotStorage, ExecutionMessage message)
	{
		ExecutionMessage sepTrade = null;

		if (message.HasOrderInfo && message.HasTradeInfo)
		{
			sepTrade = new ExecutionMessage
			{
				SecurityId = message.SecurityId,
				ServerTime = message.ServerTime,
				TransactionId = message.TransactionId,
				DataTypeEx = message.DataTypeEx,
				TradeId = message.TradeId,
				TradeVolume = message.TradeVolume,
				TradePrice = message.TradePrice,
				TradeStatus = message.TradeStatus,
				TradeStringId = message.TradeStringId,
				OriginSide = message.OriginSide,
				Commission = message.Commission,
				IsSystem = message.IsSystem,
			};

			message.TradeId = null;
			message.TradeVolume = null;
			message.TradePrice = null;
			message.TradeStatus = null;
			message.TradeStringId = null;
			message.OriginSide = null;
		}

		snapshotStorage.Update(message);

		if (sepTrade != null)
			snapshotStorage.Update(sepTrade);
	}

	/// <summary>
	/// Create a copy of <see cref="BufferMessageAdapter"/>.
	/// </summary>
	/// <returns>Copy.</returns>
	public override IMessageAdapter Clone()
	{
		return new BufferMessageAdapter(InnerAdapter.TypedClone(), Settings, Buffer.Clone(), SnapshotRegistry);
	}

	/// <inheritdoc />
	public override void Dispose()
	{
		StopStorageTimer();
		base.Dispose();
	}
}