using System.Buffers.Binary;
using Albeoris.Games.FF8.Imaging.Tex;
using Albeoris.Games.FF8.NamedicBin;
using Albeoris.Games.FF8.TextEncoding;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets;

/// <summary>Native menu sprite directories and the texture canvases reached by their consumers.</summary>
public static class NativeMenuArtworkDataReader
{
    public static void ValidateTexture(ResourceKey key, byte[] bytes)
    {
        TexFile texture = TexFile.Read(bytes);
        string name = Path.GetFileName(key.Path);
        if (name == "icon.tex")
        {
            if (texture.Header.Width != 256 || texture.Header.Height != 256 || texture.Header.BytesPerPixel != 1 || texture.Palettes.Count != 16)
            {
                throw new InvalidDataException("Native menu icons require an indexed 256 by 256 texture with sixteen palettes.");
            }
        }
        else if (name is "face_b00.tex" or "face_b01.tex" or "gf_big00.tex" or "gf_big01.tex")
        {
            if (texture.Header.Width != 256 || texture.Header.Height != 256 || texture.Palettes.Count != 0 || texture.Header.BytesPerPixel != 2 || texture.Header.RedMask != 0x1F || texture.Header.GreenMask != 0x3E0 || texture.Header.BlueMask != 0x7C00 || texture.Header.AlphaMask != 0x8000)
            {
                throw new InvalidDataException("Native PC portraits require a 256 by 256 PSX BGR555 direct-color texture.");
            }
        }
        else if (name == "magita.tex")
        {
            _ = NativeMagazineArtwork.GetPaperColor(bytes);
        }
        else if (name.Length == 8 && name.StartsWith("mc", StringComparison.Ordinal) && char.IsAsciiDigit(name[2]) && char.IsAsciiDigit(name[3]) || name.Length == 9 && name.StartsWith("mag", StringComparison.Ordinal) && char.IsAsciiDigit(name[3]) && char.IsAsciiDigit(name[4]))
        {
            if (texture.Header.Width != 256 || texture.Header.Height != 256)
            {
                throw new InvalidDataException("Native card and magazine atlases require their 256 by 256 canvas.");
            }
        }
        for (int palette = 0; palette < Math.Max(1, texture.Palettes.Count); palette++)
        {
            _ = texture.Decode(palette);
        }
    }

    public static void ValidateIconDirectory(byte[] bytes)
    {
        int count = ReadCount(bytes);
        if (count < 144)
        {
            throw new InvalidDataException("Native menu icons require the controller and direction glyph selector domain.");
        }
        for (int id = 0; id < count; id++)
        {
            int offset = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(4 + id * 4));
            int parts = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(6 + id * 4));
            if (parts == 0 || id == 0 && parts != 1 || offset < 4 + count * 4 || offset > bytes.Length || parts > (bytes.Length - offset) / 8)
            {
                throw new InvalidDataException("Native menu icon requires bounded eight-byte parts and a single-part pointer.");
            }
            for (int part = 0; part < parts; part++)
            {
                ReadOnlySpan<byte> row = bytes.AsSpan(offset + part * 8, 8);
                if (row[4] == 0 || row[6] == 0 || row[0] + row[4] > 256 || row[1] + row[6] > 256)
                {
                    throw new InvalidDataException("Native menu icon part leaves its 256 by 256 atlas.");
                }
            }
        }
    }

    public static void ValidateFaceDirectory(byte[] bytes)
    {
        int count = ReadCount(bytes);
        if (count < 48)
        {
            throw new InvalidDataException("Native menu portraits require the character, Angelo and sixteen GF selectors.");
        }
        for (int id = 0; id < count; id++)
        {
            uint rawOffset = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4 + id * 4));
            if (rawOffset < 4 + count * 4 || rawOffset > bytes.Length - 4)
            {
                throw new InvalidDataException("Native face sprite offset lies outside its directory.");
            }
            int offset = (int)rawOffset;
            uint parts = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset));
            if (parts > (bytes.Length - offset - 4) / 12)
            {
                throw new InvalidDataException("Native face sprite parts lie outside their resource.");
            }
            if (id < 11 || id == 15 || id is >= 32 and < 48)
            {
                if (parts != 1)
                {
                    throw new InvalidDataException("Native menu portraits require one sprite part per reached selector.");
                }
                ReadOnlySpan<byte> part = bytes.AsSpan(offset + 4, 12);
                if (BinaryPrimitives.ReadUInt16LittleEndian(part[4..]) != 32 || BinaryPrimitives.ReadUInt16LittleEndian(part[6..]) != 48 || part[0] % 32 != 0 || (part[1] & 127) is not (0 or 48))
                {
                    throw new InvalidDataException("Native portrait does not follow its 32 by 48 source cell contract.");
                }
            }
        }
    }

    public static void ValidateAreaNames(byte[] bytes, string language)
    {
        _ = NamedicBin.ReadStrings(bytes, NativeTextEncoding.Create(language));
    }

    private static int ReadCount(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length < 4)
        {
            throw new InvalidDataException("Native sprite directory lacks its count.");
        }
        uint count = BinaryPrimitives.ReadUInt32LittleEndian(bytes);
        if (count > (bytes.Length - 4) / 4)
        {
            throw new InvalidDataException("Native sprite directory entries leave their resource.");
        }
        return (int)count;
    }
}
