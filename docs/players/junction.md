# Junction

Junction shows the selected character's GFs, nine statistics, elemental and status assignments, commands and passive abilities together. The two percentage tables have independent **Attack / Defense** controls. Selecting an elemental or status destination sets its corresponding table to that direction; inspecting the other direction keeps the current edit.

The character's face appears beside their name. Portrait buttons and outward arrows at the upper right select the previous or next available character; a button disappears at its corresponding end of the character list. Shoulder navigation observes the same boundaries. Acquired GF portraits form one row, with up to sixteen portraits resized to fit. GFs attached to this character appear normally; unassigned GFs and those attached elsewhere are translucent. Every assigned GF shows its owner's face above it. Hovering or focusing a GF previews what attaching or removing it would change, with available command and passive abilities and their icons below. Selecting attaches or removes it immediately. Transferring another character's GF opens a centered confirmation with the donor's consequences.

The dashboard columns show commands/passives, stats, elemental/status bindings, then effect percentages. The stat table starts with its Stat, Magic, Stock and Value headings. Select a slot to open its editor beneath the dashboard. Hovering or focusing a candidate previews the entire move, including a vacated slot, lost protection, HP changes and any affected character. Increases are green and decreases red. Clicking or pressing Confirm assigns the candidate immediately; Back discards the preview. Clearing a magic slot retains its stock. Unassigned magic reads **---**; a gray lock marks unavailable positions. Shared UI navigation chooses the nearest control in that direction and stays within an open picker.

The magic picker pairs each spell name with its stock quantity in the same style. Each name column fits its longest entry. Names and quantities scroll together as complete columns; Right from the last visible column or Down from its bottom entry reveals the next column, and Left/Up returns through earlier columns. There are no magic page buttons, cast counts or search. Other pickers retain plain rows and paging.

Use the remappable **Card / detail** button for **Actions**, including Undo and these additional operations. Actions is the sole binding hint, at the lower right of the editor; there are no footer action buttons.

- **Optimize stats** offers Attack, Magic and Defense priorities. It changes only unlocked, unpinned stat magic. GFs, commands, passives and every elemental/status assignment remain configured; their spells are reserved. Inspect the tradeoffs and apply the proposal once.
- **Pin / Unpin stat** protects that destination from optimization, including an empty destination. It remains manually editable. Pins last for the loaded-game session.
- **Fill empty commands** proposes available distinct introductory commands while preserving existing choices. A GF unlocks commands but does not equip them automatically.
- The three clear operations state their scope: this character's magic, this character's junctions, or all characters' magic. Review their consequences before applying. Spell stock is retained.

An amber **! Empty** command or passive position means an additional legal unused choice is available; otherwise an empty unlocked position reads **---**. Empty elemental attack and optional passives are legitimate configurations. Leaving with actionable command gaps offers **Configure commands** or **Leave as configured**. An intentional override is remembered until that command configuration or its capabilities change.

**Undo** reverses the last confirmed assignment operation across all affected characters. It preserves stock, spent casts and current HP; it never heals an HP reduction caused by the edit. Relevant state changes make an old Undo unavailable. Loading a game starts a new editing session.

Elemental defense displays protection: **100%** nullifies an element and **150%** absorbs half its damage. Elemental attacks can heal enemies that absorb their element. Status attack percentages describe junction potency; actual success depends on the target. Stock and remaining casts are distinct: positive stock still contributes when casts are depleted.

Both Classic and Vector use the same editor and controls. At the normal 1280×720 layout, every supported assignment and both complete percentage tables remain visible while choosing candidates. Smaller windows and enlarged text use a single scrollable dashboard with a scroll indicator and a fixed editor.

Junction tutorials demonstrate this dashboard, portrait selection, Card actions and preview/confirm flow. They use isolated tutorial state and do not change the loaded party. Developers can find the supported earlier-snapshot adapter in [Junction tutorial snapshots](../developers/junction-tutorial-snapshots.md).
