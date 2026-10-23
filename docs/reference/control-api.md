# Local control API

The opt-in control queue accepts JSON requests through a local directory. It is intended for an application instance you own, test tools and diagnostics. Keyboard/controller input and ordinary queue actions use the same current engine owners. The queue exposes no raw story-variable or script program-counter mutation command.

See [command-line options](command-line.md) for startup modes. For a windowed instance, supply `--control-dir <directory>`. Add `--control-step` to suppress automatic field/world simulation and advance it through explicit actions. Drawing continues; decoded movies retain their playback clock and movie-driven event behavior. Without control-step, gameplay also advances in real time between requests. Control-enabled sessions mute host audio through the current launch policy.

For a persistent headless session, use `--headless-control --state <standalone.json> --control-dir <directory>`. It requires a field, world or movie snapshot, runs no timed simulation loop and has a smaller command set. It does not render captures. The separate `--headless-play` mode consumes an action array and writes its configured outputs; see the command-line reference.

## Transport and response envelope

The host creates `requests`, `responses`, `processed` and `captures` beneath the queue directory. Publish a request by writing `requests/<unique-id>.tmp` completely, then atomically renaming it to `requests/<unique-id>.json` on the same filesystem. Use the same unique ID in the JSON `Id` field. Requests are processed one at a time in ordinal filename order. Windowed dispatch runs on the game thread.

The response has the same filename under `responses` and is also written through a temporary file and atomic rename. The request is then moved to `processed`. A filename with an existing response is archived without executing again. Deduplication is by filename, not the JSON `Id`; never reuse either. A timeout can leave an accepted request pending: check its existing response instead of resubmitting the action under a new ID. Keep one coordinated producer/owner for a running instance.

```json
{"Id":"001-status","Command":"status","Compact":true}
```

```json
{"Id":"001-status","Ok":true,"Result":{}}
```

The empty `Result` above illustrates only the envelope. Failed deserialization or dispatch returns:

```json
{"Id":"001-status","Ok":false,"Error":"Error message","Type":"ArgumentException"}
```

Request property names are case-insensitive, command names are case-insensitive, and enums are serialized as names. Prefer the documented enum strings. A successful transport response does not imply an accepted player operation: inspect `ActionResult.Accepted`, `Detail` and `Trigger`. Unsupported commands and contracts return explicit errors.

## Request fields

| Field | Default and purpose |
| --- | --- |
| `Id`, `Command` | Required request identity and command name. |
| `Action` | `PlayerAction` payload for `action` and headless `run`. |
| `Frames` | 900; wait budget for `advance`, action repetition count for headless `run`. |
| `Path` | Optional explicit save/capture path; relative paths resolve against the host working directory. |
| `Name`, `Selection` | Optional current-prompt presentation draft. |
| `Debug` | Typed scene presentation override. |
| `Compact` | false; omit large trace arrays while retaining counts. |
| `Ui` | Typed menu request. |
| `Battle` | Typed battle request. |

## Session commands

| Command | Windowed | Headless control | Result/behavior |
| --- | --- | --- | --- |
| `status` | Yes | Yes | Current status. |
| `new-game` | Yes | No | Starts a new session through the current New Game owner, then returns status. |
| `action` | Yes | Yes | Applies `Action`; normally returns `{ActionResult, Status}`. Windowed startup/tutorial handling can return status directly. |
| `advance` | Yes | Yes | Applies an ordinary `Wait` with `Frames`; same result rules as `action`. |
| `run` | No | Yes | Repeats `Action` up to positive `Frames` times; saves `before-run-checkpoint.json` in the queue first. |
| `save` | Yes | Yes | Saves to `Path` or configured checkpoint destination, then returns status. |
| `capture` | Yes | Yes, acknowledgement only | Windowed: requests a PNG on the next draw. Headless: `{Captured:false, Reason:...}`. |
| `draft` | Yes | No | Changes the currently open prompt's name/selection draft; returns status. |
| `debug-scene` | Yes | No | Applies a typed presentation override; returns status. |
| `quit` | Yes | Yes | Normal process exit request, returning `{Exiting:true}`. |

`advance` does not auto-answer dialogue or skip a movie. An ordinary field Wait stops at the next trigger or its frame budget; it can stop early when control becomes playable. `WaitWhilePlayable:true` requests continued playable waits, still stopping at a changed trigger or observed script input boundary. Headless `run` also stops on a rejected action, field/scene change, confirmable dialogue, transition, battle, tutorial or fault. Its result reports the last action result, not an aggregate of every iteration.

Windowed `action`/`advance` is refused while a normal UI overlay owns input. Use `ui-input` for that overlay. A native tutorial accepts only its current Wait/Confirm/Cancel contract. A script fault prevents further simulation; inspect/save it or restart from a safe snapshot. Rendering faults are reported separately.

```json
{"Id":"010-move","Command":"action","Action":{"Kind":"Move","Horizontal":1,"Vertical":0,"Frames":15}}
{"Id":"011-wait","Command":"advance","Frames":120,"Compact":true}
{"Id":"012-save","Command":"save","Path":"saves/after-event.json","Compact":true}
{"Id":"013-capture","Command":"capture"}
```

The capture response acknowledges scheduling, not file completion. Wait until the PNG exists before reading it. Further queue polling is fenced while the capture is pending. The default path is `captures/<id>.png`; status exposes `CapturePath` while pending. Save commands capture actual current state and cannot write into the player-saves namespace. See [saves](../players/saves.md).

## PlayerAction fields and operations

`Kind` is required. Other fields default to `Value:0`, `Text:null`, `Horizontal:0`, `Vertical:0`, `Frames:1`, `Quantity:null`, `CameraHorizontal:0`, `WaitWhilePlayable:false`. Ordinary field/world frame budgets must be 1–30000. Axes must be finite and within -1..1; field diagonals normalize. `CameraHorizontal` belongs to world Move/Wait and is refused for field actions.

Choose identities from the active dialogue/menu/battle owner and current resources. Numeric examples demonstrate payload shape rather than guaranteeing an eligible item or actor in every save. Availability, control gates, learned abilities, stock and target rules still apply.

| `Kind` | Payload and meaning |
| --- | --- |
| `Confirm` | Confirms ordinary text, context interaction or the active owner's confirmation. Does not choose a line or supply a name. |
| `Choose` | `Value`: actual visible dialogue choice line index, within its reported first/last choice. |
| `EnterName` | `Text`: name for the active naming prompt. |
| `Interact` | `Value`: current nearby actor object ID; uses normal interaction eligibility. |
| `CardChallenge` | `Value`: selected eligible actor ID, or negative to accept the currently selected interaction target. |
| `Move` | `Horizontal`, `Vertical`, `Frames`; optional world `CameraHorizontal`. Uses ordinary movement/collision/control gates. Movement remains available during movies when the field script enables player control; each frame advances through the movie clock. |
| `Wait` | `Frames`; optional `WaitWhilePlayable`, and world `CameraHorizontal`. |
| `SkipMovie` | Completes/skips the active clip through its supported movie callback contract. |
| `Cancel` | Current dialogue/menu/card owner's cancellation; accepted only where allowed. |
| `FieldButtons` | `Value`: field button mask, for the active field input owner, including movie playback. |
| `WorldButtons` | `Value`: unsigned 16-bit digital mask for the supported active world vehicle owner. For cars and Garden, optional `Horizontal` supplies left-stick steering and `Vertical` supplies right-stick throttle; digital directions and throttle buttons override their corresponding analog axes. Up/down throttle alternatives are added by the graphical host; raw packets retain native button semantics. Unsupported vehicle/menu contracts fail explicitly. |
| `SelectPartySlot` | `Value`: slot 0..2 in an active party selection menu. |
| `SelectPartyMember` | `Value`: physical character row 0..7 or 255 for an allowed empty member. |
| `BuyShopItem`, `SellShopItem` | `Value`: item byte; explicit `Quantity`: 1..255. Requires the matching shop owner. |
| `SelectWeaponShopCharacter` | `Value`: physical character 0..5. |
| `RemodelWeapon` | `Value`: ordinary recipe 0..27; requires the active weapon shop. |
| `JunctionGuardianForce` | `Value`: character row 0..7; `Text`: decimal GF ID 0..15. Toggles its junction. |
| `SetJunctionCommand` | `Value`: character row; `Text`: decimal command ability ID 0..127; `Quantity`: slot 0..2. |
| `SetJunctionAbility` | `Value`: character row; `Text`: decimal ability ID 0..127; `Quantity`: slot 0..3. |
| `SelectGuardianForceLearning` | `Value`: GF 0..15; `Text`: decimal learning ability ID 0..127. |
| `SetJunctionMagic` | `Value`: character row; `Text`: decimal spell ID 0..56; `Quantity`: junction slot 0..18. |
| `RemoveJunctions` | `Value`: character row; `Text`: `"0"` for Magic or `"1"` for All. |
| `RefineItems` | `Value`: learned ability 97..115; `Text`: `recipe:row`; `Quantity`: batch count 1..100. Recipe is absolute index 0..142. Recipient row is physical character 0..7 where applicable. |
| `UseFieldItem` | `Value`: item byte; `Quantity`: physical character 0..7 or GF target 16..31. Optional decimal `Text` identifies the ability to forget for the corresponding item contract. Naming items require their subsequent name prompt. |
| `ReadMagazinePage` | `Value`: owned item ID; `Quantity`: absolute resource page 0..68. |
| `UseFieldMagic` | `Text`: `source:spell:target` using physical rows 0..7 and spell 1..56; `Value`: 1. |
| `TransferMagic` | Same `Text` shape; `Value`: count 1..100. |
| `TriadSelectCard` | `Value`: card selection in the active card owner. |
| `TriadPlaceCard` | `Value`: hand slot 0..4; `Quantity`: board cell 0..8. |
| `TriadAutoMove` | Advances the active player's supported automatic card move. |
| `TriadTradeCard` | `Value`: active trade selection. |

Junction, learning, refinement and magic menu operations require one frame with zero movement axes and their exact typed payload. Shop changes remain in the shop-local cache until its ordinary close commits them. Weapon remodel uses its existing shop transaction. Declaring a `Kind` does not bypass an unsupported active owner.

```json
{"Id":"020-choice","Command":"action","Action":{"Kind":"Choose","Value":2}}
{"Id":"021-name","Command":"action","Action":{"Kind":"EnterName","Text":"Cecilia"}}
{"Id":"022-buy","Command":"action","Action":{"Kind":"BuyShopItem","Value":191,"Quantity":1}}
{"Id":"023-refine","Command":"action","Action":{"Kind":"RefineItems","Value":97,"Text":"0:0","Quantity":1}}
{"Id":"024-magic","Command":"action","Action":{"Kind":"UseFieldMagic","Text":"0:1:0","Value":1}}
```

## Status and observation records

The windowed status object contains scene/field identifiers and names, startup stage, movie playback position, field/world arrival state, world state, user-control gate, camera/locked triangles, actors, dialogue channels, party/menu state, names, music/audio metadata, host UI/battle status, performance and fault information. `Dialogue.Text` and `Trigger.Text` are formatted for display; `RawDialogueText` preserves the encoded source text. `ExamTimer`, when present, includes display position/text and minimum/maximum remaining time. `WorldRenderTarget` reports raster dimensions and limit.

`FieldFaulted`/`LastFault` identify simulation failure; `FieldRenderFaulted` identifies a latched presentation failure. Status availability does not guarantee playable support beyond the current boundary. Headless status includes its relevant simulation subset, tutorial/battle snapshot and traces, without window, audio or raster metrics.

`Compact:true` on status/action/advance/save/capture suppresses the exported `Coverage`, `Decisions`, `DialogueOutcomes` and `ActionLog` arrays and retains `TraceCounts`. It does not erase the engine's traces. Use full status when an export is needed.

| Record | JSON fields |
| --- | --- |
| `PlayerTrigger` | `Kind`, `FieldId`, `Frame`, `MessageId`, `Text`. Kinds: `Unloaded`, `Waiting`, `Dialogue`, `Movie`, `FieldTransition`, `Playable`, `Faulted`, `WorldMap`, `PartyMenu`, `Battle`, `TripleTriad`, `Tutorial`. |
| `PlayerActionResult` | `Accepted`, `Trigger`, `FramesAdvanced`, `Detail`, `NativeInputObserved`. |
| `PlayerActionObservation` | `Index`, `FieldId`, `Frame`, `Action`, `Result`. |
| `BranchDecision` | `FieldId`, `ObjectId`, `ScriptId`, `Operation`, `Frame`, `Predicate`, `Value`, `ConditionPassed`, `JumpTarget`, `ActionIndex`. |
| `BranchCoverage` | `FieldId`, `ObjectId`, `ScriptId`, `Operation`, `Predicate`, `TrueCount`, `FalseCount`. |
| `DialogueOutcome` | `FieldId`, `ObjectId`, `ScriptId`, `Operation`, `Frame`, `MessageId`, `Choice`, `Name`, `ActionIndex`. |

These are response/snapshot observations, not a separate streaming event transport. Operation indices describe the loaded event program. A transition result can precede destination allocation; re-read status after the host has advanced the transition.

## Prompt drafts and debug presentation

`draft` accepts `Name` for a matching naming prompt (at most twelve printable characters) or `Selection` within the current choice lines. It changes presentation only and does not commit the story outcome. A subsequent save retains the draft.

```json
{"Id":"030-draft","Command":"draft","Name":"Cec"}
{"Id":"031-hide-dialogue","Command":"debug-scene","Debug":{"Target":"Dialogue","Visible":false}}
{"Id":"032-hide-actor","Command":"debug-scene","Debug":{"Target":"Actor","ActorId":15,"Visible":false}}
{"Id":"033-pose","Command":"debug-scene","Debug":{"Target":"Actor","ActorId":15,"AnimationId":0,"Frame":0,"Playing":true,"FramesPerSecond":15}}
{"Id":"034-hide-background","Command":"debug-scene","Debug":{"Target":"Background","ModuleId":3,"Visible":false}}
{"Id":"035-clear","Command":"debug-scene","Debug":{"Target":"Clear"}}
```

`Debug.Target` is `Clear`, `Dialogue`, `Background` or `Actor`. Background visibility can apply to all tiles or `ModuleId` 0..255. Actor overrides accept optional `Visible`, `X`, `Y`, `Z`, `AnimationId`, `Frame`, `Playing` and `FramesPerSecond`; use an existing actor and a valid animation/frame. Omitted properties retain prior overrides. Animation playback has a separate presentation clock. Overrides are field-scoped, saved in snapshots and cleared on field transition or Clear. They do not change collision, VM actors, story choices or ordinary action traces. `DebugPresentation` and `DebugActionLog` report them separately. Hidden dialogue still needs ordinary confirmation.

## UI commands

Windowed commands `debug-room`, `ui-open`, `ui-close`, `ui-input` and `ui-status` return the current UI status: `IsOpen`, `IsDemo`, `ScreenId`, `Message`, `ScreenIds`, `Entries`, `SelectedIndex`, automatic-play and tutorial status.

```json
{"Id":"040-room","Command":"debug-room"}
{"Id":"041-preview","Command":"ui-open","Ui":{"Screen":"gf","Demo":true}}
{"Id":"042-next","Command":"ui-input","Ui":{"Input":"Right"}}
{"Id":"043-pointer","Command":"ui-input","Ui":{"X":200,"Y":150,"Pressed":true}}
{"Id":"044-release","Command":"ui-input","Ui":{"X":200,"Y":150,"Pressed":false}}
{"Id":"045-close","Command":"ui-close"}
```

`Ui.Screen` defaults to `debug-room`; `Demo` defaults to true. Use `Demo:false` for live projection, subject to that screen's supported operations. `Ui.Input` accepts `Up`, `Down`, `Left`, `Right`, `Confirm`, `Cancel`, `PageLeft`, `PageRight`, `Alternate`, `Tab`, `PageUp`, `PageDown`. Pointer `X/Y` are physical viewport coordinates and must be supplied together. Include release events; held presses do not repeatedly confirm.

Canonical Config screen IDs are `config-graphics`, `config-frame-limits`, `config-audio`, `config-controls`, `config-interface`, `config-text`, `config-gameplay`, `config-combat`, `config-magic`, `config-exploration` and `mods`. Use the same page identities as player navigation:

```json
{"Id":"046-frame-limits","Command":"ui-open","Ui":{"Screen":"config-frame-limits","Demo":false}}
{"Id":"047-fonts","Command":"ui-open","Ui":{"Screen":"config-text","Demo":false}}
{"Id":"048-magic","Command":"ui-open","Ui":{"Screen":"config-magic","Demo":false}}
{"Id":"049-saving","Command":"ui-open","Ui":{"Screen":"config-exploration","Demo":false}}
```

Opening projects current values without applying changes. Player preferences are edited in Config; `debug-battle` contains service encounter execution and active battle actions. Gallery previews remain fixture-owned throughout nested navigation and cannot dispatch writes to live preference, mod or game owners. `IsDemo` describes projection mode; service callbacks still require their explicit live/fixture editing context.

`Ui.Screen:"model-browser"` opens the [model browser](../modding/model-mods.md). Its `ModelBrowser` status reports discovery progress, filtered count, the visible catalog page, selected model/variant, rotation, zoom and diagnostics. `ui-input` additionally accepts `Ui.Search` (substring or wildcard pattern) and zero-based `Ui.Variant`. Confirmation follows the same catalog → viewer → command menu flow as player input; export/import actions open native Windows dialogs on the game thread. `ui-close` closes the browser directly; `Cancel` unwinds one level.

A required naming prompt cannot be replaced/closed through screen commands. Native tutorials own their semantic selection and restrict input to their supported confirmation/cancellation boundary. See [UI framework](../developers/ui-framework.md) and [Debug Room](../players/debug-room.md).

## Battle commands

These commands are windowed. They return `{Battle, DebugMenuOpen, Definition}`; use `battle-status` for inspection. A live encounter and an isolated debug scenario have different ownership and completion rules.

| Command | `Battle` payload |
| --- | --- |
| `battle-start` | Required `Definition` (`BattleDefinition`), optional `Mode`; creates an isolated debug scenario with a valid installed stage/presentation. |
| `battle-input` | Required typed `Input`. |
| `battle-settings` | Optional `Mode`, `Policy` (`Full combat` or `Instant victory`), `RandomEncounters`; updates current settings through their owner. |
| `battle-debug-open`, `battle-debug-close` | Opens/closes the battle configuration UI. |
| `battle-debug-input` | Required `MenuInput` from the UI input enum. |
| `battle-close` | Closes only an isolated debug scenario; cannot discard a game-owned encounter. |
| `battle-status` | No payload required. |

`Battle.Input` has `Kind`, `Frames` (1..30000, default 1), `ActorId`, `TargetId`, `Mode` (`Normal` or `TurnBased`), `FullCombat`, `Ability`, `ActionId`, `DrawCast`, `RandomEncounters`, optional byte `NativeSeed`, and `GunbladeMode` (`Manual` or `Automatic`). `Ability` is `Attack`, `Magic`, `Draw`, `GuardianForce`, `Item`, `EnemyAbility`, `NativeCommand` or `LimitBreak`. Resolve actor/target IDs and available action IDs from the active battle, not from field actor IDs.

| Input `Kind` | Operation |
| --- | --- |
| `ConfigurePlaythrough` | Configures the game-owned combat policy using `FullCombat` and mode. |
| `ConfigureEncounters` | Configures game-owned random encounters. |
| `StartEncounter` | Starts the current game-owned pending encounter; optional `NativeSeed` is valid only here. |
| `Tick` | Advances active combat by `Frames`. |
| `Attack` | Queues an attack for explicit `ActorId` and `TargetId`. |
| `Action` | Queues explicit actor/target, ability and `ActionId`; `DrawCast` requests casting the drawn spell. |
| `AdvanceTurn` | Advances the active turn-based owner. Empty enemy turns automatically continue the same request until combat activity, player readiness or GF charge completion. Pending requests survive save/load. |
| `SetMode` | Sets ATB `Mode`. |
| `SetGunbladeMode` | Sets `GunbladeMode`. |
| `GunbladeTrigger` | Supplies the current gunblade trigger edge. |
| `ShotTrigger` | Supplies the active Shot process trigger for explicit `ActorId`. |
| `GuardianBoost` | Supplies the current GF boost input. |
| `GuardianFormActivate` | Requests transformation with `ActorId`, the same `TargetId`, and GF ID in `ActionId`; requires the save's GF Transformations option. |
| `GuardianFormAction` | Submits the active form's explicit actor/target action: `ActionId` 0 = Attack, 1 = Fire, 2 = Hell Fire. |
| `GuardianFormSkip` | Requests Skip for the current shortened finisher; repeated requests do not create another attack. |
| `InstantVictory`, `InstantDefeat` | Debug completion inputs; they are not ordinary combat results. |
| `Return` | Completes the game-owned encounter return through its outcome owner. |

`DrawCastTargetId` optionally selects the recipient for `Action` + `Ability:Draw` + `DrawCast:true`. `MagicTargets` optionally lists up to three target IDs for repeated `Action` + `Ability:Magic`; its first ID must match `TargetId`. All inputs remain subject to the active owner's validation. An isolated debug battle refuses game-owned configuration/start/return inputs.

```json
{"Id":"050-battle","Command":"battle-status"}
{"Id":"051-tick","Command":"battle-input","Battle":{"Input":{"Kind":"Tick","Frames":30}}}
{"Id":"052-attack","Command":"battle-input","Battle":{"Input":{"Kind":"Attack","ActorId":"party-0","TargetId":"enemy-0"}}}
```

For the full evolving definition/snapshot types, see [BattleDefinition](../../src/Memoria.FF8.Runtime/BattleDefinition.cs), [BattleInput](../../src/Memoria.FF8.Runtime/BattleInput.cs) and [PlayerActions](../../src/Memoria.FF8.Events/PlayerActions.cs). Transport implementation: [ControlQueue](../../src/Memoria.FF8.App/ControlQueue.cs), [windowed dispatch](../../src/Memoria.FF8.App/RecreateGame.cs) and [headless dispatch](../../src/Memoria.FF8.App/HeadlessControlHost.cs).
