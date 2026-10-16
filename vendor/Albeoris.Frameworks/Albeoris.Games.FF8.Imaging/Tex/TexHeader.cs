using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Albeoris.Games.FF8.Imaging.Tex;

/// <summary>The native little-endian header of an FF8 TEX texture.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct TexHeader
{
    private const Int32 FieldCount = 60;
    private const Int32 DisabledColorKey = 0;
    private const Int32 FirstColorKeyValue = 1;

    public const Int32 Size = FieldCount * sizeof(Int32);

    public TexVersion Version;
    public Int32 Unknown01;
    private Int32 _colorKeyValue;
    public Int32 Unknown03;
    public Int32 Unknown04;
    public Int32 MinimumBitsPerColor;
    public Int32 MaximumBitsPerColor;
    public Int32 MinimumAlphaBits;
    public Int32 MaximumAlphaBits;
    public Int32 MinimumBitsPerPixel;
    public Int32 MaximumBitsPerPixel;
    public Int32 Unknown11;
    public Int32 PaletteCount;
    public Int32 ColorsPerPalette;
    public Int32 BitDepth;
    public Int32 Width;
    public Int32 Height;
    public Int32 Pitch;
    public Int32 Unknown18;
    public TexPaletteFlags PaletteFlags;
    public Int32 BitsPerIndex;
    public TexConversionFlags ConversionFlags;
    public Int32 PaletteSize;
    public Int32 RepeatedColorsPerPalette;
    public Int32 RuntimeData24;
    public Int32 BitsPerPixel;
    public Int32 BytesPerPixel;
    public Int32 RedBits;
    public Int32 GreenBits;
    public Int32 BlueBits;
    public Int32 AlphaBits;
    public Int32 RedMask;
    public Int32 GreenMask;
    public Int32 BlueMask;
    public Int32 AlphaMask;
    public Int32 RedShift;
    public Int32 GreenShift;
    public Int32 BlueShift;
    public Int32 AlphaShift;
    public Int32 RedBitsConstant;
    public Int32 GreenBitsConstant;
    public Int32 BlueBitsConstant;
    public Int32 AlphaBitsConstant;
    public Int32 RedMaximum;
    public Int32 GreenMaximum;
    public Int32 BlueMaximum;
    public Int32 AlphaMaximum;
    public TexColorKeyArrayFlags ColorKeyArrayFlags;
    public Int32 RuntimeData48;
    public Int32 ReferenceAlpha;
    public Int32 RuntimeData50;
    public Int32 Unknown51;
    public Int32 RuntimePaletteIndex;
    public Int32 RuntimeData53;
    public Int32 RuntimeData54;
    public Int32 Unknown55;
    public Int32 Unknown56;
    public Int32 Unknown57;
    public Int32 Unknown58;
    public Int32 Unknown59;

    public TexHeader() => Version = TexVersion.FinalFantasy8;

    public Int32? ColorKeyIndex
    {
        readonly get => _colorKeyValue == DisabledColorKey
            ? null
            : checked(_colorKeyValue - FirstColorKeyValue);
        set => _colorKeyValue = value is null
            ? DisabledColorKey
            : checked(value.Value + FirstColorKeyValue);
    }

    internal static TexHeader Read(ReadOnlySpan<Byte> content)
    {
        EnsureLittleEndianPlatform();
        if (content.Length < Size)
            throw new InvalidDataException($"The TEX file is {content.Length} bytes long; its header requires {Size}.");
        EnsureLayout();

        return MemoryMarshal.Read<TexHeader>(content);
    }

    internal readonly void Write(Span<Byte> destination)
    {
        EnsureLittleEndianPlatform();
        if (destination.Length < Size)
            throw new ArgumentException($"A TEX header requires {Size} bytes.", nameof(destination));
        EnsureLayout();

        MemoryMarshal.Write(destination, in this);
    }

    private static void EnsureLayout()
    {
        if (Unsafe.SizeOf<TexHeader>() != Size)
            throw new InvalidOperationException("The managed TEX header layout does not match the file format.");
    }

    private static void EnsureLittleEndianPlatform()
    {
        if (!BitConverter.IsLittleEndian)
            throw new PlatformNotSupportedException("Native TEX structures require a little-endian platform.");
    }
}
