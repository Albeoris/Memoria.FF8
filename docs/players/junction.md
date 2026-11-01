# Junction

Junction shows the selected character's GFs, nine statistics, elemental and status assignments, commands and passive abilities together. The two percentage tables have independent **Attack / Defense** controls. Selecting an elemental or status destination sets its corresponding table to that direction; inspecting the other direction keeps the current edit.

The character's face appears beside their name. Portrait buttons at the upper right select the previous or next available character; a button disappears at its corresponding end of the character list. Shoulder navigation observes the same boundaries. Acquired GF portraits form one row, with up to sixteen portraits resized to fit. GFs attached to this character appear normally; unassigned GFs and those attached elsewhere are translucent. Highlighting a GF attached elsewhere shows its owner's face above it. Select the GF portrait to review attaching, removing or transferring it in the editor.

Select a slot to open its editor beneath the dashboard. Choosing a candidate previews the entire move, including a vacated slot, lost protection, HP changes and any affected character. Mouse selection previews; the named action applies. Keyboard/controller Confirm applies the selected ordinary candidate. Longer or shared consequences open a review before the final confirmation. Cancel leaves gameplay unchanged. **None** clears a magic slot without deleting stock.

Use the remappable **Card / detail** button, or the visible **Actions** button, for additional operations:

- **Optimize stats** offers Attack, Magic and Defense priorities. It changes only unlocked, unpinned stat magic. GFs, commands, passives and every elemental/status assignment remain configured; their spells are reserved. Inspect the tradeoffs and apply the proposal once.
- **Pin / Unpin stat** protects that destination from optimization, including an empty destination. It remains manually editable. Pins last for the loaded-game session.
- **Fill empty commands** proposes available distinct introductory commands while preserving existing choices. A GF unlocks commands but does not equip them automatically.
- The three clear operations state their scope: this character's magic, this character's junctions, or all characters' magic. Review their consequences before applying. Spell stock is retained.

An amber command position means an additional legal command is available. Empty elemental attack and optional passives are legitimate configurations. Leaving with actionable command gaps offers **Configure commands** or **Leave as configured**. An intentional override is remembered until that command configuration or its capabilities change.

**Undo** reverses the last confirmed assignment operation across all affected characters. It preserves stock, spent casts and current HP; it never heals an HP reduction caused by the edit. Relevant state changes make an old Undo unavailable. Loading a game starts a new editing session.

Elemental defense displays protection: **100%** nullifies an element and **150%** absorbs half its damage. Elemental attacks can heal enemies that absorb their element. Status attack percentages describe junction potency; actual success depends on the target. Stock and remaining casts are distinct: positive stock still contributes when casts are depleted.

Both Classic and Vector use the same editor and controls. At the normal 1280×720 layout, every supported assignment and both complete percentage tables remain visible while choosing candidates. Smaller windows and enlarged text use a single scrollable dashboard with a scroll indicator and a fixed editor. Search and name/stock sorting help with long inventories.

Junction tutorials demonstrate this dashboard and its preview/apply flow. They use isolated tutorial state and do not change the loaded party. Developers can find the supported earlier-snapshot adapter in [Junction tutorial snapshots](../developers/junction-tutorial-snapshots.md).
