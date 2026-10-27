using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeInputInitialization
{
    public static NativeInputConfiguration Read(byte[] initial)
    {
        NativeInitialData.Validate(initial);
        //56DA10 copies init.out to1CFDCA8; options1CFE73C is2708 bytes after that base.
        NativeInputConfiguration configuration = new(Convert.ToHexString(SHA256.HashData(initial)), BinaryPrimitives.ReadUInt32LittleEndian(initial.AsSpan(2708, 4)), ImmutableArray.CreateRange(initial.AsSpan(2712, 12).ToArray())) { BattleMessageSpeed = initial[2705], NativeBattleSpeedByte = initial[2704], FieldMessageSpeedByte = initial[2706] };
        configuration.Validate();
        return configuration;
    }
}
