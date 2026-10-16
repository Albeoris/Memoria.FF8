# Vendored Albeoris.Frameworks dependency

Upstream: https://github.com/Albeoris/Frameworks

Pinned revision: `2474bed3e52f38c19920408cb6a4908afe170580`

License: [MIT, copyright 2025 Albeoris](LICENSE).

This directory contains only the source and embedded resources needed by Memoria's project-reference closure: core utilities; FF8 archive, imaging, kernel, text, message, script and menu readers; and Steam library discovery. Tests, tools, build outputs, game data and unrelated framework projects are excluded. The upstream revision is publicly available over HTTPS. No external checkout is needed when building Memoria.

The imported source preserves these changes relative to that revision:

- `Albeoris.Games.FF8.FlArchives/FlArchive.cs`: disposal releases owned resources even when flushing fails, for both synchronous and asynchronous disposal.
- `Albeoris.Games.FF8.Jsm/Sources/Instructions/{AASK,AMES,AMESW,ASK,MES,RAMESW,WINSIZE}.cs`: expose message channel, position, choice and window-size expressions through the added instruction interfaces.
- `Albeoris.Games.FF8.Jsm/Sources/Instructions/I{ChannelMessage,ChoiceMessage,PositionedMessage,WindowSize}Instruction.cs` and `Sources/Messages/`: retain the corresponding typed field-message context API and resolver.
- `Albeoris.Games.FF8.Jsm/Albeoris.Games.FF8.Jsm.csproj`: targets .NET 10 and exposes internals to its upstream test assembly.

`Directory.Build.props` and `Directory.Build.targets` are Memoria integration files: they use .NET 10 only, retain upstream warning policy and disable package generation. All package versions come from the product's `Directory.Packages.props`.

To update, choose a reviewed upstream commit, replace this same project closure, reapply or drop the listed source changes, update this revision and notices, then run `Build.ps1 -Test` from the repository root. Do not copy unrelated projects or generated `bin`/`obj` directories.

Vendored files also receive whitespace-only cleanup of trailing spaces and redundant final blank lines.
