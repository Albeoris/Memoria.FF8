# Startup artwork

A native Memoria package can put a PNG at `shell/square-logo.png` to replace the Square splash before the credits. The image is centered and fitted to the existing 640 × 400 panel, preserving its proportions and transparency. The startup timings, transitions and manual skip controls apply to the replacement.

On Remastered, a Demaster package can supply `layout_pc/logo/square-enix-logo.png` for the same splash. Memoria checks the original archived PNG first. If the package file matches that original, the usual startup artwork remains active. Other shell assets have separate resource contracts.

A regular native Memoria texture replacement targeting `main/square.lzs` takes precedence when it is present. Then Memoria tries the native shell path, followed by the Demaster path.
