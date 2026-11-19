using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets;

public sealed partial class GameAssets
{
    private IReadOnlyList<string> CatalogVariantNames(TextureBuildContext context)
    {
        TextureVariantSourceInfo? supplied = resources.GetSource<ITextureVariantSource>()?.DescribeTextureVariants(context);
        if (supplied is { HasInstalledImage: true })
        {
            return supplied.Variants;
        }
        return context.NativeVariants is null ? [] : ["eyes-open", "eyes-closed"];
    }

    private IReadOnlyList<TextureBuildContext> ReadAuthoringModelTextures(ModelCatalogEntry model)
    {
        return PrepareAuthoringVariants(ReadCatalogModelTextures(model));
    }

    private IReadOnlyList<TextureBuildContext> PrepareAuthoringVariants(IEnumerable<TextureBuildContext> originals)
    {
        List<TextureBuildContext> result = [];
        foreach (TextureBuildContext native in originals)
        {
            TextureBuildContext source = NormalizeAuthoringSheetContext(native);
            result.Add(source);
            foreach (string variant in CatalogVariantNames(native))
            {
                result.Add(CloneTextureVariant(source, variant));
            }
        }
        return result;
    }

    private static TextureBuildContext CloneTextureVariant(TextureBuildContext source, string? variant)
    {
        TextureValue Select(TextureValue value)
        {
            if (variant is null)
            {
                return value;
            }
            if (value.Variants is not { } variants || !variants.TryGetValue(variant, out var image) || image is null)
            {
                throw new NotSupportedException($"The selected source has no declared '{variant}' material variant.");
            }
            return value with { Image = image, MipLevels = null, Animation = null };
        }
        TextureValue SelectResolved(TextureValue value)
        {
            if (variant is null || value.Variants?.ContainsKey(variant) == true)
            {
                return Select(value);
            }
            TextureValue original = source.OriginalForAuthoring?.Invoke() ?? throw new NotSupportedException("The resolved source has no declared authoring variant provider.");
            TextureValue declared = Select(original);
            var image = !original.IsReplacement && source.ProjectNativeVariant is { } project ? project(value.Image, variant) : declared.Image;
            return value with { Image = image, MipLevels = null, Animation = null };
        }
        return new(source.Key, source.Source, source.Revision, source.Layout, source.LogicalWidth, source.LogicalHeight, () => source.Original, source.Region, source.PaletteImage, source.PaletteCount, source.EncodedSource)
        {
            Game = source.Game,
            AuthoringSourceSha256 = source.AuthoringSourceSha256,
            NativeAlias = source.NativeAlias,
            NativeSheet = source.NativeSheet,
            NativePage = source.NativePage,
            NativeUpload = source.NativeUpload,
            NativeAtlas = source.NativeAtlas,
            RequestedVariant = variant,
            NativeVariants = source.NativeVariants,
            ProjectNativeVariant = source.ProjectNativeVariant,
            OriginalForAuthoring = source.OriginalForAuthoring is null ? null : () => Select(source.OriginalForAuthoring()),
            ResolvedForAuthoring = source.ResolvedForAuthoring is null ? null : () => SelectResolved(source.ResolvedForAuthoring())
        };
    }
}
