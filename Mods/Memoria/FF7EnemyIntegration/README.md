# FFVII Enemy Integration

This bundled Memoria package enriches original Final Fantasy VII encounters for parties travelling from Final Fantasy VIII. Its content belongs to FFVII and binds to native enemy IDs, independently of translated names, models, and formation placements.

The campaign preference is Automatic, Enabled, or Disabled. Automatic activates after entry from FFVIII; an ordinary FFVII campaign starts inactive. An explicit choice takes precedence. Changes apply at an encounter boundary, preserving the active encounter's captured profiles and outcomes.

Open the FFVII menu and choose **Mods** to change this campaign preference or inspect the installed packages. Package installation, package enablement and the campaign's encounter activation are separate states.

The package supplies 352 English Scan descriptions written as short pieces of world and creature prose. Names and combat measurements belong to their separate inspection fields; the prose does not repeat a resistance table. Related encounter variants may share a description. The descriptions are authored English content, without a localization layer.

For 292 enemy IDs, the package also supplies upward growth, tiered FFVIII Draw pools, native-unit FFVII reward multipliers, and selected consumable replacements. Other entries change only the Scan description: their scripted, multipart, or otherwise special roles receive no generic level or statistic promotion. Existing native actions, affinities, status rules, equipment, Materia, unique acquisitions, and Morph outcomes are not replaced by this package.

Growth uses the active encounter's captured party reference level. At or below an enemy's resolved baseline level, native statistics, rewards, and item channels remain unchanged. Draw tiers and item improvements use growth above that baseline. The package does not contain a second copy of the native enemy database.

Draw belongs to a genuinely Draw-capable FFVIII actor and its authoritative magic inventory. Installing this package does not give native FFVII characters a new stock or Junction system. A target without an authored Draw pool cannot supply spells. Scan descriptions and Draw capability do not assert that every original FFVII AI script or encounter is playable in the host.

The content is authored in [ff7/gameplay/enemy-extensions/enemies.json](ff7/gameplay/enemy-extensions/enemies.json). See [FFVII enemy extension documents](../../../docs/modding/ff7-enemy-extensions.md) for delta composition and the schema. No original game archives, models, textures, executable data, or native enemy record copies are included.
