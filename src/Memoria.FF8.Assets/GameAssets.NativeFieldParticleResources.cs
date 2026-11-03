using System.Buffers.Binary;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets;

public sealed class NativeFieldParticleResources
{
    public NativeFieldParticleResources(byte[] definition, byte[] texture)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(texture);
        if (definition.Length < 10016 || definition.Length > 24356)
        {
            throw new InvalidDataException("The native field particle definition requires sixteen type and sixteen emitter records.");
        }
        if (texture.Length < 516)
        {
            throw new InvalidDataException("The native field particle texture has no complete palette.");
        }
        int firstRow = BinaryPrimitives.ReadInt16LittleEndian(texture);
        if (firstRow < 0 || firstRow > 256 || (256 - firstRow) % 2 != 0 || texture.Length != 516 + 128 * (256 - firstRow))
        {
            throw new InvalidDataException("The native field particle texture has an invalid upload extent.");
        }
        Definition = definition.ToArray();
        Texture = texture.ToArray();
        TextureFirstRow = firstRow;
    }

    public byte[] Definition { get; }
    public byte[] Texture { get; }
    public int TextureFirstRow { get; }

    public PixelImage DecodeSprite(int u, int v, int width, int height)
    {
        if ((uint)u > 255 || (uint)v > 255 || width < 1 || width > 256 || height < 1 || height > 256)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Particle texture coordinates require their byte domain and a positive page-bounded size.");
        }
        PixelImage image = new(width, height);
        for (int y = 0; y < height; y++)
        {
            int sourceY = (v + y) & 255;
            if (sourceY < TextureFirstRow)
            {
                continue;
            }
            for (int x = 0; x < width; x++)
            {
                int sourceX = (u + x) & 255;
                byte packed = Texture[516 + (sourceY - TextureFirstRow) * 128 + sourceX / 2];
                int index = (packed >> (4 * (sourceX & 1))) & 15;
                ushort color = BinaryPrimitives.ReadUInt16LittleEndian(Texture.AsSpan(4 + 2 * index, 2));
                image[x, y] = Rgba32.FromPsxBgr555(color);
            }
        }
        return image;
    }
}

public sealed partial class GameAssets
{
    public NativeFieldParticleResources? ReadNativeFieldParticleResources(int fieldId)
    {
        if ((uint)fieldId >= fieldNames.Length || string.IsNullOrWhiteSpace(fieldNames[fieldId]))
        {
            throw new ArgumentOutOfRangeException(nameof(fieldId));
        }
        string name = fieldNames[fieldId];
        byte[] definition = ReadResource(new ResourceKey($"field/{name}/{name}.pmd"));
        if (definition.Length == 4)
        {
            return null;
        }
        return new NativeFieldParticleResources(definition, ReadResource(new ResourceKey($"field/{name}/{name}.pmp")));
    }
}
