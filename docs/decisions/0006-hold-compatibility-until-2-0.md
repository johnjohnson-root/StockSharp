# 0006. Hold upstream compatibility, break once at 2.0

Status: accepted, 2026-08-02

## Context

A consumer switches from upstream to this fork
by changing package ids alone:
assembly names, namespaces, and public API stay `StockSharp.*`-shaped.
Every refactor proposal now needs a rule for what it may break,
and the fork's own defect fixes (optimizer teardown, slot release)
have so far landed without any public-surface change.

## Decision

We will hold the drop-in contract —
`StockSharp.*` assemblies, namespaces, and public API —
through every 1.x release,
collect desired breaks as decision records marked `for 2.0`,
and break once, deliberately, at a declared 2.0.

## Consequences

Migration stays a package-id change for the whole 1.x line,
and the fork accumulates a reviewed break list
instead of a trail of incidental ones.
The cost is carrying suboptimal upstream shapes until 2.0,
which shows up as internal shims —
the sync facade and `AsyncHelper.Run` members (TODO T14)
migrate internally now
and shed their public surface only at the declared break.
A break wanted early needs its own superseding record,
which is the intended friction.
