# Repository Guide

## Scope

This repository contains the N.I.N.A. "Python Scripting" plugin and its NUnit test project.

## Editing Guidance

- Follow the existing code style in the touched file. Match naming, property patterns, command style, logging style, and local formatting before introducing new patterns.
- Keep line breaks consistent with the surrounding code. When editing wrapped expressions, object initializers, interpolated strings, or XAML attributes, preserve the local file's line-breaking style instead of reflowing unrelated code.
- When explanatory code comments are needed, prefer XML doc comments (`///`) on types and members over large blocks of regular inline comments. Use regular inline comments only when the explanation is truly local to a specific statement or block.
- When fixing a regression from an older working version, treat the old working code as the behavioral oracle. First identify the exact old-code invariant that must be preserved, then make the new code reproduce that observable behavior before considering cleanup or a more general abstraction.
- Tests for regressions must encode the old observable behavior, not just the new helper's internal math. If the old implementation and the new abstraction disagree, prefer a test that catches the user-visible regression.
- Treat all calculation and coordinate-transform changes as accuracy-sensitive. Do not make "close enough" math changes in polar-alignment, refraction, projection, or hardware-movement code.
- For formulas or math-sensitive transformations, verify the formula from a reliable source before changing behavior. Prefer primary or authoritative references when research is needed.
- If you are unsure about a formula, unit conversion, sign convention, coordinate frame, or hemisphere-specific behavior, ask instead of assuming.
- Treat all user-facing text as accuracy-sensitive too. Tooltips, guides, changelog entries, and inline help must only say things you can support from the current code or a verified source.
- Do not add speculative recommendations, inferred behavior, or hardware advice to user-facing text unless it has been explicitly verified. If the implementation meaning is unclear, inspect the code first or ask.
- Preserve message topic strings and serialized payload field names unless the change is explicitly a contract update.
