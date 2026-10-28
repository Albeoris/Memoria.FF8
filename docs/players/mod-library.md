# Downloading mods

Open **Mods** from the game menu. **Installed**, **Catalog**, **Downloads** and **Create** keep local configuration, online discovery, transfer progress and authoring separate.

The Junction catalog is available for browsing independently of installed mods. Opening Catalog refreshes an old or missing saved catalog in the background. Refresh failures preserve the last good list. Installed mods work without a connection or catalog cache. Edition labels supplied by Junction describe its original client; Memoria checks packages for its own supported Classic English target.

Select a catalog row to read its details, then choose **Download & check**. This downloads a pinned release and checks its identity, archive and supported layout. It does not install it. A successfully checked package offers **Install**, with the resulting plan and enablement consequence in Details. All installations and updates apply **on the next launch**, before resources are loaded. Until then Downloads says **Restart required**. Disk installation and activation in the running game are separate states.

New installations follow **Automatically enable new mods**. Updates preserve explicit enablement, priority and valid existing option overrides. The startup resource check either accepts the prepared package or restores the previous managed installation. A package that fails runtime preparation is not reported as active.

## Transfers and recovery

- Downloads continue when you close Mods. Pause keeps the partial file; Resume continues only when the server can prove that it is the same file. Servers without safe resume support restart that file from the beginning.
- After exiting or a crash, recovered jobs wait for **Resume**. A complete saved download is validated locally before reuse. No network job resumes silently.
- Downloads shows transfer, checking, inspection, ready-to-install and restart states separately. Unknown sizes show received bytes without an invented percentage. Details retains the useful failure explanation.
- Cancel removes an uncommitted download and its owned cache files. A checked package can be removed before Install. A transaction already waiting for startup remains protected until application or recovery completes.
- Installed jobs offer **Remove cached download** to reclaim compressed download space. This leaves the installed mod and its settings intact.
- Provider quota or sign-in requirements offer **Open download page**. This opens a recognized web address only after your action. Browser credentials are not imported. You can also place a manually downloaded `.iroj` under the selected `Mods/Junction` directory; it will be discovered as a local mod.

Only one game instance can manage a given mod directory. A second instance can use local mods but cannot apply pending changes to files retained by the first instance.

## Local files and supported packages

An exact local Junction manifest GUID associates a manual mod with a catalog entry. It does not transfer ownership: the downloader cannot overwrite or update that manual installation automatically. Names alone do not establish identity. Managed versions are compared using Junction decimal release semantics; metadata revisions do not imply a content update.

Supported downloads are a full standalone IROJ, or a ZIP/7z containing one IROJ or one unpacked Junction package. The inner IROJ and conditional folders remain intact. GUID and release version must match the selected catalog release. Existing reviewed executable-behavior rules still apply; unknown code is never executed.

Packages needing multiple component choices, coordinated dependency/option relationships, delta patching, unsupported extraction directives or a change between archive and directory installation shapes are blocked with an explanation. This release does not provide automatic adoption or uninstall of manual mods. Unsupported or ambiguous packages remain available for investigation without touching installed files.

## Controls and storage

Up/Down selects a row; Confirm opens its inspector. Left/Right or page bindings change list pages. Shoulder/tab-switch bindings change destinations. Tab/Shift+Tab and pane-focus actions move between controls. Back returns from details to the narrow-layout list. Search supports typing and an on-screen character grid. The footer shows configured Confirm/Back bindings.

On small displays, details replace the list rather than shrinking both panes. Long descriptions and errors have a separate paged Details view. Preview failures leave a stable placeholder.

Download caches use the application's per-user cache directory, scoped to the selected target. Transaction state and verified backups use a `.memoria-mod-library-*` directory beside the chosen mod root, outside package discovery. Do not delete unresolved journals or their referenced backups. Cancel individual uncommitted jobs or remove an installed job's downloaded cache to reclaim compressed downloads. Recovery backups and abandoned preparation data are retained for review; automatic pruning is not implemented. Preview storage has its own small bounded cache.

See [file replacements](../modding/file-replacements.md) for manual placement and option sidecars, and [texture mods](../modding/texture-mods.md) for package order and local enablement.
