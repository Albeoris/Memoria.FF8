using Memoria.FF8.Assets.Remastered;
using Memoria.FF8.Resources;
using Memoria.FF8.Mods;

namespace Memoria.FF8;

internal sealed partial class GameResourceHost
{
    private readonly Dictionary<string, ModFile> startupLooseData = new(StringComparer.OrdinalIgnoreCase);
    private ModCatalogSnapshot? preparedLooseDataCatalog;
    private ModFile? startupGuestFile;
    private ModDiagnostic? startupGuestDiagnostic;
    private bool looseDataSealed;

    private ModPreparationResult PrepareWithLooseData(ModCatalogSnapshot catalog)
    {
        if (Resources.GetSource<RemasteredGameDataSource>() is not { } remastered)
        {
            return Prepare(catalog);
        }
        Dictionary<string, ModFile> selected = new(StringComparer.OrdinalIgnoreCase);
        List<ModDiagnostic> diagnostics = [];
        foreach (ModFile file in catalog.Files.Where(file => file.OpenPhysicalVersion is not null))
        {
            if (!selected.TryAdd(file.RelativePath, file))
            {
                ModFile winner = selected[file.RelativePath];
                diagnostics.Add(new("Demaster.ExternalFileShadowed", $"The external path is selected from {winner.PackageId}/{winner.RelativePath}.", file.PackageId, file.RelativePath));
            }
        }
        ModFile? guestFile = catalog.FindAll(ModFormat.Demaster, RemasteredGameDataSource.ExternalGuestDataPath).FirstOrDefault();
        foreach (ModFile alias in catalog.FindAll(ModFormat.Demaster, "ff8_en.exe.dat"))
        {
            diagnostics.Add(new("Demaster.GuestAliasUnused", "The verified English wrapper requests ffviii_en.exe.dat.", alias.PackageId, alias.RelativePath));
        }
        if (looseDataSealed)
        {
            foreach (string path in startupLooseData.Keys.Union(selected.Keys, StringComparer.OrdinalIgnoreCase))
            {
                ModFile? active = startupLooseData.GetValueOrDefault(path);
                ModFile? pending = selected.GetValueOrDefault(path);
                if (active?.AbsolutePath != pending?.AbsolutePath || active?.ContentStamp != pending?.ContentStamp || active?.PackageId != pending?.PackageId)
                {
                    ModFile owner = pending ?? active!;
                    string current = active is null ? "installed game archive" : $"{active.PackageId}/{active.RelativePath}";
                    diagnostics.Add(new("Demaster.ExternalFileRestartRequired", $"Restart the game to apply this external path change. The current host retains {current}.", owner.PackageId, path));
                }
            }
            if (guestFile?.AbsolutePath != startupGuestFile?.AbsolutePath || guestFile?.ContentStamp != startupGuestFile?.ContentStamp || guestFile?.PackageId != startupGuestFile?.PackageId)
            {
                ModFile owner = guestFile ?? startupGuestFile!;
                diagnostics.Add(new("Demaster.GuestDataRestartRequired", "Restart the game to apply changes to the guest data tables.", owner.PackageId, owner.RelativePath));
            }
        }
        else if (!ReferenceEquals(preparedLooseDataCatalog, catalog))
        {
            DiscardLooseData();
            try
            {
                remastered.PrepareLooseData(selected.Values.Select(file => new RemasteredLooseDataFile(file.RelativePath, $"{file.PackageId}/{file.RelativePath}", file.Revision, file.OpenPhysicalVersion!)));
                preparedLooseDataCatalog = catalog;
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                return new(false, Diagnostics: [new("Demaster.InvalidExternalData", exception.Message)]);
            }
            if (guestFile is not null)
            {
                startupGuestFile = guestFile;
                if (remastered.Descriptor.Language == "en")
                {
                    try
                    {
                        using Stream selectedGuest = new MemoryStream(guestFile.ReadBytes(), writable: false);
                        remastered.PrepareGuestData(selectedGuest);
                    }
                    catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or NotImplementedException)
                    {
                        startupGuestDiagnostic = new("Demaster.GuestDataRejected", exception.Message, guestFile.PackageId, guestFile.RelativePath);
                    }
                }
                else
                {
                    startupGuestDiagnostic = new("Demaster.GuestLanguageUnused", "This file targets the English guest data profile.", guestFile.PackageId, guestFile.RelativePath);
                }
            }
        }
        if (startupGuestDiagnostic is { } guestDiagnostic)
        {
            diagnostics.Add(guestDiagnostic);
        }
        IEnumerable<string> usable = selected.Values.Select(file => file.PackageId);
        if (remastered.TableOverrides is not null && startupGuestFile is { } guestOwner)
        {
            usable = usable.Append(guestOwner.PackageId);
        }

        ModPreparationResult result;
        try
        {
            result = Prepare(catalog);
        }
        catch
        {
            DiscardLooseData();
            throw;
        }
        return result with
        {
            UsablePackageIds = result.UsablePackageIds?.Concat(usable).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            Diagnostics = (result.Diagnostics ?? []).Concat(diagnostics).ToArray(),
            Commit = () =>
            {
                result.Commit?.Invoke();
                if (!looseDataSealed)
                {
                    foreach ((string path, ModFile file) in selected)
                    {
                        startupLooseData.Add(path, file);
                    }
                    SealLooseData();
                }
            },
            Discard = () =>
            {
                result.Discard?.Invoke();
                DiscardLooseData();
            }
        };
    }

    private void DiscardLooseData()
    {
        if (!looseDataSealed && preparedLooseDataCatalog is not null)
        {
            Resources.GetSource<RemasteredGameDataSource>()!.DiscardPreparedLooseData();
            preparedLooseDataCatalog = null;
            startupGuestFile = null;
            startupGuestDiagnostic = null;
        }
    }

    private void SealLooseData()
    {
        looseDataSealed = true;
        preparedLooseDataCatalog = null;
        Resources.GetSource<RemasteredGameDataSource>()?.SealLooseData();
    }
}
