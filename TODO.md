# TODO

This file is the fork's work inventory,
written for an agent to execute one item at a time.
Each item stands alone:
its context, its steps, its verification, and its finish line.
`KNOWN-ISSUES.md` carries the defect history behind several items,
and `.claude/skills/` carries the prose rules every change follows.

## Working an item

1. Read the item's `Blocked by` line.
   A decision id there means the owner has not chosen yet;
   work something else.
2. Implement on the session's designated branch,
   following `illi-voice` for every prose surface
   and `illi-voice-csharp` for C# comments.
3. Run the item's `Verify` commands and the standard gate below.
4. Commit per the `illi-voice` commit rules,
   push, and let CI confirm across the 3-OS matrix.

The standard gate for any code change:

    dotnet build StockSharp_Tests.slnx -c Release
    dotnet test StockSharp_Tests.slnx --no-build -c Release \
      --filter 'FullyQualifiedName!~PythonAnalyticsScripts'

4458 pass, 0 fail, 11 documented skips, prompt test-host exit.
A new test earns its place by failing on the unfixed code:
stash the fix, build, watch the test fail, pop the stash.

## Decisions

The five founding decisions are made
and recorded under `docs/decisions/`:

- **0003 — Ecng strategy**: clean-room replacement, leaves-first,
  contract tests first; each pin drops as its consumer count reaches zero.
- **0004 — Packaging**: ids are `StockShark.*`;
  publishing stays artifacts-only until an external consumer exists.
- **0005 — Samples**: rewired to project references and compiled in CI (T9).
- **0006 — Divergence**: the drop-in `StockSharp.*` contract holds
  through 1.x;
  breaks queue as `for 2.0` decision records and land once, at 2.0.
- **0007 — Watchdog**: 60 minutes (`Tests/AsmInit.cs`).

Records 0001 and 0002 cover the prose standards already in force.
A new consequential choice takes the next number in the sequence.

## Dependency sovereignty

### T1. Ecng surface inventory — done 2026-08-02

`docs/ecng-surface.md` measures the closure from IL metadata:
28 packages as measured, 279 consumed types, 968 consumed members,
ranked in four replacement waves.
The wave 1 rank 1 pass has since taken the closure to 27.
Waves 1–2 clear 13 packages for roughly 25 members of implementation;
the first target, `Ecng.Compilation.All` (zero members), is done,
which leaves `Ecng.Interop` and `Ecng.Net` (one member each) next.
The load-bearing finding: 86 Ecng types sit in the fork's own public
surface across 14 packages,
so replacing those carries a type-forwarding shim
or a `for 2.0` record per decision 0006,
while the 14 zero-exposure packages replace freely inside 1.x.
The method section lists the measurement's blind spots
(enum constants, attribute blobs, reflection-driven lookup).

### T2. Replace the next Ecng surface clean-room

Blocked by: T1's ranking. Repeatable; one surface per pass.
Decision record 0003 sets the method.
Wave 1 rank 1 (`Ecng.Compilation.All`) is done —
see the inventory's `Completed passes` section;
the closure is 27 packages and the pin list 20 ids.

Follow the Foundation pattern end to end:

1. Pick the top-ranked package from `docs/ecng-surface.md`.
2. Write contract tests against the current binary's observed behavior
   first (`Tests/FoundationExtensionContractTests.cs` is the template —
   assert semantics, not implementation).
3. Implement replacements under `Foundation/` (or a sibling project named
   for the domain), `StockSharp.*` namespaces, no source copied.
4. Migrate consumers project-by-project; each migration compiles and
   passes the standard gate before the next begins.
5. When a package's fan-in reaches zero, proceed to T3 for it.

Verify: contract tests pass against the replacement,
the standard gate is green,
and `grep -r "<replaced namespace>" --include='*.cs'` finds no consumer.

### T3. Drop a pin whose surface reached zero

Blocked by: a completed T2 pass.

1. Remove the package's pin from `Directory.Build.targets`.
2. Run `tools/mirror-packages.sh` so the mirror matches the new closure.
3. Verify offline restore:
   restore both solutions with `--source ./nuget-mirror --no-http-cache`
   into a fresh `--packages` folder.
4. Update `docs/ecng-surface.md` and, where present, the count in
   `README.md`'s pinning section.

Verify: standard gate green; offline restore succeeds; mirror artifact
in CI ("Mirror packages" workflow) uploads without error.

### T4. Advisory watch — done 2026-08-02

`.github/workflows/advisories.yml` runs
`dotnet list package --vulnerable --include-transitive` weekly
and fails the run on any hit,
so a notification names the package, severity and advisory URL.
`dotnet list` exits 0 either way,
so the step reads the per-project report text
and fails loudly when a restore or the command itself breaks
rather than reporting a clean scan over nothing.

It scans three solutions rather than the one this item named:
`StockSharp_Tests.slnx` misses the `Localization.Langs` satellites
and the two `Algo.Analytics` script projects,
and `Ecng.Interop` reaches the graph through a sample alone.

The header records the response path.
An advisory against a pinned package escalates that package's
clean-room replacement immediately, per decision record 0003,
because replacement is the only remedy a frozen package admits;
a hit against an unpinned transitive package takes a version bump.
The `pull_request` trigger on the pin files re-scans the graph
whenever it changes, which is also what proved the workflow green —
a schedule runs from the default branch alone.

## Packaging and release

### T5. Tag-driven release workflow

Blocked by: a publishing decision superseding record 0004
(artifacts-only today).

1. On tag `v*`: build, pack with `-p:ForkPackageVersion=<tag>`,
   run the standard gate, publish to the chosen feed, attach the
   packages and the mirror artifact to a GitHub Release.
2. Route the feed credential through a repository secret;
   route the tag value through env, never template interpolation
   (the `pack.yml` header records why).
3. Seed `CHANGELOG.md` (T18) and cut the entry as part of the release.

Verify: a `v0.0.1-rc.1` tag on a scratch run publishes to the feed
and the standard gate ran before publish.

### T6. Run the mirror workflow once and stash the artifact

Blocked by: nothing. The recovery path exists and nobody has exercised
it end to end in CI.

1. Dispatch "Mirror packages" on `master`.
2. Confirm the offline-restore verification step passed in the log.
3. Download the `nuget-mirror` artifact and store it somewhere durable
   outside GitHub's 90-day artifact window —
   attaching it to a draft GitHub Release named `mirror-<date>` suffices
   and keeps it in-repo.

Done when a durable copy exists and its location is named in
`nuget-mirror/README.md`.

### T7. Lock the restore graph

Blocked by: nothing.

`Directory.Build.targets` pins direct versions; the transitive closure
still floats within constraints.

1. Set `RestorePackagesWithLockFile=true` in `Directory.Build.props`.
2. Commit every generated `packages.lock.json`.
3. Add `--locked-mode` to the CI restore
   so drift fails the build instead of passing silently.
4. Re-run `tools/mirror-packages.sh`; confirm the closure count.

Verify: standard gate green; a deliberate version bump of one package
without lock regeneration fails CI in locked mode (then revert it).

### T8. SDK pin — done 2026-08-02

`global.json` names 10.0.302 —
the version CI resolved, read from a `setup-dotnet` step on master —
with `rollForward: latestPatch` and `allowPrerelease` off,
so the 10.0.3xx band takes patches
and a jump to another band fails the build naming the missing version
instead of changing compiler and analyzer behaviour silently.
All five workflows ask `setup-dotnet` for 10.0.302 rather than `10.0.x`,
so the runner installs exactly what the build requires.
The `6.0.x` line stays floating:
nothing in either solution targets net6.0,
the entry supplies a runtime alone,
and 6.0 is out of support so its resolved patch no longer moves.
CI green on all three operating systems.

## Samples

### T9. Samples solution and CI leg — done 2026-08-02

The inventory overturned the premise:
upstream's `Samples/common_samples.props` already wires the core
by `ProjectReference`,
and every other `StockSharp.*` package the samples reference —
`Xaml`, `Xaml.Charting`, the connectors, `Web.Api.Client`,
`Studio.WebApi.UI` — has no project in this repository,
so zero references needed rewiring.
`StockSharp_Samples.slnx` carries the four samples buildable from this
repo alone; `.github/workflows/samples.yml` compiles it on ubuntu;
`Samples/README.md` lists all 31 exclusions with their blocking
dependency.
Excluded samples keep their upstream references by design
(record 0005 excludes them, it does not gut them),
including the 20 Xaml.Charting samples that would compile only against
upstream nuget.org binaries —
a coupling already broken today by fork drift (`CS7069` in two samples).
Growing the solution means porting a charting surface or dropping the
WPF samples: a future decision record.

## Correctness and tests

### T10. Bring GeneticOptimizer to BruteForce's failure semantics

Blocked by: nothing.

`BruteForceOptimizer` workers survive a poisoned iteration, release the
slot, and cap consecutive failures (commits `f98b0df`, `9b57476`).
The genetic path awaits `TryNextRunAsync` inside a GeneticSharp fitness
evaluation and propagates every exception into the GA loop.

1. Read `GeneticOptimizer.cs` end to end;
   map what GeneticSharp does with a throwing fitness function
   (termination? silent chromosome death?) and what `CompleteChannel()`
   at line ~462 covers.
2. Decide and implement the parallel behavior:
   a failed fitness evaluation scores the chromosome worthless
   (`double.MinValue`, as the iteration-limit path already does)
   rather than killing the run, with the error logged.
3. Regression tests mirroring the BruteForce ones:
   poisoned `StrategyInitialized` for one parameter set — the run
   completes and other chromosomes evaluate;
   pre-cancelled token — clean termination, zero results.
4. Prove each test bites: stash the fix, watch it fail, pop.

Verify: `--filter 'FullyQualifiedName~GeneticOptimizer|FullyQualifiedName~OptimizerTests'`
green; new tests stable over 4 consecutive runs; standard gate green.

### T11. Reconcile the two finish predicates

Blocked by: nothing. Small.

`BaseOptimizer.CheckFinished` completes the channel on
`_batchManager.IsFinished` alone;
`OnIterationCompleted` requires `_allIterationsStarted && IsFinished`.
Either prove the weaker predicate safe on every `CheckFinished` call
site (each site sets `_allIterationsStarted` first — write that proof
into a comment) or align both to the conjunction.

Verify: reasoning recorded at `CheckFinished`;
optimizer suites green; standard gate green.

### T12. Flaky tail — causes found 2026-08-02, retry removal pending

All three documented members now have a cause and a test-only fix,
one commit each, and `KNOWN-ISSUES.md` carries the reasoning:

- `CandleTests.TotalPrice` built its ticks from a raw `DateTime.UtcNow`,
  so a base time in the final second of a minute split the first two
  ticks across candles and `Single()` threw.
  The base time is anchored to the minute. Deterministic, not reasoned.
- `Subscriptions_RepeatedRounds_AllProcessed` and
  `Connection_SubscriptionsCleanedOnDisconnect` both gated on a count
  of `SubscriptionOnline` events instead of on their own transaction
  ids, so a late cross-round event or the connector's own order-status
  subscription opened the gate early;
  `UnSubscribeAll` then skipped a subscription that had not reached
  Online, and the next wait ran to the test timeout.
  Both gates wait for their own ids now.
  Reasoned from the test sources rather than reproduced,
  which is the same standard the optimizer teardown fixes met.

What remains is evidence, not code:
the retry-once mechanism in `dotnet.yml` comes out in its own commit
once five consecutive CI runs record no retry.
A new tail member appears as a new entry here and in `KNOWN-ISSUES.md`.

### T13. Skips and the Python exclusion — audited 2026-08-02

Both answers live in `KNOWN-ISSUES.md`.

The 11 skips are one cause, not eleven:
every `ExportTests` case except `Cancellation` routes through the
private `ExportAsync` helper,
which ends by building a `DatabaseExporter` around
`GetSecret("SQLSERVER_CONNECTION_STRING")`;
`BaseTestClass.GetSecret` reports the test inconclusive when the secret
is absent, and this repository configures no SQL Server credential.
The blocking condition is that one repository secret, named exactly —
not a data file, a proprietary dependency, or an upstream defect.
The coverage loss is narrower than the count suggests:
each test asserts the text, XML, JSON and XLSX exports before reaching
the database, so those four run on every CI run and only their outcome
is mislabelled.
Splitting the database assertion into its own test would leave one
honest skip instead of eleven, at a test method per data type.

The Python exclusion was recorded from ubuntu and macos alone,
and no run has ever measured windows.
`dotnet.yml` now carries a windows-only probe running the two excluded
tests, `continue-on-error` so it reports without failing the job.
Two runs of evidence settle it:
windows passing turns the wholesale filter into a per-OS one,
windows failing makes the defect platform-independent and points the
investigation at the IronPython `Indicators` import.
Remove the probe once `KNOWN-ISSUES.md` records the answer.

The audit missed a third class on its first pass,
and `KNOWN-ISSUES.md` now carries it:
roughly 32 tests guard on `Paths.HistoryDataPath` being null
and `return` after a `Console.WriteLine`,
which MSTest reports as passed with no assertion executed —
`BacktestingTests.cs:559` (24 callers),
`StrategyDecomposedEquivalenceTests.cs:222` (6 callers),
and `OptimizerPauseTests.cs:81,168`.
Six sibling sites call `Inconclusive(...)` instead,
two of them with a comment saying why,
so the fix is to make the three above match them.
That is the remaining work in this item.

### T14. Retire the sync-facade shims — inventory corrected 2026-08-02

An earlier pass read this inventory off the `CS0618` warnings in a CI
build log and reported 13 sites.
That method undercounts twice over:
the log came from the samples workflow, which compiles four sample
projects rather than the library solution,
and a suppressed site emits no warning at all —
so it excluded every `#pragma warning disable CS0618` marker,
which is precisely what step 1 asks for.

The library projects carry 71 such markers:
`Algo.Strategies` 28, `BusinessEntities` 17, `Algo` 14,
`Messages` 11, `Diagram.Core` 1.
They fall into three kinds, and only the first is this item's work.

**Internal callers on the sync facade — 31 sites, the migration target.**
Pragma-marked, found by
`grep -rn '#pragma warning disable CS0618' --include='*.cs'`:

    Algo/PositionManagement/PositionTargetManager.cs   115, 155, 277, 287
    Algo.Strategies/Quoting/QuotingProcessor.cs        159, 297, 313
    Algo.Strategies/Strategy.cs                        814, 930, 945, 1000
    Algo.Strategies/Strategy_TransactionProvider.cs    188, 196
    BusinessEntities/EntitiesExtensions.cs             123, 131, 189, 2353

Unsuppressed, read from the `CS0618` lines of a real
`dotnet build StockSharp_Tests.slnx -c Release`:

    Algo/TraderHelper.cs                            201, 1196
    Algo/Storages/Csv/CsvEntityList.cs              157
    Algo.Strategies/Optimization/BaseOptimizer.cs   608, 649
    Algo.Strategies/Quoting/QuotingProcessor.cs     183, 200, 447
    Algo.Strategies/Strategy.cs                     90, 91
    Algo.Strategies/Strategy_HighLevelMisc.cs       154
    BusinessEntities/EntitiesExtensions.cs          127, 134
    Diagram.Core/Elements/OrderMassCancelDiagramElement.cs   102

That build reports 89 distinct `CS0618` sites in all;
the 75 not listed above sit in `Tests/`,
which exercises the obsolete surface deliberately and stays as it is.

`EntitiesExtensions.ReRegisterOrderEx` shows the split inside one method:
`EditOrder` and `CancelOrder` sit under pragmas at 123 and 131,
while `ReRegisterOrder` at 127 and `RegisterOrder` at 134 sit outside them.
Suppressing all four, or none, is a one-line decision someone should make
before the migration starts.

**Compat fallbacks the divergence record keeps — 12 sites.**
The default interface implementations in `IConnector` (126, 162),
`ITransactionProvider` (138, 153, 168, 182, 202),
`ISubscriptionProvider` (171, 185),
`ISubscriptionProviderAsyncExtensions` (104, 145)
and `IConnectorAsyncExtensions` (92)
call the deprecated sync member so an external implementer that never
adopted the async members keeps compiling.
Record 0006 holds exactly that contract through 1.x,
so these stay until the declared break.

**Deprecations unrelated to the async migration — 28 sites.**
`Strategy`'s obsolete event surface (10),
the obsolete `StrategyOld` monolith (9),
`Unit`/`UnitTypes` and `ExecutionTypes` in `Messages` (11 across
`Unit.cs`, `UnitHelper.cs`, `DataType.cs`, `Extensions.cs`),
obsolete members in `Connector`, `StorageHelper` and `CandleHelper`,
`Security`'s Level1 legacy fields,
and the dated `CandleSeries` format shim in `Diagram.Core`.
These belong to their own deprecations, not to this one.

Step 2 stays open, and the reason has not changed:
none of the 31 is a local edit.
Each sits in a sync method whose signature would have to change,
so each is a chain reaching its callers —
`CsvEntityList` line 157 is inside a `byte[]`-returning member,
`TraderHelper` line 1196 inside `CreateReader`, which returns a
`FastCsvReader`,
and the `QuotingProcessor`, `PositionTargetManager` and `BaseOptimizer`
sites are called from sync event handlers where awaiting reorders
delivery.
The item's own rule — each migration compiles and passes the standard
gate before the next — needs a local build loop rather than a CI round
trip per step.

Take them one chain at a time, smallest first.
Regenerate the unsuppressed half with
`dotnet build StockSharp_Tests.slnx -c Release` and read the `CS0618`
lines; regenerate the suppressed half with
`grep -rn '#pragma warning disable CS0618' --include='*.cs'`.
Both halves have to fall for the count to reach zero,
and neither command finds the other's sites.

Read both from the tools rather than from a summary.
An earlier revision of this entry listed four `TraderHelper` sites at
829-832 that do not warn at all:
`Connector.Subscribe` is a concrete method and carries no `[Obsolete]`,
where the `ISubscriptionProvider.Subscribe` it resembles does.
The same revision missed the three `QuotingProcessor` sites
and the `Diagram.Core` one.

### T15. The two in-tree upstream defects — closed 2026-08-02, no work

The item read `KNOWN-ISSUES.md` as naming two open defects.
It names one, and that one is fixed.

The priority-queue comparer landed as commit `638eba8`:
`BaseMessageQueue` and `BasketMarketDataStorage` both build
`PriorityQueue` with the signed difference `(p1, p2) => p1 - p2`,
and `MessageByLocalTimeQueue_HighVolume_HandlesCorrectly` asserts
full sort order over 1,000 messages,
which is the test that bites on a comparer that cannot report
less-than.
`MessageByLocalTimeQueue_EnqueueDequeue_SortsMessagesByLocalTime` and
`InMemoryChannel_MessageByLocalTimeQueue_OutOfOrderTimes_SortsCorrectly`
cover the same contract at unit and channel level.

The second entry pointed at the line
`The .Abs() pattern still exists upstream`,
which closes the comparer section rather than opening another:
it says upstream kept the broken comparer, not that this tree carries
a `TimeSpan` overflow.
A grep for `.Abs()` and `.Duration()` over every library project finds
no `TimeSpan`-typed call site at all —
every hit is `decimal`, in the indicators and the tests.

The remaining upstream defects worth fixing appear as their own
`KNOWN-ISSUES.md` sections when someone finds them.

## Governance and docs

### T16. Decision records — done 2026-08-02

`docs/decisions/0001`–`0007` exist and follow the illi-voice format:
sequential number, Status field, forcing-condition Context,
"We will" Decision, and costs-beside-buys Consequences.
A future decision continues the numbering;
supersession adds a record and leaves the old one standing.

### T17. CONTRIBUTING.md — done 2026-08-02

One page in lifecycle order:
what the fork is, build and test, prose, commits and pull requests,
decisions, license.
The build section carries the standard gate verbatim
and says what a green run reports,
including why 11 tests skip and why the Python filter is there,
so a newcomer reaches a green gate from this file alone.

Two conventions are written down for the first time:
a regression test earns its place by failing on the unfixed code,
and the commit body says so and what the failure looked like;
a comment-only change carries a code-identity check.
The decisions section states the record format
and names `0006` as the one most changes touch.

### T18. CHANGELOG.md — done 2026-08-02

Seeded from the fork's shipped work,
written from what a consumer observes rather than from what the commits
touched, under Added / Changed / Fixed / Security headings.
Everything sits in `Unreleased`,
because record 0004 keeps packages as CI artifacts until a feed exists.

The compare link resolves against `22ca8fb`,
upstream's last Apache-2.0 commit and this fork's declared base,
because the repository carries no tags yet.
Cutting a release means adding a version heading above `Unreleased`
and pointing a new compare link at the tag (T5).

### T19. .editorconfig — done 2026-08-02, two sweeps deferred

Encodes what the tree measurably does:
tabs in `.cs` (384 of a 400-file sample, none at four spaces),
LF everywhere (1,636 of 1,637 tracked `.cs` files;
`Algo/TraderHelper.cs` is the one CRLF file),
two-space indent for the XML, YAML, JSON and Markdown families,
tabs in shell scripts,
and the `illi-voice-csharp` doc-comment adjacency rule.

Two settings the item asked for are deliberately unset,
because the tree is not uniform about either
and setting one would rewrite hundreds of files on the next format —
which the item's own verification forbids:

    byte-order mark   533 .cs files carry one, 931 do not
    final newline     789 .cs files end with one, 675 do not

Each is a tree-wide sweep in its own commit,
after which the value belongs in `.editorconfig`.
No analyzer severities: adding one changes the build's warning set,
and the honest way to add it is to compare warning counts across a real
build, a rule at a time.

### T20. Optional: sweep the upstream C# comment surface

Blocked by: an explicit owner request. Deliberately out of scope so
far: ~1,600 upstream files, large churn, prose that is not the fork's.
If requested: batch by project, comment-only line changes with the
code-identity check per batch, `illi-voice-csharp` rules,
upstream-authored `<summary>` prose corrected only where factually
wrong against the code.

### T21. Optional: trim the upstream marketing README tail

Blocked by: an explicit owner request; tied to D4's posture.
The English README below `## Introduction ##` is upstream marketing
(connector matrix, product links) that describes products this
repository does not contain.
If requested: replace with a short factual capability section plus a
link to upstream's site, and mirror the decision in the RU and ZH
READMEs (CJK rules apply to the ZH edit).
