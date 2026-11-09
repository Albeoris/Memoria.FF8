using Memoria.FF7.Formats;

namespace Memoria.FF7.Resources;

public sealed record Ff7MenuResources(IReadOnlyDictionary<string, PcTexture> Textures)
{
    private static readonly string[] PortraitNames = ["cloud.tex", "barre.tex", "tifa.tex", "earith.tex", "red.tex", "yufi.tex", "ketc.tex", "bins.tex", "cido.tex"];

    public PcTexture GetPortrait(int characterId)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(characterId);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(characterId, PortraitNames.Length);
        string name = PortraitNames[characterId];
        return Textures.TryGetValue(name, out PcTexture? texture) ? texture : throw new InvalidDataException($"FF7 menu portrait '{name}' is absent.");
    }

    public PcTexture GetRequiredTexture(string memberName)
    {
        return Textures.TryGetValue(memberName, out PcTexture? texture) ? texture : throw new InvalidDataException($"FF7 menu texture '{memberName}' is absent.");
    }

    public static Ff7MenuResources LoadPortraits(Ff7Installation installation)
    {
        return Load(installation, portraitsOnly: true);
    }

    public static Ff7MenuResources Load(Ff7Installation installation)
    {
        return Load(installation, portraitsOnly: false);
    }

    private static Ff7MenuResources Load(Ff7Installation installation, bool portraitsOnly)
    {
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MenuArchive).Path);
        Dictionary<string, PcTexture> textures = new(StringComparer.OrdinalIgnoreCase);
        foreach (LgpEntry entry in archive.Entries.Where(entry => entry.Name.EndsWith(".tex", StringComparison.OrdinalIgnoreCase) && (!portraitsOnly || PortraitNames.Contains(entry.FullPath, StringComparer.OrdinalIgnoreCase))))
        {
            if (!textures.TryAdd(entry.FullPath, PcTexture.Read(archive.ReadEntry(entry))))
            {
                throw new InvalidDataException($"FF7 menu texture '{entry.FullPath}' is ambiguous.");
            }
        }
        return new Ff7MenuResources(new System.Collections.ObjectModel.ReadOnlyDictionary<string, PcTexture>(textures));
    }
}
