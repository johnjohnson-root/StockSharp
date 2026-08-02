# Known issues

## Fixed here: sync-blocking public facade over the async core

The primary entry points were void methods
blocking a thread over the ValueTask-native pipeline via `AsyncHelper.Run`,
and the contracts (`IConnector`, `ITransactionProvider`, `ISubscriptionProvider`)
exposed no async members at all.
The three interfaces now declare `ConnectAsync`, `DisconnectAsync`,
`RegisterOrderAsync`, `EditOrderAsync`, `ReRegisterOrderAsync`,
`CancelOrderAsync`, `CancelOrdersAsync`,
`SubscribeAsync` and `UnSubscribeAsync` —
all with `CancellationToken`s —
with default implementations falling back to the sync members,
so existing implementers keep compiling;
the blocking void members are `[Obsolete]` at the interface level.
`Connector` implements the new members without blocking:
`ConnectAsync`/`DisconnectAsync` await the pipeline
and complete on the `Connected`/`Disconnected`/`ConnectionError` events,
and `SubscribeAsync`/`UnSubscribeAsync` route through
`ApplySubscriptionManagerActionsAsync`.
The instance members supersede
the former `IConnectorAsyncExtensions.ConnectAsync/DisconnectAsync` extensions
with the same semantics,
and the subscription keep-alive extension is renamed `HoldSubscriptionAsync`
to free the `SubscribeAsync` name.
Two items stay open:
`Strategy`'s ordering internals still drive the sync facade (pragma-marked),
and the remaining `AsyncHelper.Run` shims in the void members
stay until callers migrate.

## Flaky-test tail (CI retries failed tests once)

Beyond the deterministic fixes below,
the suite carried a long tail of low-probability timing-sensitive tests:
each 3-OS run (~13k test executions) surfaced 1–3 different failures,
drawn from three tests.
Each one now has an identified cause and a test-only fix,
so the tail list is empty:

- `CandleTests.TotalPrice` ("Sequence contains more than one element")
  built its ticks from a raw `DateTime.UtcNow` at +0s, +1s and +1min
  and asserted the first two share one 1-minute candle.
  A base time in the final second of a minute put the +1s tick
  in the next candle,
  so `Process` returned two candles and `Single()` threw —
  one second in sixty, about 1.7% of runs.
  The base time is anchored to the start of the minute.
- `Subscriptions_RepeatedRounds_AllProcessed` (windows, ~1 min)
  and `Connection_SubscriptionsCleanedOnDisconnect` (macos, 15 s)
  both gated on a *count* of `SubscriptionOnline` events
  rather than on the transaction ids they had just created.
  A late event crossing a round boundary, or the connector's own
  order-status subscription reaching Online,
  stood in for a subscription still subscribing;
  the gate opened early, `UnSubscribeAll` skipped that subscription
  because it cleans active ones alone,
  and the following wait ran to the test timeout.
  Both gates now wait for their own ids.
  Reasoned from the test sources rather than reproduced.

CI still retries exactly the failed tests once,
and a test that fails twice fails the job.
The retry mechanism comes out once five consecutive runs record no retry,
which is the evidence that the tail is closed rather than quiet.
All tests stay active.

One member of the tail is now fixed here.
The `--blame-hang` sequence file twice named
`OptimizerPauseTests.PauseHaltsBruteForce` as the only test still in flight
when the run aborted,
on windows both times,
and in a distinctive way —
every test reports `Passed` first (`Failed: 0, Passed: 4388`),
and the test host then sits idle until the 8-minute inactivity timer kills it.
So the hang came after the test's assertions complete:
the optimizer's pause/resume machinery left work running
that kept the process alive.
Auditing that machinery found four defects, all fixed here
(the windows repro is statistical,
so the fix is asserted from the defects, not from a reproduced hang):

- `HistoryMessageAdapter`'s suspend gate was a `ManualResetEventSlim` —
  every suspended backtest **blocked a thread-pool thread** in a synchronous `Wait`.
  An optimizer pause parks a full batch (default `CPU*2`) of replay loops at once,
  which on small CI runners starves the pool the test host itself needs.
  The gate is now an async `AsyncManualResetEvent`,
  and the replay loop awaits it.
- `BaseOptimizer` never disposed the `HistoryEmulationConnector` it created per iteration,
  so each one's emulation graph (replay task, gates, adapter chain) was left to leak.
  The worker that starts an iteration now owns its teardown:
  the connector is disposed in a `finally`,
  on success, error and cancellation paths alike.
- The per-iteration completion await (`await tcs.Task`) took no cancellation token,
  and the cancel sweep's state filter can miss a connector
  that had not started when the token fired —
  a worker (or a genetic-optimizer fitness evaluation) could park forever.
  The await now honours the caller's token,
  and the sweep tolerates connectors concurrently torn down by their workers.
- Each iteration's `CopyPortfolioProvider` subscribed
  to the run-lifetime portfolio provider's events and never unsubscribed
  (unbounded handler-list growth over a long optimization);
  it is now disposed with its iteration.

Additionally, `[AssemblyCleanup]` now arms a 60-minute background watchdog
(decision record 0007):
if the test host is still alive that long after cleanup,
it prints a diagnostic and exits with code 97 —
converting any recurrence of this failure class
from an 8-minute silent abort blamed on the last test
into a fast, attributed failure.

## Fixed here: async event fan-out discarded all but the last handler's task

Every pipeline event is a `Func<Message, CancellationToken, ValueTask>`
and was raised as `handler?.Invoke(...)`.
Invoking a multicast delegate returns only the **last** handler's `ValueTask`,
so with two or more subscribers (e.g. `Connector` plus every `Strategy`)
all other handlers ran unobserved:
exceptions swallowed, back-pressure lost,
and a subscriber could be handed message N+1
while its handler for message N was still running.
All ten raise sites now go through `AsyncEventExtensions.InvokeAllAsync`,
which awaits every handler sequentially in subscription order
(single-subscriber path unchanged and allocation-free).
The same area carries a related fix:
`AsyncMessageChannel` consumed one `ValueTask` four times
(`AsTask`/`await`/`IsFaulted`/`AsTask`) —
undefined behaviour for pooled sources;
it now converts to `Task` exactly once.
Both defects still exist upstream.

## Fixed here: disconnect could drop queued unsubscribes

`Connection_SubscriptionsCleanedOnDisconnect` occasionally saw the adapter
never observe an unsubscribe after `DisconnectAsync` completed.
The root cause sits in `AsyncMessageChannel`:
the scheduler picks control messages first,
so a queued `DisconnectMessage` overtook older queued unsubscribes,
and once disconnect starts,
the connection-state gate silently drops every remaining non-control message.
The scheduler now defers disconnect while an older pending unsubscribe exists
(unsubscribes have no parallel limit, so this cannot stall),
and `Disconnect_DoesNotOvertakeQueuedUnsubscribe` covers the regression.
The race still exists upstream.

## Fixed here: broken priority-queue comparer (message ordering)

`BaseMessageQueue` and `BasketMarketDataStorage` constructed
`Ecng.Collections.PriorityQueue` with the comparer `(p1, p2) => (p1 - p2).Abs()`.
`Abs()` destroys the sign,
so the comparer can never report "less than"
and heap ordering degenerates
to an implementation detail of the resolved Ecng.Collections version —
`MessageByLocalTimeQueue` delivered a fully pre-queued, shuffled batch
out of time order in CI.
Both sites now use the signed difference,
and `MessageByLocalTimeQueue_HighVolume_HandlesCorrectly` holds the contract
by asserting sort order over a thousand messages.
Upstream keeps the `.Abs()` comparer at both sites.

The sections below carry issues inherited from upstream,
observed on this fork's CI (GitHub-hosted runners, ubuntu/windows/macos).
Upstream's own CI has produced no successful run since 2025-10-27,
and every upstream master push since then fails its 10-minute job timeout,
so none of them is a regression introduced by this fork.

## Python analytics script tests fail (excluded from CI)

`CompilationTests.PythonAnalyticsScripts`
and `CompilationTests.PythonAnalyticsScriptsParallel` fail
when executing the bundled IronPython analytics scripts:

```
System.InvalidOperationException: Error running script 'indicator_script'.
 ---> System.InvalidOperationException: Error () No module named Indicators (0-0)
```

The failure appears on ubuntu (both variants)
and macos (sequential variant only — order-dependent),
with Ecng.Compilation pinned to the 2026-07-16 versions,
so the pinning rules out recent package drift.
The C#, F#, and remaining Python compilation tests pass,
so the scripting subsystem itself is still exercised.
The CLR namespace import failure inside the IronPython engine needs investigation;
until then CI runs with `--filter "FullyQualifiedName!~PythonAnalyticsScripts"`.

The record above names ubuntu and macos and stays silent about windows,
because the exclusion went in before any run measured windows.
The `Build & Tests` workflow therefore carries a probe:
a windows-only step running those two tests alone,
marked `continue-on-error` so its result reports without failing the job.
Two runs of evidence decide the filter.
Windows passing turns the wholesale exclusion into a per-OS one
and recovers the coverage there;
windows failing makes the defect platform-independent
and points the investigation at the IronPython `Indicators` module import
rather than at the runner image.

## The 11 skipped tests: every ExportTests case needing a database

`ExportTests` reports 11 skips on every run,
and they are the whole class except `Cancellation`:
`Ticks`, `Depths`, `OrderLog`, `Positions`, `News`, `Level1`,
`Candles`, `Indicator`, `Board`, `BoardState`, and `Security`.

All 11 route through the private `ExportAsync` helper,
which ends by building a `DatabaseExporter` around
`GetSecret("SQLSERVER_CONNECTION_STRING")`.
`Ecng.UnitTesting.BaseTestClass.GetSecret` reports the test inconclusive
when the secret is absent,
and no SQL Server credential is configured for this repository,
so the helper never returns.
`Cancellation` is the one test in the class that does not call the helper,
and it is the one that passes.

The blocking condition is a missing credential, named exactly:
a repository secret `SQLSERVER_CONNECTION_STRING`
pointing at a reachable SQL Server instance.
It is not a data file, a proprietary dependency, or an upstream defect.

The coverage loss is narrower than the skip count suggests.
Each test exports to text, XML, JSON and XLSX
and asserts row counts and last timestamps for all four
*before* reaching the database exporter,
so those four paths are exercised on every CI run
and only their outcome is mislabelled.
The database exporter alone goes unverified.

Splitting the database assertion into its own test
would report the four file formats as passing
and leave one honest skip instead of eleven.
That trade costs a test method per data type,
so it waits for someone who wants the reporting fidelity enough to pay for it.

## Roughly 32 history-data tests pass silently when the data is absent

The 11 skips above are the visible half of the story.
The invisible half is larger:
about 32 tests check `Paths.HistoryDataPath` for null,
print a line to the console, and `return` —
which MSTest reports as **passed**, having executed no assertion at all.

    Tests/BacktestingTests.cs:559                  SkipIfNoHistoryData(), 24 callers
    Tests/StrategyDecomposedEquivalenceTests.cs:222   SkipIfNoHistoryData(), 6 callers
    Tests/OptimizerPauseTests.cs:81, 168           inline, 2 tests

The same codebase does it correctly in six other places,
which is what makes this a defect rather than a convention:
`PathsTests.cs:25,47,57`,
`StrategyReferenceSurfaceTests.cs:776`,
`StrategyDecomposedFullEquivalenceTests.cs:1107`,
and `StrategyDecomposedEquivalenceTests.cs:2724`
all call `Inconclusive(...)` instead,
and two of them carry a comment saying why —
"Not a silent pass: without market data a zero-vs-zero comparison
would be meaningless."

`Paths.HistoryDataPath` resolves by walking the NuGet global-packages
folder for `stocksharp.samples.historydata`,
so it is null wherever that package has not been restored.
CI restores it today and these tests do run there.
The exposure is a CI image or a contributor machine where the package
is missing:
the backtesting and strategy-equivalence suites would report green
while asserting nothing,
and nothing in the run output would distinguish that from real coverage.

The fix is mechanical —
`return` becomes `Inconclusive(...)` at the three sites above —
and it converts a silent 32-test hole into 32 visible skips.

## CI hang: resolved (flaky tests, fixed)

Early CI runs aborted via `--blame-hang` after ~8 minutes of inactivity.
The blame sequence file identified `AsyncMessageChannelTests.Close_StopsProcessing`
as the only test still in flight:
it polled for the transient `ChannelStates.Stopping` with a 10 ms interval,
and `AsyncMessageChannel.Close` can pass through Stopping to Stopped faster than that —
the poll then spins forever against a Stopped channel.
The test now also accepts Stopped.
`MessageChannelTests.InMemoryChannel_MessageByLocalTimeQueue_OutOfOrderTimes_SortsCorrectly`
was similarly racy
(channel draining while the shuffled batch was still being enqueued,
so delivery order could legally interleave);
it now suspends the channel while enqueueing.
These two are the most plausible cause of upstream's long-red CI as well.
Independently, `Tests/Helper.cs`'s static `LogManager` is now disposed in `[AssemblyCleanup]`,
and `SubscriptionHolderTests` no longer leaks per-holder `LogManager` instances.
CI uploads `TestResults` (blame sequence + hang dumps) on failure
should anything recur.
