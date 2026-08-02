# 0004. Package as StockShark.*, publish as artifacts only

Status: accepted, 2026-08-02

## Context

The Pack workflow builds 59 fork-branded packages
whose id prefix was the placeholder `SSharp`,
and no feed consumes them:
publishing was deferred at packaging time,
and the placeholder awaited a real name.

## Decision

We will name fork packages `StockShark.*`
(`ForkPackagePrefix` in `common_packaging.props`;
`-p:ForkPackagePrefix=...` still overrides per pack),
and we will publish them as CI artifacts alone
until an external consumer exists.

## Consequences

The packages carry a stable identity a future feed can adopt unchanged,
and no namespace is claimed on any public feed prematurely.
Consuming the packages today means downloading a workflow artifact,
which shows up as a manual step for any early adopter;
that friction is the signal to revisit publishing (TODO T5).
Assembly names and namespaces stay `StockSharp.*` (record 0006),
so the package id remains the single difference from upstream.
