# MonoGame Linux Vulkan backbuffer readback

This narrow patch applies to MonoGame 3.8.5.1 at its exact NuGet repository commit, pinned together with all native dependency revisions in [native-runtime-lock.json](native-runtime-lock.json). It changes internal synchronization only; the managed framework, original exported MonoGame C API and shared-library dependencies remain unchanged. The recipe also adds the single [Unicode/IME text-input bridge](../MonoGame.Native.TextInput/README.md) export.

Upstream backbuffer readback waits on the binary swapchain-acquisition semaphore and signals rendering completion. The subsequent Present submits another wait on that already-consumed acquisition semaphore and another signal of completion. A screenshot can therefore succeed while the next presentation blocks indefinitely. The patch tracks whether acquisition still needs consumption, waits once across readback and presentation submissions, and signals rendering completion only in Present. Repeated reads of the same frame work with the same protocol.

Build on Linux x64 with PowerShell 7, the product's .NET SDK and tool manifest, Git, CMake 3.24+, a C/C++17 compiler, make, curl, unzip, OpenSSL and binutils. Ubuntu 24.04 setup:

```sh
sudo apt-get install build-essential cmake git curl unzip openssl binutils libsdl2-dev
```

SDL2 development packages provide platform build dependencies; the runtime links the separately pinned SDL2 and FAudio sources statically. Use a Linux toolchain whose glibc requirements do not exceed the locked reference package; the recipe checks this rather than assuming any newer distribution is compatible.

```powershell
./tools/Build-NativeVulkan.ps1 -RuntimeIdentifier linux-x64 -OutputDirectory ./artifacts/native-vulkan/linux-x64
```

From Windows, use a configured WSL distribution with those tools and the product's .NET SDK:

```powershell
./tools/Build-NativeVulkan.ps1 -RuntimeIdentifier linux-x64 -OutputDirectory ./artifacts/native-vulkan/linux-x64 -Distro Ubuntu-24.04
```

`-BuildDirectory` optionally selects the source/build cache; it defaults to a sibling of the output directory with `-build` appended. `-VerifyOnly` checks an existing output without discovering or invoking compilers or WSL. A valid receipt is also reused before tool discovery on an ordinary build.

Outputs are `libmgruntime.so`, `native-runtime.json` and `licenses/`. The receipt records locked source and recipe identities, output hash, exact compiler/CMake/.NET versions, Release/C++17 configuration, path mappings, exported C API fingerprint, required glibc and shared-library names. Source/build paths are mapped to neutral prefixes and the binary is scanned for source, build and user-home paths. `SOURCE_DATE_EPOCH` comes from the pinned source commit. These checks do not claim byte-for-byte reproducibility across toolchains.

[NativeVulkan.targets](NativeVulkan.targets) replaces only Linux x64 Vulkan output/publish runtimes, including the nested native-library location when present. The build properties are `MemoriaNativeVulkanPrepared`, `MemoriaNativeVulkanDirectory` and, on Windows, `MemoriaNativeVulkanDistro`. Verification remains mandatory for prepared outputs. Other runtime identifiers use their existing dependencies.

Validation includes a movie screenshot followed by further presentations and normal timed exit, and an independent API case with two pixel-checked backbuffer reads followed by Present on each of twelve frames. It establishes the synchronization repair, not general hardware acceptance.

The patch uses MonoGame's Ms-PL license. Retain the included [licenses](licenses) with distributions; they cover MonoGame, SDL2, FAudio, stb, minivorbis, minimp3, Vulkan-Headers, volk and Vulkan Memory Allocator. The pinned public source revisions plus the patch and recipe provide the complete source needed for this change. Generated upstream sources and build outputs remain outside the public source tree.

The portable regression harness requires a working display and Vulkan device, or Linux Xvfb with a compatible software Vulkan ICD. After preparing the native output, run:

```powershell
dotnet run --project ./vendor/MonoGame.Native.Vulkan/tests/ReadbackSmoke -p:MemoriaNativeVulkanPrepared=true
```

Run from the product root. For a custom output location, pass an absolute path with `-p:MemoriaNativeVulkanDirectory=...`. The harness checks both pixel reads and a completed Present for each frame, then exits normally; use an external timeout when comparing an unfixed runtime, since the defect blocks inside native presentation/readback.
