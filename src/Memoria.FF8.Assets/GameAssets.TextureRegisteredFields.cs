using System.Globalization;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets;

public sealed partial class GameAssets
{
    private TextureCatalogEntry? FindRegisteredFieldTextureTarget(TextureTarget target, string field)
    {
        string[] parts = target.Subresource.Split('/');
        if (target.Palette != 0 || parts.Length < 4 || parts[0] != "model" || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int modelIndex) || parts.Count(part => part == "registered") > 1 || parts[^2] != "texture" || !int.TryParse(parts[^1], NumberStyles.None, CultureInfo.InvariantCulture, out int textureIndex))
        {
            return null;
        }
        if (target.Variant is not ("" or "eyes-open" or "eyes-closed"))
        {
            throw new NotSupportedException($"Field material variant '{target.Variant}' has no source reconstruction contract. Choose its base material or a proved closed-eye variant.");
        }
        ModelCatalogEntry model = GetFieldModelSource(field, modelIndex);
        string original = target.Subresource.Replace("/registered/", "/", StringComparison.Ordinal);
        TextureBuildContext? native = ReadCatalogModelTextures(model).SingleOrDefault(context => context.Key.Subresource == original);
        if (native is null)
        {
            return null;
        }
        if (target.Variant.Length != 0 && !CatalogVariantNames(native).Contains(target.Variant, StringComparer.Ordinal))
        {
            throw new NotSupportedException($"The selected source does not declare the field variant '{target.Variant}'.");
        }
        TextureCatalogEntry entry = new(target, $"{model.Name} · registered texture {textureIndex}{(target.Variant.Length == 0 ? "" : " · " + target.Variant)}", "Field", native.Layout, native.LogicalWidth, native.LogicalHeight) { Reader = "field-registered", Model = model, Index = textureIndex };
        _ = ReadRegisteredFieldTexture(entry);
        return entry;
    }

    private TextureBuildContext ReadRegisteredFieldTexture(TextureCatalogEntry entry)
    {
        ModelCatalogEntry modelEntry = entry.Model ?? throw new InvalidDataException("The registered field source locator is missing.");
        string originalMember = entry.Target.Subresource.Replace("/registered/", "/", StringComparison.Ordinal);
        TextureBuildContext original = ReadCatalogModelTextures(modelEntry).Single(context => context.Key.Subresource == originalMember);
        TextureBuildContext context = new(entry.Target.Key, original.Source, original.Revision, original.Layout, original.LogicalWidth, original.LogicalHeight, () => original.Original, original.Region, original.PaletteImage, original.PaletteCount, original.EncodedSource)
        {
            NativeAlias = original.NativeAlias,
            NativeSheet = original.NativeSheet,
            RequestedVariant = entry.Target.Variant.Length == 0 ? null : entry.Target.Variant,
            NativeVariants = original.NativeVariants,
            ProjectNativeVariant = original.ProjectNativeVariant,
            Game = original.Game
        };
        if (entry.Target.Variant.Length != 0)
        {
            TextureValue installed = resources.ResolveOriginalForExport(context);
            if (installed.IsReplacement && installed.Variants?.ContainsKey(entry.Target.Variant) != true)
            {
                throw new NotSupportedException($"The selected installed field material has no '{entry.Target.Variant}' plane. Export a compatible source variant.");
            }
        }
        return context;
    }
}
