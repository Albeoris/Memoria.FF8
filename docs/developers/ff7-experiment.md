# Experimental FINAL FANTASY VII gameplay

The launcher's FINAL FANTASY VII tab configures a separate installed resource directory. Keep FINAL FANTASY VIII selected in its own tab, select the FINAL FANTASY VII installation, then choose **Start FFVII debug**. The shared engine opens the original FFVII title with New Game and Continue, bypassing the FFVIII startup presentation. This is an experimental opening-field implementation; installation detection does not certify full-game or original-save compatibility.

Direct invocation uses `--game-dir` for FINAL FANTASY VIII and `--ff7-game-dir` for FINAL FANTASY VII. Add `--ff7-debug` to open the FFVII title. `--ff7-new-game` explicitly starts the original New Game route instead. `MEMORIA_FF7_GAME_DIR` supplies the secondary resource directory for direct invocation; the launcher's saved selection, including Clear, takes precedence over inherited environment configuration.

Use `--crossover-save` to select the combined Memoria save destination, and `--crossover-state` to load a combined save. These saves preserve independent FFVII and FFVIII state payloads. They do not import original FFVII save files. The Debug Menu **World switch** requires the outgoing game's actual save permission. First entry starts the original destination New Game; subsequent visits resume the suspended session.

The .NET 10 projects keep native data and execution contracts separate:

- `Memoria.FF7.Formats`: bounded original archive, field, model, animation, text, kernel and encounter decoding.
- `Memoria.FF7.Resources`: installation, locale and resource-role discovery, original model/rig/clip identities and native executable profile data.
- `Memoria.FF7.Events`: independent field variables, commands, script continuations and typed host requests.
- `Memoria.FF7.Runtime`: FFVII progression, native field processes, input, movement, animation, battle transitions and combined-session saves.

Graphics backends, triangle submission, input infrastructure, media tools and process lifecycle are shared with the engine. FFVII keeps its own story, inventory, RNG, script PCs and save permissions. Character appearance mapping changes presentation independently of those facts. The initial standard field Squall profile uses native FFVIII walk/run and a separate opening-action retarget; broad model and motion replacement still requires explicit compatible profiles.

Original assets are read from user-selected installations. No original game files or resource export/import workflow are included.

Run focused checks from the product directory:

```powershell
dotnet run --project tests/Memoria.FF7.Resources.Tests --artifacts-path artifacts/ff7-resources
dotnet run --project tests/Memoria.FF7.Events.Tests --artifacts-path artifacts/ff7-events
dotnet run --project tests/Memoria.FF7.Runtime.Tests --artifacts-path artifacts/ff7-runtime
```

Set `MEMORIA_FF7_GAME_DIR` explicitly to include optional installed-resource checks; those checks skip when absent. These projects do not run battle-effect tests. Avoid repeating all-field catalog discovery unless investigating a catalog defect. The projects are registered in the main, Native and Portable solutions.
