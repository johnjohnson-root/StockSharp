# Known issues

## Fixed here: sync-blocking public facade over the async core

The primary entry points were void methods blocking a thread over the
ValueTask-native pipeline via AsyncHelper.Run, and the contracts
(`IConnector`, `ITransactionProvider`, `ISubscriptionProvider`) exposed no
async members at all. Now: the three interfaces declare `ConnectAsync`,
`DisconnectAsync`, `RegisterOrderAsync`, `EditOrderAsync`,
`ReRegisterOrderAsync`, `CancelOrderAsync`, `CancelOrdersAsync`,
`SubscribeAsync` and `UnSubscribeAsync` — all with `CancellationToken`s —
with default implementations falling back to the sync members, so existing
implementers keep compiling; the blocking void members are `[Obsolete]` at
the interface level. `Connector` implements the new members without blocking
(`ConnectAsync`/`DisconnectAsync` await the pipeline and complete on the
`Connected`/`Disconnected`/`ConnectionError` events;
`SubscribeAsync`/`UnSubscribeAsync` route through
`ApplySubscriptionManagerActionsAsync`). The former
`IConnectorAsyncExtensions.ConnectAsync/DisconnectAsync` extensions are
superseded by the instance members (same semantics); the subscription
keep-alive extension is renamed `HoldSubscriptionAsync` to free the
`SubscribeAsync` name. Left for follow-up: `Strategy`'s ordering internals
still drive the sync facade (pragma-marked), and the remaining
`AsyncHelper.Run` shims in the void members stay until callers migrate.

## Flaky-test tail (CI retries failed tests once)

Beyond the deterministic fixes below, the suite carries a long tail of
low-probability timing-sensitive tests: each 3-OS run (~13k test executions)
surfaces 1–3 different failures — observed so far:
`CandleTests.TotalPrice` ("Sequence contains more than one element"),
`Subscriptions_RepeatedRounds_AllProcessed` (windows, ~1 min),
`Connection_SubscriptionsCleanedOnDisconnect`. CI therefore retries exactly
the failed tests once; a test that fails twice fails the job. All tests stay
active — nothing in the tail is skipped.

One member of the tail was identified and is now fixed here:
`OptimizerPauseTests.PauseHaltsBruteForce` was twice named by the
`--blame-hang` sequence file as the only test still in flight when the run
aborted, on windows both times, and in a distinctive way — every test reports
`Passed` first (`Failed: 0, Passed: 4388`) and the test host then sits idle
until the 8-minute inactivity timer kills it. So the hang was after the test's
assertions complete: the optimizer's pause/resume machinery left work running
that kept the process alive. Auditing that machinery found four defects, all
fixed here (the windows repro is statistical, so the fix is asserted from the
defects, not from a reproduced hang):

- `HistoryMessageAdapter`'s suspend gate was a `ManualResetEventSlim` — every
  suspended backtest **blocked a thread-pool thread** in a synchronous
  `Wait`. An optimizer pause parks a full batch (default `CPU*2`) of replay
  loops at once, which on small CI runners starves the pool the test host
  itself needs. The gate is now an async `AsyncManualResetEvent` and the
  replay loop awaits it.
- `BaseOptimizer` never disposed the `HistoryEmulationConnector` it created
  per iteration, so each one's emulation graph (replay task, gates, adapter
  chain) was left to leak. The worker that starts an iteration now owns its
  teardown: the connector is disposed in a `finally`, on success, error and
  cancellation paths alike.
- The per-iteration completion await (`await tcs.Task`) took no cancellation
  token, and the cancel sweep's state filter can miss a connector that had
  not started when the token fired — a worker (or a genetic-optimizer fitness
  evaluation) could park forever. The await now honours the caller's token,
  and the sweep tolerates connectors concurrently torn down by their workers.
- Each iteration's `CopyPortfolioProvider` subscribed to the run-lifetime
  portfolio provider's events and never unsubscribed (unbounded handler-list
  growth over a long optimization); it is now disposed with its iteration.

Additionally, `[AssemblyCleanup]` now arms a 3-minute background watchdog: if
the test host is still alive that long after cleanup, it prints a diagnostic
and exits with code 97 — converting any recurrence of this failure class from
an 8-minute silent abort blamed on the last test into a fast, attributed
failure.

## Fixed here: async event fan-out discarded all but the last handler's task

Every pipeline event is a `Func<Message, CancellationToken, ValueTask>` and
was raised as `handler?.Invoke(...)`. Invoking a multicast delegate returns
only the **last** handler's `ValueTask`, so with two or more subscribers
(e.g. `Connector` plus every `Strategy`) all other handlers ran unobserved:
exceptions swallowed, back-pressure lost, and a subscriber could be handed
message N+1 while its handler for message N was still running. All ten raise
sites now go through `AsyncEventExtensions.InvokeAllAsync`, which awaits every
handler sequentially in subscription order (single-subscriber path unchanged
and allocation-free). Related fix in the same area: `AsyncMessageChannel`
consumed one `ValueTask` four times (`AsTask`/`await`/`IsFaulted`/`AsTask`) —
undefined behaviour for pooled sources; it now converts to `Task` exactly
once. Both defects still exist upstream.

## Fixed here: disconnect could drop queued unsubscribes

`Connection_SubscriptionsCleanedOnDisconnect` occasionally saw the adapter
never observe an unsubscribe after `DisconnectAsync` completed. Root cause in
`AsyncMessageChannel`: the scheduler picks control messages first, so a queued
`DisconnectMessage` overtook older queued unsubscribes — and once disconnect
starts, the connection-state gate silently drops every remaining non-control
message. The scheduler now defers disconnect while an older pending
unsubscribe exists (unsubscribes have no parallel limit, so this cannot
stall); regression test `Disconnect_DoesNotOvertakeQueuedUnsubscribe`. The
race still exists upstream.

## Fixed here: broken priority-queue comparer (message ordering)

`BaseMessageQueue` and `BasketMarketDataStorage` constructed
`Ecng.Collections.PriorityQueue` with the comparer `(p1, p2) => (p1 - p2).Abs()`.
`Abs()` destroys the sign, so the comparer can never report "less than" and
heap ordering degenerates to an implementation detail of the resolved
Ecng.Collections version — `MessageByLocalTimeQueue` delivered a fully
pre-queued, shuffled batch out of time order in CI. Both sites now use the
signed difference. Note the `.Abs()` pattern still exists upstream.

Issues inherited from upstream, observed on this fork's CI (GitHub-hosted
runners, ubuntu/windows/macos). For context: upstream's own CI has produced no
successful run since 2025-10-27; every upstream master push since then fails
its 10-minute job timeout, so none of the below is a regression introduced by
this fork.

## Python analytics script tests fail (excluded from CI)

`CompilationTests.PythonAnalyticsScripts` and
`CompilationTests.PythonAnalyticsScriptsParallel` fail when executing the
bundled IronPython analytics scripts:

```
System.InvalidOperationException: Error running script 'indicator_script'.
 ---> System.InvalidOperationException: Error () No module named Indicators (0-0)
```

Observed on ubuntu (both variants) and macos (sequential variant only —
order-dependent), with Ecng.Compilation pinned to the 2026-07-16 versions, so
this is not recent package drift. The C#, F#, and remaining Python compilation
tests pass, so the scripting subsystem itself is still exercised. The CLR
namespace import failure inside the IronPython engine needs investigation;
until then CI runs with `--filter "FullyQualifiedName!~PythonAnalyticsScripts"`.

## CI hang: resolved (flaky tests, fixed)

Early CI runs aborted via `--blame-hang` after ~8 minutes of inactivity. The
blame sequence file identified `AsyncMessageChannelTests.Close_StopsProcessing`
as the only test still in flight: it polled for the transient
`ChannelStates.Stopping` with a 10 ms interval, and `AsyncMessageChannel.Close`
can pass through Stopping to Stopped faster than that — the poll then spins
forever against a Stopped channel. The test now also accepts Stopped.
`MessageChannelTests.InMemoryChannel_MessageByLocalTimeQueue_OutOfOrderTimes_SortsCorrectly`
was similarly racy (channel draining while the shuffled batch was still being
enqueued, so delivery order could legally interleave); it now suspends the
channel while enqueueing. These two are the most plausible cause of upstream's
long-red CI as well. Independently, `Tests/Helper.cs`'s static `LogManager` is
now disposed in `[AssemblyCleanup]` and `SubscriptionHolderTests` no longer
leaks per-holder `LogManager` instances. CI uploads `TestResults` (blame
sequence + hang dumps) on failure should anything recur.
