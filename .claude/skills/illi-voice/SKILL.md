---
name: illi-voice
description: Write documentation, code comments, reference entries, agent files, CLI text, commit messages, changelogs, and decision records in the house voice. Use when drafting or revising any prose that ships in a repository, when the user asks how something should be worded, when a README, docstring, SKILL.md, help output, commit message, changelog entry, or decision record needs writing or review, or when the user invokes /illi-voice by name.
---

# illi-voice

These rules govern every prose surface that ships in a repository.
`~/.claude/.voice/baseline.md` holds the reasoning
and one approved example per category.

## Always

**Write affirmatively.** State what to do.

**Break at clause boundaries.** Follow Semantic Line Breaks (sembr.org):
the line ends where the thought ends,
so a one-word edit lands as a one-line diff.
Commit bodies are exempt and wrap at 72 characters.

**Calibrate with two affirmative classifications and one affirmative rule.**
A contrast leaves the wrong example in the reader's context
guarded by a single negating word,
and the conversion is lossless.

**Let structure speak.** Headings carry the shape of a document,
and a worked example is recognized on sight.

## READMEs and prose documentation

Write third person, present tense, active.
Open with a definition: what it is, what it reads, what it writes.
Order sections by the artifact's lifecycle:
definition, install, use, reference, contribute, license.

Build the onboarding ramp out of ordinary sections.
Installation followed immediately by a working invocation
carries a newcomer to competence on its own,
and every section after it assumes competence.

Use sentence case headings.
Set flag and option blocks as indented code with aligned columns,
lowercase descriptions, and no terminal periods.
Ship the file as prose and code.
The text is the entire presentation.

Prefer the shorter section, and spend an extra clause on a real destination.
`MIT. LICENSE holds the full text.` names a file the reader can open.
`MIT` names the license alone.

## Code comments

Give each function one header comment above the definition,
separated from it by a blank line.
Open with a synopsis in the imperative:
what the function does, in the order it does it.
Follow with the consequence a reader cannot get from the signature.
Keep it to two lines.

Verify each clause against what the code actually does.
A synopsis narrates a sequence,
which makes it the one comment form that can be confidently wrong.

Let the body carry the mechanism.
Statements speak for themselves.
Mark deferred work as `TODO: <tracker link> - <explanation>`.

## API reference entries

Give public items the executable contract:
a one-line summary,
the behavior a caller acts on that the signature omits,
a `Raises:` section a consumer can jump to,
and a runnable example wherever the types leave the call shape open.
Let the example run in CI.

Give internal items one line stating what the signature omits.

Spend lines on what the signature omits.
`raises: FilterSyntaxError if the expression does not parse`
states behavior the signature omits.
`source: the filter expression` repeats the parameter name.

Mood, section syntax, the example mechanism, and the test for what counts
as public follow the language and its linter.

## Agent-facing files

Match register to file type.
Hooks carry facts, as the spec requires of `additionalContext`.
Context files such as `CLAUDE.md` and `AGENTS.md` carry directives,
with emphasis markers reserved for rules
where a reasonable inference lands wrong.
Skill bodies describing a workflow carry numbered procedure,
closing on a verification step.
Skill bodies carrying knowledge take directives.

Write a description in two parts:
one sentence stating what the skill does,
then one sentence beginning `Use when` carrying the triggers —
the phrasings, synonyms, situations, and the slash-command name.
Spend the 1024-character budget on triggers.
A description that misses its trigger fails silently and completely.

State every instruction directly.
`Check field names` carries force.
`You should check field names` reads as optional.
Give each directive the observation that earns it.

Prefix project skills `illi-`,
and match `name` to the parent directory.

Let the description route the reader.
The body loads after relevance is settled.

## CLI surface text

Send data to stdout.
Send narration, warnings, and errors to stderr.
Exit 0 on success and non-zero on failure.
Offer `--json` wherever the output carries structure.
Gate color on a TTY, `NO_COLOR`, `TERM=dumb`, and `--no-color`.

Write flag descriptions lowercase, unpunctuated, aligned in a column.

Open an error with `program: summary`, lowercase, no terminal period.
Echo the user's input on its own line and mark the offending token with a caret.
State the broken rule beside the caret.
Carry the suggestion on its own `help:` line, unprefixed.
Derive the suggestion wherever the repair is determinate.
Guess it wherever the candidate set is closed.
Diagnose alone wherever the set is open.

On success, print the result and stop.

## Commit messages and pull request descriptions

Write a subject that completes `If applied, this commit will ___`.
`Short-circuit comparisons with a null operand` completes it.
`Fixed the null comparison bug` leaves it ungrammatical.

Cap the subject near 50 characters, capitalize it, and close it without a period.
Name the mechanism that changed.

Open the body on the forcing condition: what made this change necessary.
Follow with what the change does and what a caller observes.
Point at the decision record rather than restating it.
Wrap the body at 72 characters.

Give the pull request description scope, verification, and risk,
and point it at the commit body for the reasoning.
Give verification as commands a reviewer can paste,
and state what a passing run looks like.

## Changelogs

Name what a reader observes when they run the software.
`--json emits matching records as JSON, one object per line`
names what they get.
`Added the --json flag` names the flag.

Carry the kind of each change:
Added, Changed, Deprecated, Removed, Fixed, Security.
Inline bold labels carry it at small volume,
and headings earn their place once a release runs long enough to scan.

Date each release in ISO form, newest first.
Define each bracketed version as a link to its compare URL.
State the remedy wherever a change reaches a caller.

Write entries by hand at release, or author them per change.

## Decision records

Number each record sequentially and name the decision in the title.
Carry Status as its own field,
so supersession leaves the title and its anchor intact.
Write Context as the forcing condition.
Write Decision in the present tense, as `We will`.
Write Consequences with what the decision costs beside what it buys,
and give each cost the signature by which it shows up.
Supersede a decision with a new numbered record
and leave the old one standing.
