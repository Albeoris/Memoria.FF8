# Third-party components

The source under `vendor/Albeoris.Frameworks` is derived from [Albeoris/Frameworks](https://github.com/Albeoris/Frameworks), revision `2474bed3e52f38c19920408cb6a4908afe170580`, copyright 2025 Albeoris, under the [MIT license](vendor/Albeoris.Frameworks/LICENSE). Its provenance and retained changes are recorded in that directory.

Runtime packages are restored from NuGet at the versions in `Directory.Packages.props`. Their package metadata retains the authors, source and license declarations:

| Component | Version | License and source |
| --- | --- | --- |
| BCnEncoder.NET | 2.3.0 | [MIT or Unlicense](https://github.com/Nominom/BCnEncoder.NET) |
| SharpCompress | 0.48.1 | [MIT](https://github.com/adamhathcock/sharpcompress) |
| MonoGame.Framework.WindowsDX | 3.8.5.1 | [Microsoft Public License](https://github.com/MonoGame/MonoGame) |
| SDL3-CS / SDL3-CS.Windows | 3.4.18 | [Package license](https://github.com/edwardgushchin/SDL3-CS/blob/main/LICENSE); native SDL uses the [zlib license](https://github.com/libsdl-org/SDL/blob/main/LICENSE.txt) |
| Magick.NET-Q8-AnyCPU | 14.16.0 | [Apache 2.0 and included native notices](https://github.com/dlemstra/Magick.NET) |
| ValveKeyValue | 0.11.0.378 | [MIT](https://github.com/ValveResourceFormat/ValveKeyValue) |
| System.Drawing.Common | 10.0.0 | [MIT](https://github.com/dotnet/winforms) |
| .NET runtime | SDK-selected .NET 10 servicing version | [MIT and third-party notices](https://github.com/dotnet/runtime) |

The distribution includes the .NET runtime's notices and NuGet license files and package metadata under `Data/licenses`. Transitive packages retain their own licenses; consult the included metadata for the actual restored dependency graph.

Original FINAL FANTASY VIII game content belongs to its respective owners. This project reads game files from a user's installation and does not include original archives, movies, music or saves. The title crest vector is a separately supplied design asset representing the original game branding; the source software license does not grant rights to Square Enix trademarks or original artwork.

The launcher can download FFmpeg and FFprobe from [BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds/releases/latest) separately into `Data/media-tools`; these binaries are not bundled in the source or release archive. The installer identifies its download source, verifies the archive SHA-256 and writes the distribution's license/source notices alongside the installed tools. See the selected FFmpeg distribution's notices for its LGPL terms and corresponding source.

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
