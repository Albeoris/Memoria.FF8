# GF transformations

[Documentation](../README.md) / Play

Enable **GF Transformations: On** under **Config → Gameplay → Magic and Guardian Forces** or in New Game options. It defaults to Off. During a playthrough the choice belongs to that save and can be changed between battles. Original-game saves load with it Off; enabled saves use Memoria's format.

Choose **Commands → GF**, highlight a living, junctioned Ifrit or Brothers, then choose **Right → Summon** to transform. Confirming directly on the GF performs its ordinary full summon. Left or Cancel returns to the GF list. The built-in Guardian Forms package supplies the available forms.

## Ifrit

After emerging and landing, Ifrit replaces the character's appearance. His own GF level determines his innate commands.

| Ifrit's level | Physical attack | Spells | Finisher |
| --- | --- | --- | --- |
| 1–14 | Attack | Fire | Hell Fire |
| 15–29 | Heavy Strike | Fire | Hell Fire |
| 30–69 | Heavy Strike | Fira | Hell Fire |
| 70–99 | Heavy Strike | Firaga | Hell Fire |
| 100 | Heavy Strike | Firaga, Flare, Fire Breath | Hell Fire |

Fire Breath is available once per transformation at level 100. It uses the original Blue Magic attack at its first power tier and hits all enemies with fire. Ifrit's innate spells can be used with empty magic stocks.

## Brothers

Sacred and Minotaur play rock-paper-scissors when the charge completes. Each brother has an equal chance to appear. Both share Brothers' GF level, learned abilities and HP. Sacred specializes in single-target damage. Minotaur can hit a whole group with Mower.

| GF level | Sacred | Minotaur |
| --- | --- | --- |
| 1–14 | Attack (power 16), Mend (power 5) | Attack (16), Mower (11) |
| 15–29 | Heavy Strike (20), Mend (5) | Attack (16), Mower (11), Mend (10) |
| 30–69 | Heavy Strike (20), automatic Mend | Attack (16), Mower (11), automatic Mend |
| 70–99 | Heavy Strike (24), automatic Mend | Attack (16), Mower (14), automatic Mend |
| 100 | Also learns Mad Cow Special | Also learns Mad Cow Special |

Mend restores the active GF's HP once per transformation while the brother is on the ground. At level 30 it triggers before an offensive action when the GF is wounded. Float prevents the heal. At level 100, Mad Cow Special spends one full ATB turn and can be used once per transformation. The other brother appears for this joint earth attack and leaves when it finishes. Brotherly Love is the finisher at every level. Brothers can also learn Defend through their ordinary 100 AP ability.

## Combat and return

The character supplies combat statistics such as Strength and Magic. The active boss body supplies elemental defenses and status resistances. Ifrit absorbs fire and takes double ice damage. Sacred nullifies earth and Minotaur absorbs it. Both take double wind and poison-element magic damage and resist the Poison status. The attack itself supplies its element and inflicted statuses.

On entry, each existing harmful status is checked against the new body's resistance. A status that fails this check is removed, including its timer. Beneficial effects carry over. On return, the character regains their own defenses and keeps any status changes that happened during the transformation.

Learned battle commands come from the summoned GF, including item-taught abilities. The menu hides Item, Draw, Magic, GF and MiniMog. Darkside uses the active body's regular physical strike, consumes GF HP and uses the ordinary Darkside damage multiplier. Finishing attacks use the same GF level, summon modifiers and Boost as their classic summons. Damage and healing during the active form use the GF's HP pool. If the GF falls, the character returns with their retained human HP. Ultimecia's anti-GF attack can defeat a transformed GF.

The form lasts 30 seconds of advancing battle simulation time. Existing battle pauses also pause its lifetime. Choosing the finisher ends the form early. Expiry schedules the finisher as soon as the form can finish its current action. If a status prevents the finisher, the character returns immediately. Returning consumes the turn and does not refill either HP pool.

The party can summon one GF at a time. A charge reserves the summon until it is interrupted, the classic summon finishes, or the form returns. Other characters can use their normal commands while the summon is occupied.

The form's Hell Fire omits the earlier emergence. Press **Escape battle (left)** (default **D / left trigger**) to skip ahead to the falling meteor finale, which plays at normal speed. This uses the existing remappable battle control. Skip preserves earned Boost and completes the attack and cleanup once. The same input also skips all sixteen ordinary junctionable GF summons to their own finales. The scene holds its last displayed frame while advancing to the finale, and controls remain responsive. Damage, contact counts and status effects still follow the original attack.
