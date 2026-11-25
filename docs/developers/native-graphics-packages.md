# Native graphics packages

[Build and test](building-and-testing.md) · [Graphics host selection](graphics-backends.md)

A Release distribution restores a versioned native graphics package for each host that uses Memoria's patched MonoGame runtime. The four delivery targets are Windows x64, Linux x64, Intel macOS and Apple Silicon. A player receives the selected binaries, source provenance and notices inside the ordinary self-contained distribution.

## Package selection

| Game host | Runtime identifier | Managed framework | Native package |
| --- | --- | --- | --- |
| Direct3D 11 | win-x64 | MonoGame.Framework.WindowsDX | Official WindowsDX dependencies |
| Vulkan | win-x64 | MonoGame.Framework.Native | Vulkan / win-x64 |
| Direct3D 12 | win-x64 | MonoGame.Framework.Native | DX12 / win-x64 |
| Vulkan | linux-x64 | MonoGame.Framework.Native | Vulkan / linux-x64 |
| Vulkan | osx-x64 | MonoGame.Framework.Native | Vulkan / osx-x64 |
| Vulkan | osx-arm64 | MonoGame.Framework.Native | Vulkan / osx-arm64 |

The managed framework, shader compiler and reference runtime versions are pinned independently of the Memoria patch release. Vulkan and DX12 both load a file named `mgruntime.dll` on Windows. Each host resolves exactly one backend and keeps it in its own output directory. The macOS distribution still bundles the pinned MoltenVK dependency and signs the complete application on the target architecture.

The first [binary release](https://github.com/Albeoris/MonoGame/releases/tag/memoria-native-3.8.5.1-memoria.1) uses MonoGame 3.8.5.1 and fork revision `14cf3bc389916b07d1b3fef8cb034b708d4be033`. The Apple Silicon library in that release was built on a native ARM64 Xcode 27 runner with the same source and an explicitly verified macOS 26.0 minimum deployment target. [Apple Silicon build](https://github.com/Albeoris/MonoGame/actions/runs/45688495278)

## What a release build trusts

The [package lock](../../vendor/MonoGame.Native.Packages/native-runtime-packages.lock.json) selects an exact fork commit, version and archive digest for each backend/RID. The [maintained producer branch](https://github.com/Albeoris/MonoGame/tree/codex/memoria-native-packages) publishes one ZIP per row that uses a patched runtime. Each ZIP carries a single native library, its build receipt and its license notices. The release tag is `memoria-native-<version>` and the asset is `Memoria-MonoGame-<version>-<backend>-<rid>.zip`. A versioned release URL can be downloaded anonymously on a clean machine.

Restore checks the SHA-256 of the actual archive bytes, verifies its identity and contents, and stages the verified library for that host. A cached archive goes through the same checks. Failed or interrupted preparation must leave an already verified package usable. The final game distribution retains the published dependency-family, native ABI, shader and updater checks. The PSF codec has its own build prerequisites.

By default, archive copies live under `native-packages/<version>/` in the build's artifacts directory. Set `MEMORIA_NATIVE_PACKAGE_CACHE` to reuse a cache across builds or provide the locked archives on an offline build machine. Both the Release distribution and the standalone Release Portable graph restore the packages required by their hosts. The launcher-only scope needs no graphics package. Debug builds use the source recipe. Pass `-BuildNativeFromSource` to `Build.ps1` to select that recipe for a Release build.

## Updating the pin

1. Prepare and review the native changes in the fork. Record the MonoGame base commit, submodule revisions, native backend, shader compiler and platform toolchain. Publish a new patch version for changed inputs.
2. Build all five native packages on their target runners. Validate the exported API against the pinned official binary, the correct target architecture and the redistributable notices. Publish the full set and its digests.
3. In a Memoria.FF8 PR, update the producer commit and the archive SHA-256 for every supported backend/RID. Review changes to managed package versions or shader bytecode whenever the upstream version or shader compiler changes.
4. Restore from an empty cache and run the normal distribution checks on every affected platform. Confirm that a repeat restore accepts the same archive, the opposite backend is rejected and damaged bytes fail verification. Check the signed application bundle on macOS and the installed graphics host on Windows. The previous source commit and its release objects remain available for rebuilds and rollback.
