---
name: illi-format-sweep
description: Sweep a repository and place a line break after every sentence and every independent clause in its documentation, following the Semantic Line Breaks specification (sembr.org), so a grep hit returns a whole thought and a one-word edit lands as a one-line diff. Use when the user asks to format, rewrap, reflow, ventilate, or tidy documentation, mentions line breaks, SemBr, ventilated prose, one sentence per line, clause breaks, line length, or wrap width, or invokes /illi-format-sweep by name.
---

# illi-format-sweep

`/illi-format-sweep` places a line break after every sentence
and every independent clause
in every document a repository tracks.
It sweeps these files: `README.md`, `CONTRIBUTING.md`,
every numbered decision record under `docs/decisions/`,
every Markdown file under `docs/`,
every `SKILL.md`, every `CLAUDE.md`, every `AGENTS.md`,
and every other Markdown or plain-text document under version control.

Markdown, reStructuredText, and AsciiDoc join consecutive lines with a space,
so a sweep revises source alone
and leaves rendered output identical.
Each line runs as long as the clause that fills it,
and the reader's tool sets the width of the view.

## Break rules

These rules follow the Semantic Line Breaks specification at <https://sembr.org/>.
Cite that spec in the numbered decision record that adopts this skill.

**Sentences.** Place a break after every sentence,
as punctuated by a period, an exclamation mark, or a question mark.

**Independent clauses.** Place a break after every independent clause,
as punctuated by a comma, a semicolon, a colon, or an em dash.

**Dependent clauses.** Place a break after a dependent clause
when the break clarifies grammatical structure,
or when the clause runs long enough that a reader gains from the pause.

**Lists.** Place a break before every enumerated or itemized list.

**Rendered output.** Preserve the rendered document exactly.
Every break lands where the markup joins lines with a space,
so readers see the text they saw before the sweep.

**Whole lines.** Preserve these on the lines they already occupy:
URLs, code spans, fenced code blocks, table rows,
YAML front matter, headings, and link definitions.
A sweep ventilates prose and leaves structure standing.

**Commit messages.** Leave commit bodies unventilated,
including templates such as `.gitmessage`.
`git log` indents a body four spaces into an 80-column terminal
and reflows nothing,
so a ragged clause-line spends width that is already scarce.

## Running a sweep

    /illi-format-sweep                  sweep with the current Opus
    /illi-format-sweep --model sonnet   sweep with the current Sonnet
    /illi-format-sweep --model custom   read provider and model from .illi/format-sweep.toml

Clause judgment carries the whole result,
and Opus holds it steady across long documents,
so Opus stays the default.
Sonnet keeps token spend low across large trees.

Every run prints the path of each ventilated file to stdout, one path per line,
and sends counts and warnings to stderr.
Exit codes hold across every run: 0 success, 1 error, 2 refusal.

An error names the file, the position, and the repair:

    illi-format-sweep: docs/api.md:42: unterminated fenced code block

    help: close the fence or drop the file from the sweep

## Reading the source

Each line runs to the length of its clause,
so every reading tool sets its own width.
GitHub's blob view carries a soft-wrap toggle,
`bat --wrap=auto` follows the terminal,
and vim wraps at punctuation with `set wrap linebreak breakat=,;:`.
Rendered views reflow on their own,
and a docs site tunes measure with `max-width` in `ch` units.
