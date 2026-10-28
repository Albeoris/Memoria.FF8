#!/usr/bin/env bash
# macOS and Linux Native Build & Distribution Script
# Replaces the PowerShell build script completely.

set -e

CONFIGURATION="Debug"
SCOPE="Distribution"
RUNTIME_IDENTIFIER=""
TEST=false

# Parse command line arguments
while [[ "$#" -gt 0 ]]; do
    case $1 in
        --configuration|-c) CONFIGURATION="$2"; shift ;;
        --scope|-s) SCOPE="$2"; shift ;;
        --runtime|-r) RUNTIME_IDENTIFIER="$2"; shift ;;
        --test|-t) TEST=true ;;
        *) echo "Unknown parameter passed: $1"; exit 1 ;;
    esac
    shift
done

# Determine RuntimeIdentifier if not provided
if [ -z "$RUNTIME_IDENTIFIER" ]; then
    OS=$(uname -s)
    if [ "$OS" = "Linux" ]; then
        RUNTIME_IDENTIFIER="linux-x64"
    elif [ "$OS" = "Darwin" ]; then
        # hw.optional.arm64 also identifies Apple Silicon when this shell runs under Rosetta.
        if [ "$(sysctl -n hw.optional.arm64 2>/dev/null)" = "1" ]; then
            RUNTIME_IDENTIFIER="osx-arm64"
        else
            RUNTIME_IDENTIFIER="osx-x64"
        fi
    else
        echo "Unsupported desktop host: $OS"
        exit 1
    fi
fi

if [[ "$RUNTIME_IDENTIFIER" == osx-* ]] && [ "$SCOPE" != "Portable" ] && dotnet --info | grep -Eq 'Base Path:.*/(Cellar|opt)/dotnet/'; then
    # Homebrew's source-built SDK publishes its own runtime, which links Homebrew libraries and cannot ship in the bundle.
    echo "The active .NET SDK is Homebrew's source-built 'dotnet' formula. Self-contained macOS publishes would link /opt/homebrew libraries."
    echo "Install Microsoft's SDK instead (brew uninstall dotnet; brew install --cask dotnet-sdk, or https://dotnet.microsoft.com/download) and make it the first dotnet on PATH."
    exit 1
fi

invoke_dotnet() {
    dotnet "$@"
    local status=$?
    if [ $status -ne 0 ]; then
        echo "dotnet failed with exit code $status."
        exit $status
    fi
}

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [ "$SCOPE" != "Distribution" ]; then
    pushd "$ROOT_DIR" > /dev/null
    if [ "$SCOPE" = "Portable" ]; then
        invoke_dotnet build Memoria.FF8.Portable.slnx -c "$CONFIGURATION" -p:TargetFramework=net10.0 -m:1
        if [ "$TEST" = true ]; then
            for project in Memoria.FF8.Platform.Tests Memoria.FF8.Bootstrap.Tests Memoria.FF8.Audio.Tests Memoria.FF8.Presentation.Tests Memoria.Launcher.Tests Memoria.FF8.LinuxTesting.Tests; do
                invoke_dotnet test --project "tests/$project" -c "$CONFIGURATION" --no-progress
            done
            invoke_dotnet test --project "tests/Memoria.FF8.Runtime.Tests" -c "$CONFIGURATION" --no-progress --filter-class '*SteamAchievementBackendTests'
        fi
    else
        DESTINATION="$ROOT_DIR/artifacts/launcher/$RUNTIME_IDENTIFIER"
        invoke_dotnet publish src/Memoria.Launcher/Memoria.Launcher.csproj -c "$CONFIGURATION" -r "$RUNTIME_IDENTIFIER" --self-contained true -p:PublishTrimmed=false -o "$DESTINATION"
        if [ "$TEST" = true ]; then
            invoke_dotnet test --project "tests/Memoria.Launcher.Tests" -c "$CONFIGURATION" --no-progress
        fi
        echo "Launcher-only payload (game host supplied separately): $DESTINATION"
    fi
    popd > /dev/null
    exit 0
fi

ARTIFACTS="$ROOT_DIR/artifacts"
PACKAGE="$ARTIFACTS/package/$RUNTIME_IDENTIFIER"
# uuidgen is used to create a unique staging directory
STAGING="$ARTIFACTS/staging-$(uuidgen | tr -d '-')"
PAYLOAD="$STAGING"

if [[ "$RUNTIME_IDENTIFIER" == osx-* ]]; then
    PAYLOAD="$STAGING/Memoria.FF8.app/Contents/MacOS"
fi

DATA="$PAYLOAD/Data"
EXECUTABLE="Memoria.FF8"
LAUNCHER_NAME="Memoria.Launcher"

# We removed DirectX/Windows backends; only Vulkan remains for Linux and MacOS
BACKEND_NAME="Vulkan"
BACKEND_DIR="vulkan"

HOST_ASSETS=()
LAUNCHER_DIR="$STAGING/launcher"
MOD_DIRECTORIES=("Tonberry" "Demaster" "Junction")

mkdir -p "$DATA" "$LAUNCHER_DIR"
for directory in "${MOD_DIRECTORIES[@]}"; do
    mkdir -p "$PAYLOAD/Mods/$directory"
done

BUNDLED_MODS="$ROOT_DIR/Mods/Memoria"
if [ -d "$BUNDLED_MODS" ]; then
    find "$BUNDLED_MODS" -type f | while read -r file; do
        # Fixed macOS realpath incompatibility: use string manipulation instead
        relative="Data/BundledMods/Memoria/${file#$BUNDLED_MODS/}"
        destination="$PAYLOAD/$relative"
        mkdir -p "$(dirname "$destination")"
        cp "$file" "$destination"
    done
fi

pushd "$ROOT_DIR" > /dev/null

if [ -f "$ROOT_DIR/tools/Update-Shaders.sh" ]; then
    "$ROOT_DIR/tools/Update-Shaders.sh" --Check
fi

NATIVE_VULKAN="$ARTIFACTS/native-vulkan/linux-x64"
NATIVE_VULKAN_ARGUMENTS=()

if [ "$RUNTIME_IDENTIFIER" = "linux-x64" ]; then
    if [ -f "$ROOT_DIR/tools/Build-NativeVulkan.sh" ]; then
        "$ROOT_DIR/tools/Build-NativeVulkan.sh" --RuntimeIdentifier "$RUNTIME_IDENTIFIER" --OutputDirectory "$NATIVE_VULKAN" --Distro "${MemoriaNativeVulkanDistro:-}"
    fi
    NATIVE_VULKAN_ARGUMENTS=("-p:MemoriaNativeVulkanDirectory=$NATIVE_VULKAN" "-p:MemoriaNativeVulkanPrepared=true")
fi

invoke_dotnet build Memoria.FF8.Native.slnx -c "$CONFIGURATION" "-p:MemoriaTargetRuntime=$RUNTIME_IDENTIFIER" "-p:MemoriaGraphicsBackend=Vulkan" -p:TargetFramework=net10.0 -p:SkipMusicHost=true --artifacts-path "$ARTIFACTS/build/$RUNTIME_IDENTIFIER/graph" -m:1 "${NATIVE_VULKAN_ARGUMENTS[@]}"

PUBLISH_ARGUMENTS=("-c" "$CONFIGURATION" "-r" "$RUNTIME_IDENTIFIER" "--self-contained" "true" "-p:DebugType=None" "-p:DebugSymbols=false" "-p:PublishTrimmed=false")

HOST_OUTPUT="$DATA/Game/$BACKEND_DIR"
HOST_BUILD="$ARTIFACTS/build/$RUNTIME_IDENTIFIER/$BACKEND_DIR"
HOST_ASSETS+=("$HOST_BUILD/obj/Memoria.FF8.App/project.assets.json")

NATIVE_PUBLISH_ARGUMENTS=()
if [ "$RUNTIME_IDENTIFIER" = "linux-x64" ]; then
    NATIVE_PUBLISH_ARGUMENTS+=("${NATIVE_VULKAN_ARGUMENTS[@]}")
fi
if [ -n "$MEMORIA_FF8_PSF_DECODER_DIRECTORY" ]; then
    # Fixed macOS realpath incompatibility
    PSF_DECODER_ABS="$(cd "$MEMORIA_FF8_PSF_DECODER_DIRECTORY" && pwd)"
    NATIVE_PUBLISH_ARGUMENTS+=("-p:MemoriaPsfDecoderDirectory=$PSF_DECODER_ABS" "-p:MemoriaPsfDecoderPrepared=true")
fi

invoke_dotnet publish src/Memoria.FF8.App/Memoria.FF8.App.csproj -o "$HOST_OUTPUT" --artifacts-path "$HOST_BUILD" "-p:MemoriaGraphicsBackend=$BACKEND_NAME" -p:SkipMusicHost=true -p:PublishSingleFile=false -m:1 "${NATIVE_PUBLISH_ARGUMENTS[@]}" "${PUBLISH_ARGUMENTS[@]}"

if [ "$RUNTIME_IDENTIFIER" = "linux-x64" ]; then
    if [ -f "$ROOT_DIR/tools/Build-NativeVulkan.sh" ]; then
        "$ROOT_DIR/tools/Build-NativeVulkan.sh" --RuntimeIdentifier "$RUNTIME_IDENTIFIER" --OutputDirectory "$NATIVE_VULKAN" --VerifyOnly
    fi
    cp -f "$NATIVE_VULKAN/libmgruntime.so" "$NATIVE_VULKAN/native-runtime.json" "$HOST_OUTPUT" || true
    NATIVE_NOTICES="$DATA/licenses/MonoGame.Native.Vulkan"
    mkdir -p "$NATIVE_NOTICES"
    cp -rf "$NATIVE_VULKAN/licenses/"* "$NATIVE_NOTICES/" 2>/dev/null || true
fi

if [[ "$RUNTIME_IDENTIFIER" == osx-* ]]; then
    if [ -f "$ROOT_DIR/tools/Get-MoltenVK.sh" ]; then
        "$ROOT_DIR/tools/Get-MoltenVK.sh" --Destination "$HOST_OUTPUT"
    fi
fi

invoke_dotnet run --project tools/Memoria.FF8.Distribution.Check -c "$CONFIGURATION" -- "$HOST_OUTPUT" "$RUNTIME_IDENTIFIER"
invoke_dotnet run --project tools/Memoria.FF8.Distribution.Check -c "$CONFIGURATION" -- --graphics-payload "$HOST_OUTPUT" "$BACKEND_NAME" "$ROOT_DIR/tools/shader-manifest.json" "$RUNTIME_IDENTIFIER"

invoke_dotnet publish src/Memoria.FF8.Bootstrap/Memoria.FF8.Bootstrap.csproj -o "$DATA" --artifacts-path "$ARTIFACTS/build/$RUNTIME_IDENTIFIER/bootstrap" -p:PublishSingleFile=false "${PUBLISH_ARGUMENTS[@]}"

invoke_dotnet publish src/Memoria.FF8.Music/Memoria.FF8.Music.csproj -c "$CONFIGURATION" -r "$RUNTIME_IDENTIFIER" --self-contained true -o "$DATA/audio-host" --artifacts-path "$ARTIFACTS/build/$RUNTIME_IDENTIFIER/music" -p:DebugType=None -p:DebugSymbols=false -p:PublishSingleFile=false

invoke_dotnet publish src/Memoria.Launcher/Memoria.Launcher.csproj -o "$LAUNCHER_DIR" --artifacts-path "$ARTIFACTS/build/$RUNTIME_IDENTIFIER/launcher" -p:PublishSingleFile=false -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishDocumentationFile=false -p:PublishReferencesDocumentationFiles=false "${PUBLISH_ARGUMENTS[@]}"

invoke_dotnet run --project tools/Memoria.FF8.Distribution.Check -c "$CONFIGURATION" -- --native-image "$RUNTIME_IDENTIFIER" "$DATA/$EXECUTABLE"

if [ "$TEST" = true ]; then
    invoke_dotnet run --project tests/Memoria.FF8.Remastered.Tests -c "$CONFIGURATION"
    PREVIOUS_APP_PATH="${MEMORIA_FF8_APP_PATH:-}"
    export MEMORIA_FF8_APP_PATH="$DATA/Memoria.FF8"
    invoke_dotnet run --project tests/Memoria.Launcher.Tests -c "$CONFIGURATION"
    invoke_dotnet run --project tests/Memoria.FF8.App.Tests -c "$CONFIGURATION" -- --filter-class '*Remastered*' --filter-class '*FieldActorCaptureTests*' --filter-class '*OpeningPresentationTests*'
    
    if [ -z "$PREVIOUS_APP_PATH" ]; then
        unset MEMORIA_FF8_APP_PATH
    else
        export MEMORIA_FF8_APP_PATH="$PREVIOUS_APP_PATH"
    fi
    
    invoke_dotnet run --project tests/Memoria.FF8.Ui.Tests -c "$CONFIGURATION"
    invoke_dotnet run --project tests/Memoria.FF8.Assets.Tests -c "$CONFIGURATION" -- --filter-class '*FieldPoseInterpolationTests*' --filter-class '*FieldRenderInterpolationTests*'
    invoke_dotnet run --project tests/Memoria.FF8.Runtime.Tests -c "$CONFIGURATION"
fi

for relative in "$EXECUTABLE" "audio-host/Memoria.FF8.Music" "Game/$BACKEND_DIR/$EXECUTABLE" "Game/$BACKEND_DIR/Memoria.PsfDecoder"; do
    if [ ! -f "$DATA/$relative" ]; then
        echo "Incomplete distribution: Data/$relative is missing."
        exit 1
    fi
done

LAUNCHER_EXECUTABLE="$LAUNCHER_DIR/$LAUNCHER_NAME"
if [ ! -f "$LAUNCHER_EXECUTABLE" ]; then
    echo "The launcher publish did not produce $LAUNCHER_NAME."
    exit 1
fi

invoke_dotnet run --project tools/Memoria.FF8.Distribution.Check -c "$CONFIGURATION" -- --native-image "$RUNTIME_IDENTIFIER" "$LAUNCHER_EXECUTABLE"

mv "$LAUNCHER_DIR/"* "$PAYLOAD/"
# Fixed macOS realpath incompatibility: use native bash cd for full paths
LAUNCHER_FULLPATH=$(cd "$LAUNCHER_DIR" 2>/dev/null && pwd || echo "$LAUNCHER_DIR")
STAGING_FULLPATH=$(cd "$STAGING" && pwd)
if [[ "$LAUNCHER_FULLPATH" != "$STAGING_FULLPATH"* ]]; then
    echo "The launcher staging path is outside the generated staging directory."
    exit 1
fi
rm -rf "$LAUNCHER_DIR"

cp "$ROOT_DIR/LICENSE" "$ROOT_DIR/THIRD-PARTY.md" "$DATA/" 2>/dev/null || true
cp "$ROOT_DIR/tools/shader-manifest.json" "$DATA/shader-manifest.json" 2>/dev/null || true
cp "$ROOT_DIR/vendor/Albeoris.Frameworks/LICENSE" "$DATA/Albeoris.Frameworks.LICENSE" 2>/dev/null || true
cp "$ROOT_DIR/vendor/Topten.RichTextKit.Bidi/LICENSE.txt" "$DATA/Text.Unicode.LICENSE" 2>/dev/null || true
cp "$ROOT_DIR/vendor/Topten.RichTextKit.Bidi/UNICODE-LICENSE.txt" "$DATA/Text.Unicode.Data.LICENSE" 2>/dev/null || true
cp "$ROOT_DIR/vendor/Topten.RichTextKit.Bidi/UNICODE-TRIE-LICENSE.txt" "$DATA/Text.Unicode.Trie.LICENSE" 2>/dev/null || true

LICENSES="$DATA/licenses"
mkdir -p "$LICENSES"

for p in "$PAYLOAD/$LAUNCHER_NAME" "$DATA/$EXECUTABLE" "$DATA/audio-host/Memoria.FF8.Music" "$DATA/Game/$BACKEND_DIR/$EXECUTABLE" "$DATA/Game/$BACKEND_DIR/Memoria.PsfDecoder"; do
    if [ -f "$p" ]; then
        chmod 755 "$p"
    fi
done

if [[ "$RUNTIME_IDENTIFIER" == osx-* ]]; then
    PLIST="$STAGING/Memoria.FF8.app/Contents/Info.plist"
    cp "$ROOT_DIR/tools/packaging/Info.plist" "$PLIST" 2>/dev/null || true
    if [ "$RUNTIME_IDENTIFIER" = "osx-arm64" ] && [ -f "$PLIST" ]; then
        sed -i '' 's|<key>LSArchitecturePriority</key><array><string>x86_64</string></array>|<key>LSArchitecturePriority</key><array><string>arm64</string></array>|' "$PLIST"
    fi
fi

MANIFEST_ROOT="$PAYLOAD"
if [[ "$RUNTIME_IDENTIFIER" == osx-* ]]; then
    MANIFEST_ROOT="$STAGING/Memoria.FF8.app"
fi
if [ -f "$ROOT_DIR/tools/New-DistributionManifest.sh" ]; then
    "$ROOT_DIR/tools/New-DistributionManifest.sh" --PackageRoot "$MANIFEST_ROOT" --RuntimeIdentifier "$RUNTIME_IDENTIFIER"
fi

# Fixed macOS realpath incompatibility: avoid -m and use pwd directly since the parent is guaranteed absolute
RESOLVED_ARTIFACTS=$(cd "$ARTIFACTS/package" 2>/dev/null && pwd || echo "$ARTIFACTS/package")
# $PACKAGE is already built from absolute paths, so we can use string matching
RESOLVED_PACKAGE_DIR="$(dirname "$PACKAGE")"
if [[ "$RESOLVED_PACKAGE_DIR" != "$RESOLVED_ARTIFACTS" ]]; then
    echo "The package path is outside the artifacts directory."
    exit 1
fi

if [ -d "$PACKAGE" ]; then
    rm -rf "$PACKAGE"
fi
mkdir -p "$(dirname "$PACKAGE")"
mv "$STAGING" "$PACKAGE"

ARCHIVE="$ARTIFACTS/Memoria-FF8-$RUNTIME_IDENTIFIER.tar.gz"
invoke_dotnet run --project tools/Memoria.FF8.Distribution.Check -c "$CONFIGURATION" -- --create-tar "$PACKAGE" "$ARCHIVE"

if [ -f "$ROOT_DIR/tools/Test-Distribution.sh" ]; then
    "$ROOT_DIR/tools/Test-Distribution.sh" --ArchivePath "$ARCHIVE" --RuntimeIdentifier "$RUNTIME_IDENTIFIER" --Configuration "$CONFIGURATION"
fi

echo "Distribution: $PACKAGE"
echo "Archive: $ARCHIVE"
popd > /dev/null
