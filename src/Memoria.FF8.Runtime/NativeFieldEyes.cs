using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Memoria.FF8.Runtime;

public sealed record NativeEyeTexture(int Depth, int WidthWords, int Height, byte[] Words);
public sealed record NativeEyeCopy(string ModelHash, string BankHash, string TextureHash, int SourceXWords, int SourceY, int DestinationXWords, int DestinationY, int WidthWords, int Height, byte[] Words);

/// <summary>531310/532A40 original MCH eye bank and texture0 VRAM words; coordinates are relative to its allocation base.</summary>
public sealed record NativeFieldEyeDefinition(string ModelHash, int ModelOffset, int BankOffset, byte[] Bank, NativeEyeTexture? Texture)
{
    public NativeEyeCopy? Copy(bool closed)
    {
        if (Bank.Length == 0)
        {
            return null;
        }
        int frameCount = Bank.Length > 5 ? Bank[5] : 0;
        return CopyFrame(closed ? 1 : frameCount);
    }

    public NativeEyeCopy CopyFrame(int frame)
    {
        if (Bank.Length < 16 || Bank[2] != 8 || Texture is not { Depth: 1 } texture || Bank[3] % 2 != 0)
        {
            throw new NotSupportedException("Native eye bank requires the proved 8bpp texture0 copy contract.");
        }
        if (frame < 1 || frame > Bank[5] || 8 + 4 * Bank[5] + 4 > Bank.Length)
        {
            throw new InvalidDataException("Native eye frame exceeds its original table.");
        }
        int source = 8 + 4 * frame;
        int Word(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(Bank.AsSpan(offset));
        int destinationX = Word(6) / 2;
        int destinationY = Word(8);
        int width = Bank[3] / 2;
        int height = Bank[4];
        int sourceX = Word(source) / 2;
        int sourceY = Word(source + 2);
        bool Inside(int x, int y) => x >= 0 && y >= 0 && width > 0 && height > 0 && x + width <= texture.WidthWords && y + height <= texture.Height;
        bool overlaps = sourceX < destinationX + width && sourceX + width > destinationX && sourceY < destinationY + height && sourceY + height > destinationY;
        if (texture.Words.Length != checked(texture.WidthWords * texture.Height * 2) || !Inside(destinationX, destinationY) || !Inside(sourceX, sourceY) || overlaps)
        {
            throw new NotSupportedException("Native eye copy requires in-bounds source banks disjoint from its mutable destination.");
        }
        byte[] words = new byte[checked(width * height * 2)];
        for (int row = 0; row < height; row++)
        {
            texture.Words.AsSpan(((sourceY + row) * texture.WidthWords + sourceX) * 2, width * 2).CopyTo(words.AsSpan(row * width * 2));
        }
        return new NativeEyeCopy(ModelHash, Convert.ToHexString(SHA256.HashData(Bank)), Convert.ToHexString(SHA256.HashData(texture.Words)), sourceX, sourceY, destinationX, destinationY, width, height, words);
    }
}
