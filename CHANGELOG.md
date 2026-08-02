# Changelog

What changed in this fork, newest first,
written from what a consumer observes rather than from what the commits touched.
Dates are ISO.

This fork carries upstream StockSharp's history through
[`22ca8fb`][fork-base], its last Apache-2.0 commit, dated 2026-07-16.
Everything below is the fork's own work.
Nothing has been released yet:
packages build as CI artifacts under the `StockShark.*` prefix
(decision record [0004](docs/decisions/0004-stockshark-prefix-artifacts-only.md)),
so the section below stays `Unreleased` until a feed exists.

## [Unreleased]

### Added

- The trading interfaces expose async members —
  `ConnectAsync`, `DisconnectAsync`, `RegisterOrderAsync`, `EditOrderAsync`,
  `ReRegisterOrderAsync`, `CancelOrderAsync`, `CancelOrdersAsync`,
  `SubscribeAsync` and `UnSubscribeAsync`, each taking a `CancellationToken`.
  `Connector` implements them without blocking a thread.
  Existing implementers keep compiling:
  the interfaces carry default implementations that fall back to the sync members.
  The sync members are `[Obsolete]`;
  call the async member of the same name instead.
- `StockSharp.Foundation.Collections` ships first-party
  `SynchronizedDictionary`, `SynchronizedList`, `SynchronizedSet` and
  `SynchronizedQueue`, their `Cached*` variants, the `ISynchronized` marker,
  and the `SafeAdd` / `TryAdd2` / `SyncGet` extension set.
  `BusinessEntities` and `Alerts.Interfaces` run on them today.
- The repository builds and tests from a clean clone by itself:
  no sibling checkouts, no private feeds, no folder-name requirements.
- `nuget-mirror/` is a folder feed carrying the exact package closure both
  solutions build against, so a restore survives any pinned package being
  unpublished or delisted.
  Build it with `./tools/mirror-packages.sh`,
  restore from it with `dotnet restore StockSharp.slnx --source ./nuget-mirror`.
- A weekly workflow scans the pinned graph for security advisories and fails
  the run on any hit, naming the package, severity and advisory URL.
  Pinned packages take no patches, so the scan is the whole mitigation.
- `StockSharp_Samples.slnx` collects the samples that build against this
  repository alone, and CI compiles it,
  so an API change that breaks a sample breaks a build.
  `Samples/README.md` lists the 31 samples the solution excludes and the
  dependency that blocks each.
- `docs/ecng-surface.md` measures the consumed `Ecng.*` API from IL metadata —
  27 packages, 279 types, 968 members —
  and ranks them into four replacement waves.
- `docs/decisions/` records the choices the fork has made, numbered and dated.

### Changed

- Packages carry the `StockShark.*` id prefix.
  Assembly names, namespaces and public API stay `StockSharp.*`,
  so switching from upstream is a package-id change and nothing else.
  Override the prefix with `-p:ForkPackagePrefix=...`.
- External package versions are pinned to their newest release before
  2026-07-16, and `NuGet.config` declares a fixed source list,
  so a restore resolves the same graph on every machine.
- `global.json` pins the SDK to 10.0.302 with `rollForward: latestPatch`.
  A machine carrying only another feature band now fails the build with a
  message naming the missing version instead of building against a different
  compiler.
- `Algo.Compilation` references the three compiler backends by name rather
  than the `Ecng.Compilation.All` meta-package, which drops one pin.
- The post-cleanup test watchdog waits 60 minutes rather than 3 before
  reporting a leaked foreground thread.

### Fixed

- Every subscriber to a pipeline event now runs.
  Events are `Func<Message, CancellationToken, ValueTask>` raised on a
  multicast delegate, which returns only the last handler's task, so with two
  or more subscribers — a `Connector` plus any `Strategy` — every other
  handler ran unobserved: exceptions swallowed, back-pressure lost, and a
  subscriber could be handed message N+1 while its handler for message N was
  still running.
- Disconnecting no longer drops queued unsubscribes.
  The channel scheduler takes control messages first, so a `DisconnectMessage`
  overtook older queued unsubscribes, and the connection-state gate then
  dropped them.
- Message queues deliver in time order.
  `BaseMessageQueue` and `BasketMarketDataStorage` built their priority queue
  with a comparer that discarded the sign of its difference, so it could never
  report "less than" and heap order became arbitrary.
- An optimizer run no longer abandons its remaining iterations when one fails
  to start. A reserved batch slot was released only by the connector's
  `Stopped` transition, so any of five throw sites — or cancellation before
  the connector started — leaked the slot, and surviving workers then read the
  batch as exhausted and stopped early, reporting a short run as a complete
  one.
- A genetic optimization survives a parameter set that fails to start.
  The failed evaluation scores `double.MinValue` and logs the error, where it
  previously left the GA loop and ended the run with a truncated result set
  and no error reaching the caller.
- Buffered market data survives a storage failure.
  The 10-second flush cycle read each buffer with a snapshot-and-clear and
  wrapped the whole cycle in one log-only handler, so a `SaveAsync` that
  threw stranded every message already taken in that cycle - out of the
  buffer, never in storage - including securities the same call had handed
  over. The cycle now saves key by key and holds what it could not persist
  for the next pass, bounded at 100,000 messages per key with a warning
  when it trims. A cycle that fails also waits its interval before
  retrying, where it previously spun at full speed.
- A suspended backtest no longer blocks a thread-pool thread.
  An optimizer pause parks a whole batch of replay loops at once, which
  starved the pool on small machines.
- A paused or cancelled optimizer releases its per-iteration resources:
  the emulation connector, its replay task and adapter chain, and the
  per-iteration portfolio provider's event subscriptions.

### Security

- Nothing yet. The advisory scan above is the watch;
  an advisory against a pinned package escalates that package's replacement
  immediately, per decision record
  [0003](docs/decisions/0003-replace-ecng-clean-room.md).

[Unreleased]: https://github.com/johnjohnson-root/StockSharp/compare/22ca8fb...master
[fork-base]: https://github.com/johnjohnson-root/StockSharp/commit/22ca8fb697ff726271fa543d7d1df5d20af7bd05
