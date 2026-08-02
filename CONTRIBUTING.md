# Contributing

This fork preserves the last Apache-2.0 state of StockSharp.
Upstream replaced that license with a proprietary EULA-based notice on
2026-07-16; `master` here is the direct parent of that change
(upstream commit `22ca8fb69`) plus the fork's own work,
and carries no code from the proprietary era.
`LICENSE` remains Apache License 2.0,
and every contribution ships under it.

The work queue is `TODO.md`.
Each item there stands alone and states its own verification.
`KNOWN-ISSUES.md` carries the defect history behind several of them.

## Build and test

The build needs the .NET 10 SDK.
`global.json` pins 10.0.302 with `rollForward: latestPatch`,
so a machine carrying only another feature band fails with a message
naming the version it wants.

Take that SDK from Microsoft's installer rather than a distribution
package.
Ubuntu 24.04's own archive ships the 10.0.1xx band —
`apt install dotnet-sdk-10.0` lands on 10.0.110 —
and `rollForward` only moves forward,
so a 1xx SDK cannot satisfy a 3xx pin however the policy is set.
`dotnet --version` run at the repository root is the check:
it prints the pinned line or names the version it wants.

    git clone https://github.com/johnjohnson-root/StockSharp.git
    cd StockSharp
    dotnet build StockSharp_Tests.slnx -c Release
    dotnet test StockSharp_Tests.slnx --no-build -c Release \
      --filter 'FullyQualifiedName!~PythonAnalyticsScripts'

Those two commands are the standard gate every code change passes.
A green run reports 4458 passed, 0 failed, 11 skipped,
and the test host exits promptly.
The 11 skips are `ExportTests` cases needing a SQL Server credential,
and the filtered Python tests fail on an IronPython defect —
`KNOWN-ISSUES.md` explains both.

Both solutions reference only projects inside this repository:
no sibling checkouts, no private feeds, no folder-name requirements.
`dotnet build StockSharp.slnx -c Release` builds the wider set,
including the localization satellites,
and `dotnet build StockSharp_Samples.slnx -c Release` compiles the samples.

Restore offline from the mirror when a pinned package goes missing:

    ./tools/mirror-packages.sh
    dotnet restore StockSharp.slnx --source ./nuget-mirror

## Prose

Every prose surface that ships in the repository follows the house voice.
The rules live in `.claude/skills/`, and they are short:

    illi-voice              wording, for every surface that ships
    illi-voice-csharp       the same rules mapped onto C# doc comments
    illi-format-sweep       semantic line breaks, per sembr.org
    illi-format-sweep-cjk   the same sweep where CJK or raw HTML is involved

Two consequences show up in every diff.
Prose breaks at clause boundaries rather than at a column,
so a grep hit returns a whole thought and a one-word edit lands as a
one-line diff (decision record `0001`).
C# doc comments sit immediately above their member with no blank line
between, because analyzers reject the separation the general rule asks for.

Upstream-authored comments stay as they are.
Revise them only where they are factually wrong against the code.

## Commits and pull requests

Write a subject that completes "If applied, this commit will ___",
capitalized, near 50 characters, no terminal period,
naming the mechanism that changed.
Open the body on the forcing condition — what made the change necessary —
then what the change does and what a caller observes.
Wrap the body at 72 characters.
Point at a decision record rather than restating it.

Give the pull request description scope, verification and risk,
and let the commit bodies carry the reasoning.
State verification as commands a reviewer can paste,
and say what a passing run looks like.

Two proof conventions are specific to this fork.

A regression test earns its place by failing on the unfixed code.
Stash the fix, build, watch the test fail, pop the stash —
and say in the commit body that you did, and what the failure looked like.
A test that passes both ways proves nothing about the fix it accompanies.

A comment-only change carries a code-identity check:
show that the compiled output is unchanged,
so a reviewer reads the diff as prose rather than re-reviewing behaviour.

CI runs the gate across ubuntu, windows and macos on every pull request.
It retries a failed test exactly once, because the suite has carried a
tail of timing-sensitive tests; a test failing twice fails the job.
Treat a retry as a defect to investigate, not as a pass.

## Decisions

Anything consequential gets a numbered record under `docs/decisions/`
before the work lands.
Consequential means a choice a later reader would otherwise have to
reconstruct from the diff: a dependency strategy, a compatibility
boundary, a packaging identity.

A record carries a sequential number and names the decision in its title.
`Status` is its own field, so superseding one leaves the title and its
anchor intact.
`Context` states the forcing condition.
`Decision` is present tense, as "We will".
`Consequences` sets what the decision costs beside what it buys,
and gives each cost the signature by which it will show up.

Supersede a record with a new numbered record and leave the old standing.
`0001`–`0007` are the existing set, and `0006` is the one most changes
touch: the public `StockSharp.*` API holds through every 1.x release,
so a desired break becomes a record marked `for 2.0` rather than a diff.

## License

Apache License 2.0. `LICENSE` holds the full text, and `NOTICE` the
attribution this fork preserves.
