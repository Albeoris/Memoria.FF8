# Third-party components

The source under `vendor/Albeoris.Frameworks` is derived from [Albeoris/Frameworks](https://github.com/Albeoris/Frameworks), revision `2474bed3e52f38c19920408cb6a4908afe170580`, copyright 2025 Albeoris, under the [MIT license](vendor/Albeoris.Frameworks/LICENSE). Its provenance and retained changes are recorded in that directory.

The Unicode bidirectional-text source under `vendor/Topten.RichTextKit.Bidi` is a narrow retained source closure from [Topten.RichTextKit 0.5.169](https://github.com/toptensoftware/RichTextKit), revision `4345e967`, under its [Apache 2.0 license](vendor/Topten.RichTextKit.Bidi/LICENSE.txt), with the retained [Unicode data notice](vendor/Topten.RichTextKit.Bidi/UNICODE-LICENSE.txt) and [unicode-trie MIT notice](vendor/Topten.RichTextKit.Bidi/UNICODE-TRIE-LICENSE.txt). Its retained changes and provenance are recorded in that directory. The distribution includes the `Data/Text.Unicode*.LICENSE` notices.

The shared reverb source under `src/Memoria.FF8.Audio/ThirdParty/MeltySynth` is derived from [MeltySynth](https://github.com/sinshu/meltysynth), revision `4ba079c9ee1453b03be7e164d254f48426f0aac3`, under its retained MIT license. Its Freeverb algorithm derives from Jezar's public-domain implementation. The source records the narrow adaptation and includes its license and provenance in published audio payloads.

Runtime packages are restored from NuGet at the versions in `Directory.Packages.props`. Their package metadata retains the authors, source and license declarations:

| Component | Version | License and source |
| --- | --- | --- |
| Avalonia / Avalonia.Desktop / Avalonia.Themes.Fluent | 11.3.9 | [MIT](https://github.com/AvaloniaUI/Avalonia) |
| Tmds.DBus.Protocol | 0.21.3 | [MIT](https://github.com/tmds/Tmds.DBus) |
| SkiaSharp / SkiaSharp.HarfBuzz / native assets | 3.119.4 | [MIT and retained native notices](https://github.com/mono/SkiaSharp) |
| HarfBuzzSharp | Transitive package-selected version | [MIT and retained HarfBuzz notices](https://github.com/mono/SkiaSharp) |
| BCnEncoder.NET | 2.3.0 | [MIT or Unlicense](https://github.com/Nominom/BCnEncoder.NET) |
| SharpCompress | 0.48.1 | [MIT](https://github.com/adamhathcock/sharpcompress) |
| MonoGame.Framework.WindowsDX | 3.8.5.1 | [Microsoft Public License](https://github.com/MonoGame/MonoGame) |
| MonoGame.Framework.Native / MonoGame.Runtime.Windows.Vulkan / MonoGame.Runtime.Windows.DX12 / MonoGame.Runtime.Linux.Vulkan / MonoGame.Runtime.Mac.Vulkan | 3.8.5.1 | [Microsoft Public License and included runtime notices](https://github.com/MonoGame/MonoGame) |
| SDL3-CS / SDL3-CS.Windows / SDL3-CS.Linux / SDL3-CS.macOS | 3.4.18 | [Package license](https://github.com/edwardgushchin/SDL3-CS/blob/main/LICENSE); native SDL uses the [zlib license](https://github.com/libsdl-org/SDL/blob/main/LICENSE.txt) |
| Magick.NET-Q8-AnyCPU | 14.16.0 | [Apache 2.0 and included native notices](https://github.com/dlemstra/Magick.NET) |
| ValveKeyValue | 0.11.0.378 | [MIT](https://github.com/ValveResourceFormat/ValveKeyValue) |
| DocumentFormat.OpenXml / DocumentFormat.OpenXml.Framework | 3.5.1 | [MIT](https://github.com/dotnet/Open-XML-SDK) |
| System.IO.Packaging | Transitive package-selected version | [MIT](https://github.com/dotnet/runtime) |
| .NET runtime | SDK-selected .NET 10 servicing version | [MIT and third-party notices](https://github.com/dotnet/runtime) |

The distribution includes the .NET runtime's notices and NuGet license files and package metadata under `Data/licenses`. Transitive packages retain their own licenses; consult the included metadata for the actual restored dependency graph.

Original FINAL FANTASY VIII game content belongs to its respective owners. This project reads game files from a user's installation and does not include original archives, movies, music or saves. The title crest vector is a separately supplied design asset representing the original game branding; the source software license does not grant rights to Square Enix trademarks or original artwork.

The launcher can download FFmpeg and FFprobe from [BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds/releases/latest) separately on Windows and Linux; these binaries are not bundled in the source or release archive. Windows retains `Data/media-tools`; Unix uses the user's media-tools directory. The installer identifies its download source, verifies the archive SHA-256 and writes the distribution's license/source notices alongside the installed tools. macOS requires an explicitly selected Intel x64 installation and its notices. See the selected FFmpeg distribution's notices for its LGPL terms and corresponding source.

### Steamworks API

The distribution includes `steam_api64.dll`, a proprietary
Steamworks SDK redistributable provided by Valve Corporation.

- Copyright: Valve Corporation
- Source: https://partner.steamgames.com/doc/sdk
- Terms: https://partner.steamgames.com/documentation/sdk_access_agreement/

The library is distributed under the Steamworks SDK Access
Agreement and is not covered by Memoria's MIT license.

Steamworks integration is optional. Memoria is not affiliated
with or endorsed by Valve Corporation.

The pinned version, hash and runtime placement are documented in
[the redistributable notice](vendor/Steamworks/README.md), also shipped as `Data/Steam/NOTICE.md`.

Intel macOS distributions include [MoltenVK 1.4.2](https://github.com/KhronosGroup/MoltenVK/releases/tag/v1.4.2), under Apache-2.0. tools/Get-MoltenVK.ps1 fetches the checksum-pinned official macOS archive. Its complete license and component notices ship beside libMoltenVK.dylib as MoltenVK.LICENSE. Upstream source and build instructions are available at the same version tag. No Apple SDK or proprietary game resource is redistributed.
