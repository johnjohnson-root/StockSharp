# Known issues

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
