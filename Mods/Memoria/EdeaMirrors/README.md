# Edea — Mirrors

Native Memoria encounter package for Classic, bound to `ff8.disc1.edea`. It ships without an explicit enable setting and follows the Mods menu's global automatic enablement policy. Only an explicit player choice enables or disables this package independently; saved choices survive bundled updates. Enablement changes apply to the next encounter; an active battle keeps its captured definition. Later Edea encounters do not match this binding.

Edea summons two independently scheduled mirrors. The support mirror opens with Protect, Shell, Regen and Reflect on the Edea group. The assault mirror opens with Blind, Silence, Confuse and Slow on opponents. The original opens with the summon, Triple, Scan and Haste. Mirrors cast three times under Triple, twice under Double, or once without either status, using the stronger status on themselves or the living original. Casts prefer distinct eligible recipients; if there are fewer recipients than casts, they repeat within the same intended side or named ally group. Buffs do not spill onto opponents and attacks/debuffs do not spill onto allies. Scan prefers unobserved opponents, and buffs prefer recipients missing their status. Later elemental choices use successful enemy-side Scan observations; the assault mirror additionally handles reflected opponents through native reflection or Dispel.

The package removes the original's native Reflect resistance, and fresh mirrors inherit that resistance change. Reflect therefore applies through the ordinary status rules. Other resistances remain native. When all opponents reflect magic, the assault mirror can use an actually reflected ally for a native bounce; without an eligible reflector it selects Dispel.

Positive damage resolved against the original is shared equally between living activated mirrors. Integer remainders are discarded. The original takes ordinary damage when no mirror remains. Copies have independent full HP inherited from the original's resolved maximum, fresh transient state and no duplicate boss rewards or story ownership. Defeated copies disappear after their accepted contacts and effects finish.

If the party becomes unable to fight before the original's next turn, the encounter runs Edea's native story ending and continues to the movie. It preserves the party's HP and statuses rather than admitting more attacks or entering Game Over.

The arrival uses clip `002`, zero-based source frames `8…28`, with linear opacity `0…1`. These correspond to native pose counters `9…29`; source-frame indices and native counters use different origins.

The package raises the battle scene camera by 12 degrees through its `SetCameraProperty` operation. Edit `elevationDegrees` to adjust the view. Spell presentation pulls back when needed to include the caster and recipients. Native camera tracks, the initial caster close-up and Scan's individual target viewer are retained.

The original's ordinary taunts (native sequences `13/14`) wait until its fifth admitted main turn. Native terminal sequences `15/17` keep their story ownership and are not delayed by that gate. Native low-HP/party and defeat conditions can finish the encounter before later authored actions or taunts. Mirror defeat grants no duplicate boss rewards or story completion.

The behavior is authored in [gameplay/encounters/edea-disc1.json](gameplay/encounters/edea-disc1.json). See the public [encounter format](../../../docs/modding/encounters.md) for stable rule identities, guarded refinements, composition and diagnostics. No original game resources are included.

Installed Classic acceptance covers the real field entry, simultaneous CPU/GPU arrival, each actor's first six actions, linked damage and the native story terminal through its movie and following field load. This profile does not claim support for other editions.
