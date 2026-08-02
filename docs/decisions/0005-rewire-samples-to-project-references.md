# 0005. Rewire Samples to project references

Status: accepted, 2026-08-02

## Context

`Samples/**` references upstream `StockSharp.*` NuGet binaries
and sits outside both solutions,
so the samples compile against the upstream the fork replaced
and cannot see a fork API change until a reader trips over it.

## Decision

We will point every sample project at the fork's projects
by `ProjectReference`,
collect them in a samples solution,
and compile that solution in CI (TODO T9).

## Consequences

Samples become living documentation:
an API change that breaks a sample breaks the build that ships it.
The cost is a CI leg and sample maintenance alongside refactors,
which shows up as sample edits inside API-changing pull requests;
under record 0006 the public API holds until 2.0,
so that cost stays near zero until the declared break.
Samples requiring proprietary connectors stay outside the solution,
which shows up as an exclusion list beside it, each entry named.
