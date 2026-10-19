using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>The original indexed/direct texture words retained by a cached PC region before VRAM invalidation.</summary>
public sealed record NativeBattlePcTextureWindowPixels([property: JsonRequired] int X, [property: JsonRequired] int Y, [property: JsonRequired] int Width, [property: JsonRequired] int Height, [property: JsonRequired] ImmutableArray<byte> Data)
{
    private static readonly ConditionalWeakTable<NativeBattleEffectTextureState, ConcurrentDictionary<(int Row, NativeBattlePrimitiveTextureWindow Rectangle), NativeBattlePcTextureWindowPixels>> Captures = new();

    public void Validate()
    {
        if (X is < 0 or >= 1024 || Y is < 0 or >= 512 || Width is < 1 or > 256 || Height is < 1 or > 256 || Y + Height > 512 || 2 * ((Y + Height - 1) * 1024 + X + Width) > NativeBattleEffectArena.Length + NativeBattleEffectTextureState.PcPageReadTailLength || Data.IsDefault || Data.Length != 2 * Width * Height)
        {
            throw new InvalidDataException("Native cached texture words require their exact bounded physical VRAM rectangle and bytes.");
        }
    }

    public ushort ReadWord(int x, int y)
    {
        if (x < X || x >= X + Width || y < Y || y >= Y + Height)
        {
            throw new InvalidDataException("Native cached texture sample leaves its retained pixel-word rectangle.");
        }
        return BinaryPrimitives.ReadUInt16LittleEndian(Data.AsSpan(2 * ((y - Y) * Width + x - X), 2));
    }

    public void ValidateMapping(int row, NativeBattlePrimitiveTextureWindow rectangle)
    {
        Validate();
        int pixelsPerWord = 4 >> (row / 32);
        if (X != (row & 15) * 64 + rectangle.X / pixelsPerWord || Y != ((row & 31) >> 4) * 256 + rectangle.Y || Width != (rectangle.X % pixelsPerWord + rectangle.Width + pixelsPerWord - 1) / pixelsPerWord || Height != rectangle.Height)
        {
            throw new InvalidDataException("Native cached pixel words disagree with their page-depth row and crop.");
        }
    }

    public static NativeBattlePcTextureWindowPixels Capture(int row, NativeBattlePrimitiveTextureWindow rectangle, NativeBattleEffectTextureState textures)
    {
        ArgumentNullException.ThrowIfNull(rectangle);
        ArgumentNullException.ThrowIfNull(textures);
        ConcurrentDictionary<(int Row, NativeBattlePrimitiveTextureWindow Rectangle), NativeBattlePcTextureWindowPixels> captures = Captures.GetValue(textures, static _ => new());
        return captures.GetOrAdd((row, rectangle), key => CaptureUncached(key.Row, key.Rectangle, textures));
    }

    private static NativeBattlePcTextureWindowPixels CaptureUncached(int row, NativeBattlePrimitiveTextureWindow rectangle, NativeBattleEffectTextureState textures)
    {
        if (row is < 0 or >= 96)
        {
            throw new InvalidDataException("Native cached texture capture requires its actual page-depth row.");
        }
        int pixelsPerWord = 4 >> (row / 32);
        int x = (row & 15) * 64 + rectangle.X / pixelsPerWord;
        int y = ((row & 31) >> 4) * 256 + rectangle.Y;
        int width = (rectangle.X % pixelsPerWord + rectangle.Width + pixelsPerWord - 1) / pixelsPerWord;
        byte[] data = new byte[2 * width * rectangle.Height];
        NativeBattleEffectArena bank = new(textures.VramRegions);
        for (int line = 0; line < rectangle.Height; line++)
        {
            int address = 2 * ((y + line) * 1024 + x);
            int count = 2 * width;
            int known = Math.Min(count, NativeBattleEffectArena.Length - address);
            if (known > 0)
            {
                bank.Read(address, known).CopyTo(data, 2 * line * width);
            }
            if (known < count)
            {
                ImmutableArray<byte> tail = textures.VramReadTail ?? throw new NotSupportedException("Native full-page capture reaches its unobserved trailing read window; explicitly migrate the standalone state.");
                tail.AsSpan().Slice(address + known - NativeBattleEffectArena.Length, count - known).CopyTo(data.AsSpan(2 * line * width + known));
            }
        }
        NativeBattlePcTextureWindowPixels result = new(x, y, width, rectangle.Height, data.ToImmutableArray());
        result.Validate();
        return result;
    }
}
