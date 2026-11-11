# Experimental FINAL FANTASY VII gameplay

The launcher's FINAL FANTASY VII tab configures a separate installed resource directory. Keep FINAL FANTASY VIII selected in its own tab, select the FINAL FANTASY VII installation, then choose **Start FFVII debug**. The shared engine opens the original FFVII title with New Game and Continue, bypassing the FFVIII startup presentation. The validated experimental route continues through the church and selected Wall Market fields. Exhaustive autoplay deliberately stops at unreviewed interactions or area frontiers; installation detection does not certify full-game, complete world-map progression or original-save compatibility.

Choose the FFVII installation explicitly in the launcher's FINAL FANTASY VII tab. The launcher passes that folder to the game and clears inherited FFVII environment settings. When world switching is unavailable, select the entry in Debug Room to read the reason in the right-hand panel. Direct developer invocation can supply the FFVII folder with `--ff7-game-dir` or `MEMORIA_FF7_GAME_DIR`. Add `--ff7-debug` to open the FFVII title, or use `--ff7-new-game` to start the original New Game route.

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

The bounded implementation includes the original opening movie, the first two reactor missions, the church and reached Wall Market interactions, dialogues and naming, shared ordinary menus and shops, plus an explicit debug auto-victory policy. Original world resources, simulation and rendering adapters have preparatory checks; actual story-driven world entry and full world-map support remain unvalidated. Battle rendering/AI and full FFVII gameplay are outside this supported slice. Required unsupported native behavior fails with context.

Graphics backends, GPU triangle/material submission, input, fonts/windows/dialogue and naming controls, media decoding and output are shared with the engine. Dialogue uses common presentation by default; the F2 font setting selects native FFVII presentation through the shared localizer preference. Movie playback uses `MoviePresentation`; music uses shared `PcmLoopPlayback` and PCM output. `GraphicsRestartHandoff` and `UpdateSaveHandoff` reuse existing lifecycle/save transitions.

FFVII uses the current shared control profile on load, including keyboard/controller bindings, analog walking and menu prompts. Running is the default; the shared walk modifier or analog threshold selects walking, while script-owned movement retains its native choice. Game acceleration, movement preference, presentation frame rate and sound volume apply through the existing settings services. Hold the configured Cancel action to skip a movie through the same gesture used by FFVIII. Errors use the common Report/Details/Copy panel; reports require the player's action.

Message opening, text reveal and closing follow the FFVII script owner's native timing. Both font modes share the same saved dialogue facts; common text layout measures the full page and displays its revealed prefix inside the animated window. The shared message-speed preference adjusts FFVII's native speed bands, retaining its own base configuration. Loading an older continuation preserves completed pages and choices while restoring absent presentation fields from saved facts and compatible defaults.

The Debug Menu offers FFVII autoplay through the common scheduler. Its game-specific policy supplies physical movement, interaction and dialogue input; it does not write story variables or invoke event scripts directly. Native battle requests use the explicit debug auto-victory transaction and record rewards once. The continuation policy targets the native Midgar world entry and requires observed field-event completion and covered optional-area entry/return witnesses. Unknown catalogs and nested destinations stay incomplete; story navigation cannot silently skip them. Reaching a policy target does not enable saving or World switch where the original scripts forbid it.

FFVII keeps its own VM, story, inventory, RNG, script PCs and save permissions. Combined saves retain FFVIII as opaque `Data`, without translating its progress into FFVII. Character appearance mapping changes presentation independently of those facts. Each field binds model descriptors and selected animations; a local clip count is not a whole-character census. Field, world and battle rigs remain distinct resource families. The association editor offers nine FFVII story identities and eleven available FFVIII appearances, with explicit field-animation compatibility percentages and missing-clip reasons. It opens before every eligible crossing, including revisits and return to FFVIII; cancellation retains the current worlds. Native FFVIII actor presentation stays unchanged.

Appearance profiles retain native target idle/walk/run bindings and retarget admitted source actions through anatomical bindings and the common pose pipeline. Profiles are versioned, older identifiers remain supported and exact rig/allocation validation stays strict. Recognized authored bind-relative repairs do not emulate out-of-allocation reads. Catalog membership is not a guarantee of artistic contact quality, all model variants or combat compatibility.

The character mapping enables full combat when switching to FFVII. Each occupied mapped FFVII party slot can use a different FFVIII guest, up to all three slots. Each guest reads equipment, HP, junctions and stock from their own character record in the suspended FFVIII save. FFVIII's item inventory and random streams are shared by the guests. A restored encounter keeps the combat owners recorded when it began.

FFVII battles use the common battle HUD and popup windows. FFVII party members show MP below HP and spell costs in the Magic list's right column. The FFVIII guest uses their spell stock in that column.

The Battle ATB mode in Config → Combat applies to both battle systems. Classic advances ATB continuously during ordinary command selection. Turn Based advances to the next ready actor, holds ATB during a player decision, and keeps action presentation running. The configured Battle Advance Turn action advances to another readiness event when a player is already ready. FFVII victories award 1 AP per defeated ordinary enemy or 20 AP for a formation with a native boss death type.

### Mixed damage and statuses

The FFVII encounter resolves the hit. An FFVIII Attack supplies the equipped weapon, junctioned stats and the elemental and status portions from the source record. Against an FFVII target, the translated element uses the target's native affinity. Against the FFVIII guest, the translated element and status use the guest's original junction defenses. An element with no counterpart is neutral. An unmapped status application leaves the target's original statuses as they were.

Common status mappings cover KO, Poison, Petrify, Blind, Silence, Berserk, Sleep, Haste, Slow, Stop, Protect/Barrier and Shell/MBarrier. Some native behavior remains incomplete, including forced Berserk turns and several status timers. Confuse, Regen, Reflect, Doom/Death Sentence, Petrifying/Slow Numb and Invincible/Peerless have counterparts and await host behavior support.

- FFVIII statuses without a shared counterpart include Zombie, Aura, Curse, Float, Drain, Eject, Double, Triple, Charged, Vit 0, Angel Wing and The End.
- FFVII statuses without a shared counterpart include Sadness, Fury, Frog, Small, Dual, Shield, Manipulate, Paralysis, Dual Drain, Death Force, Resist, Lucky Girl and Imprisoned. Near Death is an FFVII HP state.

The shared Attack path reads each guest's original actor and weapon sequence. Squall's supported Limit paths cover ordinary Renzokuken and Rough Divide. The other Limit commands and finishers require their own native execution and presentation contracts.

The ordinary menu opens to Final Fantasy VII with Item, Magic, Materia, Equip, Status, Order, Limit, Config, PHS and Save, plus the common load and world-switch actions. Order changes the party's slot order and resident battle rows. PHS replaces active members with available reserves when native field or world permissions and story locks allow it. The configured Rotate Left/Right and Camera Left/Right actions (L1/R1 and L2/R2 by default) switch between the two main pages when the active party includes an FFVIII guest. The Final Fantasy VIII page uses the suspended game's junctions, magic stock, GF, abilities, junction exchange, cards and tutorial.

FFVII Item and Magic support single-target recovery for resident party members. Other items and spells remain visible for browsing. Equip changes compatible weapons and armor and returns displaced materia with their AP. Accessories can be browsed or removed when permitted. Limit shows learned techniques and allows setting a learned level. Confirmation resets its gauge, and support for executing individual Limit techniques still depends on the battle implementation.

Native party vitals caches are published on entry without healing beyond the original upper clamp. Field scripts pause while native real play time and music continue. Shops use original FFVII stock, item/materia/gear, Gil, AP prices and exactly-once receipts through the existing common shop layout. Their native fade callbacks stay at60Hz independently of UI FPS. Confirmed Exit and Cancel retain distinct native sounds.

Original assets are read from user-selected installations. No original game files or resource export/import workflow are included.

Run focused checks from the product directory:

```powershell
dotnet run --project tests/Memoria.FF7.Resources.Tests --artifacts-path artifacts/ff7-checks -- --filter-not-method '*OptionalInstalledAllFieldCatalogPreservesCloudOpeningAndCompatibleBindings'
dotnet run --project tests/Memoria.FF7.Events.Tests --artifacts-path artifacts/ff7-checks
dotnet run --project tests/Memoria.FF7.Runtime.Tests --artifacts-path artifacts/ff7-checks
```

Set `MEMORIA_FF7_GAME_DIR` explicitly to include optional installed-resource checks; those checks skip when absent. These projects do not run battle-effect tests. Avoid repeating all-field catalog discovery unless investigating a catalog defect. The projects are registered in the main, Native and Portable solutions.
