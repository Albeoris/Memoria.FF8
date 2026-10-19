using System.Buffers.Binary;
using System.Collections.Immutable;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed record NativeBattleTextureReplacement(ushort Page, ushort Clut, int U, int V, int Width, int Height, PixelImage Image, int WordWidth, ImmutableArray<byte> PixelWords, ImmutableArray<byte> PaletteWords);

public static class NativeBattleTextureReplacements
{
    public static IReadOnlyList<NativeBattleTextureReplacement> Bind(BattleModel model, NativeBattleModelTextureBinding binding, bool weapon = false)
    {
        if (model.Textures is not ResourceTextureList textures)
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
        if (count != textures.Count)
        {
            throw new InvalidDataException("Native replacement materials disagree with their original counted TIM pack.");
        }
        List<NativeBattleTextureReplacement> result = [];
        for (int texture = 0; texture < count; texture++)
        {
            TextureValue value = textures.GetTexture(texture);
            if (!value.Provenance.StartsWith("remastered:", StringComparison.Ordinal))
            {
                continue;
            }
            if (value.Image.Width == value.LogicalWidth && value.Image.Height == value.LogicalHeight)
            {
                continue;
            }
            if (value.Image.Width != value.LogicalWidth * 3 || value.Image.Height != value.LogicalHeight * 3)
            {
                throw new NotSupportedException("Native battle replacement sampling requires the supported threefold material scale.");
            }
            int offset = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(4 + texture * 4));
            int flags = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset + 4));
            if ((flags & 15) != 9)
            {
                throw new NotSupportedException("Native battle replacement sampling requires its original indexed eight-bit TIM.");
            }
            int paletteLength = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset + 8));
            int imageOffset = offset + 8 + paletteLength;
            int imageLength = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(imageOffset));
            int wordWidth = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(imageOffset + 8));
            ImmutableArray<byte> words = bytes.Slice(imageOffset + 12, imageLength - 12).ToArray().ToImmutableArray();
            ImmutableArray<byte> palette = bytes.Slice(offset + 20, paletteLength - 12).ToArray().ToImmutableArray();
            HashSet<(ushort Page, ushort Clut, int U, int V)> materials = [];
            foreach (BattleFace face in model.Meshes.SelectMany(mesh => mesh.Faces).Where(face => face.Texture == texture))
            {
                NativeBattleFaceMaterial material = NativeBattleFaceMaterialBinding.Apply507550(face.NativeMaterial ?? throw new NotSupportedException("Native replacement sampling requires original polygon material fields."), binding);
                int u = checked((int)(material.Uv[0].X - face.Uv[0].X));
                int v = checked((int)(material.Uv[0].Y - face.Uv[0].Y));
                if (u < 0 || v < 0 || u + value.LogicalWidth > 256 || v + value.LogicalHeight > 256)
                {
                    throw new NotSupportedException("Native replacement material leaves its original texture page.");
                }
                ushort page = (ushort)(material.TexturePage & 0x19F);
                ushort clut = (ushort)(material.Clut & 0x7FFF);
                if (materials.Add((page, clut, u, v)))
                {
                    result.Add(new(page, clut, u, v, value.LogicalWidth, value.LogicalHeight, value.Image, wordWidth, words, palette));
                }
            }
        }
        return result;
    }
}
