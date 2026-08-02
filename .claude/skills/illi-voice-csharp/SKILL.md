---
name: illi-voice-csharp
description: Write C# code comments and XML documentation in the house voice, mapping illi-voice's comment and reference rules onto the conventions the C# compiler and .NET tooling enforce. Use when drafting or revising comments or XML documentation in .cs files, when illi-voice's blank-line, two-line, or imperative-mood comment rules meet C# doc-comment conventions, or when the user invokes /illi-voice-csharp by name.
---

# illi-voice-csharp

illi-voice governs every prose surface,
and C# routes two of its surfaces through one mechanism:
the XML documentation comment is both the header comment
and the API reference entry.
This variant maps the parent rules onto that mechanism
and changes nothing else.
Where the two skills speak to the same line,
this one wins;
everything it leaves unstated follows illi-voice.

## XML documentation comments

Place the `///` block immediately above its member.
The compiler associates a documentation comment
with the declaration that follows it,
and analyzers reject an intervening blank line,
so the parent rule's separating blank line does not apply.

Write `<summary>` in the third person indicative,
as .NET convention sets the mood:
`Releases the reserved slot`, `Gets the batch size`,
`Initializes a new instance`.
The parent's two-line cap governs the summary text,
and the tags stand outside the count.

Open with what the member does, in the order it does it.
Follow with the consequence a caller cannot get from the signature,
in the summary or in `<remarks>`.
Verify each clause against what the code actually does.

Carry the executable contract in the tags the tooling reads:
`<param>` and `<returns>` for behavior the signature omits,
`<exception cref="...">` as the Raises: section,
`<example>` with `<code>` wherever the types leave the call shape open.
Skip a tag that would repeat the parameter name.

Write `<inheritdoc/>` where the base member's contract holds,
and write the difference where it does not.

Break XML text at clause boundaries.
IntelliSense and the documentation generators join lines with a space,
so ventilated source renders identically.

Public means public and protected:
the accessibility keyword is the language's test,
and CS1591 enforces it wherever documentation files generate.
Give internal members one line stating what the signature omits,
as `//` above the member or a single-line `<summary>`.

## Implementation comments

`//` comments inside bodies follow illi-voice unchanged:
state the constraint the code cannot show,
verify it against the code,
and mark deferred work as `TODO: <tracker link> - <explanation>`.
