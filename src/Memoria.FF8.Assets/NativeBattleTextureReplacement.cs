using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed record NativeBattleTextureReplacement(ushort Page, ushort Clut, int U, int V, int Width, int Height, PixelImage Image, int WordWidth, ImmutableArray<byte> PixelWords, ImmutableArray<byte> PaletteWords)
{
    public string? SourceOwner { get; init; }
    public Memoria.FF8.Resources.TextureAnimation? Animation { get; init; }
    public int ImageSourceX { get; init; }
    public int ImageSourceY { get; init; }
    public int ImageCanvasWidth { get; init; }
    public int ImageCanvasHeight { get; init; }
    public int LogicalImageWidth => ImageCanvasWidth > 0 ? ImageCanvasWidth : Width;
    public int LogicalImageHeight => ImageCanvasHeight > 0 ? ImageCanvasHeight : Height;
}

public static class NativeBattleTextureReplacements
{
    public static IReadOnlyList<NativeBattleTextureReplacement> Bind(BattleModel model, NativeBattleModelTextureBinding binding, bool weapon = false)
    {
        ResourceTextureList? textures = model.Textures as ResourceTextureList;
        EditableModel? imported = model.NativeGeometryBytes is not null ? model.EditableOverride : null;
        if (textures is null && imported is null)
        {
            return [];
        }
        NativeBattleEffectHeapAllocation? pack = weapon ? model.NativeWeaponTexturePack : model.NativeBodyTexturePack;
        if (pack is null)
        {
            return [];
        }
        ReadOnlySpan<byte> bytes = pack.Data.AsSpan();
        int count = BinaryPrimitives.ReadInt32LittleEndian(bytes);
        if (imported is null && count != textures!.Count)
        {
            throw new InvalidDataException("Native replacement materials disagree with their original counted TIM pack.");
        }
        List<NativeBattleTextureReplacement> result = [];
        for (int texture = 0; texture < count; texture++)
        {
            TextureValue? value = textures?.GetTexture(texture);
            int importedTexture = imported?.Meshes.FirstOrDefault(mesh => mesh.NativeTexture == texture)?.Texture ?? -1;
            if (importedTexture < 0 && value?.IsReplacement != true)
            {
                continue;
            }
            PixelImage image = importedTexture >= 0 ? imported!.Textures[importedTexture] : value!.Image;
            int offset = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(4 + texture * 4));
            TimData tim = ReadTim(bytes, offset);
            if (value is not null && importedTexture < 0 && (value.LogicalWidth != tim.Width || value.LogicalHeight != tim.Height))
            {
                throw new InvalidDataException("Native replacement material changed its original logical canvas.");
            }
            IReadOnlyList<TextureCopyVariant> eyeCopies = !weapon && importedTexture < 0 && value?.Variants is { } eyeVariants && eyeVariants.TryGetValue("eyes-closed", out PixelImage? closed) ? EyeCopies(model, binding, pack, texture, tim, value, image, closed) : [];
            HashSet<(ushort Page, ushort Clut, int U, int V)> materials = [];
            foreach (BattleFace face in (model.NativeControlMeshes ?? model.Meshes).SelectMany(mesh => mesh.Faces).Where(face => face.Texture == texture))
            {
                NativeBattleFaceMaterial original = face.NativeMaterial ?? throw new NotSupportedException("Native replacement sampling requires original polygon material fields.");
                PixelImage materialImage = importedTexture < 0 && value?.Variants is { } variants && variants.TryGetValue($"palette/{((original.Clut & 0x7FFF) >> 6) - tim.PaletteY}", out PixelImage? variant) ? variant : image;
                NativeBattleFaceMaterial material = NativeBattleFaceMaterialBinding.Apply507550(original, binding);
                int u = checked((int)(material.Uv[0].X - face.Uv[0].X));
                int v = checked((int)(material.Uv[0].Y - face.Uv[0].Y));
                if (u < 0 || v < 0 || u + tim.Width > 256 || v + tim.Height > 256)
                {
                    throw new NotSupportedException("Native replacement material leaves its original texture page.");
                }
                ushort page = (ushort)(material.TexturePage & 0x19F);
                ushort clut = (ushort)(material.Clut & 0x7FFF);
                if (materials.Add((page, clut, u, v)))
                {
                    result.Add(new(page, clut, u, v, tim.Width, tim.Height, materialImage, tim.WordWidth, tim.Words, Palette(tim, original.Clut)));
                    if (ReferenceEquals(materialImage, image))
                    {
                        foreach (TextureCopyVariant eyeCopy in eyeCopies)
                        {
                            result.Add(new(page, clut, u, v, tim.Width, tim.Height, eyeCopy.Image, tim.WordWidth, eyeCopy.Words, Palette(tim, original.Clut)));
                        }
                    }
                }
            }
        }
        return result;
    }

    private sealed record TextureCopyVariant(PixelImage Image, ImmutableArray<byte> Words);

    private static readonly ConditionalWeakTable<BattleModel, ModelEyeCopyCache> eyeCopyCaches = new();

    private sealed class ModelEyeCopyCache
    {
        public ConditionalWeakTable<TextureValue, ValueEyeCopyCache> Values { get; } = new();
    }

    private sealed class ValueEyeCopyCache
    {
        public ConditionalWeakTable<NativeBattleModelTextureBinding, BindingEyeCopyCache> Bindings { get; } = new();
    }

    private sealed class BindingEyeCopyCache
    {
        public ConcurrentDictionary<int, Lazy<IReadOnlyList<TextureCopyVariant>>> Textures { get; } = new();
    }

    private static IReadOnlyList<TextureCopyVariant> EyeCopies(BattleModel model, NativeBattleModelTextureBinding binding, NativeBattleEffectHeapAllocation pack, int texture, TimData tim, TextureValue value, PixelImage open, PixelImage closed)
    {
        ModelEyeCopyCache modelCache = eyeCopyCaches.GetValue(model, static _ => new());
        ValueEyeCopyCache valueCache = modelCache.Values.GetValue(value, static _ => new());
        BindingEyeCopyCache bindingCache = valueCache.Bindings.GetValue(binding, static _ => new());
        return bindingCache.Textures.GetOrAdd(texture, _ => new(() => BuildEyeCopies(model, binding, pack, texture, tim, open, closed))).Value;
    }

    private static IReadOnlyList<TextureCopyVariant> BuildEyeCopies(BattleModel model, NativeBattleModelTextureBinding binding, NativeBattleEffectHeapAllocation pack, int texture, TimData tim, PixelImage open, PixelImage closed)
    {
        if (model.NativeTextureAnimations is not { IsDefaultOrEmpty: false } motion)
        {
            return [];
        }
        if (closed.Width != open.Width || closed.Height != open.Height || open.Width <= 0 || open.Height <= 0)
        {
            throw new InvalidDataException("Native closed-eye material must retain its open material's physical canvas.");
        }
        ushort mask = 0;
        foreach (byte? slot in binding.PaletteRows)
        {
            if (slot is byte allocated)
            {
                if (allocated >= 12)
                {
                    return [];
                }
                mask |= (ushort)(1 << allocated);
            }
        }
        NativeBattleModelTextureLoad load = NativeBattleModelTextures.Prepare507400(pack, unchecked((ushort)~mask));
        if (!load.Binding.PaletteRows.SequenceEqual(binding.PaletteRows))
        {
            return [];
        }
        TimData[] materials = load.TimOffsets.Select(offset => ReadTim(load.Source.Data.AsSpan(), offset)).ToArray();
        NativeBattleTextureCopy? openCopy = NativeBattleTextureMotion.Plan50C780(motion, mask, 0, 0);
        NativeBattleTextureCopy? closedCopy = NativeBattleTextureMotion.Plan50C780(motion, mask, 0, 1);
        if (openCopy is null || closedCopy is null || openCopy.Width <= 0 || openCopy.Height <= 0 || closedCopy.Width <= 0 || closedCopy.Height <= 0 || openCopy.DestinationX != closedCopy.DestinationX || openCopy.DestinationY != closedCopy.DestinationY || openCopy.Width != closedCopy.Width || openCopy.Height != closedCopy.Height || !Contains(materials[texture], openCopy.DestinationX, openCopy.DestinationY, openCopy.Width, openCopy.Height) || !Contains(materials[texture], closedCopy.DestinationX, closedCopy.DestinationY, closedCopy.Width, closedCopy.Height))
        {
            return [];
        }
        TimData destination = materials[texture];
        ImmutableArray<byte>? openWords = CopyWords(openCopy);
        ImmutableArray<byte>? closedWords = CopyWords(closedCopy);
        if (openWords is null || closedWords is null || openWords.Value.AsSpan().SequenceEqual(closedWords.Value.AsSpan()))
        {
            return [];
        }
        int pixelsPerWord = tim.Width / tim.WordWidth;
        int x = (closedCopy.DestinationX - destination.X) * pixelsPerWord;
        int y = closedCopy.DestinationY - destination.Y;
        int width = closedCopy.Width * pixelsPerWord;
        int height = closedCopy.Height;
        if ((long)x * open.Width % tim.Width != 0 || (long)width * open.Width % tim.Width != 0 || (long)y * open.Height % tim.Height != 0 || (long)height * open.Height % tim.Height != 0)
        {
            throw new NotSupportedException("Native eye-copy rectangle must map to whole replacement pixels.");
        }
        int imageX = x * open.Width / tim.Width;
        int imageY = y * open.Height / tim.Height;
        int imageWidth = width * open.Width / tim.Width;
        int imageHeight = height * open.Height / tim.Height;
        PixelImage composed = new(open.Width, open.Height, open.Pixels.ToArray());
        for (int row = 0; row < imageHeight; row++)
        {
            closed.Pixels.AsSpan((imageY + row) * closed.Width + imageX, imageWidth).CopyTo(composed.Pixels.AsSpan((imageY + row) * composed.Width + imageX, imageWidth));
        }
        List<TextureCopyVariant> result = [];
        if (!openWords.Value.AsSpan().SequenceEqual(tim.Words.AsSpan()))
        {
            result.Add(new(open, openWords.Value));
        }
        result.Add(new(composed, closedWords.Value));
        return result;

        ImmutableArray<byte>? CopyWords(NativeBattleTextureCopy copy)
        {
            TimData[] sources = materials.Where(material => Contains(material, copy.SourceX, copy.SourceY, copy.Width, copy.Height)).ToArray();
            if (sources.Length != 1 || sources[0].Width / sources[0].WordWidth != destination.Width / destination.WordWidth)
            {
                return null;
            }
            TimData source = sources[0];
            byte[] words = tim.Words.ToArray();
            int sourceX = copy.SourceX - source.X;
            int sourceY = copy.SourceY - source.Y;
            int destinationX = copy.DestinationX - destination.X;
            int destinationY = copy.DestinationY - destination.Y;
            for (int row = 0; row < copy.Height; row++)
            {
                source.Words.AsSpan().Slice(((sourceY + row) * source.WordWidth + sourceX) * 2, copy.Width * 2).CopyTo(words.AsSpan(((destinationY + row) * tim.WordWidth + destinationX) * 2, copy.Width * 2));
            }
            return words.ToImmutableArray();
        }

        static bool Contains(TimData material, int x, int y, int width, int height) => x >= material.X && y >= material.Y && x + width <= material.X + material.WordWidth && y + height <= material.Y + material.Height;
    }

    public static IReadOnlyList<NativeBattleTextureReplacement> Bind(BattleStage stage)
    {
        if (stage.Textures is not ResourceTextureList textures || stage.NativeSource is null)
        {
            return [];
        }
        TimData tim = ReadTim(stage.NativeSource.Texture.Data.AsSpan(), 0);
        List<NativeBattleTextureReplacement> result = [];
        HashSet<(int Texture, ushort Page, ushort Clut)> materials = [];
        foreach (BattleFace face in stage.Meshes.SelectMany(mesh => mesh.Faces))
        {
            TextureValue value = textures.GetTexture(face.Texture);
            NativeBattleFaceMaterial material = face.NativeMaterial ?? throw new NotSupportedException("Native stage replacement sampling requires original polygon material fields.");
            ushort page = (ushort)(material.TexturePage & 0x19F);
            ushort clut = (ushort)(material.Clut & 0x7FFF);
            bool nativePage = stage.NativePages.TryGetValue((page, clut), out AssetHandle<TextureValue>? pageHandle) && pageHandle.Value.IsReplacement;
            if (nativePage)
            {
                value = pageHandle!.Value;
            }
            if (!value.IsReplacement || !materials.Add((nativePage ? -1 : face.Texture, page, nativePage ? (ushort)0 : clut)))
            {
                continue;
            }
            if (!nativePage && (value.LogicalWidth != tim.Width || value.LogicalHeight != tim.Height))
            {
                throw new InvalidDataException("Native stage replacement changed its original logical canvas.");
            }
            int pixelsPerWord = 4 >> ((page >> 7) & 3);
            int sourceX = ((page & 15) * 64 - tim.X) * pixelsPerWord;
            int sourceY = (page & 16) * 16 - tim.Y;
            int u = Math.Max(0, -sourceX);
            int v = Math.Max(0, -sourceY);
            int width = Math.Min(256 - u, tim.Width - Math.Max(0, sourceX));
            int height = Math.Min(256 - v, tim.Height - Math.Max(0, sourceY));
            if (width <= 0 || height <= 0)
            {
                continue;
            }
            sourceX += u;
            sourceY += v;
            int wordWidth = width / pixelsPerWord;
            byte[] words = new byte[wordWidth * height * 2];
            for (int row = 0; row < height; row++)
            {
                tim.Words.AsSpan().Slice(((sourceY + row) * tim.WordWidth + sourceX / pixelsPerWord) * 2, wordWidth * 2).CopyTo(words.AsSpan(row * wordWidth * 2));
            }
            result.Add(new(page, clut, u, v, width, height, value.Image, wordWidth, words.ToImmutableArray(), Palette(tim, clut))
            {
                SourceOwner = nativePage ? $"battle/a0stg{stage.Id:000}.x" : null,
                Animation = nativePage ? value.Animation : null,
                ImageSourceX = nativePage ? u : sourceX,
                ImageSourceY = nativePage ? v : sourceY,
                ImageCanvasWidth = nativePage ? 256 : tim.Width,
                ImageCanvasHeight = nativePage ? 256 : tim.Height
            });
        }
        return result;
    }

    private sealed record TimData(int X, int Y, int WordWidth, int Width, int Height, int PaletteX, int PaletteY, int PaletteWidth, int PaletteSize, ImmutableArray<byte> Words, ImmutableArray<byte> Palettes);

    private static TimData ReadTim(ReadOnlySpan<byte> bytes, int offset)
    {
        int flags = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset + 4));
        int depth = flags & 7;
        if (depth > 2)
        {
            throw new NotSupportedException("Native battle replacement requires a four-bit, eight-bit or direct sixteen-bit TIM.");
        }
        int paletteLength = (flags & 8) != 0 ? BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset + 8)) : 0;
        int imageOffset = offset + 8 + paletteLength;
        int imageLength = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(imageOffset));
        int wordWidth = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(imageOffset + 8));
        return new(BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(imageOffset + 4)), BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(imageOffset + 6)), wordWidth, wordWidth * (4 >> depth), BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(imageOffset + 10)), paletteLength == 0 ? 0 : BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(offset + 12)), paletteLength == 0 ? 0 : BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(offset + 14)), paletteLength == 0 ? 0 : BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(offset + 16)), depth == 0 ? 16 : depth == 1 ? 256 : 0, bytes.Slice(imageOffset + 12, imageLength - 12).ToArray().ToImmutableArray(), paletteLength == 0 ? [] : bytes.Slice(offset + 20, paletteLength - 12).ToArray().ToImmutableArray());
    }

    private static ImmutableArray<byte> Palette(TimData tim, ushort clut)
    {
        if (tim.PaletteSize == 0)
        {
            return [];
        }
        int x = (clut & 63) * 16 - tim.PaletteX;
        int y = ((clut & 0x7FFF) >> 6) - tim.PaletteY;
        if (x < 0 || x + tim.PaletteSize > tim.PaletteWidth || y < 0 || (y * tim.PaletteWidth + x + tim.PaletteSize) * 2 > tim.Palettes.Length)
        {
            throw new InvalidDataException("Native replacement material CLUT leaves its original TIM palette.");
        }
        return tim.Palettes.AsSpan().Slice((y * tim.PaletteWidth + x) * 2, tim.PaletteSize * 2).ToArray().ToImmutableArray();
    }
}
