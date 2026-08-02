# 0001. Adopt semantic line breaks in documentation

Status: accepted, 2026-08-02

## Context

Prose wrapped at a fixed column scatters each thought across
arbitrary line boundaries:
a grep hit returns half a sentence,
and a one-word edit rewraps a paragraph into a many-line diff.
Markdown joins consecutive source lines with a space,
so line placement inside a paragraph is invisible to readers.

## Decision

We will break documentation source after every sentence
and every independent clause,
following the Semantic Line Breaks specification at <https://sembr.org/>,
as implemented by the `illi-format-sweep` skill
and its CJK-and-HTML variant `illi-format-sweep-cjk`.

## Consequences

A grep hit returns a whole thought,
and a one-word edit lands as a one-line diff.
The cost is ragged source lines,
which shows up as long lines in editors without soft wrap;
readers set their tool's wrap mode once.
CJK prose ventilates only where rendering provably survives,
which shows up as `README.zh.md` staying mostly single-line —
renderers place a visible space between joined CJK lines,
and preserving rendered output outranks source shape.
