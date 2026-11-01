# Graphical effect and whole-ability preview

`--effect-preview` opens a common graphical arena. Whole-ability mode resolves an original kernel ability and uses its complete graphical command service, including summon presentation, final contacts, camera and actor restoration.

```json
{"enemy_id":1,"ally_id":0,"command_id":3,"ability_id":1}
```

Ability IDs use the original menu command namespace. Command3 selects a GF row, so ability1 is Shiva; command2 selects a Magic row, so ability1 is Fire. Command3 is the default when `ability_id` is supplied without `command_id`. The host reads the actual installed kernel row to derive the executed command, action and effect; Shiva uses execution command254/action65 and the original GF service. Magic and GF are the supported whole-ability command namespaces. No spell stock, menu eligibility, damage formula or reward is evaluated for this visual preview.

The advanced raw-effect alternative selects a single graphical registry entry:

```json
{"enemy_id":1,"ally_id":0,"effect_id":2}
```

Select exactly one of `ability_id` or `effect_id`. Enemy IDs address original enemy models, ally IDs address original character models and effect IDs address the original one-based graphical registry. The default arena stage and seed are0. Optional `stage_id`, `seed` and `duration_seconds` select the common environment and maximum lifetime. The default is300 seconds. In whole-ability mode this is a failure deadline, not a recording cut point; the coordinator waits for both complete sequences before stopping recording.

```powershell
./Memoria.FF8.exe --game-dir $env:FF8_GAME_DIRECTORY --effect-preview preview.json --settings-dir output/settings --control-dir output/control --save-state output/disposable/unused.json --resolution 1280x720 --mute-audio
```

Use fresh isolated settings, control and disposable-save directories. No gameplay save is loaded or produced. Installed original resources are required. The host initializes an ally in physical row0 and the selected enemy in row3, with enemy AI and ATB growth disabled. The ally's raw gauge is filled to its authentic maximum without registering a command menu; readiness reports `atb_gating:false`. Both modes publish a neutral graphical header/contact context directly to the existing native descriptor, resource, camera and effect-controller owners. Raw-effect mode uses fixed command2/action1 carrier fields and invokes only the selected module; it does not recreate a GF's complete summon/camera/return service. Use whole-ability mode for that sequence. Unsupported registry entries or existing controller/resource contracts fail at their actual graphical boundary.

For paired recording, a coordinator can add the optional `presentation` transport described in [Scheduled battle presentation](battle-presentation-gate.md), including distinct command and visual signal paths. `ready.json` is emitted only after the initialized arena has completed its first native scene presentation. The command signal begins graphical preparation. The visual signal releases the genuine effect entry inside the same native callback after both endpoints report `visual-ready.json`. No focal effect entry executes while waiting. The host emits `ready.json`, `started.json` and `visual-started.json` with the selected effect identity and observed entry/presentation timestamps. Keep the in-frame visual hold short; coordinators should prepare the slower endpoint before admitting the faster endpoint, and verify their actual capture-source pixels before recording.

Whole-ability receipts include top-level `command_id`, `ability_id` and metadata-derived `expected_effect`. `completed.json` is withheld until all native graphics, camera, descriptors, contacts, files and temporary model resources drain, followed by two presented tail frames. Its `native_identity` includes `scope:"full-ability"` and `drained:true`. The coordinator waits for both endpoints' completion receipts, records its requested tail and owns process shutdown. The host keeps the final arena until its maximum lifetime.

Raw-effect mode preserves its existing fixed-duration behavior: `completed.json` acknowledges `graphical-preview-initialized` immediately after the first effect presentation and does not claim full ability drain. Standalone previews start immediately when no transport is supplied. Neither mode's receipts alone establish native equivalence.
