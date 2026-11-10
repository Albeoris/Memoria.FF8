#!/usr/bin/env bash
set -euo pipefail
recipe=$1
output=$2
build=$3
repository=$4
revision=$5
version=$6
reference_hash=$7
dependencies=$8
rid=$9
reference_package_name=${10}
export LC_ALL=C
case "$rid" in
    win-x64)
        export MSYS2_ARG_CONV_EXCL='*'
        dumpbin=${11}
        configuration=(-G "${12}" -A x64 "-DCMAKE_GENERATOR_INSTANCE=${13}")
        library=mgruntime.dll
        build_library=Release/$library
        reference_rid=win-x64
        platform_tools=("$dumpbin")
        ;;
    osx-x64|osx-arm64)
        arch=x86_64
        [[ "$rid" != osx-arm64 ]] || arch=arm64
        configuration=("-DCMAKE_OSX_ARCHITECTURES=$arch" -DCMAKE_OSX_DEPLOYMENT_TARGET=26.0 "-DMOLTENVK_ROOT=$build/moltenvk")
        library=libmgruntime.dylib
        build_library=$library
        reference_rid=osx
        platform_tools=(nm otool lipo)
        ;;
    linux-x64)
        configuration=()
        library=libmgruntime.so
        build_library=$library
        reference_rid=linux-x64
        platform_tools=(nm readelf objdump strings)
        ;;
    *) exit 1 ;;
esac
for tool in git cmake dotnet curl unzip openssl "${platform_tools[@]}"; do
    command -v "$tool" >/dev/null || { echo "Required build tool is missing: $tool" >&2; exit 1; }
done
mkdir -p "$build" "$output"
source="$build/source"
if [[ ! -d "$source/.git" ]]; then
    git clone --filter=blob:none --no-checkout --sparse --config core.autocrlf=false "$repository" "$source"
    git -C "$source" sparse-checkout set --skip-checks native/monogame MonoGame.Framework/Platform/Graphics/Effect/Resources external/stb
    git -C "$source" checkout --detach "$revision"
fi
[[ $(git -C "$source" rev-parse HEAD) == "$revision" ]] || { echo 'Unexpected upstream source revision.' >&2; exit 1; }
while IFS=$'\t' read -r path dependency_repository dependency_revision; do
    directory="$source/$path"
    if [[ ! -e "$directory/.git" ]]; then
        git clone --filter=blob:none --no-checkout --config core.autocrlf=false "$dependency_repository" "$directory"
        git -C "$directory" checkout --detach "$dependency_revision"
    fi
    [[ $(git -C "$directory" rev-parse HEAD) == "$dependency_revision" ]] || { echo "Unexpected dependency revision: $path" >&2; exit 1; }
    git -C "$directory" diff --cached --exit-code
    if [[ "$rid" == osx-* && "$path" == native/monogame/external/faudio ]]; then
        faudio_patch="$build/faudio-alignment.patch"
        tr -d '\r' < "$recipe/FAudioCurveAlignment.patch" > "$faudio_patch"
        if git -C "$directory" diff --quiet; then
            git -C "$directory" apply "$faudio_patch"
        fi
        git -C "$directory" diff --binary --full-index > "$build/faudio-current.patch"
        cmp "$faudio_patch" "$build/faudio-current.patch"
    else
        git -C "$directory" diff --exit-code
    fi
done < "$dependencies"
patch="$build/readback.patch"
tr -d '\r' < "$recipe/BackBufferReadback.patch" > "$patch"
runtime_source='native/monogame/vulkan/MGG_Vulkan.cpp'
git -C "$source" diff --cached --exit-code
git -C "$source" diff --binary --full-index --ignore-submodules > "$build/current.patch"
if [[ -s "$build/current.patch" ]] && ! cmp -s "$patch" "$build/current.patch"; then
    # Undo only the previously verified patch in this build cache.
    [[ $(git -C "$source" diff --name-only --ignore-submodules) == "$runtime_source" ]]
    cmp "$build/applied.patch" "$build/current.patch"
    git -C "$source" apply --reverse "$build/current.patch"
fi
if git -C "$source" apply --check "$patch" 2>/dev/null; then
    git -C "$source" apply "$patch"
else
    git -C "$source" apply --reverse --check "$patch"
fi
# Require precisely the checked patch, including when a previous build populated the cache.
git -C "$source" diff --binary --full-index --ignore-submodules > "$build/current.patch"
cmp "$patch" "$build/current.patch"
mv "$build/current.patch" "$build/applied.patch"
cp "$recipe/CMakeLists.txt" "$source/CMakeLists.txt"
mkdir -p "$source/.config"
cp "$recipe/../../.config/dotnet-tools.json" "$source/.config/dotnet-tools.json"
(
    cd "$source"
    dotnet tool restore
    for shader in MonoGame.Framework/Platform/Graphics/Effect/Resources/*.fx; do
        name=$(basename "$shader" .fx)
        dotnet mgfxc "$shader" "native/monogame/vulkan/$name.vk.mgfxo.h" /Profile:Vulkan
    done
)
export SOURCE_DATE_EPOCH=$(git -C "$source" show -s --format=%ct HEAD)
cmake -S "$source" -B "$build/build" -DCMAKE_BUILD_TYPE=Release "${configuration[@]}"
cmake --build "$build/build" --config Release --target mgruntime --parallel
candidate="$build/build/$build_library"
reference_package="$build/reference.nupkg"
if [[ ! -f "$reference_package" ]]; then
    curl --fail --location --retry 3 "https://api.nuget.org/v3-flatcontainer/$reference_package_name/$version/$reference_package_name.$version.nupkg" --output "$reference_package"
fi
[[ $(openssl dgst -sha512 -binary "$reference_package" | openssl base64 -A) == "$reference_hash" ]] || { echo 'Reference NuGet SHA-512 mismatch.' >&2; exit 1; }
mkdir -p "$build/reference"
unzip -oq "$reference_package" -d "$build/reference"
reference="$build/reference/runtimes/$reference_rid/native/$library"
for name in candidate reference; do
    file=${!name}
    case "$rid" in
        win-x64)
            "$dumpbin" /exports "$file" | awk '/^[[:space:]]+[0-9]+[[:space:]]+[0-9A-F]+[[:space:]]+[0-9A-F]+[[:space:]]+/ { sub(/\r$/, "", $4); print $4 }'
            "$dumpbin" /dependents "$file" | awk '/^[[:space:]]+[^[:space:]]+\.[dD][lL][lL][[:space:]]*$/ { sub(/\r$/, "", $1); print tolower($1) }' | sort > "$build/$name-needed.txt"
            ;;
        osx-*)
            lipo "$file" -verify_arch "$arch"
            nm -arch "$arch" -gU "$file" | awk '{ sub(/^_/, "", $NF); print $NF }'
            otool -arch "$arch" -L "$file" | awk 'NR > 2 && /^[[:space:]]/ { print $1 }' | sort > "$build/$name-needed.txt"
            ;;
        *)
            nm -D --defined-only "$file" | awk '{ print $3 }'
            readelf -d "$file" | sed -n 's/.*Shared library: \[\(.*\)\]/\1/p' | sort > "$build/$name-needed.txt"
            ;;
    esac | grep -E '^(MG_|MGA_|MGG_|MGI_|MGM_|MGP_)' | sort -u > "$build/$name-exports.txt"
done
[[ -s "$build/candidate-exports.txt" ]]
[[ -s "$build/candidate-needed.txt" ]]
cmp "$build/reference-exports.txt" "$build/candidate-exports.txt"
: > "$output/native-abi-versions.txt"
if [[ "$rid" == linux-x64 ]]; then
    cmp "$build/reference-needed.txt" "$build/candidate-needed.txt"
    # Reject a toolchain that raises the imported C++ runtime or compiler ABI floor.
    for family in GLIBC GLIBCXX CXXABI GCC; do
        required_reference=$(objdump -T "$reference" | grep -o "${family}_[0-9.]*" | sort -Vu | tail -1)
        required_candidate=$(objdump -T "$candidate" | grep -o "${family}_[0-9.]*" | sort -Vu | tail -1)
        [[ $(printf '%s\n%s\n' "$required_reference" "$required_candidate" | sort -V | tail -1) == "$required_reference" ]] || { echo "Candidate requires a newer $family ABI than the reference runtime." >&2; exit 1; }
        printf '%s\n' "$required_candidate" >> "$output/native-abi-versions.txt"
    done
    for prefix in "$source" "$build/build" "$HOME"; do
        if strings "$candidate" | grep -F -- "$prefix" > /dev/null || strings -el "$candidate" | grep -F -- "$prefix" > /dev/null; then
            echo 'Native runtime contains a build-machine path.' >&2
            exit 1
        fi
    done
else
    # CMake may omit unused system imports. Keep the runtime self-contained.
    if [[ "$rid" == win-x64 ]]; then
        unexpected=$(comm -23 "$build/candidate-needed.txt" "$build/reference-needed.txt")
    else
        unexpected=$(grep -Ev '^(/System/Library/Frameworks/|/usr/lib/)' "$build/candidate-needed.txt" || true)
    fi
    [[ -z "$unexpected" ]] || { printf 'Unexpected native runtime dependencies:\n%s\n' "$unexpected" >&2; exit 1; }
fi
cp "$candidate" "$output/$library"
cp "$build/candidate-exports.txt" "$output/native-exports.txt"
cp "$build/candidate-needed.txt" "$output/native-needed.txt"
if [[ "$rid" == win-x64 ]]; then
    compiler_version=$("$dumpbin" /headers "$candidate" | sed -n '1p')
else
    compiler=$(sed -n 's/^CMAKE_CXX_COMPILER:FILEPATH=//p' "$build/build/CMakeCache.txt")
    compiler_version=$("$compiler" --version | sed -n '1p')
fi
printf '%s\n' "$(cmake --version | sed -n '1p')" "$compiler_version" "$(dotnet --version)" > "$output/native-toolchain.txt"
