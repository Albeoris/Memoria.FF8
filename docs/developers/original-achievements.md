# Original FF8 achievement register

[Achievement architecture](achievements.md) · [Original Steam catalogue](https://steamcommunity.com/stats/39150/achievements)

The original edition has 45 achievements. **42 condition handlers are implemented; three are deferred.** The authoritative machine-readable register is [OriginalAchievementCatalog](../../src/Memoria.FF8.Runtime/OriginalAchievementCatalog.cs). Each supported entry has a typed handler and a verified Steam key. A catalogue test requires an executable qualifying handler for every supported entry and excludes deferred entries from external delivery.

“Implemented” describes the achievement condition and its publication path. It does not assert that every encounter, effect or complete story route needed to reach that condition is playable. GF acquisition and learning achievements do not require their summon or limit-break animation to be implemented: they observe the accepted acquisition/learning owners. Missing or unknown owners never count as completed. Tests exercise the conditions and persistence contracts; they are not 42 complete gameplay playthroughs or real account unlock tests.

## Register

| # | Achievement | Status | Authoritative condition / publication |
| ---: | --- | --- | --- |
| 0 | Quezacotl | Implemented | Nonzero saved GF acquisition flag. |
| 1 | Shiva | Implemented | Nonzero saved GF acquisition flag. |
| 2 | Ifrit | Implemented | Nonzero saved GF acquisition flag. |
| 3 | Siren | Implemented | Nonzero saved GF acquisition flag. |
| 4 | Brothers | Implemented | Nonzero saved GF acquisition flag. |
| 5 | Diablos | Implemented | Acquired GF flag observed on accepted victory. |
| 6 | Carbuncle | Implemented | Nonzero saved GF acquisition flag. |
| 7 | Leviathan | Implemented | Nonzero saved GF acquisition flag. |
| 8 | Pandemona | Implemented | Nonzero saved GF acquisition flag. |
| 9 | Cerberus | Implemented | Nonzero saved GF acquisition flag. |
| 10 | Alexander | Implemented | Nonzero saved GF acquisition flag. |
| 11 | Doomtrain | Implemented | Nonzero saved GF acquisition flag. |
| 12 | Bahamut | Implemented | Nonzero saved GF acquisition flag. |
| 13 | Cactuar | Implemented | Nonzero saved GF acquisition flag. |
| 14 | Tonberry | Implemented | Nonzero saved GF acquisition flag. |
| 15 | Eden | Implemented | Nonzero saved GF acquisition flag. |
| 16 | Magician | Implemented | 100 executed enemy Stock Draw operations with positive native quantity, including full stock. |
| 17 | Top Rank | Implemented | Saved SeeD rank points at least 3100. |
| 18 | Card player | Implemented | Accepted entry into the Triple Triad module, including a subsequently cancelled match; accepted match settlement also qualifies. |
| 19 | Collector | Implemented | Exact common-card ownership bytes, rare-card player locations and complete rare-card acquisition history. |
| 20 | Handyman | Implemented | One of the six saved weapon IDs exceeds that character's initial weapon ID. |
| 21 | Maximum HP | Implemented | An active native playable battle row publishes derived maximum HP exactly 9999. |
| 22 | Maximum Gil | Implemented | Saved gil reaches 99,999,999. |
| 23 | Top level | Implemented | One of the eight saved character experience totals reaches 99,000. |
| 24 | End of Game | Implemented | Accepted request for the original ending movie, disc 3 clip 6. |
| 25 | Contrived Finish | Implemented | Same ending request with Squall's saved experience below 7000. Unknown experience cannot qualify. |
| 26 | Chocobo | Implemented | Known saved world capture flag. |
| 27 | Ragnarok | Implemented | Accepted request for original disc 2 clip 25. |
| 28 | First salary | Implemented | Actual accepted salary payment in field or world gameplay. |
| 29 | Choco loot | Deferred | Chocobo World item import is not implemented. Publish item-group receipts from its future accepted import. |
| 30 | Omega Destroyed | Deferred | The Omega-completion byte lacks a current canonical owner; required encounter content is not established. Implement its owner and accepted mutation before registering the rule. |
| 31 | Obel Lake Secret | Implemented | Both required known saved world quest bits are set. |
| 32 | UFO | Implemented | Saved PuPu completion flag observed on accepted victory. |
| 33 | Cards Club Master | Implemented | Accepted card transfer gains a card from the final CC opponent. |
| 34 | Loser | Implemented | Accepted card transfer loses a rare card. |
| 35 | Professional Player | Implemented | Absolute saved Triple Triad win count reaches 100. |
| 36 | Timber Maniacs | Implemented | Exact saved magazine masks; the supported English resource path uses the original English predicate. |
| 37 | Magazine Addict | Implemented | All 22 distinct magazine item IDs are currently held with positive quantity. |
| 38 | Magic Miner | Implemented | 100 qualifying accepted draw-point depletion writes, including the original masked-byte comparison. |
| 39 | Top Level Boko | Deferred | Chocobo World level import is not implemented; its raw level encoding must be established before conversion. |
| 40 | Blue Magic Master | Implemented | All 16 saved Blue Magic learning flags are set. |
| 41 | Dog Trainer | Implemented | All eight saved Angelo learning flags are set. |
| 42 | 100 kills | Implemented | Absolute sum of the 24 saved character/GF kill counters reaches 100 at accepted battle completion. |
| 43 | 1000 kills | Implemented | Same absolute sum reaches 1000. |
| 44 | 10000 kills | Implemented | Same absolute sum reaches 10000. |

## Delivery and extension

The original player host samples persistent owner predicates once per second and on flush, outside active encounter transactions. Battle completions, Stock Draw, draw-point writes, card transfers, movie requests and salary payments publish separate immutable facts at their accepted boundaries. Both event queues and their cursors survive checkpoint save/restore. Loading an older checkpoint retains already accepted pending delivery; missing legacy action ledgers do not reconstruct historical draws. Current persistent conditions can be observed after importing an existing save.

Loading a checkpoint starts a fresh branch for future action receipts. Genuinely executing another Draw after rewinding counts again, as an original account counter does; replaying a previously saved pending receipt keeps its old identity and does not count twice. This applies to both an in-process load and a fresh process loading the same checkpoint. Saved absolute counters keep their high-water semantics.

Stock Draw and draw-point progress are account-cumulative, with `mag_stck` and `mag_draw` respectively. Card wins use save high-water `won_card`. The 100-kill binding owns `nmy_kill`; the 1000/10000 bindings submit unlocks from the same local absolute sum without conflicting counter writers. Original completed outcomes retain the existing original-edition kill policy. Rejected candidates and automatic research victories cannot fabricate battle action receipts. Cast Draw and GF Draw do not count as Stock Draw.

Deferred entries retain their IDs and native keys, but have no handler or platform binding. `TODO(TD-018)` marks the future import and Omega extension points. Once the corresponding gameplay owner is implemented, add a typed accepted fact, the rule and boundary/persistence tests, then update both this register and the executable catalogue. Do not substitute a loaded raw-save byte, an inventory delta or a presentation log for a current gameplay owner.

This expansion applies to the original edition. Remastered retains its existing separately verified 100-kill rule; GOG remains an unimplemented provider extension point.
