using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>The retained 256 by256 WORD capture texture; an unsuccessful lock leaves its previous bytes intact.</summary>
public sealed record NativeBattlePcFramebufferCaptureState([property: JsonRequired] ImmutableArray<ushort>? Pixels)
{
    public const int Size = 256;
    public const int PixelCount = Size * Size;

    public static NativeBattlePcFramebufferCaptureState FromColdProcess() => new((ImmutableArray<ushort>?)null);

    public void Validate()
    {
        if (Pixels is { } pixels && (pixels.IsDefault || pixels.Length != PixelCount))
        {
            throw new InvalidDataException("Native PC capture texture requires exactly256 by256 retained WORD pixels.");
        }
    }

    public ImmutableArray<ushort> RequirePixels()
    {
        Validate();
        return Pixels ?? throw new NotSupportedException("Native PC capture draw lacks initialized or retained texture bytes after the surface lock failed.");
    }

    public NativeBattlePcFramebufferCaptureState Capture56D530(ReadOnlySpan<byte> source, int pitchBytes, NativeBattlePcRenderViewport viewport, bool locksSucceeded)
    {
        Validate();
        if (!locksSucceeded)
        {
            return this;
        }
        ArgumentNullException.ThrowIfNull(viewport);
        ushort[] pixels = new ushort[PixelCount];
        int xStep = unchecked(viewport.Width << 16) / Size;
        int yStep = unchecked(viewport.Height << 16) / Size;
        int y = unchecked(viewport.Y << 16);
        for (int row = 0; row < Size; row++)
        {
            int rowOffset = unchecked((y >> 16) * pitchBytes);
            int x = unchecked(viewport.X << 16);
            for (int column = 0; column < Size; column++)
            {
                int offset = unchecked(rowOffset + (x >> 16) * 2);
                if (offset < 0 || offset > source.Length - 2)
                {
                    throw new InvalidDataException("Native PC capture sampled outside the supplied locked WORD surface.");
                }
                pixels[row * Size + column] = BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(offset, 2));
                x = unchecked(x + xStep);
            }
            y = unchecked(y + yStep);
        }
        return new(pixels.ToImmutableArray());
    }

    public ImmutableArray<uint> DecodeArgb(NativeBattlePcFramebufferCaptureProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        profile.Validate();
        ImmutableArray<ushort> pixels = RequirePixels();
        ImmutableArray<uint>.Builder result = ImmutableArray.CreateBuilder<uint>(PixelCount);
        foreach (ushort pixel in pixels)
        {
            result.Add(profile.Format.DecodePixel(pixel));
        }
        return result.MoveToImmutable();
    }

    /// <summary>The portable RGBA target is explicitly quantized using the current418AC4 direct WORD format before capture.</summary>
    public static byte[] EncodeArgbSurface(ReadOnlySpan<uint> source, NativeBattlePcFramebufferCaptureProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        profile.Validate();
        byte[] result = new byte[checked(source.Length * 2)];
        for (int index = 0; index < source.Length; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(index * 2, 2), profile.Format.EncodePixel(source[index]));
        }
        return result;
    }
}
