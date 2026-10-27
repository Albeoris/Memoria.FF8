using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattlePain34State([property: JsonRequired] NativeBattleEffectArena Globals)
{
    public const uint Address = 0x26BA9E0;
    public const int Length = 0x59A0;
    public static NativeBattlePain34State FromColdProcess() => new(NativeBattleEffectArena.Clear());
    public void Validate()
    {
        Globals.Validate();
        Globals.ValidateExtent(Length);
    }
    public byte[] Read(uint address, int length) => Globals.Read(Offset(address, length), length);
    public uint Dword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));
    public NativeBattlePain34State Write(uint address, ReadOnlySpan<byte> bytes)
    {
        Globals.Write(Offset(address, bytes.Length), bytes);
        return this;
    }
    public NativeBattlePain34State WriteDword(uint address, uint value)
    {
        Span<byte> encoded = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(encoded, value);
        return Write(address, encoded);
    }
    private static int Offset(uint address, int length)
    {
        if (address < Address || length < 0 || length > Length || (ulong)address - Address > (uint)(Length - length))
        {
            throw new InvalidDataException("Native Pain read or store leaves its physical module range.");
        }
        return checked((int)(address - Address));
    }
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattlePain34State? Pain34 { get; init; }
}

public sealed record NativeBattlePain34Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeBattleFileStaging Staging)
{
    public NativeBattleStackBytes? PlayerQueueStack { get; init; }
}
public sealed record NativeBattlePain34Resources(NativeBattleGfAnimationSource Glow, NativeBattleGfAnimationSource Model, NativeBattleEffectHeapAllocation Camera, uint Sound)
{
    public NativeBattleGfAnimationSource Program(uint address) => address switch
    {
        0x15E7040 => Glow,
        0x15EA2E8 => Model,
        _ => throw new NotSupportedException("Native Pain player requires its selected original animation source.")
    };
}
