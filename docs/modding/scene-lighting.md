# Scene lighting

An installed Demaster package can supply `demaster.conf`, `field_time.txt`, `scene_light.txt` and field settings under `textures/field_bg/<prefix>/<field>/`. A native Memoria package can supply the same data in its `scene-lighting/` payload directory. The native payload takes precedence for a document supplied by both formats. Within a format, regular mod order selects the document.

## Clock

`DAY_NIGHT` enables the clock. `DAY_NIGHT_DAY` and `DAY_NIGHT_NIGHT` specify the day and night span in seconds, with defaults of 480 and 240. `DAY_NIGHT_FADE` defaults to 40 seconds and is capped by the shorter span. The fades are centered at the day and night boundaries. `DAY_NIGHT_EVENING` sets the fraction of the day where evening lighting starts and defaults to 0.9. `DAY_NIGHT_BATTLE` divides the clock rate in battle and defaults to 3. Saves preserve the clock phase. A save without this field uses its recorded play seconds plus half the fade duration as an approximate phase.

A field takes `time = cycle | day | night` and `sky = 0 | 1` from `<field>_pbr.txt`. A matching file named `<field>_<language>_pbr.txt` takes precedence. Fields default to cycle time and an enclosed sky. In `field_time.txt`, a `[rule <name>]` section can set:

- `field = name*, !excluded` for case-insensitive names and wildcard matching. Exclusions win inside a rule.
- `moment = first-last` for an inclusive range of story moments from 0 to 65535. A single number matches only that moment. Omit the key for the whole story.
- `time = cycle | day | night` for the field's time. The last applicable rule wins.

Fixed fields hold the clock, including during battles they initiate. World-map travel resumes the clock. `DAY_NIGHT_SAVES=1` (the default) gives each save its own phase. Set `DAY_NIGHT_SAVES=0` for one phase in Memoria’s user settings. That phase persists across save loads and normal shutdown. Existing Demaster clock files stay in their original installation.

## Battle and world settings

`SCENE_LIGHT` enables battle and world lighting settings. The `[battle]` and `[world]` sections of `scene_light.txt` define their respective defaults. A later `[rule <name>]` overrides the values it lists when the optional `where = battle | world | both`, `stage`, and `moment` selectors match. A stage selector limits the rule to battles and accepts a comma-separated list of numbers and inclusive ranges. The moment selector is inclusive.

- `time = cycle | day | night` selects the clock or a fixed look.
- `night = auto | 0 | 1` controls when cycle time affects the scene. Automatic night applies on the world map, in battles started there, and in battles started in open fields. A battle from a fixed field uses the day look in cycle mode. An explicit scene time takes precedence.
- `day_tint` and `night_tint` take three space-separated linear RGB multipliers from 0 to 4. `night_level` from 0 to 1 scales the night tint.
- Material and direction data can specify `ao`, `sun`, `fighter_sun`, and `shadow` from 0 to 1, `glow`, `relief`, and `specular` from 0 to 4, `parallax` from 0 to 64, `sun_azimuth` within Â±360 degrees, `sun_height` from 5 to 90 degrees, and boolean `smooth`.
- `light = x y z r g b range [always|night|day]` appends a point light in scene coordinates. A battle keeps the first four entries. The world map keeps up to 64.


## Color lookup tables

Place PNG strips in `lut/` for Demaster or `scene-lighting/lut/` for native packages. Supported phases are `dawn.png`, `day.png`, `dusk.png` and `night.png`. Prefix a phase with `lunar_` to supply its story variant. A place can override individual phases under `field/<name>/`, `battle/a0stgNNN/` or `world/`. A missing lunar phase uses the ordinary phase. A missing ordinary phase uses the original colors for that part of the blend.

A color lookup table consists of square slices arranged in one horizontal row or vertical column. Red increases to the right, green down, and blue selects the slice. Memoria accepts 2 to 65 samples per axis, including a 1024 × 32 strip for a 32-point table. Lookup and interpolation operate on sRGB colors. `LUT` enables use, `LUT_STRENGTH` controls the overall blend and `LUT_INSIDE` scales its strength in enclosed scenes. `LUT_LUNAR_FROM` and `LUT_LUNAR_TO` select the inclusive story range. A value of zero for `LUT_LUNAR_FROM` disables automatic lunar selection.

Grading applies to field backgrounds and actors, battle stages and actors, and the world view. Menus, text, videos and additive effects keep their own colors. Scene settings also supply a linear tint for battle stages and the world, and directional shading for world terrain and native battle geometry.

## Prepared field paintings

Painted PuPu and FME layers use their complete exported identity at `textures/field_bg/<prefix>/<field>/<field>_<8-hex-layer>.png` or `.dds`. Native packages use the same relative paths below `scene-lighting/field-paintings/` in place of `textures/field_bg/`. An optional language token goes before the identity. Memoria crops each selected painting to its matched native tile with the original depth, layer, animation and blend state. Opaque painted tiles use alpha-weighted filtering inside their own crop. A compatible same-package layer albedo and baked lightmap can supply the opaque day image.

Memoria currently publishes base paint and baked field lighting. Dynamic field normals, material and glow channels, scene material maps, reflection surfaces and volume probes require rendering support beyond these paths.
