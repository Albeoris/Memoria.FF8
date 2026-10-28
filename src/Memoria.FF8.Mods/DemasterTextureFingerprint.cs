using System.IO.Hashing;

namespace Memoria.FF8.Mods;

public readonly record struct DemasterTextureFingerprint(ulong High64, ulong Low64)
{
    private const long Seed = 0x85EBCA77;

    public static bool TryCalculate(ReadOnlySpan<byte> uploadBytes, int width, int height, int bytesPerPixel, int nativeMode, out DemasterTextureFingerprint fingerprint)
    {
        fingerprint = default;
        if (width <= 0 || height <= 0 || width >= 1024 || height >= 1024 || bytesPerPixel is not (3 or 4))
        {
            return false;
        }

        int uploadLength = width * height * bytesPerPixel;
        if (uploadBytes.Length < uploadLength)
        {
            return false;
        }

        int hashLength = nativeMode == 5 && width == 256 && height == 256 ? 256 * 192 * bytesPerPixel : uploadLength;
        UInt128 hash = XxHash128.HashToUInt128(uploadBytes[..hashLength], Seed);
        fingerprint = new DemasterTextureFingerprint((ulong)(hash >> 64), (ulong)hash);
        return true;
    }
}
