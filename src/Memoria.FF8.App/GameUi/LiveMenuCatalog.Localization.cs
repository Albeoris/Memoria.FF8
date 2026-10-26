using Memoria.FF8.Assets.Localization;

namespace Memoria.FF8.GameUi;

public sealed partial class LiveMenuCatalog
{
    private string? LocalizedTable(string table, int ordinal, string? original)
    {
        if (original is null || resources is null)
        {
            return original;
        }
        string key = LocalizationSourceExporter.IndexedKey(resources.Source.Language, table, ordinal);
        return resources.Localize(key, original);
    }

    private string? LocalizedKernel(string collection, string role, int ordinal, string? original) => LocalizedTable(LocalizationSourceExporter.KernelTable(collection, role), ordinal, original);

    private (string Collection, int Ordinal)? AbilityIdentity(int id)
    {
        if (id < 0)
        {
            return null;
        }
        foreach ((string collection, int count) in new[]
        {
            (nameof(kernel.JunctionAbilities), kernel.JunctionAbilities.Count),
            (nameof(kernel.CommandAbilities), kernel.CommandAbilities.Count),
            (nameof(kernel.CharacterStatAbilities), kernel.CharacterStatAbilities.Count),
            (nameof(kernel.CharacterAbilities), kernel.CharacterAbilities.Count),
            (nameof(kernel.PartyAbilities), kernel.PartyAbilities.Count),
            (nameof(kernel.GuardianAbilities), kernel.GuardianAbilities.Count),
            (nameof(kernel.MenuAbilities), kernel.MenuAbilities.Count)
        })
        {
            if (id < count)
            {
                return (collection, id);
            }
            id -= count;
        }
        return null;
    }

}

