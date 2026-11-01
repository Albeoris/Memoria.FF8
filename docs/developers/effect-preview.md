# Graphical effect preview

`--effect-preview` opens a common graphical arena using three numeric content identities. It does not submit a spell, GF, Limit or other gameplay command.

```json
{"enemy_id":1,"ally_id":0,"effect_id":2}
```

Enemy IDs address original enemy models, ally IDs address original character models and effect IDs address the original one-based graphical registry. The default arena stage and seed are0. Optional `stage_id`, `seed` and `duration_seconds` select the common environment and preview lifetime; the lifetime defaults to300 seconds after the first actual effect presentation.

```powershell
./Memoria.FF8.exe --game-dir $env:FF8_GAME_DIRECTORY --effect-preview preview.json --settings-dir output/settings --control-dir output/control --save-state output/disposable/unused.json --resolution 1280x720 --mute-audio
```

Use fresh isolated settings, control and disposable-save directories. No gameplay save is loaded or produced. Installed original resources are required. The host initializes an ally in physical row0 and the selected enemy in row3, with enemy AI and ATB growth disabled. The ally's raw gauge is filled to its authentic maximum without registering a command menu; readiness reports `atb_gating:false`. It publishes a neutral graphical header/contact context directly to the existing native descriptor, resource, camera and effect-controller owners. Command2/action1 are fixed graphical carrier fields; they do not select a gameplay spell, stock, damage formula or reward. Every numeric effect uses the same publication path. Unsupported registry entries or existing controller/resource contracts fail at their actual graphical boundary.

For paired recording, a coordinator can add the optional `presentation` transport described in [Scheduled battle presentation](battle-presentation-gate.md), including distinct command and visual signal paths. `ready.json` is emitted only after the initialized arena has completed its first native scene presentation. The command signal begins graphical preparation. The visual signal releases the genuine effect entry inside the same native callback after both endpoints report `visual-ready.json`. No focal effect entry executes while waiting. The host emits `ready.json`, `started.json` and `visual-started.json` with the selected effect identity and observed entry/presentation timestamps. Keep the in-frame visual hold short; coordinators should prepare the slower endpoint before admitting the faster endpoint, and verify their actual capture-source pixels before recording.

In this mode, `completed.json` acknowledges `graphical-preview-initialized` immediately after the first effect presentation. It does not claim effect drain, gameplay completion or native equivalence. The coordinator records a fixed duration and owns recording/process shutdown; set the host's `duration_seconds` long enough to include its recording tail. Standalone previews start immediately when no transport is supplied.
