# 0003. Use DocFX and Doxygen for documentation

- **Status:** Accepted
- **Date:** 2026-10-18

## Context

We need user documentation for the package and API references for both the C# and C++ code,
generated from source comments so they stay in step with the code.

## Decision

- **DocFX** builds the site: the manual from the package's `Documentation~/`, the C# API from
  XML doc comments, and the architecture, development and ADR pages from `Docs/`.
- **Doxygen** generates the native API reference, which DocFX includes under `native/`.
- DocFX is pinned as a local .NET tool in `.config/dotnet-tools.json`.

## Consequences

- One site and one build script (`Docs/build-docs.ps1`) for all documentation.
- The manual follows Unity's package convention (`Documentation~/`), so it ships with the package.
- Contributors need the .NET SDK and Doxygen to build the docs.
- The native reference keeps Doxygen's own look rather than matching the DocFX theme.

## Alternatives considered

- **Doxygen for everything.** Handles C#, but has weaker Markdown/manual support.
- **Sphinx with Breathe.** Strong for C++, but no first-class C# support.
