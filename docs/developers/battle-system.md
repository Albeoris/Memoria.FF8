# Battle system

The battle engine owns interactive combat, native participant/AI state, actions, live snapshots and the result transaction returning to exploration. Support is bounded by the implemented resource and execution contracts. Reading a formation or displaying its models alone does not establish that its encounter is playable.

The current command surface includes Attack, Magic, Draw Stock/Cast, GF, items, native commands and Limit Break inputs. Individual abilities, resources and active processes are validated when used. Installed data supplies character/monster rules, combat statistics and supported action metadata. Unsupported AI, effects or presentation fail explicitly rather than being replaced by a generic attack.

## Owners and entry points

| Owner | Responsibility |
| --- | --- |
| `GameWorld` battle integration | Observes scripted/random encounters, constructs the live battle, advances shared owners and returns the completed result. |
| `BattleSession` | Participants, readiness, action scheduling, AI, statuses, animation/action state and combat callbacks. |
| `BattleDefinition` | Typed scenario/encounter inputs and resource-dependent rules. |
| `BattleSnapshot` | Complete supported current combat state for capture/restore. |
| `BattleEncounterOccurrence` and `BattleResult` | Identity and facts for one encounter's completion transaction. |
| `BattleResultProjection` | Pure calculation of the resulting character/inventory/card owners and visible rewards. |
| App battle HUD and presentation | User selection, result display, rendering and audio for the current battle. |

Source entry points are [BattleSession](../../src/Memoria.FF8.Runtime/BattleSession.cs), [BattleDefinition](../../src/Memoria.FF8.Runtime/BattleDefinition.cs) and [GameWorld battle integration](../../src/Memoria.FF8.App/GameWorld.Battle.cs).

## Live combat and settings

The Debug Room's Battle settings control combat policy, random encounters and ATB mode. Changes apply through the active game owner. Random encounter changes affect future encounters and preserve an already pending encounter. The full combat policy uses interactive encounters. The instant victory policy can complete a pending/active interactive battle and selects the automatic backend for future scripted battles. The automatic backend has an explicit AP-only reward policy; debug victory produces no combat rewards. These policies are distinct from winning a normal battle.

The Battle laboratory is an isolated scenario editor. Its party, opponents and stage configure a debug battle rather than replacing the running encounter's ownership. `battle-close` can discard an isolated scenario; a game-owned battle must complete through its result/return owner. See [Debug Room](../players/debug-room.md).

Normal ATB dispatches continuously. TurnBased automatically advances toward readiness while preserving a ready party member's decision. Ready enemies and queued actions can continue. Advance Turn waits for another participant without discarding that choice. Readiness depends on the actual active native participant state, including suspension, allocation and pending action state.

The HUD exposes equipped commands, available action lists and eligible targets. Input bindings are configurable; see [controls](../players/controls.md). Victory confirmation commits the result once and returns to the suspended field/world owner. Defeat does not manufacture a winning outcome or replay an encounter; recovery uses an existing [standalone save](../players/saves.md).

## Formation, allocation and AI

The formation reader preserves eight scene slot records and their coordinates, level data and enabled/hidden/untargetable/suspended flags. Scene slot identity is distinct from a live actor allocation. Hidden or suspended actors may await activation; their resource identity does not imply that they are currently selectable or loaded for drawing.

Reinforcement templates defer live level selection and random consumption until allocation. Removal releases the allocation. AI target selectors, comparison operands, variable banks and action ownership must preserve these distinctions. An active slot, a selectable target and a scene resource are not equivalent identities.

When extending an AI opcode or selector, implement its complete reached contract: operand widths, selector domain, comparison behavior, active participant ownership, random use, side effects and completion timing. Keep saved variable ownership explicit. Add capture/restore support for any new live state in the same change. Do not use content-specific default targets or replace an unsupported action with a primitive attack.

## Omega Weapon

Original formation 462 uses the shared encounter loader, native statistics and original AI. Its level-100 maximum HP of 1,161,000 is retained; native enemy templates are not constrained by the generic component HP limit. Installed-resource checks cover host construction, initialization, bounded AI action enqueue and independent JSON continuation. They deliberately stop before dispatching graphics effects; native logical action formulas already run during enqueue.

The original death AI publishes tutorial availability 127, Proof of Omega, before its queued death action. Accepted live battle snapshots and the current scene tutorial owner feed the corresponding achievement. Original-save initialization imports the full tutorial bitmap, and export writes the current owner. The field script's victory flag and automatic/debug victory do not manufacture this proof.

Remaining unsupported effects keep their existing explicit failures. `TODO(TD-019)` tracks complete Omega combat validation after those effects are implemented. Diagnostic AI callbacks and death-state fixtures do not establish a playable complete fight. Existing converted checkpoints whose tutorial owner omitted original availability require an explicit migration from their preserved original source before relying on that missing information; newly importing the original save establishes the correct owner.

## Results and rewards

A completed encounter produces a result associated with its exact occurrence and suspended owner. The result carries observed party HP/status/magic/item effects, enemy defeat receipts and supported progression/loot facts. Defeat receipts account for actors activated as reserves, revived or removed by death AI; rewards must follow those actual outcomes rather than the initial formation list alone.

The result projection computes candidate persistent owners without mutating the session. Victory UI displays a cached preview of that same projection, including character/GF growth, AP, items and cards where supported. Confirmation validates that the source session and occurrence still match, then publishes the result once. Reusing a receipt is rejected before another reward is applied.

Keep preview and commit on the same calculation path. A preview must not roll loot, award inventory or consume a receipt. Debug and automatic completion retain their explicit reward policies. A diagnostic command must not present debug victory as native combat evidence.

## Snapshots and presentation

`BattleSession.Capture` and `Restore` retain supported transient combat state, including participant and action owners. The root session also retains the encounter/result ownership and shared process state needed for continuation. Restoring a battle does not replay historical turns or reconstruct an encounter from New Game.

Windowed presentation and CPU previews share installed models, poses, weapons, materials and stage resources. Scene slot identity drives placement, hidden/inactive participants are omitted and reserve graphics are loaded at activation. Native camera/action presentation is supported only for implemented contracts; a successful simulation-only check does not establish rendering or audio parity. See [rendering](rendering.md).

The [control API](../reference/control-api.md#battle-commands) documents every battle command, typed input and windowed/headless distinction. The [command-line reference](../reference/command-line.md) covers `--battle-preview` and its scenario inputs. A diagnostic scenario definition is not a game save.

## Development checks

Use focused tests for changed contracts, then validate affected installed encounters through initialization, at least one meaningful turn and snapshot continuation. A formation decode alone is insufficient. AI changes need operand/selector/comparison coverage and the affected area's encounter tables, including hidden/suspended participants and reinforcement activation when reached.

Relevant public test projects include [battle tests](../../tests/Memoria.FF8.Battle.Tests/Memoria.FF8.Battle.Tests.csproj), [runtime tests](../../tests/Memoria.FF8.Runtime.Tests/Memoria.FF8.Runtime.Tests.csproj), [asset tests](../../tests/Memoria.FF8.Assets.Tests/Memoria.FF8.Assets.Tests.csproj) and [app tests](../../tests/Memoria.FF8.App.Tests/Memoria.FF8.App.Tests.csproj). See [building and testing](building-and-testing.md) for commands and resource configuration.

For lifecycle changes, cover terminal animation/return boundaries, TurnBased readiness and active allocation ownership. For result changes, compare preview with commit, validate persistent effects, confirm once and reject duplicate application. For state changes, preserve the original save and perform any necessary explicit migration before a minimal continuation check. Keep simulation, presentation and original-game equivalence claims separate in validation results.
