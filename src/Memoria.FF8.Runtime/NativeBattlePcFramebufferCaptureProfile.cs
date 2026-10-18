using System.Numerics;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattlePcFramebufferFormat([property: JsonRequired] ushort RedMask, [property: JsonRequired] ushort GreenMask, [property: JsonRequired] ushort BlueMask, [property: JsonRequired] ushort AlphaMask)
{
    public static NativeBattlePcFramebufferFormat Rgb555() => new(0x7C00, 0x03E0, 0x001F, 0);

    public void Validate()
    {
        uint used = 0;
        foreach (ushort mask in new[] { RedMask, GreenMask, BlueMask, AlphaMask })
        {
            if ((used & mask) != 0 || mask != 0 && !Contiguous(mask))
            {
                throw new InvalidDataException("Native PC framebuffer requires disjoint contiguous WORD channel masks.");
            }
            used |= mask;
        }
        if (RedMask == 0 || GreenMask == 0 || BlueMask == 0)
        {
            throw new InvalidDataException("Native PC framebuffer requires all three direct RGB masks.");
        }
    }

    public uint DecodeArgb(ushort pixel)
    {
        Validate();
        return DecodePixel(pixel);
    }

    internal uint DecodePixel(ushort pixel)
    {
        uint alpha = AlphaMask == 0 ? 255u : Expand(pixel, AlphaMask);
        return alpha << 24 | Expand(pixel, RedMask) << 16 | Expand(pixel, GreenMask) << 8 | Expand(pixel, BlueMask);
    }

    public ushort EncodeArgb418AC4(uint argb)
    {
        Validate();
        return EncodePixel(argb);
    }

    internal ushort EncodePixel(uint argb)
    {
        uint pixel = Pack(argb >> 16 & 255, RedMask, true) | Pack(argb >> 8 & 255, GreenMask, true) | Pack(argb & 255, BlueMask, true);
        if (AlphaMask != 0)
        {
            pixel |= Pack(argb >> 24, AlphaMask, false);
        }
        return unchecked((ushort)pixel);
    }

    private static bool Contiguous(uint mask)
    {
        uint shifted = mask >> BitOperations.TrailingZeroCount(mask);
        return (shifted & (shifted + 1)) == 0;
    }

    private static uint Expand(ushort pixel, ushort mask)
    {
        int shift = BitOperations.TrailingZeroCount((uint)mask);
        uint maximum = (uint)mask >> shift;
        uint value = ((uint)pixel & mask) >> shift;
        return value * 255 / maximum;
    }

    private static uint Pack(uint value, ushort mask, bool round)
    {
        int shift = BitOperations.TrailingZeroCount((uint)mask);
        uint maximum = (uint)mask >> shift;
        uint scaled = value * maximum;
        uint channel = scaled / 255;
        if (round && scaled % 255 > 127)
        {
            channel++;
        }
        return channel << shift;
    }
}

public sealed record NativeBattlePcFramebufferBlend([property: JsonRequired] uint Source, [property: JsonRequired] uint Destination);

public sealed record NativeBattlePcFramebufferCaptureProfile([property: JsonRequired] NativeBattlePcFramebufferFormat Format, [property: JsonRequired] uint SourceBlendCapabilities, [property: JsonRequired] uint DestinationBlendCapabilities, [property: JsonRequired] uint LegacyCapabilities350, [property: JsonRequired] bool OverrideTextureMapBlend, [property: JsonRequired] bool OverrideBlend, [property: JsonRequired] NativeBattlePcFramebufferBlend? BlendOverride, [property: JsonRequired] bool LinearFiltering)
{
    [JsonRequired] public int SurfaceWidth { get; init; }
    [JsonRequired] public int SurfaceHeight { get; init; }

    public static NativeBattlePcFramebufferCaptureProfile PrimaryRgba8() => new(NativeBattlePcFramebufferFormat.Rgb555(), 0x10, 0x20, 1, false, false, null, false) { SurfaceWidth = 640, SurfaceHeight = 480 };

    public void Validate()
    {
        if (Format is null || OverrideBlend && BlendOverride is null || SurfaceWidth is < 1 or > 8192 || SurfaceHeight is < 1 or > 8192)
        {
            throw new InvalidDataException("Native PC capture requires its current device format and active blend override owner.");
        }
        Format.Validate();
    }

    public NativeBattlePcFramebufferCaptureMaterial ResolveMaterial()
    {
        Validate();
        if ((SourceBlendCapabilities & 0x10) == 0 || (LegacyCapabilities350 & 1) == 0)
        {
            throw new NotSupportedException("Native PC capture mode zero has no supported current driver blend entry.");
        }
        uint destination;
        if ((DestinationBlendCapabilities & 0x20) != 0)
        {
            destination = 6;
        }
        else if ((DestinationBlendCapabilities & 0x10) != 0)
        {
            destination = 5;
        }
        else
        {
            throw new NotSupportedException("Native PC capture mode zero lacks both destination blend candidates.");
        }
        NativeBattlePcFramebufferBlend blend = OverrideBlend ? BlendOverride! : new(5, destination);
        return new(blend, OverrideTextureMapBlend ? 1u : 2u, 128, Format.AlphaMask != 0, LinearFiltering);
    }
}

public sealed record NativeBattlePcFramebufferCaptureMaterial([property: JsonRequired] NativeBattlePcFramebufferBlend Blend, [property: JsonRequired] uint TextureMapBlend, [property: JsonRequired] byte ImageMaterialAlpha, [property: JsonRequired] bool TextureHasAlpha, [property: JsonRequired] bool LinearFiltering)
{
    public uint ModulateSourceArgb(uint textureArgb, uint diffuseArgb)
    {
        if (TextureMapBlend != 2)
        {
            throw new NotSupportedException("Native PC capture source evaluation requires the explicitly selected legacy MODULATE mode.");
        }
        uint alpha = TextureHasAlpha ? textureArgb >> 24 : diffuseArgb >> 24;
        uint red = (textureArgb >> 16 & 255) * (diffuseArgb >> 16 & 255) / 255;
        uint green = (textureArgb >> 8 & 255) * (diffuseArgb >> 8 & 255) / 255;
        uint blue = (textureArgb & 255) * (diffuseArgb & 255) / 255;
        return alpha << 24 | red << 16 | green << 8 | blue;
    }
}
