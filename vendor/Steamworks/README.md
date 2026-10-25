# Steamworks native redistributable

`redistributable_bin/win64/steam_api64.dll` is the unmodified Windows AMD64 redistributable from Valve Steamworks SDK 1.65 (23 July 2026). File version: `10.87.46.89`.

SHA-256: `E6D9BAFB9A41E42FBA7B21553DB49F8719027AF3F0DEB23FF86CFC44D60E776D`.

Copyright © 1996–2026 Valve Corporation. All rights reserved. This proprietary binary is separate from Memoria's source license; it is not MIT-licensed. Steamworks SDK use and redistribution are governed by Valve's [Steamworks SDK Access Agreement](https://partner.steamgames.com/documentation/sdk_access_agreement/).

Valve identifies `redistributable_bin` as the SDK's runtime redistributables in the [SDK documentation](https://partner.steamgames.com/doc/sdk). The [API overview](https://partner.steamgames.com/doc/sdk/api) instructs applications to ship the corresponding Steam API DLL with their runtime. Only this runtime DLL is included here; the SDK tools, examples, headers, import libraries and Steam client binaries are not included.

Memoria copies this binary and this notice to `Data/Steam/` in the distribution and resolves the DLL by its explicit runtime path. It does not replace files in an installed game. Updating the SDK requires updating the pinned hash, reviewing flat API signatures and callback layouts, and repeating initialization, identity and read checks for each supported AppID.
