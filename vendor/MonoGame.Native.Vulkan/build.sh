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
for tool in git cmake dotnet curl unzip openssl nm readelf objdump sha256sum; do
    command -v "$tool" >/dev/null || { echo "Required build tool is missing: $tool" >&2; exit 1; }
done
mkdir -p "$build" "$output"
source="$build/source"
if [[ ! -d "$source/.git" ]]; then
    git clone --filter=blob:none --no-checkout --sparse "$repository" "$source"
    git -C "$source" sparse-checkout set --skip-checks native/monogame MonoGame.Framework/Platform/Graphics/Effect/Resources external/stb
    git -C "$source" checkout --detach "$revision"
fi
[[ $(git -C "$source" rev-parse HEAD) == "$revision" ]] || { echo 'Unexpected upstream source revision.' >&2; exit 1; }
while IFS=$'\t' read -r path dependency_repository dependency_revision; do
    directory="$source/$path"
    if [[ ! -e "$directory/.git" ]]; then
        git clone --filter=blob:none --no-checkout "$dependency_repository" "$directory"
        git -C "$directory" checkout --detach "$dependency_revision"
    fi
    [[ $(git -C "$directory" rev-parse HEAD) == "$dependency_revision" ]] || { echo "Unexpected dependency revision: $path" >&2; exit 1; }
    git -C "$directory" diff --exit-code
    git -C "$directory" diff --cached --exit-code
done < "$dependencies"
patch="$build/readback.patch"
tr -d '\r' < "$recipe/BackBufferReadback.patch" > "$patch"
if git -C "$source" apply --check "$patch" 2>/dev/null; then
    git -C "$source" apply "$patch"
else
    git -C "$source" apply --reverse --check "$patch"
fi
[[ $(git -C "$source" diff --name-only --ignore-submodules) == 'native/monogame/vulkan/MGG_Vulkan.cpp' ]] || { echo 'Unexpected modified upstream sources.' >&2; exit 1; }
git -C "$source" diff --cached --exit-code
# Require precisely the checked patch, including when a previous build populated the cache.
git -C "$source" diff --binary --full-index -- native/monogame/vulkan/MGG_Vulkan.cpp > "$build/applied.patch"
cmp "$patch" "$build/applied.patch"
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
cmake -S "$source" -B "$build/build" -DCMAKE_BUILD_TYPE=Release
cmake --build "$build/build" --parallel
candidate="$build/build/libmgruntime.so"
reference_package="$build/reference.nupkg"
if [[ ! -f "$reference_package" ]]; then
    curl --fail --location --retry 3 "https://api.nuget.org/v3-flatcontainer/monogame.runtime.linux.vulkan/$version/monogame.runtime.linux.vulkan.$version.nupkg" --output "$reference_package"
fi
[[ $(openssl dgst -sha512 -binary "$reference_package" | openssl base64 -A) == "$reference_hash" ]] || { echo 'Reference NuGet SHA-512 mismatch.' >&2; exit 1; }
mkdir -p "$build/reference"
unzip -oq "$reference_package" -d "$build/reference"
reference="$build/reference/runtimes/linux-x64/native/libmgruntime.so"
for name in candidate reference; do
    file=${!name}
    nm -D --defined-only "$file" | awk '{ print $3 }' | grep -E '^(MG_|MGA_|MGG_|MGI_|MGM_|MGP_)' | sort -u > "$build/$name-exports.txt"
    readelf -d "$file" | sed -n 's/.*Shared library: \[\(.*\)\]/\1/p' | sort > "$build/$name-needed.txt"
    objdump -T "$file" | grep -o 'GLIBC_[0-9.]*' | sort -Vu | tail -1 > "$build/$name-glibc.txt"
done
[[ -s "$build/candidate-exports.txt" ]]
cmp "$build/reference-exports.txt" "$build/candidate-exports.txt"
cmp "$build/reference-needed.txt" "$build/candidate-needed.txt"
reference_glibc=$(cat "$build/reference-glibc.txt")
candidate_glibc=$(cat "$build/candidate-glibc.txt")
[[ $(printf '%s\n%s\n' "$reference_glibc" "$candidate_glibc" | sort -V | tail -1) == "$reference_glibc" ]] || { echo 'Candidate requires a newer glibc than the reference runtime.' >&2; exit 1; }
# Reject a toolchain that raises the imported C++ runtime or compiler ABI floor.
: > "$output/native-abi-versions.txt"
for family in GLIBC GLIBCXX CXXABI GCC; do
    required_reference=$(objdump -T "$reference" | grep -o "${family}_[0-9.]*" | sort -Vu | tail -1)
    required_candidate=$(objdump -T "$candidate" | grep -o "${family}_[0-9.]*" | sort -Vu | tail -1)
    [[ $(printf '%s\n%s\n' "$required_reference" "$required_candidate" | sort -V | tail -1) == "$required_reference" ]] || { echo "Candidate requires a newer $family ABI than the reference runtime." >&2; exit 1; }
    printf '%s\n' "$required_candidate" >> "$output/native-abi-versions.txt"
done
# Scan the actual binary for UTF-8/ASCII and UTF-16 build-machine prefixes.
for prefix in "$source" "$build/build" "$HOME"; do
    if strings "$candidate" | grep -F -- "$prefix" > /dev/null || strings -el "$candidate" | grep -F -- "$prefix" > /dev/null; then
        echo 'Native runtime contains a build-machine path.' >&2
        exit 1
    fi
done
cp "$candidate" "$output/libmgruntime.so"
cp "$build/candidate-exports.txt" "$output/native-exports.txt"
cp "$build/candidate-needed.txt" "$output/native-needed.txt"
compiler=$(sed -n 's/^CMAKE_CXX_COMPILER:FILEPATH=//p' "$build/build/CMakeCache.txt")
printf '%s\n' "$(cmake --version | head -1)" "$("$compiler" --version | head -1)" "$(dotnet --version)" "$candidate_glibc" > "$output/native-toolchain.txt"
