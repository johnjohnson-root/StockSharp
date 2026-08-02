---
name: illi-format-sweep-cjk
description: Sweep repositories whose documents mix CJK prose, raw HTML, and machine-read or data files, placing semantic line breaks only where the rendered output stays identical. Use when ventilating documentation that includes Chinese, Japanese, or Korean text, HTML-heavy Markdown, generated or machine-read Markdown, or data .txt trees, when /illi-format-sweep meets such files, or when the user invokes /illi-format-sweep-cjk by name.
---

# illi-format-sweep-cjk

illi-format-sweep promises one thing above all:
readers see the text they saw before the sweep.
Three file populations break that promise
under the parent's break rules,
and this variant carries the amendments.
Where the two skills speak to the same line,
this one wins;
everything it leaves unstated follows illi-format-sweep.

## CJK prose

Renderers join consecutive source lines with a space,
and between two CJK characters that space is visible:
the sweep would change the rendered document.
Break CJK prose only where the rendering provably survives —
after a character that already borders a space,
at a structural boundary the markup owns,
or where the target renderer is verified
to collapse breaks between CJK characters.
Leave the line standing when in doubt.
A file the sweep leaves untouched keeps the promise;
a file it reflows visibly breaks it.

Latin prose embedded in a CJK document ventilates
under the parent rules.

## Raw HTML

Preserve raw HTML on the lines it already occupies,
alongside the parent's whole-line list:
URLs, code spans, fences, table rows,
front matter, headings, and link definitions.
Markup inside an HTML block belongs to the HTML parser,
and a break there gambles the rendering.

## Documents against data

Sweep documents alone.
A `.txt` file under a test-fixture or resource tree carries data,
a generated file carries its generator's format,
and a machine-read file — `AnalyzerReleases.Shipped.md`
and its siblings — carries a schema its consumer parses.
Ventilating any of them changes what a machine reads,
and no reader gains.
When a tree mixes documents and data,
name the swept files explicitly
and let everything else stand.
