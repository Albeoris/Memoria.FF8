using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Albeoris.Games.FF8.Imaging.Workspace.Internal;

internal static class OpenRasterBinaryPayload
{
    private const Int32 Width = 1024;
    private const Int32 LengthSize = sizeof(Int32);
    private const Int32 HashSize = 32;
    private static ReadOnlySpan<Byte> Magic => "ALBFF8D1"u8;
    private static Int32 HeaderSize => Magic.Length + LengthSize + HashSize;

    public static PixelImage Encode(ReadOnlySpan<Byte> content)
    {
        Int32 byteCount = checked(HeaderSize + content.Length);
        Int32 pixelCount = checked((byteCount + 2) / 3);
        Int32 height = checked((pixelCount + Width - 1) / Width);
        PixelImage image = new(Width, height);
        Byte[] payload = new Byte[checked(image.Pixels.Length * 3)];
        Magic.CopyTo(payload);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(Magic.Length), content.Length);
        SHA256.HashData(content, payload.AsSpan(Magic.Length + LengthSize, HashSize));
        content.CopyTo(payload.AsSpan(HeaderSize));
        for (Int32 index = 0; index < image.Pixels.Length; index++)
            image.Pixels[index] = new Rgba32(payload[index * 3], payload[index * 3 + 1], payload[index * 3 + 2]);
        return image;
    }

    public static Byte[] Decode(PixelImage image, String description)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Byte[] payload = new Byte[checked(image.Pixels.Length * 3)];
        for (Int32 index = 0; index < image.Pixels.Length; index++)
        {
            payload[index * 3] = image.Pixels[index].R;
            payload[index * 3 + 1] = image.Pixels[index].G;
            payload[index * 3 + 2] = image.Pixels[index].B;
        }
        if (payload.Length < HeaderSize || !payload.AsSpan(0, Magic.Length).SequenceEqual(Magic))
            throw new InvalidDataException($"The {description} layer does not contain an Albeoris FF8 binary payload.");
        Int32 length = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(Magic.Length, LengthSize));
        if (length < 0 || length > payload.Length - HeaderSize)
            throw new InvalidDataException($"The {description} layer contains an invalid payload length.");
        ReadOnlySpan<Byte> content = payload.AsSpan(HeaderSize, length);
        ReadOnlySpan<Byte> expectedHash = payload.AsSpan(Magic.Length + LengthSize, HashSize);
        Span<Byte> actualHash = stackalloc Byte[HashSize];
        SHA256.HashData(content, actualHash);
        if (!actualHash.SequenceEqual(expectedHash))
            throw new InvalidDataException($"The {description} layer payload is damaged.");
        return content.ToArray();
    }
}
