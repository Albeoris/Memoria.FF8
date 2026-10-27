# Backend shader artifacts

The three effects share readable HLSL sources. The pinned MonoGame compiler defines `VULKAN` for the Vulkan profile: that branch uses shader model 6 and `Texture2D.Sample`. Vulkan textures and samplers declare matching explicit `tN`/`sN` register slots: slot 0 for the scene/title texture, and slots 0/1 for current/previous frame comparison textures. This keeps the effect's reflected sampler bindings within the runtime texture collection. Unassigned registers can compile successfully while failing when the effect pass binds its textures. DirectX 11 retains shader model 4 and legacy `tex2D` sampling. The sampling coordinates, material calculations, techniques and effect parameter names remain shared.

From the product root on Windows, regenerate every supported variant:

```powershell
pwsh -File tools/Update-Shaders.ps1
```

This restores `dotnet-mgfxc` 3.8.5.1 from `.config/dotnet-tools.json`, compiles `Triangles.fx`, `FrameDifference.fx` and `TitleScene.fx` for `DirectX_11` and `Vulkan`, and records source hashes, output hashes, profiles and compiler version in [shader-manifest.json](../../tools/shader-manifest.json). Source hashes use UTF-8 without a BOM and normalized LF line endings so checkout conventions do not invalidate them; output hashes cover the exact binary bytes. Commit the sources, six artifacts and manifest together. Outputs contain no debug information or generation timestamps. Repeating generation with unchanged inputs produces the same artifact hashes.

Existing `.mgfxo` files belong to DirectX 11; `.vulkan.mgfxo` files belong to Vulkan. Each host embeds its own variant under the existing logical resource name. Runtime packages consume committed blobs and require no compiler or graphics SDK.

The generator always invokes the compiler from the product root with product-relative source paths. MGFX records the input path in its metadata, so this convention is required for reproducible artifacts and keeps checkout-specific absolute paths out of distributed blobs.

Run the following validation on any build host before packaging:

```powershell
pwsh -File tools/Update-Shaders.ps1 -Check
```

Validation checks the complete required inventory, the pinned compiler identity, source and artifact hashes, and the MGFX binary header's backend profile. It fails if a source or compiled payload changes without regeneration, or a payload from another backend is substituted. It does not require a graphics device or run gameplay tests. Compilation and hash validation do not establish visual equivalence; exercise each host's renderer separately.

`pwsh -File tools/Test-Shaders.ps1` exercises validation with isolated temporary fixtures, including changed sources, missing profiles, compiler drift, line ending changes and a DX11 payload substituted for Vulkan. On Windows after tool restore, add `-Compile` to check that all six artifacts reproduce byte-for-byte from a different source directory.

After publishing each Windows host, the distribution checker validates the actual shipped resources without loading MonoGame or creating a graphics device:

```powershell
dotnet <checker.dll> --graphics-payload <host-directory> <Direct3D11|Vulkan> tools/shader-manifest.json
```

This checks the host's `win-x64` dependency graph and pinned MonoGame package family, the actual framework's platform type metadata, required Native runtime ABI, shipped native images, and all three embedded effects against the selected profile and artifact hashes. Both profile entries must identify the same source hash. When checking against the source manifest, available source files are hashed as well; a manifest copied into the distribution remains usable without shipping HLSL sources. Hashes establish artifact correspondence, not authenticity of an independently supplied manifest.

`pwsh -File tools/Test-GraphicsPayload.ps1 -CheckerAssembly <checker.dll>` tests both packaged hosts and isolated negative fixtures for swapped frameworks, scene/title blobs, dependencies, native runtime, compiler versions and hashes. Host paths default to `artifacts/package/Data/Game/dx11` and `vulkan`; override them with `-Dx11Host` and `-VulkanHost` when validating an extracted package.

## Visual comparison

Compare analytic rendering cases separately from the title's procedural detail. The Windows DX11/Vulkan probes produced identical pixels for winding, depth, alpha/depth, blend modes, texture coordinates, retained targets and multisample resolve. Preserve those exact checks.

The title uses floating-point `frac` hashes and layered noise, which amplify small arithmetic differences between compiler profiles. At fixed times 0, 2.5 and 12 seconds, observed title differences were concentrated in local cloud/filament detail: composition, drawing bounds, light location, palette and the black lower region matched on visual inspection. At 160 × 120, maximum channel differences were 140–166, mean maximum-channel errors were 3.47–3.68/255, and about 21–23% of pixels exceeded two channel levels. These are recorded observations, not a general tolerance for other rendering paths or a guarantee across hardware.

A bounded experiment with precise hash intermediates reduced the discrepancy but did not establish pixel equivalence; extending precision qualifiers across title intermediates did not resolve it. Those experimental changes were discarded to preserve the established DX11 arithmetic, shared functions and noise seeds. Review the title's composition and temporal behavior alongside local detail differences; shader compilation alone does not verify either.
