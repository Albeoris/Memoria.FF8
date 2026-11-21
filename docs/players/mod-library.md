# Downloading mods

The launcher also provides **Mods > Installed / Catalog** with search, category counts, component toggles, details, downloads and transfer controls. Select a mod in the left list to read its scrollable description on the right; drag the divider to resize the columns. **Download missing components** queues compatible missing official components. Unavailable downloads show their reason; engine-delivered components are excluded from this action. The launcher releases its download queue before starting the game; saved transfers remain resumable. Advanced configuration and load order use the in-game manager.

Open **Mods** from the game menu. **Installed**, **Catalog**, **Downloads** and **Create** keep local configuration, online discovery, transfer progress and authoring separate.

Set **Mods > Install mods in** in the launcher to choose the destination for new installations and verified updates. The in-game manager reads the same setting. A destination change leaves installed mods available where they are. On startup, an accepted update moves the managed package to the chosen destination and carries its existing settings with it. If the destination is busy or a file already occupies it, the manager keeps the source and reports what blocked the move.

The Junction catalog is available for browsing independently of installed mods. Opening Catalog refreshes an old or missing saved catalog in the background. Refresh failures preserve the last good list. Installed mods work without a connection or catalog cache. Edition labels supplied by Junction describe its original client; Memoria checks packages for its own supported Classic English target.

Select a catalog row to read its details, then choose **Download & check**. This downloads a pinned release and checks its identity, archive and supported layout. It does not install it. A successfully checked package offers **Install**, with the resulting plan and enablement consequence in Details. New packages can be installed during play. The game discovers them through the normal mod refresh and lists them in **Installed**, where you can enable or disable them. Updates and repairs of existing managed packages apply once the resource directory is free. Downloads shows **Waiting to apply** while checked files are staged. Activation follows the game's usual resource boundaries.

New installations follow **Automatically enable new mods**. Updates preserve explicit enablement, priority and valid existing option overrides. The startup resource check can restore a previous managed installation after a rejected update. A package that fails runtime preparation is not reported as active.

## Transfers and recovery

- Downloads continue when you close Mods. Pause keeps the partial file; Resume continues only when the server can prove that it is the same file. Servers without safe resume support restart that file from the beginning.
- After exiting or a crash, recovered jobs wait for **Resume**. A complete saved download is validated locally before reuse. No network job resumes silently.
- Downloads shows transfer, checking, inspection, ready-to-install and restart states separately. Unknown sizes show received bytes without an invented percentage. Details retains the useful failure explanation.
- Cancel removes an uncommitted download and its owned cache files. A checked package can be removed before Install. An accepted transaction remains protected until application or recovery completes.
- Installed jobs offer **Remove cached download** to reclaim compressed download space. This leaves the installed mod and its settings intact.
- Provider quota or sign-in requirements offer **Open download page**. This opens a recognized web address only after your action. Browser credentials are not imported. You can also place a manually downloaded `.iroj` under the selected `Mods/Junction` directory; it will be discovered as a local mod.

Only one game instance can manage a given mod directory. A second instance can use local mods but cannot apply pending changes to files retained by the first instance.

## Local files and supported packages

An exact local Junction manifest GUID associates a manual mod with a catalog entry. It does not transfer ownership: the downloader cannot overwrite or update that manual installation automatically. Names alone do not establish identity. Managed versions are compared using Junction decimal release semantics; metadata revisions do not imply a content update.

Supported downloads are a full standalone IROJ, or a ZIP/7z containing one IROJ or one unpacked Junction package. The inner IROJ and conditional folders remain intact. The package GUID must match the catalog GUID. If only the versions differ, the launcher shows both versions in a warning. **Preview changes** remains available and **Install anyway** installs the version declared in the package. Existing package validation and reviewed executable-behavior rules still apply.

The launcher refreshes the catalog at startup and before **Retry**. Retry checks the download against the refreshed release. If the download source is unchanged, it can reuse the verified archive.

A Junction package can require or exclude other active mods by GUID and constrain their options. These rules are checked when applying the active configuration. If a rule blocks an installed mod, the mod stays installed and **Installed** explains the conflict. After you resolve the conflict, the mod can activate using its existing enablement setting.

Packages needing multiple component choices, catalog-level installation requirements, delta patching, unsupported extraction directives or a change between archive and directory installation shapes are blocked with an explanation. The manager does not automatically adopt manual mods. Unsupported or ambiguous packages remain available for investigation without touching installed files.

## Removing downloaded mods

Both the launcher and the in-game manager provide separate actions for downloaded archives and installed mods. **Delete downloaded archive** / **Delete download** reclaims cached download space while keeping the installed mod. **Uninstall** / **Uninstall on restart** schedules removal of a manager-owned installation before resources load on the next game start. Active resources remain available until that boundary.

Managed uninstall verifies file ownership and preserves migrated originals and locally modified payloads. A failed verification explains why removal could not proceed. Native component preferences stay in the central settings file.

For manually installed packages, **Mods > Installed > Delete** in the launcher permanently removes the selected package after confirmation. For a local IROJ, this removes the selected archive and preserves its sidecar settings and surrounding files. Close the game before deleting a local package. Bundled components and managed installations are protected from this action.

## Previewing changes

Choose **Preview changes** on an installed Memoria, Junction or Demaster package, or on a catalog release after the download is checked. In the game, individual Memoria components are listed under **Settings** on the Memoria catalog row. The comparison uses the selected game's original resources on the left and the selected package on the right. It leaves installations, mod settings and saves alone.

When the preview opens, the tree selects the first change and expands its path. Selecting a folder keeps the current comparison visible. Use the disclosure arrow to open or close a folder. Folder counts show how many package files they contain. Expand a supported atlas to compare individual images and see the number of images in that atlas. Select **Expand comparison** to give both sides more space. World map comparisons apply the package's world resources together and move both views to the same position. Selecting a supported model texture shows the model with its original materials on the left and the package's replacements on the right. Use the shared orientation and pose controls to compare the same view on both sides. The **Use item** button previews the package's image action inside the comparison.

In the launcher's **World map → Explore** comparison, use **W/S** to move forward or backward, **A/D** to rotate left or right, **Shift+A/D** to strafe, and **Q/E** to rise or descend.

Reviewed Junction patches describe their gameplay effects in text. Other supported gameplay changes report the original and applied rules, table entries or models. Field scene comparisons include supported actor placements and the background states named in the package. For files with changed text or movie camera records, use **Previous line** and **Next line** to read the paired entries.

In the launcher or the game, choose **Play** to start both media sides. Each video follows its own frame timing on the shared timeline. **Sound · Before / After** changes which side you hear at the current position. Click the timeline to seek or focus it and use Left, Right, Page Up and Page Down. The timeline reaches the end of the longer source. A shorter video holds its last frame. DirectMusic previews play the full authored segment, with **Replay** available from the beginning.

For a localization package, choose **Play from the beginning** and expand the comparison. Both sessions start with the opening. They receive the same movement and interaction input, including native tutorial controls and ordinary battle commands. Use **Interact** to confirm, **Scene Cancel** or the Card shortcut to cancel within the scene, and Back to return to the change list. Individual changed lines remain available through the tree.

## Controls and storage

Use Up and Down to select a row and Confirm to open its inspector. Left, Right and the page bindings change list pages. Shoulder or tab-switch bindings change destinations. Tab, Shift+Tab and pane-focus actions move between controls. Back returns from the inspector to the list in a narrow window.

Select the catalog search field to type directly. Use the arrow keys, Home and End to move the caret. Backspace and Delete remove characters, and Enter or Escape leaves the field. With a controller, Search opens a character grid with English, Symbols and Cyrillic pages. Choose **Done** to return to the catalog.

Open **Expand details** to read the full description, requirements, release notes or download errors. Scroll with Up and Down, the page bindings or the mouse wheel. Back returns to the selected package. If the preview image is unavailable, the package details are still available.

Download caches use the application's per-user cache directory, scoped to the selected target. Transaction state and verified backups use a `.memoria-mod-library-*` directory beside the chosen mod root, outside package discovery. Do not delete unresolved journals or their referenced backups. Cancel individual uncommitted jobs or remove an installed job's downloaded cache to reclaim compressed downloads. Recovery backups and abandoned preparation data are retained for review; automatic pruning is not implemented. Preview storage has its own small bounded cache.

See [file replacements](../modding/file-replacements.md) for manual placement and option sidecars, and [texture mods](../modding/texture-mods.md) for package order and local enablement.
