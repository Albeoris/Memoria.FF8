# Vendored Albeoris.Frameworks dependency

Upstream: https://github.com/Albeoris/Frameworks

Pinned revision: `2474bed3e52f38c19920408cb6a4908afe170580`

License: [MIT, copyright 2025 Albeoris](LICENSE).

This directory contains only the source and embedded resources needed by Memoria's project-reference closure: core utilities; FF8 archive, imaging, kernel, text, message, script and menu readers; and Steam library discovery. Tests, tools, build outputs, game data and unrelated framework projects are excluded. The upstream revision is publicly available over HTTPS. No external checkout is needed when building Memoria.

The imported source preserves these changes relative to that revision:

- `Albeoris.Games.FF8.TextEncoding/Resources/japanese.field-characters.json`: retains the declared blank third glyph in `tgview1` as an explicit space, rather than dropping the trailing blank entry.
- The same resource supplies the field-specific character tables for `bg2f_1a`, `bgmast_6`, `bgroom_2`, `ewbrdg2` and `glstage2`, and corrects character 23 in `testno` to 慣. `FieldCharacterSet.Id` and `DefaultJapaneseFieldCharacters` support name-only tables without assigning them a numeric map identity; all existing numeric table identities are retained.

- `Albeoris.Games.FF8.Imaging/PixelImage.cs`: accepts image dimensions up to 16384 for wide native texture sheets while retaining the previous maximum total allocation of 64 million pixels.

- `Albeoris.Games.FF8.TextEncoding.Abstractions/Tags/TextTagKey.cs`: imports the semantic junction, ability and element icon keys from upstream commit `39f302eaea422236342d00c748af45ed1f85f4cc`.
- The same enum imports the thirteen semantic status icon keys from upstream commit `0e3fc21c28c0ad45aa37e5f054a757a2c6abf2d3`.
- `Albeoris.Games.FF8.TextEncoding.Abstractions/Tags/TextTagCode.cs` and `Albeoris.Games.FF8.TextEncoding/Internal/TextTag.cs`: preserve native item-name control `0x0D` and its single unsigned-byte operand through text decoding and encoding.
- The same tag files and `Internal/{Codepage,TextDecoder}.cs` retain European font wordmark tiles `0xCC..0xE2` as lossless `{Glyph N}` references. These textual references encode to a single original glyph byte, rather than introducing a native opcode or substituting Unicode for artwork. Japanese and Russian decoding retain their existing character tables.
- Native text encoding requires an explicit dictionary language independently of the glyph codepage. Term operands use separate EN/FR/DE/ES/IT/JP enums and lossless language-qualified tags; other parameter families retain their shared selectors. Tags support up to64characters for complete dictionary parameter names.
- `Albeoris.Games.FF8.NamedicBin/NamedicBin.cs`: accepts at most three zero padding bytes ending at a DWORD boundary after the final terminated string; pointer and termination validation remains strict.

- `Albeoris.Games.FF8.FlArchives/FlArchive.cs`: disposal releases owned resources even when flushing fails, for both synchronous and asynchronous disposal.
- `Albeoris.Games.FF8.Jsm/Sources/Instructions/{AASK,AMES,AMESW,ASK,MES,RAMESW,WINSIZE}.cs`: expose message channel, position, choice and window-size expressions through the added instruction interfaces.
- `Albeoris.Games.FF8.Jsm/Sources/Instructions/I{ChannelMessage,ChoiceMessage,PositionedMessage,WindowSize}Instruction.cs` and `Sources/Messages/`: retain the corresponding typed field-message context API and resolver.
- `Albeoris.Games.FF8.Jsm/Albeoris.Games.FF8.Jsm.csproj`: targets .NET 10 and exposes internals to its upstream test assembly.
- `Albeoris.Games.FF8.Jsm/Sources/`: annotate optional control branches, execution state, factory results and formatter inputs for nullable reference checking; require an active execution context when accessing the current field object.

`Directory.Build.props` and `Directory.Build.targets` are Memoria integration files: they use .NET 10 only, retain upstream warning policy and disable package generation. All package versions come from the product's `Directory.Packages.props`.

To update, choose a reviewed upstream commit, replace this same project closure, reapply or drop the listed source changes, update this revision and notices, then run `Build.ps1 -Test` from the repository root. Do not copy unrelated projects or generated `bin`/`obj` directories.

Vendored files also receive whitespace-only cleanup of trailing spaces and redundant final blank lines.
