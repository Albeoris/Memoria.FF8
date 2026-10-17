# Optional Remastered resource checks

This project is included in the solution and `Build.ps1 -Test`. It tests synthetic archive and serialized guest image data, provider routing and explicit unsupported runtime boundaries without requiring an installation.

From the product directory, run:

```powershell
dotnet run --project tests/Memoria.FF8.Remastered.Tests
```

Set `MEMORIA_FF8_REMASTERED_DIR` to a selected Remastered installation to enable the additional read-only installed resource checks. They are skipped when this optional input is absent. No game process is launched and no proprietary fixtures are distributed with the project.
