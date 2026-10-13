using System.Security.Cryptography;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeAppearanceInitialization
{
    public static NativeCharacterAppearance Read(byte[] initial)
    {
        if (initial.Length != 2812)
        {
            throw new InvalidDataException("Original New Game appearance template structure is unsupported.");
        }
        NativeCharacterDress[] records = new NativeCharacterDress[8];
        for (int index = 0; index < records.Length; index++)
        {
            ReadOnlySpan<byte> record = initial.AsSpan(1088 + index * 152, 152);
            records[index] = new NativeCharacterDress(record[8], record[91]);
        }
        NativeCharacterAppearance state = new() { Version = 1, Rule = NativeCharacterAppearance.InitializationRule, InitOutSha256 = Convert.ToHexString(SHA256.HashData(initial)), Records = records };
        state.Validate();
        return state;
    }
}
