# Experimental FINAL FANTASY VII gameplay

The launcher's FINAL FANTASY VII tab configures a separate installed resource directory. Keep FINAL FANTASY VIII selected in its own tab, select the FINAL FANTASY VII installation, then choose **Start FFVII debug**. The shared engine opens the original FFVII title with New Game and Continue, bypassing the FFVIII startup presentation. This is an experimental opening-field implementation; installation detection does not certify full-game or original-save compatibility.

Direct invocation uses `--game-dir` for FINAL FANTASY VIII and `--ff7-game-dir` for FINAL FANTASY VII. Add `--ff7-debug` to open the FFVII title. `--ff7-new-game` explicitly starts the original New Game route instead. `MEMORIA_FF7_GAME_DIR` supplies the secondary resource directory for direct invocation; the launcher's saved selection, including Clear, takes precedence over inherited environment configuration.

Use `--crossover-save` to select the combined Memoria save destination, and `--crossover-state` to load a combined save. These saves preserve independent FFVII and FFVIII state payloads. They do not import original FFVII save files. The Debug Menu **World switch** requires the outgoing game's actual save permission. First FFVII entry starts its original New Game; subsequent visits resume its suspended session, while return to FFVIII restores its retained continuation. The shared GPU host verified bidirectional switching at the first native reactor save point (`nmkin_4`, tick 421), returning to the same FFVII tick with optional Cloud→Squall mapping preserved. Combined cold reload also passed. After the existing FFVIII cold-load normalization, a warm round trip preserved both payloads and mappings byte-exact; advancing FFVIII one callback tick and saving retained FFVII for re-entry at tick 421. First FFVII entry starts original New Game at tick 0, and departure is refused without native save permission. Diagnostic snapshots do not enable World switch.

The .NET 10 projects keep native data and execution contracts separate:

- `Memoria.FF7.Formats`: bounded original archive, field, model, animation, text, kernel and encounter decoding.
- `Memoria.FF7.Resources`: installation, locale and resource-role discovery, original model/rig/clip identities and native executable profile data.
- `Memoria.FF7.Events`: independent field variables, commands, script continuations and typed host requests.
- `Memoria.FF7.Runtime`: FFVII progression, native field processes, input, movement, animation, battle transitions and combined-session saves.

The bounded implementation includes the original opening movie, field progression, dialogues and naming, plus an explicit debug auto-victory policy. Battle rendering/AI, full FFVII gameplay and broad engine consolidation are outside this supported slice. Required unsupported native behavior fails with context.

Graphics backends, GPU triangle/material submission, input, fonts/windows/dialogue and naming controls, media decoding and output are shared with the engine. Dialogue uses common presentation by default; the F2 font setting selects native FFVII presentation through the shared localizer preference. Movie playback uses `MoviePresentation`; music uses shared `PcmLoopPlayback` and PCM output. `GraphicsRestartHandoff` and `UpdateSaveHandoff` reuse existing lifecycle/save transitions.

FFVII keeps its own VM, story, inventory, RNG, script PCs and save permissions. Combined saves retain FFVIII as opaque `Data`, without translating its progress into FFVII. Character appearance mapping changes presentation independently of those facts. Standard field Squall `d000` has 25 bones and 162 compatible clips; world Squall is a separate 16-bone/eight-clip rig. The Cloud catalog has 495 compatible clips. The initial profile uses three native Squall bindings (idle/walk/run) and eight explicitly admitted unique retargets (`BVJF`, `AUEE`, `AJDD`, `BXIB`, `BYAA`, `BXIF`, `BYAD`, `APJF`). Catalog membership alone does not admit an action; broader replacement requires compatible profiles and validation.

Original assets are read from user-selected installations. No original game files or resource export/import workflow are included.

Run focused checks from the product directory:

```powershell
dotnet run --project tests/Memoria.FF7.Resources.Tests --artifacts-path artifacts/ff7-resources
dotnet run --project tests/Memoria.FF7.Events.Tests --artifacts-path artifacts/ff7-events
dotnet run --project tests/Memoria.FF7.Runtime.Tests --artifacts-path artifacts/ff7-runtime
```

Set `MEMORIA_FF7_GAME_DIR` explicitly to include optional installed-resource checks; those checks skip when absent. These projects do not run battle-effect tests. Avoid repeating all-field catalog discovery unless investigating a catalog defect. The projects are registered in the main, Native and Portable solutions.
