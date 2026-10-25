# Chocobo World integration

Choose a provider during New Game setup or through **Config > Gameplay > Chocobo World**. Classic assets default to Native Import; Remastered assets default to Angelo Search. The individual game save owns the choice and reward history. Memoria mode currently offers an explicit claim of the remaining exclusive items; its minigame is under development.

## Reward ownership

`ChocoboRewardCatalog` defines Ribbon (item 100, teaches ability 77), Mog's Amulet (65, teaches 38), Friendship (32, native Moomba battle item), and Chocobo's Tag (165, native naming item). The UI resolves installed item names, descriptions and icons. All four are once per save history across providers. Consuming an item or changing providers preserves its acquired flag; loading an older save restores the older history.

`ChocoboRewardTransactions` publishes inventory and acquisition history together after capacity validation. Native imports additionally publish PocketStation progression, menu RNG and source receipts in the same candidate state. A stale preview must be refreshed before committing. An import or claim that cannot fit leaves every owner unchanged. Ordinary items remain repeatable for new acquisitions.

The ledger migrates reliable evidence from current stock, learned MiniMog/Ribbon abilities, and retained item-taught history even after an ability was forgotten. A previously consumed item without a lasting trace cannot be reconstructed from an older save. Legacy duplicate stock is preserved. Native FFVIII save formats cannot retain this ledger or provider choice; saves requiring this metadata must use Memoria format.

## Native import

`NativeChocoboWorldReader` identifies containers from their contents:

| Format | Identification |
| --- | --- |
| PC compressed, including the verified Steam 2013 encoding | Four-byte compressed-size prefix and FFVIII LZSS; exactly 514 decoded bytes with `08FF` magic |
| PC uncompressed | Exactly 514 bytes with the same magic |
| PlayStation MCS | Seven-block application export with the Chocobo World directory identifier and SC header |
| Raw PocketStation application | Seven-block SC image with the native Chocobo World title |

The reader preserves the full decoded application and its unknown state bytes. Level, HP, Boko ID and class counters use packed decimal externally; the level's zero represents 100. PSX application banks use unsigned 32-bit save counters. The structurally valid bank with the larger counter is selected; a malformed newer bank falls back to a structurally valid older bank. The preview exposes both counters when present.

**Validation limits:** real PC/Steam encoding has been checked against native data. MCS/raw bank tests currently use synthetic images; a genuine external PSX fixture and any native integrity/checksum rule remain to be verified. Structural decimal/range checks cannot detect arbitrary corruption in unknown bytes or plausible changed values. Full memory-card images, emulator process states and unrecognized wrappers are unsupported. The decoder does not invent a checksum.

`GameAssets.ReadNativeChocoboWorldTransferTable` reads the licensed installed `menu/cyocobo.bin`. It contains 128 addressable rows with four A/B/C/D columns. Each class counter is a count of draws, never an inventory item ID. Each draw advances the existing menu RNG once and indexes the row with its low seven bits. Selection does not consume battle RNG. Exclusive-item row occupancy is:

| Item | Class A | Class B | Class C | Class D |
| --- | --- | --- | --- | --- |
| Ribbon | 10/128 | 1/128 | 0 | 0 |
| Mog's Amulet | 0 | 1/128 | 0 | 0 |
| Friendship | 10/128 | 5/128 | 0 | 0 |
| Chocobo's Tag | 0 | 2/128 | 3/128 | 0 |

These are table counts, not independent draw probabilities from a fixed seed. The native generator preserves seed parity and traverses a 64-row low-seven-bit cycle. Ribbon has 5/64 class A entries in either cycle, and 1/64 class B entries only in the even cycle. Mog's Amulet has 1/64 class B entries only in the even cycle. Friendship has 5/64 class A entries in either cycle and 3/64 versus 2/64 class B entries for even versus odd seeds. Tag has no even-cycle class B entry versus 2/64 in the odd cycle, and 2/64 versus 1/64 class C entries. Other catalog entries in those classes are absent. The native cold seed is odd; the separate MiniMog first-discovery award can still supply its amulet. The MiniMog discovery transition and native item substitution are handled separately. The implementation preserves this native RNG behavior.

Import receipts use native world identity and Boko ID, retain selected/backup revisions, and checkpoint consumed counters. Reopening unchanged data or editing unrelated container bytes does not replay loot. A later revision with larger counters grants only their delta. Decreases or changed counts without a later revision fail closed because they do not prove a new acquisition batch. A changed native world identity requires explicit association review when the playthrough is already bound; after review it has a separate checkpoint. Arbitrary same-world counter-reset cycles are not inferred. Source files remain read-only.

The transfer requires the native Boko unlock and a safe gameplay boundary. An unbound source can associate with the playthrough. Progression merges preserve higher level, maximum HP and attack tier while retaining native event state and unknown bytes. No unrelated original FFVIII save is required. A native source name has not been established in the external data; the preview must not invent one from unknown bytes.

## Angelo Search

Angelo mode selects the Remastered ordinary table on either asset edition. Its existing action prerequisites and timing remain in the battle owner. Category selection uses the native CRT stream with modulus 258, or 259 with Rare Item, and thresholds 128/160/176/192/200/256. Ordinary rows use the native nine-entry selector.

The rare row deliberately contains `[Friendship, Ribbon, Mog's Amulet, Chocobo's Tag]` repeated three times. Each occupies one quarter of its 32768 possible selector values. Stock Remastered contains only the first three rewards; adding Tag redistributes the existing rare-row share and does not create extra category rolls. There are 254/32768 rare category values normally and 378/32768 with Rare Item, including native modulo bias. Consecutive CRT draws are correlated; these counts do not assert independence between category and item draws.

An already acquired exclusive result is suppressed without rerolling or refunding RNG. Other modes retain Classic Angelo selection. Battle receipts and reward history travel with battle snapshots and the normal result commit.

## Focused validation

Run `NativeChocoboWorldReaderTests`, `ChocoboWorldTransferTests`, and `ChocoboRewardTests` using the repository's focused-test instructions. Configure optional resources with:

- `MEMORIA_FF8_GAME_DIR`: installed licensed game resources, for transfer-table validation on either asset edition.
- `MEMORIA_FF8_CHOCOBO_WORLD_SAVE`: native save selected for decoding validation.

Tests cover decimal fields, LZSS bounds, bank selection/fallback, unknown-byte preservation, all four unique rewards' native table frequencies and RNG parity cycles, duplicate imports, counter deltas, explicit association review, capacity rollback, progression merges and shared unique rewards. These tests do not require unrelated battle-effect tests.
