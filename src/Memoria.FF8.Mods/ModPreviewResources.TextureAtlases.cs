using System.Globalization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Mods;

public static partial class ModPreviewResources
{
    internal static IReadOnlyList<string> DescribeTextureAtlasMembers(ModFile file)
    {
        if (!TextureModPaths.IsImage(file.RelativePath))
        {
            return [];
        }
        return file.Format switch
        {
            ModFormat.Junction => FfnxTextureResolver.DescribeCardAtlasMembers(file.RelativePath),
            ModFormat.Demaster => DescribeDemasterAtlasMembers(file),
            _ => []
        };
    }

    internal static string DescribeTextureAtlasMember(string member) => member.Split('/') switch
    {
        ["front", var index] => $"Card {int.Parse(index, CultureInfo.InvariantCulture) + 1:000}",
        ["back"] => "Card back",
        ["element", var index] => ((TripleTriadElement)(int.Parse(index, CultureInfo.InvariantCulture) + 1)).ToString(),
        ["outcome", "0"] => "Player wins",
        ["outcome", "1"] => "Opponent wins",
        ["outcome", "2"] => "Draw",
        ["capture", "0"] => "Same",
        ["capture", "1"] => "Plus",
        ["capture", "2"] => "Combo",
        _ => member
    };
}
