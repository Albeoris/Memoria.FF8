using System.Text.RegularExpressions;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods;

public sealed partial class DemasterTextureResolver
{
    private static bool SupportsExplicitTextureLayout(ModFile file)
    {
        if (file.TextureLayout is not { } layout)
        {
            return true;
        }
        string extension = Path.GetExtension(file.RelativePath);
        if (!extension.Equals(".dds", StringComparison.OrdinalIgnoreCase) && !extension.Equals(".png", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        Match target = modelSheetTarget.Match(file.RelativePath[..^extension.Length]);
        return layout.Mode == TextureLayoutMode.WholeNativeAtlas && layout.CanvasWidth == 256 && layout.CanvasHeight == 256 && target.Success && target.Groups["name"].Value.StartsWith("c0m", StringComparison.OrdinalIgnoreCase) && TryNonnegative(target.Groups["row"].Value, out int row) && (row & 1) == 0;
    }

    private static void ValidateExplicitTextureLayout(ModFile file)
    {
        if (!SupportsExplicitTextureLayout(file))
        {
            throw new ModTextureLoadException(file.PackageId, file.RelativePath, new NotSupportedException("The declared texture layout has no compatible native atlas adapter or canvas."));
        }
    }

    private void ValidateSelectedTextureLayout(TextureBuildContext context)
    {
        if (!hasExplicitTextureLayouts)
        {
            return;
        }
        if (TryModelSheetAddress(context, out _, out TextureSourceAddress address) && address.Owner.Path.StartsWith("battle/c0m", StringComparison.Ordinal))
        {
            return;
        }
        foreach (string path in CandidatePaths(context))
        {
            ModFile? file = catalog.FindAll(ModFormat.Demaster, path).FirstOrDefault();
            if (file is not null)
            {
                ValidateExplicitTextureLayout(file);
                return;
            }
        }
    }
}
