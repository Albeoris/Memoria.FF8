Highly Experimental source: https://github.com/myst6re/highly_experimental
Pinned commit: 8a045a8619116ce2b4c11387f527cc48636ef7a9
License: GPL version 3; see LICENSE.TXT. Original authors: Neill Corlett,
Christopher Snowhill (kode54), and contributors. Upstream provenance points
to https://git.lopez-snowhill.net/chris/highly_experimental.

This is a separately licensed standalone codec executable. No BIOS or game
resources are included. Its input is an externally supplied 512 KiB Highly
Experimental BIOS plus PS-X EXE overlays; its output is stereo PCM16 at
44100 Hz. Parent applications communicate through standard input/output.

Local additions: decoder.c and CMakeLists.txt (GPL-3.0-or-later). Upstream
source changes: normalize include separators to '/' for portable compilation;
recover the two pointer-shaped STATEOFS memory-map offsets through uintptr_t
instead of truncating to uint32. No emulation algorithm changes. Complete corresponding source and build
instructions are provided here; distribute this source with codec binaries.

Build using CMake 3.20+ and a C compiler:
  cmake -S . -B build -DCMAKE_BUILD_TYPE=Release
  cmake --build build --config Release
  cmake --install build --config Release --prefix output

Protocol (unsigned little endian u32): magic 0x31534650, BIOS byte count,
executable count, refresh Hz (50/60), BIOS bytes, then each executable as
byte count followed by bytes. Reply: ASCII PSF1. Each subsequent request is
frame count (1..4096); reply contains exactly count*4 PCM bytes. Zero or EOF
ends the process. BIOS/preboot/emulation failures terminate with nonzero
status. Hosts must impose startup and request deadlines and kill on cancel.

The PSF1 loader setup also follows OpenPSF's MIT-licensed integration:
https://github.com/myst6re/openpsf at
17911dffbe7ed462be5c34b542ce56a20104739d. Its notice is retained in
OPENPSF-LICENSE.txt. Decoder/parser glue has been adapted for in-memory
managed resource resolution and a bounded standalone process protocol.
