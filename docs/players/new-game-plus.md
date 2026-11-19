# Completed Games and New Game+

[Documentation](../README.md) / Players

After the ending and final credits finish, choose **Save Completed Game** and a manual Memoria slot. Canceling the slot picker returns to the completion prompt. You can also return to the title without saving. A failed write keeps the completed progression available to try again.

Completed saves show **Completed Game** with the same animated text colors as a learned GF ability. Replacing one requires a separate **Cancel / Overwrite** confirmation, initially on Cancel. Quick-save cleanup preserves completed saves even if they were copied into quick-save history.

**New Game+** appears first on the title menu while a readable compatible completed save exists. Choose a source in the completed-save picker. Loading a completed save directly opens the same setup; loading an ordinary New Game+ save resumes its current progress.

## Configure a fresh playthrough

The first screen is the normal new-game settings screen, seeded from the source's gameplay settings. The cursor starts on **Configure New Game+**. You may change the other settings, including turning Hardcore off for the new run. Display, accessibility and input preferences stay with your installation.

The next screen offers these choices:

| Category | Starting choices |
| --- | --- |
| Character Levels | Stored or Native; Config edits individual levels and availability. |
| EXP Gain | 0.0–10.0× in steps of 0.1; affects character and GF EXP. |
| Guardian Forces | Stored or Native; Config edits ownership and each GF's abilities. |
| AP Gain | 0.0–10.0× in steps of 0.1. |
| Magic, Items, Cards | Stored or Empty; Config edits the collection and quantities. |
| Maximum Enemy Level | 1–300, default 100. |
| Maximum Character Level | 1–300, default 100. Applies to starting levels and later level gains. |
| Party Level Bonus | 0–200, default 0. Adds levels to the party average used for enemy level scaling. |

The currently bound card-game action opens Config from the selected category and opens ability details from a GF. Config is a contextual action, shown in the binding hint; it has no separate on-screen button. The on-screen hint follows keyboard/controller remapping. Lists scroll; Back preserves edits. Changing a preset replaces its category only after confirmation. Edited categories display **Custom**.

**Level for All Characters = 0** means **Individual / No Override**. Values from 1 through **Maximum Character Level** temporarily override individual levels. Lowering the cap also lowers any higher starting level for this run. Raising it again during setup restores the previous choices. Squall must remain available for the opening party. The five additional characters are independent choices; enabling one does not unlock all five. Checked guests remain selectable through later story departures. Regular characters still follow their story availability. **Allow Party Changes on the World Map** permits switching during interactive world-map control before the usual story unlock.

The level rules start at 100 for both caps and 0 for the bonus on every New Game+ setup, even if the source used different rules or the new run uses Hardcore. The chosen values are saved with the new run. The party level bonus affects scaled enemies while fixed-level enemies keep their encounter level subject to the enemy cap. The character level cap leaves Guardian Force levels unchanged.

Both reward sliders start at 1.0×, even when the source was another New Game+ run. Rewards are summed for each recipient, multiplied once, then rounded with halves upward: 3 EXP at 0.5× gives 2; 1 EXP at 0.4× gives 0. Zero disables that reward category while items, story events and battle completion continue normally.

GF ability confirmation toggles learned state. Resetting a learned ability removes its AP; untouched Stored entries retain partial AP. The card-game action provides Add Ability and Remove Ability. Removing an ability removes its effects. Editing an unavailable GF does not grant it; its configured definition remains available for later acquisition.

Magic follows the new run's selected stock mode. Shared stock is one pool. Merging personal stocks caps each visible spell at 100 and retains excess in the original donor banks; review the overflow details. Switching back preserves ownership instead of copying the pool to every character. Empty clears visible and retained stocks. Personal inventories enforce their normal capacity. Items and common cards allow 0–100; rare cards allow only absent/present. Zero removes stock.

Review the final carryover/rules summary, then choose **Begin New Game+**. Validation errors leave the setup available for correction. The completed source is reusable and stays unchanged. If setup began while loading from an existing game, canceling returns to that game.

The new run starts with a fresh story, normal opening party, currency, weapons, quest state, timers and random state. Selected inventory is starting stock, so later gifts and rewards work normally. Ordinary saves retain the new run's rules and current earned progress. They do not depend on keeping the completed source file. Original-game save formats cannot preserve these choices; use Memoria slots.

Resource loading for another edition does not establish playable support. The completion detector recognizes the supported original ending sequence; a replacement ending needs its own verified terminal contract.
