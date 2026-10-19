# Public source tree

Everything in this directory, including earlier committed versions, is intended for public distribution.

- Include only source, public documentation, build/CI configuration, redistributable resources and their licenses.
- Never commit credentials, personal information, machine-specific paths, proprietary game files, saves, dumps, research results or private workspace dependencies.
- Keep builds self-contained. Use relative project references and retain dependency licenses and provenance.
- Obtain installed game paths through user selection or environment variables. Optional test inputs must be explicitly configured and skipped when absent.
- Use conventional C# indentation. Put statements and control-flow bodies on separate lines. Do not wrap solely to meet a line length limit.
- Validate distribution changes with `pwsh ./Build.ps1`; the ZIP root must contain `Memoria.Launcher.exe` and `Data/`. Run focused tests separately as described in [Build and test](docs/developers/building-and-testing.md). Documentation-only changes need document/link/diff checks rather than a package build.
- Run battle-effect tests only for implementation changes affecting those effects and only for the affected effects, unless the user explicitly requests those tests. The complete battle-effect suite requires changes affecting all effects or an explicit user request for the complete suite. Aggregate validation, shared infrastructure, checkpoints, rebases and integration do not override this restriction. Filter or exclude unrelated effect tests. `Build.ps1 -Test` runs the entire Runtime.Tests project, including battle-effect tests; use it only when its complete test selection is authorized.
