namespace Memoria.FF8.Mods;

public static partial class ModPreviewResources
{
    internal static IReadOnlyList<string> DescribeDemasterAtlasMembers(ModFile file)
    {
        string stem = Path.ChangeExtension(file.RelativePath, null);
        return DemasterTextureResolver.DescribeCardArtworkTarget(stem)?.Members ?? [];
    }

    private static LegacyTextureTarget? FindDemasterTextureTarget(string path)
    {
        string stem = path[..^Path.GetExtension(path).Length];
        if (DemasterTextureResolver.DescribeCardArtworkTarget(stem) is { } cards)
        {
            return new(cards.Owner, cards.Members[0], Palette: 0, Members: cards.Members);
        }
        if (DemasterTextureResolver.DescribeFieldPageTarget(stem) is { } field)
        {
            return new($"field/{field.Map}/{field.Map}.mim", Palette: field.Palette, Field: field.Map, NativeMember: $"page/{field.NativePage}");
        }
        if (DemasterTextureResolver.DescribeWorldTerrainTarget(stem) is { } terrain)
        {
            return new("world/texl.obj", $"texl/{terrain.Page}", terrain.Palette == 0 ? null : terrain.Palette);
        }
        return null;
    }
}
