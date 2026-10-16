# Public source tree

Everything in this directory, including earlier committed versions, is intended for public distribution.

- Include only source, public documentation, build/CI configuration, redistributable resources and their licenses.
- Never commit credentials, personal information, machine-specific paths, proprietary game files, saves, dumps, research results or private workspace dependencies.
- Keep builds self-contained. Use relative project references and retain dependency licenses and provenance.
- Obtain installed game paths through user selection or environment variables. Optional test inputs must be explicitly configured and skipped when absent.
- Use conventional C# indentation. Put statements and control-flow bodies on separate lines. Do not wrap solely to meet a line length limit.
- Validate distribution changes with `pwsh ./Build.ps1 -Test`; the ZIP root must contain `Memoria.Launcher.exe` and `Data/`.
