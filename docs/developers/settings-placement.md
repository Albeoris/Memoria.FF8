# Settings placement and ownership

[Documentation](../README.md) / Developers

The canonical registries are [SettingsPlacement](../../src/Memoria.FF8.App/GameUi/SettingsPlacement.cs), [MenuSettings](../../src/Memoria.FF8.App/GameUi/MenuDemoData.cs) and [ControlsProfile](../../src/Memoria.FF8.App/ControlsProfile.cs). Placement adapts existing action keys; it does not introduce another persistence owner. Unsupported definitions need an explicit Fixture or Excluded disposition.

Global means the existing per-user preferences shared across saves. Game means current-session state retained by a normal game save. At Title, default means the existing new-game default; session means a choice held for the next New Game. Neither opening a page nor focusing a row writes a preference.

## Registry coverage

Player rows have one canonical Config editor; developer rows have one Debug editor. Display labels may be clearer than the legacy command key shown here.

| Legacy key | Old location | Kind | Audience and canonical home | Owner / scope |
| --- | --- | --- | --- | --- |
| Show FPS | Config / Display; Debug / UI | Scalar | Player · Graphics | UI preferences / Global |
| V-Sync | Config / Display | Scalar | Player · Graphics | UI preferences / Global |
| Anti-aliasing | Config / Graphics | Scalar | Player · Graphics | Graphics preferences / Global |
| Field FPS | Config / Display | Scalar | Player · Graphics / Frame rate limits | UI preferences / Global |
| World FPS | Config / Display | Scalar | Player · Graphics / Frame rate limits | UI preferences / Global |
| Battle FPS | Config / Display | Scalar | Player · Graphics / Frame rate limits | UI preferences / Global |
| Menu FPS | Config / Display | Scalar | Player · Graphics / Frame rate limits | UI preferences / Global |
| Sound | Hidden | Placeholder | Excluded · no live editor | Unsupported native placeholder |
| Controller | Hidden | Placeholder | Excluded · no live editor | Unsupported native mapping placeholder |
| Quick saves | Config / Interface | Scalar | Player · Gameplay / Exploration and saving | UI preferences plus player save store / Global |
| Cursor | Config / Interface | Scalar | Player · Interface | UI preferences / Global |
| ATB | Fixture Config / Gameplay | Placeholder | Fixture only | Unsupported native placeholder |
| Chocobo World | New Game; Config / Gameplay / Chocobo World | Enum and explicit reward actions | Player · Gameplay / Chocobo World | Gameplay preferences and acquisition ledger / Game, edition-aware Title session |
| Difficulty | Config / Gameplay; Debug / Battle; New Game | Scalar | Player · Gameplay / Combat | Gameplay preferences / Game, Title default |
| Battle ATB mode | Config / Gameplay; Debug / Battle; New Game | Scalar | Player · Gameplay / Combat | Gameplay preferences / Game, Title default |
| Shared magic stock | Config / Gameplay; New Game | Scalar | Player · Gameplay / Magic and Guardian Forces | Gameplay and magic owners / Game, Title session |
| Infinite magic | Config / Gameplay; New Game | Scalar | Player · Gameplay / Magic and Guardian Forces | Gameplay preferences / Game, Title session |
| Smart Attack | Config / Gameplay; New Game | Scalar | Player · Gameplay / Magic and Guardian Forces | Gameplay preferences / Game, Title session |
| GF Transformations | Config / Gameplay; New Game | Scalar | Player · Gameplay / Magic and Guardian Forces | Gameplay preferences / Game, Title default |
| Gunblade trigger | Config / Gameplay; Debug / Battle | Scalar | Player · Gameplay / Combat | Gameplay preferences / Game, Title default |
| Battle policy | Config / Gameplay; Debug / Battle | Scalar | Developer · Battle / Encounter execution | Encounter policy / Game, Title default |
| Rare card markers | Config / Interface; Debug / UI; New Game | Scalar | Player · Gameplay / Exploration and saving | UI preferences / Global, including New Game |
| Random encounters | Config / Gameplay; Debug / Battle | Scalar | Developer · Battle | Encounter owner / Game, Title default |
| Battle speed | Fixture Config / Gameplay | Placeholder | Fixture only | Unsupported native placeholder |
| Battle message | Fixture Config / Gameplay | Placeholder | Fixture only | Unsupported native placeholder |
| Field message | Config / Interface | Scalar | Player · Interface / Text and fonts | UI dialogue reveal preference / Global |
| Scan | Fixture Config / Gameplay | Placeholder | Fixture only | Unsupported native placeholder |
| Camera movement | Fixture Config / Gameplay | Placeholder | Fixture only | Unsupported native placeholder |
| Battle control | Fixture Config / Gameplay | Placeholder | Fixture only | Unsupported native placeholder |
| Vibration | Config / Controls and controller settings; Debug / Controls | Scalar | Player · Controls / Controller settings | Controls profile / Global |
| Music volume | Config / Audio; Debug / Audio | Scalar | Player · Audio | UI preferences / Global |
| Effect volume | Config / Audio; Debug / Audio | Scalar | Player · Audio | UI preferences / Global |
| UI presentation | Config / Interface; Debug / UI; F7 | Scalar | Player · Interface | UI preferences / Global |
| Text scale | Config / Interface; Debug / UI | Scalar | Player · Interface / Text and fonts | UI preferences / Global |
| UI font | Config / Interface; Debug / UI | Scalar | Player · Interface / Text and fonts | UI preferences / Global |
| UI font size | Config / Interface; Debug / UI | Scalar | Player · Interface / Text and fonts | UI preferences / Global |
| Dialogue font | Config / Interface; Debug / UI | Scalar | Player · Interface / Text and fonts | UI preferences / Global |
| Dialogue font size | Config / Interface; Debug / UI | Scalar | Player · Interface / Text and fonts | UI preferences / Global |

The 28 player settings, one developer setting, six fixture definitions and two excluded placeholders account for the complete registry. New Game retains its supported setup subset; Junction retains the per-character Attack mode, dependent on Smart Attack. Shared-stock review and deferred safe-boundary adoption stay with the existing magic owner. GF changes remain unavailable during active or pending battles.

Defaults and domains remain in their existing owners. Volumes expose every integer 0–100. Numeric adjustments clamp. Valid custom font sizes, volumes and retention limits survive browsing. Field-FPS load migration remains unchanged. Menu design-pixel size compounds with text scale, while dialogue uses field pixels.

Audit reconciliation: the current implementation already has separate `ClassicUiFontFamily` and `VectorUiFontFamily` overrides. Each explicit family, including Arial, is honored for its presentation. **Theme default — Georgia** (Vector) or **Theme default — Arial** (Classic) appears only when that presentation has no override and the legacy family is still the default Arial. A non-default legacy family remains the fallback when no presentation override exists. This preserves the existing stored fields rather than adding a replacement font model.

Quick-save editing stages a draft. Save previews the exact removal count and asks before deletion. The store validates, persists the accepted preference, adopts the runtime limit, then prunes. Failed persistence changes neither limit nor files; partial cleanup retains the accepted limit and reports removed/remaining files. Increasing a limit does not prune an outstanding backlog; the next quick save applies the adopted retention limit. Manual slots and the explicit checkpoint are protected.

## Binding and device coverage

Each action has distinct keyboard and controller values in the same controls profile. Both devices use the same owner partition and capture/conflict workflow.

| Group / canonical editor | Action IDs (both devices) |
| --- | --- |
| Config / Controls / Navigation | MoveUp, MoveDown, MoveLeft, MoveRight, UiTab, UiPageUp, UiPageDown |
| Config / Controls / Interaction | Confirm, Cancel, Menu, Card, Select, Pause |
| Config / Controls / Camera and battle | CameraLeft, CameraRight, RotateLeft, RotateRight, BattleAdvanceTurn |
| Config / Controls / Shortcuts | SpeedToggle, QuickSave, QuickLoad, SaveMenu, LoadMenu, BattleInstantVictory, BattleInstantDefeat |
| Debug Room / Shortcuts | DebugRoom, BattleDebug |

Previously, all 26 action bindings for both devices appeared in Config / Controls and Debug / Controls. Their kind is **binding**; capture, clear, restore and Keep/Replace are **commands**, with filtering and group selection local **navigation**. The five controller fields are **scalar** preferences previously reachable through controller device settings in both trees.

Instant victory and instant defeat are unbound player shortcuts; Debug Room and Battle Laboratory remain developer shortcuts. CameraLeft/Right retain their escape/camera roles; RotateRight retains gunblade/next-character behavior. Permanent Enter/Escape and F7 behavior remain outside editable profile fields.

Controller settings has exactly five preferences: vibration, button labels, left stick deadzone, right stick deadzone and run threshold. Their defaults remain On, Automatic, 20%, 20% and 50%. Vibration is the registry row above, not another preference.

Restore player bindings and Restore debug shortcuts affect only the selected owner and device. Restore controller settings affects only those five fields. Proposals retain whole-profile conflict validation; an explicitly accepted conflict replacement can remove an opposite-owner chord. Capture preserves cancellation, neutral/release detection, input draining, disconnection/focus-loss handling, chord limits and required controller Confirm/Cancel bindings.

## Mods and service tools

Config / Mods uses the existing dynamic package service. Search and status filters are local view state. Automatically enable new mods remains policy in the primary mods configuration; requested package enable state stays in each package configuration or archive sidecar. Refresh rescans through the same service. Requested enable state, effective resources, edition compatibility, reload-pending state and actionable diagnostics remain distinct. Explicit disables retain precedence. The optional main-menu shortcut opens this same canonical page.

Previously Mods was a main-menu page: its policy and package enablement are **scalar** preferences, Refresh is a **command**, and effective status/diagnostics are **read-only**. Its new Config placement keeps those owners and kinds.

The ordered Debug root is Models, Battle, Scene, UI tools, Session, Automation, Shortcuts. These families account for the static service controls; dynamic catalog entries are supplied by their existing owners.

Old tool locations were Debug Room / Model browser (catalog, viewer, exchange), Battle laboratory (scenario, participants, presets), Scene inspector, Names (list and editor), Menu gallery (including synthetic SeeD fixtures), and Automatic play (automation commands/status). They map respectively to Models, Battle / Laboratory, Scene, Session, UI tools and Automation. Old Battle outcome commands remain under Battle; the old UI, Audio and Controls preference pages are removed in favor of the canonical owners above. Opening tools, tabs, filters and catalogs is **navigation**; explicit import/export, scenario/preset actions, resets, renames, outcomes and automation operations are **commands**; statuses and details are **read-only**. Scenario fields are **scalar** laboratory values, and gallery values are **fixture** state.

| Tool family | Controls and effects |
| --- | --- |
| Models / catalog | Search (substring/wildcards), model selection, page navigation, original/package variant, View, Close; local selection and preview state. |
| Models / viewer and exchange | Previous/next model, yaw/pitch, zoom, variant, Commands, Back; explicit Blender GLB export/import, destination dialogs, validation/overwrite review and result status. Variant selection does not enable a mod. |
| Battle | Laboratory entry; Encounter execution (Full combat / Automatic victory); active Game/Laboratory battle status; explicit Win/Lose actions. Automatic victory retains its stored encoding/default and confirms an immediate active/pending encounter consequence. |
| Laboratory / Scenario | Scenario ATB mode, battle stage, party/enemy counts, Start and Return. Initialization reads player settings without editing them; launch uses the isolated laboratory owner. |
| Laboratory / Participants | Actor selection; identity/model/costume; maximum/current HP, Speed, Attack, Defense, initial ATB; weapon, equipment, abilities; Poison, Regen, Haste, Slow and enabled-status duration, interval and amount where applicable. |
| Laboratory / Presets | Slot 1–3, Save, Load, Current party + last enemy. Loading does not launch. Current party + last enemy restores the definition captured on entry without resetting scenario ATB or preset slot. |
| Scene | Layers/Objects/Events tabs, All/Enabled/Disabled filter, select/toggle, list/details paging, Reset all, Back; field/frame and native/render/event details. Overrides are temporary. Hiding actors does not remove collision; suspended events can leave scripts waiting. Reset does not clear saved API DebugView state. |
| UI tools / Menu gallery | Eligible fixture-screen selection, fixture navigation, Reset fixture data; synthetic SeeD Start/Yes/No/Restart and score. Fixture ownership prevents live settings, mod, save, naming or preset writes. |
| Session / Names | Supported Character/GF/Entity target list; keyboard, English, Symbols, Cyrillic, character pages, Space, Confirm, Delete, Default, Clear, Cancel. Draft until acceptance; save the current game to retain the rename. |
| Automation | Start a new game, Pause/Resume, Stop, Replay saved reproduction; status, error and reproduction details. Replacement of a live game requires confirmation; existing run/reproduction lifecycle remains the owner. |
| Shortcuts | Device selector, the three service action bindings above and scoped restore; shared profile and capture implementation. |

Startup dimensions, game/mod/save/media paths, native input bytes, renderer/audio diagnostics, CLI lifecycle controls, API-only scene overrides, launcher history and deck history remain outside these editors. No new screen/display/audio-device placeholders are implied by this mapping.
