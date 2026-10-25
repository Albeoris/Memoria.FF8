using System.Buffers.Binary;
using System.Collections.Immutable;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed record NativeBattleTextureReplacement(ushort Page, ushort Clut, int U, int V, int Width, int Height, PixelImage Image, int WordWidth, ImmutableArray<byte> PixelWords, ImmutableArray<byte> PaletteWords)
{
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
                }
            }
        }
        return result;
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
            if (!value.IsReplacement)
            {
                continue;
            }
            NativeBattleFaceMaterial material = face.NativeMaterial ?? throw new NotSupportedException("Native stage replacement sampling requires original polygon material fields.");
            ushort page = (ushort)(material.TexturePage & 0x19F);
            ushort clut = (ushort)(material.Clut & 0x7FFF);
            if (!materials.Add((face.Texture, page, clut)))
            {
                continue;
            }
            if (value.LogicalWidth != tim.Width || value.LogicalHeight != tim.Height)
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
            result.Add(new(page, clut, u, v, width, height, value.Image, wordWidth, words.ToImmutableArray(), Palette(tim, clut)) { ImageSourceX = sourceX, ImageSourceY = sourceY, ImageCanvasWidth = tim.Width, ImageCanvasHeight = tim.Height });
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
