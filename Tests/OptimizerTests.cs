namespace StockSharp.Tests;

using System.Collections;

using StockSharp.Algo.Strategies;
using StockSharp.Algo.Strategies.Optimization;
using StockSharp.Designer;

[TestClass]
public class OptimizerTests : BaseTestClass
{
	private static Security CreateTestSecurity()
	{
		return new() { Id = Paths.HistoryDefaultSecurity };
	}

	private static Portfolio CreateTestPortfolio()
	{
		return Portfolio.CreateSimulator();
	}

	private static IStorageRegistry GetHistoryStorage()
	{
		var fs = Helper.FileSystem;
		return fs.GetStorage(Paths.HistoryDataPath);
	}

	/// <summary>
	/// Yields the SMA strategy parameter combinations to optimize over,
	/// skipping every pair whose short period is not below its long period.
	/// </summary>
	private static IEnumerable<(Strategy strategy, IStrategyParam[] parameters)> CreateStrategyIterations(
		Security security, Portfolio portfolio, int shortFrom, int shortTo, int shortStep, int longFrom, int longTo, int longStep)
	{
		for (var s = shortFrom; s <= shortTo; s += shortStep)
		{
			for (var l = longFrom; l <= longTo; l += longStep)
			{
				if (s >= l)
					continue; // Short SMA should always be less than Long SMA

				var strategy = new SmaStrategy
				{
					Security = security,
					Portfolio = portfolio,
					Volume = 1,
					CandleType = TimeSpan.FromMinutes(1).TimeFrame(),
					Long = l,
					Short = s,
				};

				var shortParam = strategy.Parameters[nameof(SmaStrategy.Short)];
				var longParam = strategy.Parameters[nameof(SmaStrategy.Long)];

				yield return (strategy, [shortParam, longParam]);
			}
		}
	}

	/// <summary>
	/// Tests that BruteForceOptimizer.RunAsync completes all iterations.
	/// </summary>
	[TestMethod]
	public async Task BruteForceRunAsyncCompletesAllIterations()
	{
		var security = CreateTestSecurity();
		var portfolio = CreateTestPortfolio();

		var secProvider = new CollectionSecurityProvider([security]);
		var pfProvider = new CollectionPortfolioProvider([portfolio]);
		var storageRegistry = GetHistoryStorage();

		using var optimizer = new BruteForceOptimizer(secProvider, pfProvider, storageRegistry);

		var startTime = Paths.HistoryBeginDate;
		var stopTime = Paths.HistoryBeginDate.AddDays(6);

		var strategies = CreateStrategyIterations(security, portfolio, 20, 30, 10, 60, 80, 20).ToList();
		var expectedCount = strategies.Count;

		var results = new List<Strategy>();

		await foreach (var (strategy, _) in optimizer.RunAsync(startTime, stopTime, strategies, CancellationToken))
		{
			results.Add(strategy);
		}

		AreEqual(expectedCount, results.Count, $"Expected {expectedCount} results but got {results.Count}");
	}

	/// <summary>
	/// Tests that BruteForceOptimizer.RunAsync raises SingleProgressChanged events.
	/// </summary>
	[TestMethod]
	public async Task BruteForceRunAsyncRaisesProgressEvents()
	{
		var security = CreateTestSecurity();
		var portfolio = CreateTestPortfolio();

		var secProvider = new CollectionSecurityProvider([security]);
		var pfProvider = new CollectionPortfolioProvider([portfolio]);
		var storageRegistry = GetHistoryStorage();

		using var optimizer = new BruteForceOptimizer(secProvider, pfProvider, storageRegistry);

		var startTime = Paths.HistoryBeginDate;
		var stopTime = Paths.HistoryBeginDate.AddDays(6);

		var strategies = CreateStrategyIterations(security, portfolio, 20, 30, 10, 60, 80, 20).ToList();

		var singleProgressCount = 0;

		optimizer.SingleProgressChanged += (strategy, parameters, progress) =>
		{
			Interlocked.Increment(ref singleProgressCount);
		};

		await foreach (var _ in optimizer.RunAsync(startTime, stopTime, strategies, CancellationToken))
		{
		}

		IsTrue(singleProgressCount > 0, "Expected single progress events to be raised");
	}

	/// <summary>
	/// Tests that BruteForceOptimizer.RunAsync raises StrategyInitialized event.
	/// </summary>
	[TestMethod]
	public async Task BruteForceRunAsyncRaisesStrategyInitializedEvent()
	{
		var security = CreateTestSecurity();
		var portfolio = CreateTestPortfolio();

		var secProvider = new CollectionSecurityProvider([security]);
		var pfProvider = new CollectionPortfolioProvider([portfolio]);
		var storageRegistry = GetHistoryStorage();

		using var optimizer = new BruteForceOptimizer(secProvider, pfProvider, storageRegistry);

		var startTime = Paths.HistoryBeginDate;
		var stopTime = Paths.HistoryBeginDate.AddDays(6);

		var strategies = CreateStrategyIterations(security, portfolio, 25, 35, 10, 70, 90, 20).ToList();

		var initializedStrategies = new List<Strategy>();

		optimizer.StrategyInitialized += (strategy, parameters) =>
		{
			lock (initializedStrategies)
			{
				initializedStrategies.Add(strategy);
			}
		};

		await foreach (var _ in optimizer.RunAsync(startTime, stopTime, strategies, CancellationToken))
		{
		}

		IsTrue(initializedStrategies.Count > 0, "Expected strategies to be initialized");
	}

	/// <summary>
	/// Tests that BruteForceOptimizer.RunAsync can be cancelled mid-run.
	/// </summary>
	[TestMethod]
	public async Task BruteForceRunAsyncCancellation()
	{
		var security = CreateTestSecurity();
		var portfolio = CreateTestPortfolio();

		var secProvider = new CollectionSecurityProvider([security]);
		var pfProvider = new CollectionPortfolioProvider([portfolio]);
		var storageRegistry = GetHistoryStorage();

		using var optimizer = new BruteForceOptimizer(secProvider, pfProvider, storageRegistry);
		optimizer.EmulationSettings.BatchSize = 1;

		var startTime = Paths.HistoryBeginDate;
		var stopTime = Paths.HistoryBeginDate.AddDays(6);

		var strategies = CreateStrategyIterations(security, portfolio, 10, 40, 5, 50, 100, 5).ToList();
		var totalCount = strategies.Count;

		using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
		var count = 0;

		try
		{
			await foreach (var _ in optimizer.RunAsync(startTime, stopTime, strategies, cts.Token))
			{
				count++;
				if (count >= 2)
					cts.Cancel();
			}
		}
		catch (OperationCanceledException)
		{
			// expected
		}

		IsTrue(count >= 2, $"Should have received at least 2 results, got {count}");
		IsTrue(count < totalCount, $"Should have been cancelled before all {totalCount} iterations, got {count}");
	}

	/// <summary>
	/// Tests that BruteForceOptimizer.RunAsync respects MaxIterations.
	/// </summary>
	[TestMethod]
	public async Task BruteForceRunAsyncRespectsMaxIterations()
	{
		var security = CreateTestSecurity();
		var portfolio = CreateTestPortfolio();

		var secProvider = new CollectionSecurityProvider([security]);
		var pfProvider = new CollectionPortfolioProvider([portfolio]);
		var storageRegistry = GetHistoryStorage();

		using var optimizer = new BruteForceOptimizer(secProvider, pfProvider, storageRegistry);
		optimizer.EmulationSettings.MaxIterations = 2;

		var startTime = Paths.HistoryBeginDate;
		var stopTime = Paths.HistoryBeginDate.AddDays(6);

		var strategies = CreateStrategyIterations(security, portfolio, 20, 40, 5, 60, 100, 10).ToList();

		var count = 0;

		await foreach (var _ in optimizer.RunAsync(startTime, stopTime, strategies, CancellationToken))
		{
			count++;
		}

		AreEqual(2, count, $"MaxIterations=2 should yield exactly 2 results, but got {count}");
	}

	/// <summary>
	/// Tests that BruteForce optimizer with batch size=2 works correctly.
	/// </summary>
	[TestMethod]
	public async Task BruteForceRunAsyncWithBatchSize()
	{
		var security = CreateTestSecurity();
		var portfolio = CreateTestPortfolio();

		var secProvider = new CollectionSecurityProvider([security]);
		var pfProvider = new CollectionPortfolioProvider([portfolio]);
		var storageRegistry = GetHistoryStorage();

		using var optimizer = new BruteForceOptimizer(secProvider, pfProvider, storageRegistry);
		optimizer.EmulationSettings.BatchSize = 2;

		var startTime = Paths.HistoryBeginDate;
		var stopTime = Paths.HistoryBeginDate.AddDays(6);

		var strategies = CreateStrategyIterations(security, portfolio, 20, 30, 10, 60, 80, 20).ToList();
		var expectedCount = strategies.Count;

		var count = 0;

		await foreach (var _ in optimizer.RunAsync(startTime, stopTime, strategies, CancellationToken))
		{
			count++;
		}

		AreEqual(expectedCount, count, $"Expected {expectedCount} results but got {count}");
	}

	/// <summary>
	/// Tests that optimizer handles multiple securities.
	/// </summary>
	[TestMethod]
	public async Task OptimizerHandlesMultipleSecurities()
	{
		var security1 = CreateTestSecurity();
		var security2 = new Security { Id = Paths.HistoryDefaultSecurity2 };

		var portfolio = CreateTestPortfolio();

		var secProvider = new CollectionSecurityProvider([security1, security2]);
		var pfProvider = new CollectionPortfolioProvider([portfolio]);
		var storageRegistry = GetHistoryStorage();

		using var optimizer = new BruteForceOptimizer(secProvider, pfProvider, storageRegistry);

		var startTime = Paths.HistoryBeginDate;
		var stopTime = Paths.HistoryBeginDate.AddDays(6);

		var strategies = new List<(Strategy strategy, IStrategyParam[] parameters)>();

		foreach (var security in new[] { security1, security2 })
		{
			var strategy = new SmaStrategy
			{
				Security = security,
				Portfolio = portfolio,
				Volume = 1,
				CandleType = TimeSpan.FromMinutes(1).TimeFrame(),
				Long = 80,
				Short = 30,
			};

			var shortParam = strategy.Parameters[nameof(SmaStrategy.Short)];
			var longParam = strategy.Parameters[nameof(SmaStrategy.Long)];

			strategies.Add((strategy, [shortParam, longParam]));
		}

		var count = 0;

		await foreach (var _ in optimizer.RunAsync(startTime, stopTime, strategies, CancellationToken))
		{
			count++;
		}

		AreEqual(strategies.Count, count, $"Expected {strategies.Count} results but got {count}");
	}

	/// <summary>
	/// Tests that optimizer yields strategy statistics after completion.
	/// </summary>
	[TestMethod]
	public async Task OptimizerYieldsStrategyStatistics()
	{
		var security = CreateTestSecurity();
		var portfolio = CreateTestPortfolio();

		var secProvider = new CollectionSecurityProvider([security]);
		var pfProvider = new CollectionPortfolioProvider([portfolio]);
		var storageRegistry = GetHistoryStorage();

		using var optimizer = new BruteForceOptimizer(secProvider, pfProvider, storageRegistry);

		var startTime = Paths.HistoryBeginDate;
		var stopTime = Paths.HistoryBeginDate.AddDays(6);

		var strategies = CreateStrategyIterations(security, portfolio, 25, 35, 10, 70, 90, 20).ToList();

		var results = new List<(Strategy strategy, IStrategyParam[] parameters)>();

		await foreach (var result in optimizer.RunAsync(startTime, stopTime, strategies, CancellationToken))
		{
			results.Add(result);
		}

		foreach (var (strategy, parameters) in results)
		{
			IsNotNull(strategy, "Strategy should not be null");
			IsNotNull(parameters, "Parameters should not be null");
			IsTrue(parameters.Length > 0, "Parameters should not be empty");

			var statisticManager = strategy.StatisticManager;
			statisticManager.AssertNotNull("StatisticManager should not be null");

			// The SMA strategy trades over this history, so the statistics must reflect real activity.
			IsTrue(strategy.Orders.Any(), "Strategy should have placed orders during the backtest");
			IsTrue(strategy.MyTrades.Any() || strategy.PnL != 0, "Strategy should have trades or non-zero PnL");
		}
	}

	/// <summary>
	/// Tests that iteration count matches expected.
	/// </summary>
	[TestMethod]
	public async Task OptimizerIterationCountMatchesExpected()
	{
		var security = CreateTestSecurity();
		var portfolio = CreateTestPortfolio();

		var secProvider = new CollectionSecurityProvider([security]);
		var pfProvider = new CollectionPortfolioProvider([portfolio]);
		var storageRegistry = GetHistoryStorage();

		using var optimizer = new BruteForceOptimizer(secProvider, pfProvider, storageRegistry);

		var startTime = Paths.HistoryBeginDate;
		var stopTime = Paths.HistoryBeginDate.AddDays(6);

		var strategies = CreateStrategyIterations(security, portfolio, 20, 30, 10, 60, 80, 20).ToList();
		var expectedIterations = strategies.Count;

		var count = 0;

		await foreach (var _ in optimizer.RunAsync(startTime, stopTime, strategies, CancellationToken))
		{
			count++;
		}

		AreEqual(expectedIterations, count,
			$"Expected {expectedIterations} iterations but got {count}");
	}

	/// <summary>
	/// Tests that within each optimizer iteration, strategy events have increasing times.
	/// </summary>
	[TestMethod]
	public async Task OptimizerStrategyEventsHaveIncreasingTimes()
	{
		var security = CreateTestSecurity();
		var portfolio = CreateTestPortfolio();

		var secProvider = new CollectionSecurityProvider([security]);
		var pfProvider = new CollectionPortfolioProvider([portfolio]);
		var storageRegistry = GetHistoryStorage();

		using var optimizer = new BruteForceOptimizer(secProvider, pfProvider, storageRegistry);

		// Force sequential execution for easier time validation
		optimizer.EmulationSettings.BatchSize = 1;

		var startTime = Paths.HistoryBeginDate;
		var stopTime = Paths.HistoryBeginDate.AddDays(6);

		var strategies = CreateStrategyIterations(security, portfolio, 25, 35, 10, 70, 90, 20).ToList();

		var iterationTimeErrors = new List<string>();
		var syncLock = new object();

		optimizer.StrategyInitialized += (strategy, parameters) =>
		{
			DateTime? lastEventTime = null;
			var strategyErrors = new List<string>();

			strategy.OrderReceived += (sub, order) =>
			{
				var time = order.Time;
				if (lastEventTime.HasValue && time < lastEventTime.Value)
				{
					strategyErrors.Add($"Order time {time} < last event time {lastEventTime.Value}");
				}
				lastEventTime = time;
			};

			strategy.OwnTradeReceived += (sub, trade) =>
			{
				var time = trade.Trade.ServerTime;
				if (lastEventTime.HasValue && time < lastEventTime.Value)
				{
					strategyErrors.Add($"Trade time {time} < last event time {lastEventTime.Value}");
				}
				lastEventTime = time;
			};

			strategy.PnLReceived2 += (s, pf, time, realized, unrealized, commission) =>
			{
				if (lastEventTime.HasValue && time < lastEventTime.Value)
				{
					strategyErrors.Add($"PnL time {time} < last event time {lastEventTime.Value}");
				}
				lastEventTime = time;
			};

			strategy.ProcessStateChanged += (s) =>
			{
				if (s.ProcessState == ProcessStates.Stopped && strategyErrors.Count > 0)
				{
					lock (syncLock)
					{
						iterationTimeErrors.AddRange(strategyErrors.Select(e =>
							$"Strategy {strategy.Name}: {e}"));
					}
				}
			};
		};

		await foreach (var _ in optimizer.RunAsync(startTime, stopTime, strategies, CancellationToken))
		{
		}

		if (iterationTimeErrors.Count > 0)
		{
			Fail($"Time ordering violations within strategy iterations:\n{iterationTimeErrors.Take(20).JoinN()}");
		}
	}

	/// <summary>
	/// Tests that GeneticOptimizer.RunAsync yields results.
	/// </summary>
	[TestMethod]
	public async Task GeneticRunAsyncYieldsResults()
	{
		var security = CreateTestSecurity();
		var portfolio = CreateTestPortfolio();

		var secProvider = new CollectionSecurityProvider([security]);
		var pfProvider = new CollectionPortfolioProvider([portfolio]);
		var storageRegistry = GetHistoryStorage();

		using var optimizer = new GeneticOptimizer(secProvider, pfProvider, storageRegistry, Paths.FileSystem);

		var startTime = Paths.HistoryBeginDate;
		var stopTime = Paths.HistoryBeginDate.AddDays(6);

		var strategy = new SmaStrategy
		{
			Security = security,
			Portfolio = portfolio,
			Volume = 1,
			CandleType = TimeSpan.FromMinutes(1).TimeFrame(),
			Long = 80,
			Short = 30,
		};

		var shortParam = strategy.Parameters[nameof(SmaStrategy.Short)];
		var longParam = strategy.Parameters[nameof(SmaStrategy.Long)];

		optimizer.EmulationSettings.MaxIterations = 5;

		var geneticParams = new (IStrategyParam param, object from, object to, object step, IEnumerable values)[]
		{
			(shortParam, 20, 40, 5, null),
			(longParam, 60, 100, 10, null),
		};

		var results = new List<Strategy>();

		await foreach (var (s, _) in optimizer.RunAsync(startTime, stopTime, strategy, geneticParams, s => s.PnL, cancellationToken: CancellationToken))
		{
			results.Add(s);
		}

		IsTrue(results.Count > 0, "Expected at least one result from genetic optimizer");
	}

	/// <summary>
	/// Tests that GeneticOptimizer.RunAsync raises SingleProgressChanged events.
	/// </summary>
	[TestMethod]
	public async Task GeneticRunAsyncRaisesProgressEvents()
	{
		var security = CreateTestSecurity();
		var portfolio = CreateTestPortfolio();

		var secProvider = new CollectionSecurityProvider([security]);
		var pfProvider = new CollectionPortfolioProvider([portfolio]);
		var storageRegistry = GetHistoryStorage();

		using var optimizer = new GeneticOptimizer(secProvider, pfProvider, storageRegistry, Paths.FileSystem);

		var startTime = Paths.HistoryBeginDate;
		var stopTime = Paths.HistoryBeginDate.AddDays(6);

		var strategy = new SmaStrategy
		{
			Security = security,
			Portfolio = portfolio,
			Volume = 1,
			CandleType = TimeSpan.FromMinutes(1).TimeFrame(),
			Long = 80,
			Short = 30,
		};

		var shortParam = strategy.Parameters[nameof(SmaStrategy.Short)];
		var longParam = strategy.Parameters[nameof(SmaStrategy.Long)];

		var singleProgressCount = 0;
		optimizer.SingleProgressChanged += (s, parameters, progress) =>
		{
			Interlocked.Increment(ref singleProgressCount);
		};

		optimizer.EmulationSettings.MaxIterations = 5;

		var geneticParams = new (IStrategyParam param, object from, object to, object step, IEnumerable values)[]
		{
			(shortParam, 20, 40, 5, null),
			(longParam, 60, 100, 10, null),
		};

		await foreach (var _ in optimizer.RunAsync(startTime, stopTime, strategy, geneticParams, s => s.PnL, cancellationToken: CancellationToken))
		{
		}

		IsTrue(singleProgressCount > 0, "Expected single progress events to be raised");
	}

	/// <summary>
	/// Tests that GeneticOptimizer.RunAsync can be cancelled.
	/// </summary>
	[TestMethod]
	public async Task GeneticRunAsyncCancellation()
	{
		var security = CreateTestSecurity();
		var portfolio = CreateTestPortfolio();

		var secProvider = new CollectionSecurityProvider([security]);
		var pfProvider = new CollectionPortfolioProvider([portfolio]);
		var storageRegistry = GetHistoryStorage();

		using var optimizer = new GeneticOptimizer(secProvider, pfProvider, storageRegistry, Paths.FileSystem);

		var startTime = Paths.HistoryBeginDate;
		var stopTime = Paths.HistoryEndDate;

		var strategy = new SmaStrategy
		{
			Security = security,
			Portfolio = portfolio,
			Volume = 1,
			CandleType = TimeSpan.FromMinutes(1).TimeFrame(),
			Long = 80,
			Short = 30,
		};

		var shortParam = strategy.Parameters[nameof(SmaStrategy.Short)];
		var longParam = strategy.Parameters[nameof(SmaStrategy.Long)];

		// Many iterations, so the run can be stopped mid-way.
		optimizer.EmulationSettings.MaxIterations = 100;

		var geneticParams = new (IStrategyParam param, object from, object to, object step, IEnumerable values)[]
		{
			(shortParam, 20, 40, 5, null),
			(longParam, 60, 100, 10, null),
		};

		using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
		var count = 0;

		try
		{
			await foreach (var _ in optimizer.RunAsync(startTime, stopTime, strategy, geneticParams, s => s.PnL, cancellationToken: cts.Token))
			{
				count++;
				if (count >= 2)
					cts.Cancel();
			}
		}
		catch (OperationCanceledException)
		{
			// expected
		}

		IsTrue(count >= 2, $"Should have received at least 2 results before cancellation, got {count}");
		IsTrue(count < optimizer.EmulationSettings.MaxIterations,
			"Cancellation must stop the genetic optimizer before all iterations complete");
	}

	/// <summary>
	/// Tests cancellation by iteration count inside the loop (consumer-side limit).
	/// </summary>
	[TestMethod]
	public async Task BruteForceRunAsyncCancelByIterationCount()
	{
		var security = CreateTestSecurity();
		var portfolio = CreateTestPortfolio();

		var secProvider = new CollectionSecurityProvider([security]);
		var pfProvider = new CollectionPortfolioProvider([portfolio]);
		var storageRegistry = GetHistoryStorage();

		using var optimizer = new BruteForceOptimizer(secProvider, pfProvider, storageRegistry);

		var startTime = Paths.HistoryBeginDate;
		var stopTime = Paths.HistoryBeginDate.AddDays(6);

		var strategies = CreateStrategyIterations(security, portfolio, 10, 40, 5, 50, 100, 5).ToList();

		const int maxResults = 3;
		using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
		var results = new List<(Strategy strategy, IStrategyParam[] parameters)>();

		try
		{
			await foreach (var result in optimizer.RunAsync(startTime, stopTime, strategies, cts.Token))
			{
				results.Add(result);

				if (results.Count >= maxResults)
					cts.Cancel();
			}
		}
		catch (OperationCanceledException)
		{
			// expected
		}

		IsTrue(results.Count >= maxResults, $"Should have at least {maxResults} results, got {results.Count}");
		IsTrue(results.Count < strategies.Count, $"Should have been cancelled before all {strategies.Count} iterations");
	}

	/// <summary>
	/// Tests cancellation by timeout (CancelAfter).
	/// </summary>
	[TestMethod]
	public async Task BruteForceRunAsyncCancelByTimeout()
	{
		var security = CreateTestSecurity();
		var portfolio = CreateTestPortfolio();

		var secProvider = new CollectionSecurityProvider([security]);
		var pfProvider = new CollectionPortfolioProvider([portfolio]);
		var storageRegistry = GetHistoryStorage();

		using var optimizer = new BruteForceOptimizer(secProvider, pfProvider, storageRegistry);

		var startTime = Paths.HistoryBeginDate;
		var stopTime = Paths.HistoryEndDate; // long period so it won't finish naturally

		var strategies = CreateStrategyIterations(security, portfolio, 10, 40, 5, 50, 100, 5).ToList();

		using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
		cts.CancelAfter(TimeSpan.FromSeconds(30));

		var count = 0;

		try
		{
			await foreach (var _ in optimizer.RunAsync(startTime, stopTime, strategies, cts.Token))
			{
				count++;
			}
		}
		catch (OperationCanceledException)
		{
			// expected
		}

		IsTrue(count < strategies.Count, $"Should have been cancelled by timeout before all {strategies.Count} iterations, got {count}");
	}

	/// <summary>
	/// Tests that a single failing iteration costs only that iteration:
	/// the worker survives, its batch slot is released, and every remaining iteration still runs.
	/// </summary>
	/// <remarks>
	/// Regression guard for the stale reservation that made a surviving worker treat the batch
	/// as exhausted and silently abandon the rest of the run.
	/// </remarks>
	[TestMethod]
	public async Task BruteForceRunAsyncSurvivesIterationStartFailure()
	{
		var security = CreateTestSecurity();
		var portfolio = CreateTestPortfolio();

		var secProvider = new CollectionSecurityProvider([security]);
		var pfProvider = new CollectionPortfolioProvider([portfolio]);
		var storageRegistry = GetHistoryStorage();

		using var optimizer = new BruteForceOptimizer(secProvider, pfProvider, storageRegistry);

		// A single worker makes the regression sharp: with a leaked slot, or a dead worker,
		// nothing after the poisoned iteration would run at all.
		optimizer.EmulationSettings.BatchSize = 1;

		var startTime = Paths.HistoryBeginDate;
		var stopTime = Paths.HistoryBeginDate.AddDays(6);

		var strategies = CreateStrategyIterations(security, portfolio, 20, 30, 10, 60, 80, 20).ToList();
		IsTrue(strategies.Count >= 3, "Need at least 3 iterations so the failure sits mid-run");

		// Poison a middle iteration, so survival is observable on both sides of it.
		var poisoned = strategies[1].strategy;

		optimizer.StrategyInitialized += (strategy, parameters) =>
		{
			if (ReferenceEquals(strategy, poisoned))
				throw new InvalidOperationException("Poisoned iteration.");
		};

		var results = new List<Strategy>();

		await foreach (var (strategy, _) in optimizer.RunAsync(startTime, stopTime, strategies, CancellationToken))
		{
			results.Add(strategy);
		}

		AreEqual(strategies.Count - 1, results.Count,
			$"All {strategies.Count - 1} healthy iterations must complete despite one startup failure, got {results.Count}");
		IsFalse(results.Contains(poisoned), "The failed iteration must not be reported as a result");
	}

	/// <summary>
	/// Tests that a token already cancelled before the run starts
	/// terminates the enumeration with no completed iterations and no hang.
	/// </summary>
	/// <remarks>
	/// Regression guard for reserved slots being released on the cancellation path.
	/// </remarks>
	[TestMethod]
	public async Task BruteForceRunAsyncCancelledBeforeStart()
	{
		var security = CreateTestSecurity();
		var portfolio = CreateTestPortfolio();

		var secProvider = new CollectionSecurityProvider([security]);
		var pfProvider = new CollectionPortfolioProvider([portfolio]);
		var storageRegistry = GetHistoryStorage();

		using var optimizer = new BruteForceOptimizer(secProvider, pfProvider, storageRegistry);

		var startTime = Paths.HistoryBeginDate;
		var stopTime = Paths.HistoryBeginDate.AddDays(6);

		var strategies = CreateStrategyIterations(security, portfolio, 20, 30, 10, 60, 80, 20).ToList();

		using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
		cts.Cancel();

		var count = 0;

		try
		{
			await foreach (var _ in optimizer.RunAsync(startTime, stopTime, strategies, cts.Token))
			{
				count++;
			}
		}
		catch (OperationCanceledException)
		{
			// expected
		}

		AreEqual(0, count, "No iterations should complete when cancelled before start");
	}

	private class CountingPortfolioProvider(IPortfolioProvider inner) : IPortfolioProvider
	{
		private int _subscriptions;

		/// <summary>
		/// Live event subscriptions: adds minus removes across both events.
		/// </summary>
		public int Subscriptions => _subscriptions;

		public IEnumerable<Portfolio> Portfolios => inner.Portfolios;

		public Portfolio LookupByPortfolioName(string name) => inner.LookupByPortfolioName(name);

		public event Action<Portfolio> NewPortfolio
		{
			add { Interlocked.Increment(ref _subscriptions); inner.NewPortfolio += value; }
			remove { Interlocked.Decrement(ref _subscriptions); inner.NewPortfolio -= value; }
		}

		public event Action<Portfolio> PortfolioChanged
		{
			add { Interlocked.Increment(ref _subscriptions); inner.PortfolioChanged += value; }
			remove { Interlocked.Decrement(ref _subscriptions); inner.PortfolioChanged -= value; }
		}
	}

	/// <summary>
	/// Tests that a throwing tryGetNext leaves no event handlers hooked on the
	/// run-lifetime portfolio provider and costs no batch capacity: every
	/// remaining iteration still runs. Regression test for the undisposed
	/// per-iteration CopyPortfolioProvider on the tryGetNext error path.
	/// </summary>
	[TestMethod]
	public async Task BruteForceRunAsyncDisposesProviderWhenTryGetNextThrows()
	{
		var security = CreateTestSecurity();
		var portfolio = CreateTestPortfolio();

		var secProvider = new CollectionSecurityProvider([security]);
		var counting = new CountingPortfolioProvider(new CollectionPortfolioProvider([portfolio]));
		var storageRegistry = GetHistoryStorage();

		using var optimizer = new BruteForceOptimizer(secProvider, counting, storageRegistry);

		// Single worker keeps the call sequence deterministic.
		optimizer.EmulationSettings.BatchSize = 1;

		var startTime = Paths.HistoryBeginDate;
		var stopTime = Paths.HistoryBeginDate.AddDays(6);

		var strategies = CreateStrategyIterations(security, portfolio, 20, 30, 10, 60, 80, 20).ToList();
		IsTrue(strategies.Count >= 2, "Need at least 2 iterations around the poisoned call");

		var queue = new Queue<(Strategy strategy, IStrategyParam[] parameters)>(strategies);
		var calls = 0;
		var results = 0;

		await foreach (var _ in optimizer.RunAsync(startTime, stopTime, pfProvider =>
		{
			// The second call throws without consuming a strategy; the worker's
			// failure handling retries, so every queued strategy still runs.
			if (++calls == 2)
				throw new InvalidOperationException("Poisoned tryGetNext.");

			if (queue.Count == 0)
				return null;

			var next = queue.Dequeue();
			next.strategy.Portfolio = pfProvider.LookupByPortfolioName(next.strategy.Portfolio.Name);
			return next;
		}, CancellationToken))
		{
			results++;
		}

		AreEqual(strategies.Count, results,
			$"All {strategies.Count} strategies must complete despite the poisoned call, got {results}");
		AreEqual(0, counting.Subscriptions,
			"Every per-iteration portfolio provider must unhook its handlers, including the poisoned call's");
	}
}
