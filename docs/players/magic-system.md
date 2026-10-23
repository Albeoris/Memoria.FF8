# Magic rules and Smart Attack

Choose these four independent options before **Begin** on the New Game screen, or under **Config → Gameplay → Magic and Guardian Forces** during a game. All four default to **Off**. Each save restores its own choices; loading a save does not replace its rules with installation defaults.

| Option | Behavior |
| --- | --- |
| Draw once per spell | Successful Draw Stock or Cast exhausts that spell on that enemy for the whole party during the encounter. |
| Shared magic stock | One accessible party bank, with exclusive Junction ownership for each spell. |
| Infinite magic | Ordinary casts preserve stock and spend a separate, limited allowance. |
| Smart Attack | Unlocks a personal choice of physical attacks or automatic magic in Junction Abilities. |

Any combination works. With Shared off, characters retain personal inventories and cannot borrow another character's stock or allowance. With Infinite off, ordinary spells consume stock normally. Smart Attack follows whichever storage and casting rules the save uses.

## Draw once per spell

**Draw once per spell** defaults to Off. When enabled, a successful Draw Stock or Draw Cast exhausts that spell on the source enemy for the whole party until the battle ends. Failed Draw attempts leave the spell available. Other spells and other enemies remain available. Smart Attack respects the same exhausted entries. A new encounter starts with fresh Draw availability; saving and loading during battle preserves exhaustion.

This option belongs to the save. Change it outside battles under **Config → Gameplay → Magic and Guardian Forces**; active and pending battles lock the option. Changing it at Title sets the default for future New Games. Enabled saves require Memoria format. The rule works independently of Shared magic stock and Infinite magic.

## Shared stock and Junction owners

Enabling Shared merges stock into the first physical character record, including while its actor is Laguna or it is outside the active party. A spell holds at most 100 units in the shared bank. Excess remains in its original record and is inaccessible while Shared is enabled; casting does not automatically draw from that excess. The shared inventory can retain more than 32 distinct spell types.

Each spell can have one Junction owner across all saved characters, including reserves. A bound spell may be cast only by its owner. An unbound spell is available to otherwise eligible characters. Field magic resolves the actual caster separately: the available owner casts a bound spell; an unbound shared spell uses the eligible active-party member with the highest Magic, breaking ties by party order. The menu names that caster.

Conservative **Auto Junction** preserves other characters' owners. **Junction → Off → Entire party magic** removes every magic binding from all characters, including reserve and unavailable characters, while retaining GF, commands, abilities and spell stock. Party removal and manual reassignment offer a preview of removed bindings and affected maximum HP before applying a change. Losing an HP Junction clamps current HP when necessary. Manual magic transfers are disabled while Shared is active.

Changing Shared mode displays quantities moved, overflow retained, removed Junctions and affected maximum HP. **Apply** commits the whole change; **Cancel** or Escape changes nothing. Disabling Shared leaves quantities in their current banks and removes bindings without local stock. It does not redistribute stock or restore earlier bindings. Extra spell types already stored in the first record remain usable after Shared is disabled.

Changes wait for a safe menu boundary. A change requested during battle remains pending through result settlement. If it changes Shared mode, the migration is reviewed against the resulting party after returning from battle. Loading a battle checkpoint resumes its saved rules and reservations.

## Stock and limited uses

Infinite magic preserves the stock used by Junction calculations. Remaining casts are displayed separately, such as `Cure ×100 · Field 2/4` or `Firaga ×100 · Battle 0/3`. Positive stock with zero remaining uses still provides its Junction bonus. Zero stock provides neither casting access nor a valid Junction.

The allowance for a bound spell depends on its owner's profile and stock:

| Stock | Male profile | Female profile |
| --- | --- | --- |
| 0 | 0 | 0 |
| 1–33 | 1 | 1 |
| 34–49 | 1 | 2 |
| 50–66 | 2 | 2 |
| 67–99 | 2 | 3 |
| 100 | 3 | 4 |

An unbound spell with positive stock has one use. In Shared mode this is one allowance for the spell across the party, rather than one per caster. Personal mode keeps independent inventories and histories. Female profiles are Quistis, Rinoa, Selphie and Edea; other supported profiles use the male limits.

Battle and field allowances are independent. A genuine new encounter starts its battle allowance. A completed victory restores one spent field use per relevant spell; escape, defeat and repeated settlement do not. Rest services do not refill field uses. Repeated victories can therefore restore field healing, an intentional balance consequence.

Spent uses persist through sorting, menu changes, Junction changes, transfers, dropping and reacquiring stock, saving and loading, and toggling the options. Reducing capacity does not erase usage history. Double and Triple spend one use for each cast that actually begins; canceled remaining casts release their reservations. Refinement ingredients, discards and transfers still consume or move real stock. Free or special-source effects retain their own rules.

## Choosing Smart Attack

Enable **Smart Attack**, then open **Junction → Ability** and select the first Attack slot. Choose **Physical only** or **Smart magic** for that character. The initial choice is Physical only. Disabling the global option makes attacks physical while preserving personal choices for later.

Smart magic requires an available Magic or Draw command and an eligible caster. It uses actual enemy HP, defenses, elemental behavior and immediate damage forecasts, including otherwise hidden information. It does not perform Scan or reveal those numerical values in the HUD. This hidden-information advantage and favorable automatic selection are deliberate gameplay changes. With Draw available, it also considers revealed spells from every living, targetable enemy; entries displayed as `????` are excluded.

For the highlighted enemy, selection follows these rules:

1. Keep the physical attack if it already has a kill probability strictly above 90%.
2. Prefer a revealed Draw/Cast spell that reliably kills, choosing the weakest sufficient action. The forecast includes failed draws and the normal Draw/Cast damage variation. A reliable kill means strictly above 90%; exactly 90% does not qualify.
3. Otherwise, group eligible stocked damage spells by element and prefer the group with the largest sum of remaining usable casts, then stock. For example, Fire 1 + Fira 1 + Firaga 2 contributes four available casts to the fire group. Within the first preferred group with a reliable kill, choose the weakest sufficient action. A reliable stocked spell takes precedence over an unreliable Draw/Cast.
4. If no eligible spell reliably kills, choose the eligible action with the highest expected immediate damage across Draw/Cast and stock. Use magic only when its expected damage is positive and strictly greater than the physical attack. Equal damage between spells favors Draw/Cast to preserve reserves; a tie with the physical attack retains the physical command. If none is usable, retain the physical command.

Double and Triple are included for stocked spells when available and affordable; Draw/Cast performs one cast. The target-time caption displays both the chosen spell and repetition count, for example `Firaga ×3`, or `Draw: Fire` for a drawn spell. The source enemy can differ from the selected damage target. Each cast follows the normal action and resource rules; Draw/Cast consumes no personal stock or limited-use allowance.

Mug takes priority whenever the target still has an available steal item. After that item is unavailable, normal Smart selection applies. Limit selections retain their own commands.

Smart candidates are supported, pure, single-target damage spells. Healing, status and protective magic, mixed damage/status spells, drain, percentage damage, group spells, random multi-hit spells, GF effects, items and Limits remain manual. Reflected or unsafe redirected magic is excluded. Immunity and absorption affect the forecast; nominal spell tier cannot make an ineffective spell a useful candidate.

## Target preview and physical override

The target panel keeps a selected-action caption while choosing an enemy. Keyboard, controller and mouse movement update it. Hovering selects a target without confirming or consuming battle randomness.

Hold the mapped **Camera left** control while choosing an Attack target to force the physical command; release it to recompute Smart magic. The HUD shows the current binding. The full two-button escape chord takes precedence over the override where retreat handling is supported. This option does not add a new retreat system.

Confirmation latches the displayed choice and reserves its resource for stocked spells. Two actors cannot reserve the same final use. Before execution, the engine rechecks the actor, confirmed target, ownership, status, reserved resource and any Draw source. An invalid automatic spell releases its reservation and falls back to a legal physical action or cancels; it does not silently upgrade to a different spell. Ordinary physical attacks retain gunblade timing, while automatic spells create no gunblade trigger window.

## Save compatibility

Use a **Memoria** save slot whenever these rules, personal Attack choices, extra inventory entries or initialized allowance histories must be retained. Saving such a game offers the Memoria bank and keeps original FF8 files intact. Quick saves also use Memoria format.

Original `.ff8` saves cannot represent the additional state, so exporting a feature-bearing save to that format is blocked. Turning the options off does not erase retained personal choices, extra entries or usage history, and does not make that export safe. Deliberately discarding those fields is outside the supported conversion workflow.

Old Memoria saves and original imports start with all features off, no extra stock and uninitialized allowance history. The first explicit activation initializes the history. Invalid or unsupported future save state produces a diagnostic while preserving the original file. See [Saves](saves.md) for save banks, loading and file locations.
