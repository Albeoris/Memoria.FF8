# Released updater compatibility gate

Run this tool against the complete generated distribution ZIP before publishing:

```powershell
dotnet run --project tools/Memoria.FF8.Updater.Compatibility -c Release -- artifacts/Memoria-FF8-win-x64.zip
```

The ZIP argument is mandatory. This project has no reference to the current launcher. `Frozen/` contains the unchanged original manifest validator and installer from the first released automatic updater. These are test fixtures, never an alternate production implementation. Keep them unchanged when modifying the current updater or package format: an already installed launcher must accept and install the next release using its existing code.

The fixture source identities are SHA-256 over UTF-8 text with LF line endings:

| Fixture | SHA-256 |
| --- | --- |
| PackageManifest.cs | `6ab487324d9a8b6cd5ce13b7a7aeef871da62695f0fd3987fb043b07b41f9902` |
| UpdateInstaller.cs | `3bda5477acc0885565bc95d0c1b8d45e1849990eeb2ad91bb2ae7ee44b6d30a2` |

The runner checks these identities, extracts the entire archive with the frozen validator, installs it over a synthetic original three-executable inventory, and verifies every resulting owned file against the incoming manifest. It repeats with an injected failure immediately before the manifest replacement and verifies rollback, original file hashes and retained unowned files. Each scenario also retains user mods, notes and unowned Data files. Scenarios run sequentially and remove their installation directories after completion to limit disk usage.

Small negative archives check that the frozen validator still rejects unexpected root files/directories and the incompatible top-level `Mods/Memoria` layout. Supporting a future package format requires retaining compatibility with every supported released updater, or an explicit staged upgrade design; changing these fixtures cannot upgrade existing installations.
