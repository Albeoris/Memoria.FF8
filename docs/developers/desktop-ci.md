# Desktop validation and publication

The [desktop workflow](../../.github/workflows/build.yml) has one validation matrix: Windows x64,
Linux x64, Intel macOS and Apple Silicon macOS. Pull requests run these four jobs. Each job builds and
checks the complete distribution and runs the selected portable tests. The
portable tests exclude the battle-effect suite. There is no second launcher or
portable build matrix. Both macOS jobs use macOS 26 runners matching their native architecture (`macos-26-intel` and `macos-26`), as documented in [GitHub’s runner announcement](https://github.blog/changelog/2026-02-26-macos-26-is-now-generally-available-for-github-hosted-runners/).

A new PR revision cancels its obsolete validation run automatically. Main and
tag publication are not cancelled by PR updates.

Pull requests use a read-only token and never publish releases. Fork pull
requests use the ordinary `pull_request` event; the workflow does not run fork
code through `pull_request_target` or use repository secrets. GitHub may require a
maintainer to approve a contributor's workflow run. This policy is configured in
the repository's **Settings > Actions > General > Approval for running fork pull
request workflows from contributors**. Workflow YAML cannot override that policy.
See [GitHub's approval documentation](https://docs.github.com/en/actions/how-tos/manage-workflow-runs/approve-runs-from-forks).

A push to `main` uses the same four validation jobs, followed by publication of
the complete platform set to the `nightly` prerelease. A version tag such as
`v1.2.3` publishes a release from that tag's source. Only the publication job has
write permission. Manual runs follow the selected branch or tag's publication
gate.

Packaging is serialized by source and platform, with a queue that retains pending
publications. Pushing main and a tag still creates two workflow runs, but only
the first job for each source/RID builds and tests. The next job reuses its
uploaded artifact, even if the original run is still publishing or publication
failed. Reuse skips SDK setup, dependency restore, tests and compilation. Only
main/version-tag push or manual runs from this repository can supply reused
artifacts; PR artifacts cannot become release inputs.

CI packages use the channel-independent manifest version `source` and the exact
commit SHA as `BuildId`. A tag added later, multiple tags and either event order
all reuse the same tested bytes. The installed version label is consequently
`source` followed by its commit identifier, including for tagged installations;
update detection compares `BuildId`. macOS Apple version keys remain `0.0.0`,
with `MemoriaReleaseVersion=source`. Local packages retain `local`.

The GitHub release tag/title and `release-artifacts.json.channel` carry the
release version outside the signed package. Publication verifies all four
source receipts and archive digests, then uploads the archives unchanged to
nightly or the versioned release. It performs no SDK setup, compilation,
version mutation or re-signing. Signed macOS archives still require successful
native signature validation. A source/RID/recipe artifact that expired or was
deleted must be recovered; it is not silently rebuilt. Reused artifacts are
copied into the current workflow run for publication of its complete RID set.

Both Mac jobs run the native signing regression tests, sign all nested Mach-O
files and the assembled app with free ad hoc signatures, and verify signatures
again after archive extraction. No Apple account or signing secret is used.
Ad hoc signing does not provide Developer ID trust or notarization.

Fresh builds first ask the installed SDK to resolve the unchanged `global.json`.
`setup-dotnet` runs only if that resolution fails. Jobs restore the NuGet cache;
only pushes to `main` save new entries. Build outputs and tested distribution
artifacts are separate from this dependency cache. Portable tests reuse the
assemblies built by their graph. The portable subset uses the native graph's
output directory and build properties, so it updates that graph incrementally
after packaging rather than compiling a second copy. A standalone portable run
still builds its full subset before testing. Repeated packaging checks reuse
their compiled checker by invoking its assembly directly, without starting MSBuild for each assertion.

Artifact reuse, orchestration, archive packaging, release publication, native macOS signing and project coverage have separate validation steps with individual time limits. Packaging tests print each checker operation before executing it, so a stalled operation remains visible in the log.

The native graph is built for the selected RID with the same self-contained and
debug settings as the distribution. Vulkan, dispatcher, music and launcher
publishes use `--no-build` against that graph; they still compute and validate
their individual payloads. DX11 and DX12 retain separate backend outputs. Each
`dotnet` invocation reports elapsed time, so restore/build/publish delays remain
visible in the job log.

Transitive projects not listed in the solution inherit the selected
configuration and platform. This prevents a Release solution build from
silently producing Debug dependencies that publishing would compile again.

Windows Direct3D 12 and Linux Vulkan builds also cache the prepared native
library, receipt and license notices. These small caches use exact runner-image
and source-recipe identities, with no fallback key. Only main pushes save them.
The build scripts verify restored binaries and receipts against the pinned
source recipe before reuse; CMake build trees and downloaded source trees are
not cached.
