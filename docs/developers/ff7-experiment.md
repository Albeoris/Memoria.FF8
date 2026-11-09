# Experimental FINAL FANTASY VII resources

The launcher has a separate FINAL FANTASY VII tab for an optional installed resource directory. FINAL FANTASY VIII remains the primary game selection. Configuring a FINAL FANTASY VII directory does not enable FINAL FANTASY VII gameplay, a title screen or New Game/Continue.

For direct engine invocation, keep `--game-dir` for the FINAL FANTASY VIII installation and pass `--ff7-game-dir` separately. `MEMORIA_FF7_GAME_DIR` supplies the optional directory for direct invocations. The launcher's saved selection, including Clear, takes precedence over inherited environment configuration. Installation layout detection does not certify executable or original-save compatibility.

The separate .NET 10 projects provide:

- `Memoria.FF7.Formats`: read-only LGP archives, bounded raw/framed LZSS, PC field section containers, HRC hierarchies and PC field animations.
- `Memoria.FF7.Resources`: independent installation/locale/resource-role discovery for classic and nested Steam resource layouts.
- `Memoria.FF7.Events`: an independent field command subset with an explicit PS1-derived semantic profile, separate variable storage and deterministic continuation snapshots. PC runtime equivalence and full original field execution are not established.

No resource export/import workflow or original game assets are included. Resource readers preserve native identities; field rendering, dialogue, movies, FFVII gameplay saves and crossover character mapping require further implementation.

Run the focused checks from the product directory:

```powershell
dotnet run --project tests/Memoria.FF7.Resources.Tests --artifacts-path artifacts/ff7-resources
dotnet run --project tests/Memoria.FF7.Events.Tests --artifacts-path artifacts/ff7-events
```

Set `MEMORIA_FF7_GAME_DIR` explicitly to include optional installed-resource checks. Without it, those checks skip. The Events console checks use authored synthetic programs. Neither command runs battle-effect tests. These projects are registered in the main, Native and Portable solutions.
