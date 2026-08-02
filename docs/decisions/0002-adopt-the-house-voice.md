# 0002. Adopt the house voice for repository prose

Status: accepted, 2026-08-02

## Context

Documentation, comments, commit messages, and CLI text accumulated
in mixed registers:
passive constructions, fragment labels, war-story comments,
and negation-led claims that leave the wrong example in the reader's mind.
A shared standard did not exist,
so each surface drifted with its author.

## Decision

We will write every prose surface that ships in this repository
in the house voice defined by the `illi-voice` skill,
with two variants resolving its clashes with language conventions:
`illi-voice-csharp` maps the comment and reference rules onto
C# XML documentation
(adjacent `///` blocks, third-person `<summary>`,
`<exception cref>` as the raises section),
and `illi-format-sweep-cjk` (record 0001) governs CJK and HTML formatting.
Where a variant overlaps its parent, the variant wins.

## Consequences

Prose across the repository reads as one author:
affirmative, active, verified against the code it describes.
The cost is a rule set contributors learn,
which shows up as review comments on wording;
`CONTRIBUTING.md` and the skills under `.claude/skills/` carry the rules,
so the learning is a read, not an apprenticeship.
Upstream-authored member documentation stays untouched,
which shows up as a register seam at fork boundaries;
the seam is deliberate — rewriting upstream prose buys merge friction
with no behavioral gain.
