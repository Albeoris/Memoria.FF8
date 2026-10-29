# Movement

This bundled Memoria mod adjusts movement projection in individual fields. The Galbadia Garden lobby (`ggkodo1`) rotates ordinary player input by -47.8125 degrees to align movement with the screen axes.

The correction applies to future player movement, including stick input and movement during field movies. Scripted actors, saved positions, native INF/AXIS values and world-map controls retain their existing contracts. No save migration is needed. The view and geometry are unchanged.

Enable or disable `Memoria/Movement` through the Mods menu. It uses the standard automatic enablement policy; `mod.ini` with `enabled = 0` disables the package. No installed game files are changed.

Each correction is a separate `fields/<field-name>.json` file. See `docs/modding/field-mods.md` for the format and package priority rules.
