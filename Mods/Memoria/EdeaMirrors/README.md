# Edea — Mirrors

Optional native Memoria encounter package for Classic, bound to `ff8.disc1.edea`. It ships with `enabled = 0`; saved user enablement survives bundled updates. Enable `Memoria/EdeaMirrors` in Mods before entering or restarting the Disc 1 Edea encounter. Enablement changes apply to the next encounter; an active battle keeps its captured definition. Later Edea encounters do not match this binding.

Edea summons two independently scheduled mirrors. The support mirror opens with Protect, Shell, Regen and Reflect on the Edea group. The assault mirror opens with Blind, Silence, Confuse and Slow on opponents. The original opens with the summon, Double, Scan and Haste. Later elemental choices use successful enemy-side Scan observations; the assault mirror additionally handles reflected opponents through native reflection or Dispel.

Positive damage resolved against the original is shared equally between living activated mirrors. Integer remainders are discarded. The original takes ordinary damage when no mirror remains. Copies have independent full HP inherited from the original's resolved maximum, fresh transient state and no duplicate boss rewards or story ownership.

The arrival uses clip `002`, zero-based source frames `8…28`, with linear opacity `0…1`. These correspond to native pose counters `9…29`; source-frame indices and native counters use different origins.

The original's ordinary taunts (native sequences `13/14`) wait until its fifth admitted main turn. Native terminal sequences `15/17` keep their story ownership and are not delayed by that gate. Native low-HP/party and defeat conditions can finish the encounter before later authored actions or taunts. Mirror defeat grants no duplicate boss rewards or story completion.

The behavior is authored in [gameplay/encounters/edea-disc1.json](gameplay/encounters/edea-disc1.json). See the public [encounter format](../../../docs/modding/encounters.md) for stable rule identities, guarded refinements, composition and diagnostics. No original game resources are included.

Windowed rendering and the complete real-story continuation remain pending acceptance; parsing and component tests alone do not establish that result.
