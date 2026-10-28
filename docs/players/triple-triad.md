# Triple Triad

Config → Triple Triad stores board dimensions, hand size, blocked cells, Random redraws, assistance and Elemental Same/Plus with the current game. At Title these settings apply to new games. Settings cannot change during a match.

Boards range from 2×2 to 5×5, with width and height independently editable. The default is 3×3 with five cards per hand. Each hand needs at least half the number of playable cells, rounded up; increasing the board raises the hand size when needed. You can deal up to 50 cards per hand, including more cards than fit on the board. Large hands use multiple columns. Unplayed cards still count toward their owner's score. A bounded pool determines which cards participate in the trade.

Hardcore uses at least 4×4 with at least two blocked cells. With exactly two blocks, the minimum hand is seven cards. Larger boards and hands remain available. Hardcore permits at most one Random redraw and only Off or Direct assistance.

## Blocked cells

Set the number of blocked cells from zero to one third of the full board area, rounded down. The default is zero; Hardcore requires at least two. Cells are chosen randomly once per challenge and shown as stone blocks. They cannot hold cards, do not count toward filling the board and act as A walls under Same Wall. Redraws and Sudden Death retain their positions. Without Same Wall, a block simply prevents placement and captures across that cell.

Rule assistance explains Same Wall at both the outer border and stone blocks with a gold connection and an A wall marker.

## Reward pool

Each side contributes at most half the full board area rounded up, without subtracting blocked cells, capped by its hand size. All cards played in the final round participate; the remaining places are filled randomly from that side's unplayed hand. The selected pool is saved and does not change on reload. After Sudden Death, selection uses the final round's source hands and retains original card-instance identities for settlement.

A 3×3 board has a pool of up to ten cards, five per side. With five-card hands, the entire hand participates exactly as before. With fifty-card hands, only five cards per side participate. One, Diff, All and Direct apply to this pool; cards outside it cannot be selected or transferred.

## Random redraws

The default allowance is zero. Configure up to three redraws and use the Redraw button or Page Up / Page Down before the first move. Only your hand changes; the opponent's cards, elemental cells and starting player stay fixed. Redraws are saved with the current match. If the opponent starts, confirm Begin match after deciding whether to redraw.

## Assistance

- **Off:** the normal presentation.
- **Direct:** a translucent card previews the selected empty cell. Blue arrows identify ordinary captures using elemental adjustments. Predicted enemy cards slowly pulse toward blue.
- **Rules:** also includes Same and Plus captures. Participating numbers shimmer; the explanation shows rank comparisons or matching sums from the selected calculation mode, including Same Wall.
- **Combo:** also displays subsequent capture waves, with the original Combo artwork on their arrows. Combo appears only after a rule trigger that permits it.
- **Hand:** also marks hand cards that can trigger Same or Plus in at least one currently empty cell.

Use the directional keys to choose a cell, or move the pointer over the board after choosing a card. Assistance never changes the match or opponent decisions.

## Elemental numbers

**Elemental Same/Plus** is Off by default. Off uses printed ranks for Same, Plus and Same Wall. On uses the elemental adjusted values of both cards; a Same Wall edge compares its adjusted rank with the wall's fixed A (ten). The opponent AI and every assistance mode use the same saved calculation mode. Ordinary and combo captures always use elemental adjustments.

With Elemental Same/Plus On, or when neither Same nor Plus is active, cards display their adjusted ranks: increases in green and decreases in red. A is ten, so A + 1 displays **B** (eleven), and 1 − 1 displays **0**.

With the option Off and Same or Plus active, cards keep their printed ranks and normal rank colors, with the ordinary +1/−1 label to show their elemental adjustment. This also applies to placement previews. Rule assistance still shimmers the ranks participating in a Same/Plus trigger.

The calculation mode is frozen when a challenge starts and survives saving, reloading, redraws and Sudden Death.

The scalable parchment board and its ornaments are drawn procedurally. Installed card artwork replacements continue to supply the card faces.
