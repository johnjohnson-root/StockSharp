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

## Test host may not exit after the run completes

On the first CI runs, all 4,365 tests executed (4,361 green) and the test
host process then sat idle until `--blame-hang` (8 min inactivity) aborted it,
failing the job. No `new Thread(` exists in this repository, so the
foreground thread keeping the process alive comes from a dependency.
Addressed so far: the static `Tests/Helper.cs` `LogManager` is now disposed in
`[AssemblyCleanup]`, and `SubscriptionHolderTests` no longer creates
per-holder `LogManager` instances. If the hang recurs, CI now uploads
`TestResults` (blame sequence files + hang dumps) as artifacts for diagnosis.
