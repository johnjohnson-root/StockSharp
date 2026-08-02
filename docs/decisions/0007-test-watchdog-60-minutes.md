# 0007. Set the post-cleanup test watchdog to 60 minutes

Status: accepted, 2026-08-02

## Context

`Tests/AsmInit.cs` arms a background watchdog after `[AssemblyCleanup]`:
a test host still alive past the threshold prints a diagnostic
naming the leak class and exits with code 97.
The initial 3-minute threshold was a packaging-time judgment call,
tight enough to preempt CI's 8-minute `--blame-hang` abort.

## Decision

We will run the watchdog at 60 minutes.

## Consequences

The watchdog stops racing legitimate slow teardown outright:
a false exit 97 on a loaded machine becomes practically impossible.
In CI, `--blame-hang` now fires first,
which shows up as hang attribution returning to the sequence file
rather than the watchdog's message;
the watchdog remains the backstop for runs without `--blame-hang`,
where a leaked foreground thread costs an hour instead of forever.
