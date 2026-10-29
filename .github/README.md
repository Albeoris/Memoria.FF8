# Desktop validation and publication

The [desktop workflow](workflows/build.yml) has one validation matrix: Windows x64,
Linux x64 and Intel macOS. Pull requests run these three jobs. Each job builds and
checks the complete distribution and runs the selected portable tests. The
portable tests exclude the battle-effect suite. There is no second launcher or
portable build matrix.

A new PR revision cancels its obsolete validation run automatically. Main and
tag publication are not cancelled by PR updates.

Pull requests use a read-only token and never publish releases. Fork pull
requests use the ordinary `pull_request` event; the workflow does not run fork
code through `pull_request_target` or use repository secrets. GitHub may require a
maintainer to approve a contributor's workflow run. This policy is configured in
the repository's **Settings > Actions > General > Approval for running fork pull
request workflows from contributors**. Workflow YAML cannot override that policy.
See [GitHub's approval documentation](https://docs.github.com/en/actions/how-tos/manage-workflow-runs/approve-runs-from-forks).

A push to `main` uses the same three validation jobs, followed by publication of
the complete platform set to the `nightly` prerelease. A version tag such as
`v1.2.3` publishes a release from that tag's source. Only the publication job has
write permission. Manual runs follow the selected branch or tag's publication
gate.

Packaging is serialized by source and platform. A tested artifact from an
earlier run of the same source and recipe is reused, including when a version
tag points to an already built main commit. Reuse skips SDK setup, dependency
restore and compilation. Publication verifies the source receipts and archive
digests before applying version metadata. An expired or deleted tested artifact
must be recovered; it is not silently rebuilt.

Fresh builds first ask the installed SDK to resolve the unchanged `global.json`.
`setup-dotnet` runs only if that resolution fails. Jobs restore the NuGet cache;
only pushes to `main` save new entries. Build outputs and tested distribution
artifacts are separate from this dependency cache. Portable tests reuse the
assemblies built by their graph. The portable subset uses the native graph's
output directory and build properties, so it updates that graph incrementally
after packaging rather than compiling a second copy. A standalone portable run
still builds its full subset before testing. Repeated packaging checks reuse
their compiled checker.

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
