namespace StockSharp.Algo.Strategies.Optimization;

using StockSharp.Algo.Testing;

/// <summary>
/// The base optimizer of strategies.
/// </summary>
public abstract class BaseOptimizer : BaseLogReceiver
{
	private class CacheAllocator(MarketDataStorageCache original)
	{
		private readonly MarketDataStorageCache _original = original ?? throw new ArgumentNullException(nameof(original));

		public MarketDataStorageCache Allocate() => _original;

		public void Free(MarketDataStorageCache cache) { }
	}

	private class CopyPortfolioProvider : IPortfolioProvider, IDisposable
	{
		private readonly IPortfolioProvider _provider;
		private readonly SynchronizedDictionary<string, Portfolio> _copies = new(StringComparer.InvariantCultureIgnoreCase);

		public CopyPortfolioProvider(IPortfolioProvider provider)
		{
			_provider = provider ?? throw new ArgumentNullException(nameof(provider));

			_provider.NewPortfolio += OnNewPortfolio;
			_provider.PortfolioChanged += OnPortfolioChanged;
		}

		// Per-iteration instance over a run-lifetime provider: unhook, or every
		// iteration permanently grows the shared provider's handler lists.
		public void Dispose()
		{
			_provider.NewPortfolio -= OnNewPortfolio;
			_provider.PortfolioChanged -= OnPortfolioChanged;
		}

		private void OnNewPortfolio(Portfolio portfolio)
			=> NewPortfolio?.Invoke(GetCopy(portfolio));

		private void OnPortfolioChanged(Portfolio portfolio)
			=> PortfolioChanged?.Invoke(GetCopy(portfolio));

		private Portfolio GetCopy(Portfolio portfolio)
			=> LookupByPortfolioName(portfolio.CheckOnNull(nameof(portfolio)).Name);

		public Portfolio LookupByPortfolioName(string name)
			=> _copies.SafeAdd(name, key => (Portfolio)_provider.LookupByPortfolioName(key)?.Clone() ?? new Portfolio { Name = key });

		public IEnumerable<Portfolio> Portfolios => _provider.Portfolios.Select(GetCopy);

		public event Action<Portfolio> NewPortfolio;
		public event Action<Portfolio> PortfolioChanged;
	}

	private readonly HashSet<HistoryEmulationConnector> _startedConnectors = [];

	private MarketDataStorageCache _adapterCache;
	private MarketDataStorageCache _storageCache;

	private CacheAllocator _adapterCacheAllocator;
	private CacheAllocator _storageCacheAllocator;

	private readonly Lock _sync = new();
	private bool _cancelEmulation;
	private bool _allIterationsStarted;

	private volatile TaskCompletionSource _pauseTcs;

	private readonly OptimizationBatchManager _batchManager = new();

	private Channel<(Strategy strategy, IStrategyParam[] parameters)> _resultsChannel;
	private CancellationTokenSource _linkedCts;

	/// <summary>
	/// Initializes a new instance of the <see cref="BaseOptimizer"/>.
	/// </summary>
	/// <param name="securityProvider">The provider of information about instruments.</param>
	/// <param name="portfolioProvider">The portfolio to be used to register orders. If value is not given, the portfolio with default name Simulator will be created.</param>
	/// <param name="exchangeInfoProvider">Exchanges and trading boards provider.</param>
	/// <param name="storageRegistry">Market data storage.</param>
	/// <param name="storageFormat">The format of market data. <see cref="StorageFormats.Binary"/> is used by default.</param>
	/// <param name="drive">The storage which is used by default. By default, <see cref="IStorageRegistry.DefaultDrive"/> is used.</param>
	protected BaseOptimizer(ISecurityProvider securityProvider, IPortfolioProvider portfolioProvider, IExchangeInfoProvider exchangeInfoProvider, IStorageRegistry storageRegistry, StorageFormats storageFormat, IMarketDataDrive drive)
	{
		SecurityProvider = securityProvider ?? throw new ArgumentNullException(nameof(securityProvider));
		PortfolioProvider = portfolioProvider ?? throw new ArgumentNullException(nameof(portfolioProvider));
		ExchangeInfoProvider = exchangeInfoProvider ?? throw new ArgumentNullException(nameof(exchangeInfoProvider));

		EmulationSettings = new();

		StorageSettings = new()
		{
			StorageRegistry = storageRegistry,
			Drive = drive,
			Format = storageFormat,
		};
	}

	/// <summary>
	/// Storage settings.
	/// </summary>
	public StorageCoreSettings StorageSettings { get; }

	/// <summary>
	/// Emulation settings.
	/// </summary>
	public OptimizerSettings EmulationSettings { get; }

	/// <summary>
	/// <see cref="HistoryMessageAdapter.AdapterCache"/>.
	/// </summary>
	public MarketDataStorageCache AdapterCache
	{
		get => _adapterCache;
		set
		{
			_adapterCache = value;
			_adapterCacheAllocator = value is null ? null : new(value);
		}
	}

	/// <summary>
	/// <see cref="HistoryMessageAdapter.StorageCache"/>.
	/// </summary>
	public MarketDataStorageCache StorageCache
	{
		get => _storageCache;
		set
		{
			_storageCache = value;
			_storageCacheAllocator = value is null ? null : new(value);
		}
	}

	/// <summary>
	/// Allocate <see cref="AdapterCache"/>.
	/// </summary>
	/// <returns><see cref="AdapterCache"/></returns>
	protected internal MarketDataStorageCache AllocateAdapterCache()
		=> _adapterCacheAllocator?.Allocate();

	/// <summary>
	/// Allocate <see cref="StorageCache"/>.
	/// </summary>
	/// <returns><see cref="StorageCache"/></returns>
	protected internal MarketDataStorageCache AllocateStorageCache()
		=> _storageCacheAllocator?.Allocate();

	/// <summary>
	/// Free <see cref="AdapterCache"/>.
	/// </summary>
	/// <param name="cache"><see cref="AdapterCache"/></param>
	protected internal void FreeAdapterCache(MarketDataStorageCache cache)
		=> _adapterCacheAllocator?.Free(cache);

	/// <summary>
	/// Free <see cref="StorageCache"/>.
	/// </summary>
	/// <param name="cache"><see cref="StorageCache"/></param>
	protected internal void FreeStorageCache(MarketDataStorageCache cache)
		=> _storageCacheAllocator?.Free(cache);

	/// <summary>
	/// <see cref="ISecurityProvider"/>
	/// </summary>
	public ISecurityProvider SecurityProvider { get; }

	/// <summary>
	/// <see cref="IPortfolioProvider"/>
	/// </summary>
	public IPortfolioProvider PortfolioProvider { get; }

	/// <summary>
	/// <see cref="IExchangeInfoProvider"/>
	/// </summary>
	public IExchangeInfoProvider ExchangeInfoProvider { get; }

	/// <summary>
	/// <see cref="HistoryEmulationConnector.StopOnSubscriptionError"/>
	/// </summary>
	public bool StopOnSubscriptionError { get; set; }

	/// <summary>
	/// Whether optimization is currently paused.
	/// </summary>
	public bool IsPaused => _pauseTcs is not null;

	/// <summary>
	/// Pauses optimization: new iterations do not start until <see cref="Resume"/> runs,
	/// and the backtests already in flight are suspended so progress halts promptly.
	/// </summary>
	/// <returns><see cref="Task"/></returns>
	public async Task Pause()
	{
		// A non-null previous value means a pause is already in effect.
		if (Interlocked.CompareExchange(ref _pauseTcs, new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously), null) is not null)
			return;

		// Blocking new starts alone would still let the whole in-flight batch run to
		// completion, and each iteration can take seconds, so suspend the running connectors too.
		await SetConnectorsSuspendedAsync(true);
	}

	/// <summary>
	/// Resumes paused optimization.
	/// </summary>
	/// <returns><see cref="Task"/></returns>
	public async Task Resume()
	{
		// Order matters: resume the running backtests before letting new iterations start,
		// or the batch slots stay occupied by connectors that are still suspended.
		await SetConnectorsSuspendedAsync(false);
		UnblockPauseWaiters();
	}

	// Releases the gate that parks new iteration starts in TryNextRunAsync, leaving the running
	// connectors alone: the synchronous teardown paths only need paused waiters to wake and
	// observe cancellation.
	private void UnblockPauseWaiters()
		=> Interlocked.Exchange(ref _pauseTcs, null)?.TrySetResult();

	private async Task SetConnectorsSuspendedAsync(bool suspend)
	{
		HistoryEmulationConnector[] connectors;

		using (_sync.EnterScope())
			connectors = [.. _startedConnectors];

		if (connectors.Length == 0)
			return;

		// Await each connector's own SuspendAsync/StartAsync on the caller's async chain, never on a
		// fire-and-forget Task.Run: the pool is already saturated by the BatchSize (CPU*2) in-flight
		// backtests, so a queued suspend only runs once the whole batch has finished.
		// All connectors are handled concurrently, so the batch halts at once.
		async Task ApplyAsync(HistoryEmulationConnector connector)
		{
			try
			{
				if (suspend)
				{
					if (connector.State == ChannelStates.Started)
						await connector.SuspendAsync();
				}
				else
				{
					if (connector.State is ChannelStates.Suspended or ChannelStates.Suspending)
						await connector.StartAsync();
				}
			}
			catch (Exception ex)
			{
				this.AddErrorLog(ex);
			}
		}

		await Task.WhenAll(connectors.Select(ApplyAsync));
	}

	/// <summary>
	/// The event of single progress change.
	/// </summary>
	public event Action<Strategy, IStrategyParam[], int> SingleProgressChanged;

	/// <summary>
	/// Strategy initialized event.
	/// </summary>
	public event Action<Strategy, IStrategyParam[]> StrategyInitialized;

	/// <summary>
	/// Init <see cref="Connector"/>. Called before <see cref="Connector.Connect"/>.
	/// </summary>
	public event Action<Connector> ConnectorInitialized;

	/// <summary>
	/// Initializes the results channel, the batch manager,
	/// and the linked cancellation source a run is driven by.
	/// </summary>
	/// <param name="totalIterations">Total number of iterations (or int.MaxValue if unknown).</param>
	/// <param name="cancellationToken">External cancellation token.</param>
	protected void InitializeRunAsync(int totalIterations, CancellationToken cancellationToken)
	{
		_cancelEmulation = false;
		_allIterationsStarted = false;

		// No connectors are running yet, so clearing the start gate is the whole pause reset.
		UnblockPauseWaiters();

		_batchManager.Reset(EmulationSettings.BatchSize, totalIterations);

		_resultsChannel = Channel.CreateUnbounded<(Strategy, IStrategyParam[])>(new UnboundedChannelOptions
		{
			SingleReader = true,
		});

		_linkedCts?.Dispose();
		_linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

		_linkedCts.Token.Register(() =>
		{
			_cancelEmulation = true;

			// Wake paused waiters so they observe cancellation; the connectors are disconnected
			// just below, so their replay does not need resuming first.
			UnblockPauseWaiters();

			using (_sync.EnterScope())
			{
				foreach (var connector in _startedConnectors)
				{
					try
					{
						if (connector.State is
							ChannelStates.Started or
							ChannelStates.Starting or
							ChannelStates.Suspended or
							ChannelStates.Suspending)
							connector.Disconnect();
					}
					catch (Exception ex)
					{
						// A connector being torn down concurrently by its worker must not
						// break the sweep for the remaining connectors (or the Cancel call).
						this.AddErrorLog(ex);
					}
				}
			}
		});
	}

	/// <summary>
	/// Yields each completed iteration as it is written to the results channel,
	/// ending when <see cref="CompleteChannel"/> closes the channel.
	/// </summary>
	protected async IAsyncEnumerable<(Strategy Strategy, IStrategyParam[] Parameters)> ReadResultsAsync(
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		await foreach (var result in _resultsChannel.Reader.ReadAllAsync(cancellationToken))
		{
			yield return result;
		}
	}

	/// <inheritdoc />
	protected override void DisposeManaged()
	{
		UnblockPauseWaiters();

		if (_linkedCts is { } linkedCts)
		{
			// Cancel runs the registration synchronously (disconnects still-running
			// connectors); dispose afterwards so the source itself is not leaked.
			linkedCts.Cancel();
			linkedCts.Dispose();
			_linkedCts = null;
		}

		base.DisposeManaged();
	}

	/// <summary>
	/// Completes the results channel, so a RunAsync enumeration ends.
	/// </summary>
	protected void CompleteChannel()
	{
		_resultsChannel?.Writer.TryComplete();
	}

	/// <summary>
	/// Reserves a batch slot and runs the next iteration to completion,
	/// returning <see langword="false"/> once no further iteration is available to start.
	/// </summary>
	/// <remarks>
	/// The slot is released exactly once, either by the connector's Stopped transition
	/// or by this method's own teardown, whichever gets there first.
	/// </remarks>
	/// <param name="startTime">Date in history for starting the paper trading.</param>
	/// <param name="stopTime">Date in history to stop the paper trading (date is included).</param>
	/// <param name="tryGetNext">Handler to try to get next strategy object.</param>
	/// <param name="adapterCache"><see cref="HistoryMessageAdapter.AdapterCache"/></param>
	/// <param name="storageCache"><see cref="HistoryMessageAdapter.StorageCache"/></param>
	/// <param name="cancellationToken">Cancellation token.</param>
	protected internal async ValueTask<bool> TryNextRunAsync(DateTime startTime, DateTime stopTime,
		Func<IPortfolioProvider, (Strategy strategy, IStrategyParam[] parameters)?> tryGetNext,
		MarketDataStorageCache adapterCache, MarketDataStorageCache storageCache,
		CancellationToken cancellationToken = default)
	{
		if (tryGetNext is null)
			throw new ArgumentNullException(nameof(tryGetNext));

		var pauseTcs = _pauseTcs;
		if (pauseTcs is not null)
			await pauseTcs.Task.WaitAsync(cancellationToken);

		Strategy strategy;
		IStrategyParam[] parameters;
		HistoryEmulationConnector connector;
		CopyPortfolioProvider pfProvider = null;
		Guid iterationId = default;

		using (_sync.EnterScope())
		{
			if (_cancelEmulation || _allIterationsStarted)
			{
				CheckFinished();
				return false;
			}

			if (!_batchManager.CanStartNext)
			{
				_allIterationsStarted = true;
				CheckFinished();
				return false;
			}

			pfProvider = new CopyPortfolioProvider(PortfolioProvider);

			var reserved = false;

			try
			{
				var next = tryGetNext(pfProvider);

				if (next is null)
				{
					pfProvider.Dispose();

					_allIterationsStarted = true;
					CheckFinished();
					return false;
				}

				(strategy, parameters) = next.Value;

				// CanStartNext was checked above under this same lock, so the reservation cannot fail here.
				if (!_batchManager.TryReserveSlot(out iterationId))
				{
					pfProvider.Dispose();
					return false;
				}

				reserved = true;

				strategy.Parent ??= this;

				connector = CreateConnector(pfProvider, adapterCache, storageCache, startTime, stopTime);
				_startedConnectors.Add(connector);
			}
			catch
			{
				// The completion gate does not exist yet, so this catch owns the cleanup:
				// the provider's handlers leave the run-lifetime provider, and a reserved
				// slot returns to the batch instead of shrinking it for the rest of the run.
				pfProvider.Dispose();

				if (reserved)
					_batchManager.CompleteIteration(iterationId);

				throw;
			}
		}

		var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		var completionGate = new IterationCompletionGate();

		SetupIteration(connector, strategy, parameters, iterationId, tcs, completionGate);

		try
		{
			await StartIterationAsync(connector, strategy, parameters, cancellationToken);

			// The completion tcs is set from the connector's Stopped transition, and the cancel
			// sweep in InitializeRunAsync can miss a connector that had not started when the
			// token fired; without the token here, the worker could park forever.
			return await tcs.Task.WaitAsync(cancellationToken);
		}
		finally
		{
			// The worker owns its iteration's teardown: disposing the connector releases the whole
			// emulation graph (replay task, suspend gate, adapters) even on the paths where nobody
			// else stops it — the post-run test-host hang tracked in KNOWN-ISSUES.md.
			using (_sync.EnterScope())
				_startedConnectors.Remove(connector);

			pfProvider.Dispose();
			connector.Dispose();

			if (completionGate.TryEnter())
			{
				// The iteration never completed naturally, so return its reserved slot here:
				// a stale reservation makes surviving workers treat the batch as exhausted
				// and silently abandon the remaining iterations.
				bool isFinished;

				using (_sync.EnterScope())
				{
					_batchManager.CompleteIteration(iterationId);
					isFinished = _allIterationsStarted && _batchManager.IsFinished;
				}

				if (isFinished || (_cancelEmulation && _batchManager.RunningCount == 0))
					CompleteChannel();
			}
		}
	}

	private void CheckFinished()
	{
		if (_batchManager.IsFinished)
			CompleteChannel();
	}

	private HistoryEmulationConnector CreateConnector(
		IPortfolioProvider pfProvider,
		MarketDataStorageCache adapterCache,
		MarketDataStorageCache storageCache,
		DateTime startTime,
		DateTime stopTime)
	{
		var connector = new HistoryEmulationConnector(SecurityProvider, pfProvider, ExchangeInfoProvider, StorageSettings.StorageRegistry)
		{
			Parent = this,
			StopOnSubscriptionError = StopOnSubscriptionError,

			HistoryMessageAdapter =
			{
				Drive = StorageSettings.Drive,
				StorageFormat = StorageSettings.Format,

				StartDate = startTime,
				StopDate = stopTime,

				AdapterCache = adapterCache,
				StorageCache = storageCache,
			},

			MaxMessageCount = EmulationSettings.MaxMessageCount,
		};

		connector.EmulationSettings.Load(EmulationSettings.Save());

		return connector;
	}

	// Single-entry latch shared by an iteration's two possible finalizers: the connector's
	// Stopped transition and the worker's unwind in TryNextRunAsync. CompleteIteration throws
	// on an id that is no longer running, so exactly one of them may release the slot.
	private sealed class IterationCompletionGate
	{
		private int _entered;

		public bool TryEnter() => Interlocked.Exchange(ref _entered, 1) == 0;
	}

	private void SetupIteration(
		HistoryEmulationConnector connector,
		Strategy strategy,
		IStrategyParam[] parameters,
		Guid iterationId,
		TaskCompletionSource<bool> tcs,
		IterationCompletionGate completionGate)
	{
		var lastStep = 0;

		connector.ProgressChanged += step => SingleProgressChanged?.Invoke(strategy, parameters, lastStep = step);

		connector.StateChanged2 += state =>
		{
			if (state != ChannelStates.Stopped)
				return;

			// Completing here after the worker's unwind, or after a duplicate Stopped transition,
			// would double-release the slot and report a result for a torn-down iteration.
			if (!completionGate.TryEnter())
				return;

			OnIterationCompleted(connector, strategy, parameters, iterationId, lastStep, tcs);
		};
	}

	private void OnIterationCompleted(
		HistoryEmulationConnector connector,
		Strategy strategy,
		IStrategyParam[] parameters,
		Guid iterationId,
		int lastStep,
		TaskCompletionSource<bool> tcs)
	{
		if (lastStep < 100)
		{
			SingleProgressChanged?.Invoke(strategy, parameters, 100);
			strategy.Stop();
		}

		bool isFinished;

		using (_sync.EnterScope())
		{
			_startedConnectors.Remove(connector);
			_batchManager.CompleteIteration(iterationId);
			isFinished = _allIterationsStarted && _batchManager.IsFinished;
		}

		_resultsChannel?.Writer.TryWrite((strategy, parameters));

		tcs.TrySetResult(true);

		if (isFinished || (_cancelEmulation && _batchManager.RunningCount == 0))
			CompleteChannel();
	}

	private async ValueTask StartIterationAsync(HistoryEmulationConnector connector, Strategy strategy, IStrategyParam[] parameters, CancellationToken cancellationToken)
	{
		strategy.Connector = connector;
		strategy.WaitRulesOnStop = false;
		strategy.Reset();

		StrategyInitialized?.Invoke(strategy, parameters);
		ConnectorInitialized?.Invoke(connector);

		if (StopOnSubscriptionError)
		{
			strategy.ProcessStateChanged += s =>
			{
				if (s == strategy && s.ProcessState == ProcessStates.Started && !((ISubscriptionProvider)s).Subscriptions.Any(sub => sub.DataType.IsMarketData))
				{
					s.LogError("No any market data subscription.");
					connector.Disconnect();
				}
			};
		}

		strategy.Start();

		connector.Connect();
		await connector.StartAsync(cancellationToken);
	}
}
