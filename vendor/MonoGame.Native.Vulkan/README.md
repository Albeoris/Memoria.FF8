# MonoGame desktop Vulkan backbuffer fixes

The same patch builds for Windows x64, Linux x64, Intel macOS and Apple Silicon macOS. It applies to MonoGame 3.8.5.1 at the exact commit and dependency revisions in [native-runtime-lock.json](native-runtime-lock.json). The native build checks its exported MonoGame API against the matching NuGet runtime.

Upstream backbuffer readback waits on the binary swapchain-acquisition semaphore and signals rendering completion. The subsequent Present submits another wait on that already-consumed semaphore and another signal of completion. A screenshot can succeed while the next presentation blocks. The patch waits on acquisition once across readback and presentation submissions and signals rendering completion in Present.

Upstream also skips swapchain recreation when the backbuffer dimensions change with the same VSync and MSAA settings. The patch recreates on size changes and retains the requested dimensions separately from the extent that the surface currently allows. Presentation-driven recreation can then retry with the latest requested size if the compositor applies the resize asynchronously.

## Build

Run from the product root with PowerShell 7, Git, CMake 3.24+ and the product's .NET SDK. Windows uses Git for Windows Bash and Visual Studio 2022 or 2026 C++ desktop tools with a Windows SDK. macOS uses native Xcode tools on macOS 26 and the same pinned MoltenVK release used by packaging. For Linux x64, a native build or WSL2 distro needs a C/C++17 toolchain, make, curl, unzip, OpenSSL, binutils and SDL2 development packages.

```powershell
./tools/Build-NativeVulkan.ps1 -RuntimeIdentifier win-x64 -OutputDirectory ./artifacts/native-vulkan/win-x64
./tools/Build-NativeVulkan.ps1 -RuntimeIdentifier linux-x64 -OutputDirectory ./artifacts/native-vulkan/linux-x64
./tools/Build-NativeVulkan.ps1 -RuntimeIdentifier osx-arm64 -OutputDirectory ./artifacts/native-vulkan/osx-arm64
```

Use `osx-x64` for Intel macOS. From Windows, add `-Distro Ubuntu-24.04` to build the Linux runtime through an existing WSL2 distro. `-BuildDirectory` optionally selects the source/build cache and defaults to a sibling of the output directory with `-build` appended. A valid receipt is reused before tool discovery. `-VerifyOnly` requires a valid existing runtime and receipt.

The output contains the platform's native library, `native-runtime.json` and `licenses/`. The receipt records the locked recipe identity, library hash, compiler and API fingerprint. Windows checks system DLL imports against the reference runtime. Linux checks the imported ABI floors and shared-library names. macOS verifies the target slice and system-only shared dependencies. The build scans the library for build-machine paths. These checks do not claim bitwise reproducibility across toolchains.

[NativeVulkan.targets](NativeVulkan.targets) places the library in build and publish outputs for the matching Vulkan host. Build properties include `MemoriaNativeVulkanPrepared`, `MemoriaNativeVulkanDirectory` and `MemoriaNativeVulkanDistro`. Prepared outputs still require verification.

The patch uses MonoGame's Ms-PL license. Keep the included [licenses](licenses) with distributions. The pinned public source revisions, patch and recipe provide the source for this change. macOS packaging also includes the MoltenVK notice. Keep generated dependency sources and build outputs outside the public source tree.

## Resize and readback smoke

Run on a supported desktop with a working graphical session and Vulkan device. Linux can also use a compatible software ICD.

```powershell
dotnet build ./vendor/MonoGame.Native.Vulkan/tests/ReadbackSmoke -o ./artifacts/vulkan-smoke -p:MemoriaNativeVulkanPrepared=true
# Run this preparation command on macOS.
./tools/Get-MoltenVK.ps1 -Destination ./artifacts/vulkan-smoke
dotnet ./artifacts/vulkan-smoke/ReadbackSmoke.dll
dotnet ./artifacts/vulkan-smoke/ReadbackSmoke.dll --no-vsync
```

Pass `-p:MemoriaNativeVulkanDirectory=...` for a custom prepared-runtime location. The smoke changes the size during initialization, then grows and shrinks the window at a fixed VSync setting. It checks two pixel reads and a completed Present per frame. Apply an external timeout when comparing the upstream runtime because it can block inside native presentation.
