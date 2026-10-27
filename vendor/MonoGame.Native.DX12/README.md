# MonoGame Native Direct3D 12 render-target MSAA

This is a narrow source patch to MonoGame 3.8.5.1 at the exact NuGet repository commit recorded in [native-runtime-lock.json](native-runtime-lock.json). The managed framework and compiler remain 3.8.5.1. The native exported ABI is unchanged.

Upstream `MGG_RenderTarget_Create` ignores its sample-count argument and creates single-sample offscreen color/depth resources. The managed target consequently reports multisampling without coverage samples. Upstream 3.8.6 retains this omission. The patch creates a multisampled drawing resource and a single-sample sampling/readback resource, sets matching depth samples, resolves using MonoGame's existing D3D12 command context and barriers, and frees both resources. Existing mipmap generation runs after resolution. Array/cube slices resolve into their corresponding first mip. There is no alternate scene renderer.

Build on Windows x64 with PowerShell 7, Git, CMake 3.24 or newer, the .NET SDK/tool manifest required by this product, and Visual Studio C++ desktop build tools plus the Windows SDK:

```powershell
./tools/Build-NativeDirect3D12.ps1 -OutputDirectory ./artifacts/native-dx12
```

A valid output receipt reuses the existing runtime before source discovery or compiler invocation. Missing or invalid outputs are repaired from the pinned recipe.

Visual Studio is discovered through `vswhere`; no fixed SDK installation path is required. The recipe fetches the pinned public source revisions, applies the checked patch, builds static SDL2 and the Release native runtime, and records source/patch/recipe/toolchain identities plus its output hash. Stock effect headers use the pinned compiler and relative source names. Compiler path mapping removes build-machine paths and the linker uses reproducible metadata; a binary check rejects remaining source/build prefixes. Players require neither these tools nor a separate C++ runtime installation. Generated source/build files stay in the ignored artifacts directory by default.

App and title-preview DX12 builds and publishes automatically run this same recipe. Build.ps1 may prepare it once and pass the explicit `MemoriaNativeDirect3D12Prepared=true` and `MemoriaNativeDirect3D12Directory` build properties; verification remains mandatory before copying. Only the isolated Windows DX12 payload receives this runtime. Keep the receipt and [licenses](licenses) with distributions; the upstream source revisions and this patch are the complete source recipe for this change. MonoGame uses Ms-PL; the patch uses the same license. SDL2, stb, D3D12 Memory Allocator, Microsoft DirectX helpers, minivorbis and minimp3 retain their included notices.

A bounded API reproduction and the shared renderer's synthetic contracts validate zero- and four-sample targets, including partially covered resolved pixels. This does not replace hardware, installed-resource or gameplay acceptance on other targets.
