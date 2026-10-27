# Build and test

[Documentation](../README.md) / Developers

## A clean checkout

Use Windows x64, PowerShell 7 and the .NET 10 SDK for the complete current game distribution. Portable core and launcher builds also target Linux and Intel macOS; see [Cross-platform desktop migration](cross-platform.md) and [Linux testing from Windows](linux-testing.md). Run commands from the directory containing `Build.ps1` and `Memoria.FF8.slnx`. Package restore needs access to the configured public NuGet feeds.

```powershell
./Build.ps1
```

The script publishes a self-contained x64 launcher, dispatcher and isolated DX11/Vulkan engines built from the same source, plus the common x64 music worker. It checks the actual published dependency assemblies, native ABI and backend-specific embedded shaders before packaging, including embedded codepage and menu resources. Original game data, media tool binaries, saves and fixture inputs are not bundled. See [graphics host selection](graphics-backends.md) and [shader generation](shaders.md).

| Output | Purpose |
| --- | --- |
| `artifacts/package/Memoria.Launcher.exe` | Start the local packaged application. |
| `artifacts/package/Data/` | Engine, runtimes, audio host and distribution notices. |
| `artifacts/package/Data/Memoria.FF8.exe` | Dispatcher; selects a backend and waits for the actual game. |
| `artifacts/package/Data/Game/dx11/`, `Data/Game/vulkan/` | Isolated graphics hosts sharing gameplay and rendering source. |
| `artifacts/package/Data/BundledMods/` | Bundled native `Memoria/Depixelation` defaults, discovered after user packages. |
| `artifacts/package/Mods/` | Empty `Tonberry/`, `Demaster/` and `Junction/` directories for user packages. |
| `artifacts/Memoria-FF8-win-x64.zip` | The distributable archive; its root contains the launcher, `Data/` and `Mods/`. |

`Build.ps1` replaces its generated package directory on a successful build. Keep personal saves, mod packages and independently installed tools outside disposable build outputs, or preserve them before rebuilding.

Use `./Build.ps1 -Configuration Debug` for a Debug package. `Release` is the default.

## Dependencies and project builds

Open [Memoria.FF8.slnx](../../Memoria.FF8.slnx) in your IDE, or build it directly:

```powershell
dotnet build Memoria.FF8.slnx -c Release
```

The required Albeoris.Frameworks source closure is included in [vendor/Albeoris.Frameworks](../../vendor/Albeoris.Frameworks/README.md), with its license, upstream revision and retained changes. No sibling checkout or submodule is needed. NuGet versions are centralized in [Directory.Packages.props](../../Directory.Packages.props).

Ordinary builds copy missing bundled native mod files beside the app and launcher without overwriting existing files. Edit source packages under `Mods/Memoria` to change future distributions; preserve or remove an output copy explicitly when testing changed defaults.

Ordinary builds use the checked-in compiled title shader. Shader regeneration and artwork preparation are component authoring tasks; see [Rendering](rendering.md) and the linked component READMEs.

## Distribution checks

```powershell
./Build.ps1
```

This validates published resources and the completed distribution before creating the ZIP, including compatibility with the frozen first automatic updater.

The optional `-Test` switch adds Remastered, launcher, selected App, UI, field pose/render interpolation and the entire Runtime.Tests project. The App selection includes portable actor sampling regressions and the optional installed-resource opening presentation check at 30 and 60 FPS. Runtime.Tests includes battle-effect tests, so this switch is a broad test run rather than the default check for a focused change. Run focused tests separately when only part of that selection is relevant or authorized. It does not run every optional integration or gameplay acceptance check.

GitHub Actions builds the Windows package on pushes to `main` and on `v*` tags. A successful `main` build updates the `nightly` prerelease; a version tag publishes its corresponding release. Portable PR/main jobs install the configured SDK on all three desktop hosts and run the explicit portable test selection. They do not publish complete Unix game distributions or execute unrelated battle-effect tests.

## Focused tests

Most test projects use xUnit with Microsoft Testing Platform, selected in `global.json`. Run them through `dotnet test`; their executable entry points also remain available. `Memoria.FF8.Events.Tests` has its own console scenario runner. Select the project closest to the changed contract:

```powershell
dotnet test --project tests/Memoria.FF8.Ui.Tests -c Release --no-progress
dotnet test --project tests/Memoria.FF8.Runtime.Tests -c Release --no-progress --filter-class '*NativePcSaveCompressionTests'
dotnet test --project tests/Memoria.FF8.App.Tests -c Release --no-progress --filter-class '*MediaToolsInstallationTests'
```

| Project | Main responsibility |
| --- | --- |
| `Memoria.Launcher.Tests` | Installation selection, settings and launch setup. |
| `Memoria.FF8.Ui.Tests` | Renderer-independent layout, focus and controls. |
| `Memoria.FF8.Runtime.Tests` | Simulation and persisted state contracts. |
| `Memoria.FF8.Events.Tests` | Event execution contracts. |
| `Memoria.FF8.Battle.Tests` | Battle state and execution. |
| `Memoria.FF8.Assets.Tests` | Asset readers and catalog construction. |
| `Memoria.FF8.Mods.Tests` | Mod parsing, routing and replacement behavior. |
| `Memoria.FF8.App.Tests` | Host integration, game UI, media and automated exploration. |
| `Memoria.FF8.Remastered.Tests` | Optional synthetic and installed Remastered resource checks. |

`BattleResultsRenderTests` renders the result pages at 640×448 and 1280×896 using installed text and artwork. Set `MEMORIA_FF8_RESULT_UI_OUTPUT` to a local directory to retain the page PNGs for inspection. These component previews do not modify a save.

`MenuTextScaleAuditTests` checks every registered menu at the supported font and text-scale extremes. With `MEMORIA_FF8_GAME_DIR` configured, it also renders the same screens with installed artwork at default and enlarged sizes, at the beginning and during text overflow. Set `MEMORIA_FF8_MENU_UI_OUTPUT` to retain the PNGs for inspection. These synthetic menu previews do not advance gameplay or modify a save.

Some tests require a locally supplied game or a specific fixture. A skipped optional test does not establish support for its scenario. The separate [acceptance tool](../../tests/Memoria.FF8.Acceptance/Memoria.FF8.Acceptance.csproj) also requires external tooling and inputs; it is not a portable game test included in `Build.ps1 -Test`.

For field animation changes, run the portable `FieldActorCaptureTests` in App.Tests and `FieldPoseInterpolationTests` / `FieldRenderInterpolationTests` in Assets.Tests. With `MEMORIA_FF8_GAME_DIR` configured, also run App.Tests with `--filter-class '*OpeningPresentationTests'`. Both opening cases must pass without skips: they start from a cold process and render through the playable corridor, including the beginning, midpoint and end of each presentation interval at 60 FPS. Hosted CI has no original game data, so its portable regression gate cannot replace this installed-resource check.

Choose filters for the contract being changed; the examples are not a mandatory checklist. Battle-effect tests must be limited to affected effects unless the user explicitly requests other tests. A complete effect-suite run requires changes affecting all effects or an explicit request for that complete suite. Aggregate commands and shared infrastructure changes do not override that scope. Documentation-only changes need document, link and diff checks rather than engine builds or gameplay runs.

The App `WindowModeControllerTests` are visible desktop checks, enabled with `MEMORIA_FF8_WINDOW_MODE_TEST=1`. They create their own initialized graphics host and check monitor coverage, backbuffer dimensions and restoration of normal and maximized windows. They require no original game resources. `WindowModeHotkeyTests` and `BorderlessWindowSettingsTests` cover the shortcut and persisted configuration without showing a window.

## Optional local inputs

An explicitly selected native execution-stack profile can be stored in `Memoria.FF8/native-execution-stack.json` under the user's local application-data directory, or selected with `MEMORIA_FF8_EXECUTION_STACK_PROFILE`. Its JSON follows [NativeBattleExecutionStack](../../src/Memoria.FF8.Runtime/NativeBattleExecutionStack.cs), including the observed common-frame ESP, return site, executable/capture identities and process/thread lineage. Select these values from an authenticated observation; the executable image does not supply a process stack address. Fresh processes apply the selected profile before New Game and retain it in subsequent saves. An absent selection remains unknown; an invalid or explicitly missing selection fails. Loading an existing save preserves that save's owners and does not implicitly migrate it. An external profile may additionally contain an explicitly verified HeroCrtStack contract for its recognized CRT environment, with the same capture owner and thread. The selection validates and transfers that contract independently; an ordinary execution-stack profile does not infer CRT behavior.

| Variable | Purpose |
| --- | --- |
| `MEMORIA_FF8_GAME_DIR` | Supported original game installation for resource-dependent tests. |
| `MEMORIA_FF8_OPENING_OUTPUT` | Optional directory for standalone 30/60 FPS opening test snapshots at the playable corridor; otherwise they are temporary test artifacts. |
| `MEMORIA_FF8_TEST_FIXTURE_CATALOG` | External JSON object mapping test resource identifiers to local fixture paths. |
| `MEMORIA_FF8_TEST_FIXTURES` | Base directory for relative catalog paths; otherwise they resolve against the catalog directory. |
| `MEMORIA_FF8_AUTOPLAY_ACCEPTANCE=1` | Opt into the full automated New Game-to-ending acceptance attempt in the App tests. It stops at unsupported contracts or traversal failures. |
| `MEMORIA_FF8_REMASTERED_DIR` | Optional Remastered installation for its separate read-only resource checks. |
| `MEMORIA_FF8_FIELD_TEXTURE_PACKAGES` | Path to an external JSON array of unpacked field texture package directories, ordered base then addon. Relative paths resolve against the catalog directory. Enables installed field-page coverage and CPU/GPU acceptance tests; packages must share a `Mods/Demaster` parent for App.Tests. |
| `MEMORIA_FF8_FIELD_TEXTURE_OPTIONAL_IMAGES` | Path to an external JSON array of directories containing optional extracted PNGs for independent field-image decode checks. Relative paths resolve against the catalog directory. |
| `MEMORIA_FF8_FIELD_STATE` | Concrete standalone field save for read-only installed CPU/GPU presentation acceptance. The test renders without advancing gameplay and checks state and source-save hashes. |
| `MEMORIA_FF8_FIELD_PATCH_FIELD` | Optional original field ID restricting the addon comparison to that field. Otherwise the test searches native fields addressed by changed addon files for a visible CPU/GPU difference. |
| `MEMORIA_FF8_BATTLE_MODEL_TEXTURE_PACKAGES` | Path to an external JSON array of unpacked battle model texture package directories. Relative paths resolve against the catalog directory. Enables installed coverage of every PNG/DDS against actual original character, weapon or monster texture contexts; App acceptance requires a shared `Mods/Demaster` parent. |
| `MEMORIA_FF8_BATTLE_MODEL_STATE` | Concrete standalone battle save for installed model texture acceptance. The test compares enabled and disabled CPU/GPU renders, checks resident material bindings, restores a snapshot and compares a bounded continuation. The supplied save remains unchanged; a separately named validated save is written to the evidence directory. |
| `MEMORIA_FF8_BATTLE_EYE_MODEL_CATALOG` | Path to an external JSON array of canonical original body DAT filenames, such as `d0c000.dat`. With the original game directory, enables source-backed checks of open/closed texture-copy plans and closed-plane replacement guards for every selected model. |
| `MEMORIA_FF8_FIELD_MODEL_TEXTURE_PACKAGES` | Path to an external JSON array of unpacked model texture package directories, in ascending priority. Enables source-backed field and battle material coverage; App acceptance requires a shared `Mods/Demaster` parent. |
| `MEMORIA_FF8_FIELD_MODEL_STATE` | Concrete standalone field save for installed model CPU/GPU, package toggle and restore checks with the corresponding resource edition. The original is preserved; bounded continuation writes a separate validated save to the evidence directory. |
| `MEMORIA_FF8_MOD_EVIDENCE_DIRECTORY` | Optional directory retaining installed mod coverage reports, render hashes and CPU/GPU field or battle PNGs. |

Installed eye animation checks use the original body resources and native open/closed copy plans selected by `MEMORIA_FF8_BATTLE_EYE_MODEL_CATALOG`. Run the focused `NativeBattleEyePlaneReplacementTests` in Assets.Tests for closed-plane pixel selection and raw-word/CLUT guards. With the model package catalog and standalone battle state configured, the App model acceptance cases additionally check material bindings, CPU/GPU changes, snapshot restore and bounded continuation. These checks preserve the native eye-status producer and do not establish a replacement blink timer.

Fixture catalogs and original game data stay local. Use neutral fixture identifiers in shared tests, and do not check in proprietary assets or personal saved games. See [Environment variables](../reference/environment-variables.md) for runtime and specialized integration configuration, and [Remastered resources](remastered-resources.md) for that optional test project.

### Snapshot schema migrations

`GameState.SnapshotSchemaVersion` versions the saved payload independently of the compact container and VM versions. Unversioned payloads are schema 0. `SnapshotMigrations` applies sequential forward JSON transformations before deserializing the single current object model, for both plain and compressed saves. New incompatible payload changes require a new schema version and a migration from the preceding version; do not add historical state classes or reverse migrations. Preserve supplied values and explicit nulls; missing initialization data may use established defaults, while missing executed state needs a proved conversion or an explicit migration error.

Schema 0 → 1 converts copied model-part rows into sparse logical translations in both current and departing field checkpoints. Inactive rows need no translations; active and completed rows retain their saved counters, vectors, duration and mode. Empty retained draw samples gain an empty logical sample. Nonempty legacy draw samples lack the pre-callback logical state required by the current renderer and need a separately established conversion; they are refused rather than reset.

Schema 1 → 2 initializes absent process components through `GameSession.CreateColdProcess` and the existing `FromColdProcess` factories, including saves made during a battle. It completes root memory, battle-entry memory, current battle mirrors and the current/departing field eye-process memory independently. Existing values, explicit nulls and executing process counters are preserved. Embedded field checkpoints receive the array encoding their serializer expects. Retained model rows receive only their newly modeled flags/bounds, and an independent Gilgamesh actor owner is initialized without resetting its active entry. Matching renderer profiles receive their startup control. Missing world map modes derive from the saved allocation.

Historical staging offsets keep their absolute byte addresses when rebased to the expanded initial memory image. Newly missing loader-zero storage and world-process globals are deliberately initialized as a new process; recorded nonzero bytes continue to override those zeros. An existing loader-zero map, including an empty one, is preserved. This initializes previously unmodeled storage and does not reconstruct unobserved historical writes. Missing counters inside an already executing effect, explicitly unknown world samples and missing resource execution evidence still require a proved conversion.

Run `SnapshotMigrationTests`, `SnapshotInitialStateMigrationTests`, `SnapshotFileStagingMigrationTests`, `SnapshotBattleMetadataMigrationTests`, `SnapshotWorldMigrationTests`, `SnapshotDefaultsTests` and `SnapshotStorageTests` in Runtime.Tests for this boundary. These are state migration checks and do not execute battle effects. The optional App `SnapshotMigrationLoadTests` additionally loads an installed-resource field snapshot, writes the current schema, restores it in a fresh world, compares rendering and advances one ordinary callback. Configure `MEMORIA_FF8_SAVE_MIGRATION_SOURCE` and `MEMORIA_FF8_GAME_DIR`; `MEMORIA_FF8_SAVE_MIGRATION_OUTPUT` optionally retains a separately named migrated standalone and must not already exist. The source remains unchanged.

## Report a reproducible failure

Keep the command, error text, affected mode and a suitable standalone snapshot when available. Automated exploration writes a reproduction manifest; see [Automatic exploration](../players/automatic-exploration.md). The [control API](../reference/control-api.md) can inspect a running session and advance it in bounded steps. Preview modes are useful for layout checks but do not prove GPU rendering or complete gameplay support.

### Automatic update compatibility

Every `Build.ps1` run validates the completed ZIP with the frozen first automatic updater in `tools/Memoria.FF8.Updater.Compatibility`, independently of the current updater. Extraction, installation from a minimal older inventory, rollback and preservation of user files must pass before the build succeeds. CI publishes only after this gate. Keep this baseline immutable; add additional contracts when necessary rather than replacing older supported contracts.

Schema 1 update payloads must remain under `Data/`, with `Memoria.Launcher.exe` at the root. Only the existing empty `Mods/Tonberry/`, `Mods/Demaster/` and `Mods/Junction/` scaffolding belongs outside that payload. Put future bundled resources under `Data/` and retain the schema contract so already installed launchers can update directly.

An older launcher that rejected an archive before installation can retry against the next compatible release; manually creating the missing directory does not change its archive policy. This fix takes effect for those installations when a compatible ZIP is published.
