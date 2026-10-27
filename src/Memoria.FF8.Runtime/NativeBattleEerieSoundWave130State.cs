using System.Buffers.Binary;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEerieSoundWave130Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleEerieSoundWave130State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleEerieSoundWave130State([property: JsonRequired] NativeBattleEffectArena Globals)
{
    public const uint BaseAddress = 0x25210A8;
    public const int Length = 0x68;

    public static NativeBattleEerieSoundWave130State FromColdProcess() => new(NativeBattleEffectArena.Clear());
    public static NativeBattleEerieSoundWave130State FromUnknown() => new(NativeBattleEffectArena.Clear().MarkUnknown(0, Length));

    public void Validate()
    {
        Globals.Validate();
        foreach (NativeBattleEffectStorageRange region in Globals.EnumerateDataRanges())
        {
            Require(checked(BaseAddress + (uint)region.Offset), region.Length);
        }
        Globals.ValidateExtent(Length);
    }

    public uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Globals.Read(Require(address, 4), 4));
    public NativeBattleEerieSoundWave130State WriteDword(uint address, uint value)
    {
        Span<byte> encoded = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(encoded, value);
        Globals.Write(Require(address, 4), encoded);
        return this;
    }

    private static int Require(uint address, int length)
    {
        if (address < BaseAddress || length < 0 || (ulong)address + (uint)length > BaseAddress + Length || address < 0x25210CC && (ulong)address + (uint)length > 0x25210B8 || address < 0x2521110 && (ulong)address + (uint)length > 0x25210D0)
        {
            throw new InvalidDataException("Eerie Sound Wave globals exclude the original task bank and descriptors.");
        }
        return checked((int)(address - BaseAddress));
    }
}
