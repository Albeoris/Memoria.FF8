# Scene lighting

An installed Demaster package can supply `demaster.conf`, `field_time.txt`, `scene_light.txt` and field settings under `textures/field_bg/<prefix>/<field>/`. A native Memoria package can supply the same data in its `scene-lighting/` payload directory. The native payload takes precedence for a document supplied by both formats. Within a format, regular mod order selects the document.

## Clock

`DAY_NIGHT` enables the clock. `DAY_NIGHT_DAY` and `DAY_NIGHT_NIGHT` specify the day and night span in seconds, with defaults of 480 and 240. `DAY_NIGHT_FADE` defaults to 40 seconds and is capped by the shorter span. The fades are centered at the day and night boundaries. `DAY_NIGHT_EVENING` sets the fraction of the day where evening lighting starts and defaults to 0.9. `DAY_NIGHT_BATTLE` divides the clock rate in battle and defaults to 3. Saves preserve the clock phase. A save without this field uses its recorded play seconds plus half the fade duration as an approximate phase.

A field takes `time = cycle | day | night` and `sky = 0 | 1` from `<field>_pbr.txt`. A matching file named `<field>_<language>_pbr.txt` takes precedence. Fields default to cycle time and an enclosed sky. In `field_time.txt`, a `[rule <name>]` section can set:

- `field = name*, !excluded` for case-insensitive names and wildcard matching. Exclusions win inside a rule.
- `moment = first-last` for an inclusive range of story moments from 0 to 65535. A single number matches only that moment. Omit the key for the whole story.
- `time = cycle | day | night` for the field's time. The last applicable rule wins.

Fixed fields hold the clock, including during battles they initiate. World-map travel resumes the clock.

## Battle and world settings

`SCENE_LIGHT` enables battle and world lighting settings. The `[battle]` and `[world]` sections of `scene_light.txt` define their respective defaults. A later `[rule <name>]` overrides the values it lists when the optional `where = battle | world | both`, `stage`, and `moment` selectors match. A stage selector limits the rule to battles and accepts a comma-separated list of numbers and inclusive ranges. The moment selector is inclusive.

- `time = cycle | day | night` selects the clock or a fixed look.
- `night = auto | 0 | 1` controls when cycle time affects the scene. Automatic night applies on the world map, in battles started there, and in battles started in open fields. A battle from a fixed field uses the day look in cycle mode. An explicit scene time takes precedence.
- `day_tint` and `night_tint` take three space-separated linear RGB multipliers from 0 to 4. `night_level` from 0 to 1 scales the night tint.
- Material and direction data can specify `ao`, `sun`, `fighter_sun`, and `shadow` from 0 to 1, `glow`, `relief`, and `specular` from 0 to 4, `parallax` from 0 to 64, `sun_azimuth` within ±360 degrees, `sun_height` from 5 to 90 degrees, and boolean `smooth`.
- `light = x y z r g b range [always|night|day]` appends a point light in scene coordinates. A battle keeps the first four entries. The world map keeps up to 64.
