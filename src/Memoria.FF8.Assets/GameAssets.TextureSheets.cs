using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Resources;
using System.Buffers.Binary;
using System.Globalization;

namespace Memoria.FF8.Assets;

public sealed partial class GameAssets
{
    private TextureCatalogEntry? FindGlobalFieldSheetTarget(TextureTarget target, CancellationToken cancellationToken)
    {
        string[] owner = target.Owner.Split('/');
        string[] member = target.Subresource.Split('/');
        if (target.Palette != 0 || owner[2] is not ("main_chr" or "second_chr") || member.Length is not (2 or 3) || member[0] != "row" || !int.TryParse(member[1], NumberStyles.None, CultureInfo.InvariantCulture, out _) || member.Length == 3 && member[2] != "alpha")
        {
            return null;
        }
        if (owner[2] == "main_chr")
        {
            TextureBuildContext? context = ReadMainCharacterAuthoringSheets(owner[3], member.Length == 3).SingleOrDefault(context => TextureTarget.FromContext(context) == target);
            return context is null ? null : Entry(context, $"{owner[3]} · {target.Subresource} · {target.Variant}", "Field") with { Reader = "main-sheet" };
        }
        foreach (ModelCatalogEntry model in EnumerateFieldModelSources())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (model.LoadError is not null || model.ModelLibraryResource is not null || !model.Name.StartsWith(owner[3], StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            TextureBuildContext? context = ReadAuthoringModelTextures(model).SingleOrDefault(context => TextureTarget.FromContext(context) == target);
            if (context is not null)
            {
                return Entry(context, $"{owner[3]} · {target.Subresource} · {target.Variant}", "Field") with { Reader = "model", Model = model, UsedBy = [model.ResourcePath + "#model/" + model.ModelIndex] };
            }
        }
        return null;
    }

    private IReadOnlyList<TextureBuildContext> ReadMainCharacterAuthoringSheets(string name, bool alpha)
    {
        ResourceKey owner = new($"field/main_chr/{name}.mch");
        byte[] bytes = ReadTextureSource(owner);
        FieldModel model = resources.GetSource<IFieldModelSource>()?.ReadMainCharacterMaterials(owner, bytes) ?? FieldModelReader.ReadMainCharacterMaterials(name, bytes);
        int position = 0;
        while (BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(position)) != uint.MaxValue)
        {
            position += 4;
            if (position > 400)
            {
                throw new InvalidDataException("The main-character material table exceeds its supported bounds.");
            }
        }
        int modelOffset = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(position + 4));
        if (model.Textures.Count > 0)
        {
            int firstTexture = checked((int)(BinaryPrimitives.ReadUInt32LittleEndian(bytes) & 0xFFFFFF));
            model = model with { NativeEyes = ReadCatalogEyeDefinition(bytes, modelOffset, WorldAssetReader.ReadTim(bytes, firstTexture).Image), TextureSourceVariant = alpha ? "alpha" : null };
        }
        bool variants = FieldEyeMaterial.HasNativeVariants(model);
        TextureBuildContext[] sources = model.Textures.Select((image, ordinal) => Context(new(owner, $"texture/{ordinal}"), image, "classic-field-model", model.TextureSourceDescriptor, ordinal < model.TextureRegions.Count ? model.TextureRegions[ordinal] : null, NativeFieldModelTextureSheets.Address(model, ordinal), nativeVariants: ordinal == 0 && variants ? () => FieldEyeMaterial.NativeVariants(model) : null, projectNativeVariant: ordinal == 0 && variants ? (edited, variant) => FieldEyeMaterial.Project(model.NativeEyes!, edited, variant) : null)).ToArray();
        return PrepareAuthoringVariants(sources);
    }

    /// <summary>Source-proved sheet aliases share identity across field usages; image similarity never establishes sharing.</summary>
    internal TextureBuildContext NormalizeAuthoringSheetContext(TextureBuildContext source, TextureValue? capturedOriginal = null, TextureValue? capturedResolved = null)
    {
        if (source.Layout is not ("classic-field-model" or "classic-model") || source.NativeSheet is not { } sheet)
        {
            return source;
        }
        if (sheet.CanvasWidth != 128 || sheet.CanvasHeight != 128 || sheet.X < 0 || sheet.Y < 0 || sheet.Width <= 0 || sheet.Height <= 0 || (long)sheet.X + sheet.Width > sheet.CanvasWidth || (long)sheet.Y + sheet.Height > sheet.CanvasHeight || source.LogicalWidth != sheet.Width || source.LogicalHeight != sheet.Height)
        {
            throw new NotSupportedException("Field sheet authoring requires the complete source-proved 128x128 row and its exact material rectangle.");
        }
        TextureValue Project(TextureValue value) => ProjectTextureSheetValue(value, sheet);
        ResourceContent? encoded = source.EncodedSource;
        if (sheet.Owner.Path.StartsWith("field/model/main_chr/", StringComparison.Ordinal))
        {
            string name = sheet.Owner.Path.Split('/')[^1];
            encoded = resources.Get(new($"field/main_chr/{name}.mch"));
        }
        return new TextureBuildContext(new(sheet.Owner, sheet.Subresource, sheet.Palette), source.Source, source.Revision, "native-field-model-sheet", sheet.CanvasWidth, sheet.CanvasHeight, () => ProjectTextureSheetImage(source.Original, sheet.Width, sheet.Height, sheet), new(BitsPerPixel: source.Region.BitsPerPixel, CanvasWidth: sheet.CanvasWidth, CanvasHeight: sheet.CanvasHeight), paletteCount: source.PaletteCount, encodedSource: encoded)
        {
            Game = source.Game,
            RequestedVariant = source.RequestedVariant,
            NativeSheet = sheet with { X = 0, Y = 0, Width = sheet.CanvasWidth, Height = sheet.CanvasHeight },
            OriginalForAuthoring = () => Project(capturedOriginal ?? resources.ResolveOriginalForExport(source)),
            ResolvedForAuthoring = () => Project(capturedResolved ?? resources.ResolveForExport(source)),
            NativeVariants = source.NativeVariants is null ? null : () => source.NativeVariants().ToDictionary(pair => pair.Key, pair => ProjectTextureSheetImage(pair.Value, source.LogicalWidth, source.LogicalHeight, sheet), StringComparer.Ordinal),
            ProjectNativeVariant = source.ProjectNativeVariant is null ? null : (image, variant) => ProjectSheetVariant(image, variant, source, sheet)
        };
    }

    private static TextureValue ProjectTextureSheetValue(TextureValue value, TextureSourceAddress sheet)
    {
        PixelImage image = ProjectTextureSheetImage(value.Image, value.LogicalWidth, value.LogicalHeight, sheet);
        IReadOnlyDictionary<string, PixelImage>? variants = value.Variants?.ToDictionary(pair => pair.Key, pair => ProjectTextureSheetImage(pair.Value, value.LogicalWidth, value.LogicalHeight, sheet), StringComparer.Ordinal);
        IReadOnlyList<PixelImage>? mips = null;
        if (value.MipLevels is not null)
        {
            List<PixelImage> projected = [];
            foreach (PixelImage level in value.MipLevels)
            {
                if (level.Width % value.LogicalWidth != 0 || level.Height % value.LogicalHeight != 0 || level.Width / value.LogicalWidth != level.Height / value.LogicalHeight)
                {
                    break;
                }
                projected.Add(ReferenceEquals(level, value.Image) ? image : ProjectTextureSheetImage(level, value.LogicalWidth, value.LogicalHeight, sheet));
            }
            mips = projected.Count == 0 ? null : projected;
        }
        return new(image, sheet.CanvasWidth, sheet.CanvasHeight, value.Provenance, mips, variants) { IsReplacement = value.IsReplacement, Animation = value.Animation is null ? null : new TextureAnimation(value.Animation.Frames.Select(frame => ProjectTextureSheetImage(frame, value.LogicalWidth, value.LogicalHeight, sheet)).ToArray(), value.Animation.FrameDurationMilliseconds) };
    }

    private static PixelImage ProjectTextureSheetImage(PixelImage image, int logicalWidth, int logicalHeight, TextureSourceAddress sheet)
    {
        if (logicalWidth != sheet.Width || logicalHeight != sheet.Height || image.Width % logicalWidth != 0 || image.Height % logicalHeight != 0 || image.Width / logicalWidth != image.Height / logicalHeight)
        {
            throw new InvalidDataException("Field sheet pixels must preserve the source material's uniform integer density.");
        }
        int scale = image.Width / logicalWidth;
        if (sheet.X == 0 && sheet.Y == 0 && sheet.Width == sheet.CanvasWidth && sheet.Height == sheet.CanvasHeight)
        {
            return image;
        }
        PixelImage canvas = new(checked(sheet.CanvasWidth * scale), checked(sheet.CanvasHeight * scale));
        for (int y = 0; y < image.Height; y++)
        {
            image.Pixels.AsSpan(y * image.Width, image.Width).CopyTo(canvas.Pixels.AsSpan((sheet.Y * scale + y) * canvas.Width + sheet.X * scale, image.Width));
        }
        return canvas;
    }

    private static PixelImage ProjectSheetVariant(PixelImage image, string variant, TextureBuildContext source, TextureSourceAddress sheet)
    {
        if (image.Width % sheet.CanvasWidth != 0 || image.Height % sheet.CanvasHeight != 0 || image.Width / sheet.CanvasWidth != image.Height / sheet.CanvasHeight)
        {
            throw new InvalidDataException("Field sheet variants require a coherent uniform integer canvas.");
        }
        int scale = image.Width / sheet.CanvasWidth;
        PixelImage material = new(sheet.Width * scale, sheet.Height * scale);
        for (int y = 0; y < material.Height; y++)
        {
            image.Pixels.AsSpan((sheet.Y * scale + y) * image.Width + sheet.X * scale, material.Width).CopyTo(material.Pixels.AsSpan(y * material.Width, material.Width));
        }
        PixelImage projected = source.ProjectNativeVariant!(material, variant);
        if (projected.Width != material.Width || projected.Height != material.Height)
        {
            throw new InvalidDataException("The native eye-copy projection changed its source material dimensions.");
        }
        PixelImage result = new(image.Width, image.Height, image.Pixels.ToArray());
        for (int y = 0; y < projected.Height; y++)
        {
            projected.Pixels.AsSpan(y * projected.Width, projected.Width).CopyTo(result.Pixels.AsSpan((sheet.Y * scale + y) * result.Width + sheet.X * scale, projected.Width));
        }
        return result;
    }
}
