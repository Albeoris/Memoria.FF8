# Experimental FINAL FANTASY VII gameplay

The launcher's FINAL FANTASY VII tab configures a separate installed resource directory. Keep FINAL FANTASY VIII selected in its own tab, select the FINAL FANTASY VII installation, then choose **Start FFVII debug**. The shared engine opens the original FFVII title with New Game and Continue, bypassing the FFVIII startup presentation. This is an experimental opening-field implementation; installation detection does not certify full-game or original-save compatibility.

Direct invocation uses `--game-dir` for FINAL FANTASY VIII and `--ff7-game-dir` for FINAL FANTASY VII. Add `--ff7-debug` to open the FFVII title. `--ff7-new-game` explicitly starts the original New Game route instead. `MEMORIA_FF7_GAME_DIR` supplies the secondary resource directory for direct invocation; the launcher's saved selection, including Clear, takes precedence over inherited environment configuration.

For a source checkout, set both installation variables to your selected directories and run from the product directory:

```powershell
dotnet run --project src/Memoria.FF8.App -- --game-dir "$env:MEMORIA_FF8_GAME_DIR" --ff7-game-dir "$env:MEMORIA_FF7_GAME_DIR" --ff7-debug
```

This uses your current shared settings and control profile. Add `--crossover-state '<combined save path>'` to resume a supported combined standalone instead of the title.

Use `--crossover-save` to select the combined Memoria save destination, and `--crossover-state` to load a combined save. These saves preserve independent FFVII and FFVIII state payloads. They do not import original FFVII save files. The Debug Menu **World switch** requires the outgoing game's actual save permission. First FFVII entry starts its original New Game; subsequent visits resume its suspended session, while return to FFVIII restores its retained continuation. The shared save menu also lists FFVII-only Memoria slots: loading one from an eligible FFVIII session retains that session for the return trip and prepares FFVII before committing the switch. A failed destination load leaves the current world intact. Diagnostic snapshots do not enable World switch.

The .NET 10 projects keep native data and execution contracts separate:

- `Memoria.FF7.Formats`: bounded original archive, field, model, animation, text, kernel and encounter decoding.
- `Memoria.FF7.Resources`: installation, locale and resource-role discovery, original model/rig/clip identities and native executable profile data.
- `Memoria.FF7.Events`: independent field variables, commands, script continuations and typed host requests.
- `Memoria.FF7.Runtime`: FFVII progression, native field processes, input, movement, animation, battle transitions and combined-session saves.

The bounded implementation includes the original opening movie, field progression, dialogues and naming, plus an explicit debug auto-victory policy. Battle rendering/AI, full FFVII gameplay and broad engine consolidation are outside this supported slice. Required unsupported native behavior fails with context.

Graphics backends, GPU triangle/material submission, input, fonts/windows/dialogue and naming controls, media decoding and output are shared with the engine. Dialogue uses common presentation by default; the F2 font setting selects native FFVII presentation through the shared localizer preference. Movie playback uses `MoviePresentation`; music uses shared `PcmLoopPlayback` and PCM output. `GraphicsRestartHandoff` and `UpdateSaveHandoff` reuse existing lifecycle/save transitions.

FFVII uses the current shared control profile on load, including keyboard/controller bindings, analog walking and menu prompts. Running is the default; the shared walk modifier or analog threshold selects walking, while script-owned movement retains its native choice. Game acceleration, movement preference, presentation frame rate and sound volume apply through the existing settings services. Hold the configured Cancel action to skip a movie through the same gesture used by FFVIII. Errors use the common Report/Details/Copy panel; reports require the player's action.

The Debug Menu offers FFVII autoplay through the common scheduler. Its game-specific policy supplies physical movement, interaction and dialogue input; it does not write story variables or invoke event scripts directly. Native battle requests use the explicit debug auto-victory transaction and record rewards once.

FFVII keeps its own VM, story, inventory, RNG, script PCs and save permissions. Combined saves retain FFVIII as opaque `Data`, without translating its progress into FFVII. Character appearance mapping changes presentation independently of those facts. Each field binds model descriptors and its selected animations; a character name alone is not a skeleton identity. Standard field Squall `d000` has 25 bones and 162 compatible clips; world Squall is a separate 16-bone/eight-clip rig. The standard Cloud catalog has 495 compatible clips. These counts describe compatible resource groups, rather than clips loaded by every field instance.

The Cloud→Squall profile retains native Squall idle/walk/run and retargets unique Cloud actions through explicit anatomical bindings, neutral calibration and the common pose pipeline. The v2 profile also admits the tutorial's party-character Cloud rig; saved v1 profile identifiers remain accepted. Exact rig validation replaces the initial eight-animation filename list. A selected pose must remain within its original animation allocation, and an unsupported rig or channel fails with context. Catalog membership does not establish artistic quality for every action; broad model-replacement mods still require profile and clip validation.

Original assets are read from user-selected installations. No original game files or resource export/import workflow are included.

Run focused checks from the product directory:

```powershell
dotnet run --project tests/Memoria.FF7.Resources.Tests --artifacts-path artifacts/ff7-checks -- --filter-not-method '*OptionalInstalledAllFieldCatalogPreservesCloudOpeningAndCompatibleBindings'
dotnet run --project tests/Memoria.FF7.Events.Tests --artifacts-path artifacts/ff7-checks
dotnet run --project tests/Memoria.FF7.Runtime.Tests --artifacts-path artifacts/ff7-checks
```

Set `MEMORIA_FF7_GAME_DIR` explicitly to include optional installed-resource checks; those checks skip when absent. These projects do not run battle-effect tests. Avoid repeating all-field catalog discovery unless investigating a catalog defect. The projects are registered in the main, Native and Portable solutions.
