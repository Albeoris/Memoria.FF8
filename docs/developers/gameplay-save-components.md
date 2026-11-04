# Gameplay save component contracts

The opt-in component codec is separate from the existing execution snapshot and original FF8 save readers. It is an incremental building block for a gameplay save coordinator; it is not a complete playable save format and must not be routed into ordinary slots, quicksaves, transfers or update handoffs yet.

`GameplaySaveComponentDocument` declares its coverage as `characters,economy,limits`. Its capture API takes these three owners explicitly rather than accepting `GameState`. This prevents a partial projection from appearing to cover the entire game. No existing save reader, default writer, schema or extension dispatch changes to use this document.

## Ownership and absence

| Component | Saved authority | Reconstructed or excluded | Version 1 absence policy |
| --- | --- | --- | --- |
| Characters | Eight character records: identity, exact HP/status, junctions, experience, bonuses, equipment, magic stock, membership, kills/deaths and saved compatibility values; three combat-party slots, dream mode/weapons and junction backup. | Installed stat/menu definitions, initial-data and kernel fingerprints, model resources and allocation identities. Current installed definitions are supplied to `Restore`. | Every component property is required. Missing or malformed data fails; known progress never falls back to a new-game value. Nullable saved scalar values remain null. |
| GFs | Experience, flags, current HP, learned bank/AP/current learning, forgotten abilities, passive preference, accumulated semantic progression, kills/deaths and four opaque saved scalar values. | Maximum HP is derived from current installed definitions and saved experience without clamping the saved current HP. Native compatibility projection tables, custom profile definitions and pending recovery are not serialized. | All primary values are required. Nullable semantic progression and opaque saved values retain their represented absence. Unresolved recovery/profile/projection state rejects this opt-in projection. |
| Magic | Save-owned rules, attack modes, inventory extension, field-use history, committed-action and recovered-result receipts. | Active cast reservations, battle encounter identity and battle-use masks. Pending casts reject projection; this component is for a separately authorized nonbattle resume. | All properties are required, including explicit empty receipt arrays and false rules. No native/resource state is reachable through the component. |
| Economy | Complete item slots and optional item-order permutation; current/past gil and the separate current/past event-bank gil values. | Initialization rule/fingerprint comes from the installed initialized economy supplied to `Restore`. | All properties are required. Explicit null item order and zero gil are preserved; malformed permutations fail. |
| Limits | Blue Magic, Duel, Angelo learning, selected training, all eight counters and optional special-magic/Pet Pals/options values. | Installed initialization fingerprint comes from the `Restore` input. | Every property is required; explicit zero and null values are preserved. A future missing-owner initializer must run in the coordinator under an explicit older schema, not infer absence from a zero counter. |
| Resume | Versioned field or world logical location and field-entry identity. | VM instruction pointers/stacks, actor allocations, camera/animation phase, native addresses and host resource generations. | Exactly the matching position kind is required. Structural validation does not establish eligibility or reconstructability. |

The reused `NativeCharacterVital` and junction/stock scalar records currently contain only saved progression. A type-graph/property-ownership regression test makes adding installed resources or runtime state to these reused records an explicit review decision. Capturing and restoring detach mutable saved arrays; installed immutable definition objects remain the selected host's authority.

## Storage

`GameplaySaveCodec.Write` and `Read` operate on caller-owned streams and leave them open. The signature is `MFF8GPC1`, distinct from execution snapshot `MFF8SAVE`. The version 1 container is an eight-byte signature, four-byte container version, four-byte payload length, 32-byte SHA-256 checksum and UTF-8 JSON payload. Lengths are bounded to 4 MiB while writing and before allocating the read payload. Truncation, trailing bytes, checksum mismatch, unknown JSON members and unsupported format/version/coverage fail explicitly.

These component bytes contain no original save block, installed stat tables, source asset blobs, VM stacks, staging arenas or render objects. The codec does not claim atomic slot publication or a reconstructed gameplay load; those remain the future coordinator's responsibility. It must validate the full reconstructed candidate before publishing a player slot.

## Remaining coordinator work

Before enabling a complete gameplay writer, explicitly account for names and opaque original-save facts; story and local event facts; cards/rules/minigames; appearance; world flags, vehicles and transport state; timers and movement progress; shops/recipes/knowledge/statistics; gameplay preferences/mod state; and pending achievement/reward transactions. Custom GF profiles and compatibility recovery need a semantic adapter rather than table embedding or silent loss. Existing snapshots must retain their current continuation and reading behavior throughout this work.

A central host boundary evaluator and a registered semantic resume handler are both required. Player control by itself does not prove that a field VM can be discarded. No component method executes field initialization, replays script rewards or resets a running session. The later normal-load path must prove it preserves committed effects and all remaining gameplay owners before this component document can participate in a complete save.
