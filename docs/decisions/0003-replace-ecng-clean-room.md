# 0003. Replace the Ecng dependency clean-room

Status: accepted, 2026-08-02

## Context

The fork consumes `Ecng.*` and `StockSharp.Samples.HistoryData`
as binaries pinned to the last pre-relicense releases,
published on nuget.org by the upstream owner.
Pinning plus the offline mirror removes the availability risk
and leaves the evolution risk:
a frozen layer takes no fixes, no CVE patches, and no API growth,
and every advisory against it is unpatchable for as long as it remains.
The Foundation collections proved the replacement pattern:
contract tests written from observed behavior,
then an implementation written without reading the replaced source.

## Decision

We will replace the consumed Ecng surface clean-room,
package by package in leaves-first order,
writing contract tests before each implementation
and dropping each pin when its consumer count reaches zero
(TODO items T1–T3).

## Consequences

The fork ends up owning its whole dependency layer
with provenance no license audit questions.
The cost is engineering time proportional to the consumed surface,
which shows up as the `docs/ecng-surface.md` inventory's size classes;
the leaves-first order keeps every intermediate state shippable.
Until a pin falls, its package stays frozen,
which shows up in the advisory watch (TODO T4):
an advisory against a pinned package escalates
that package's replacement immediately.
