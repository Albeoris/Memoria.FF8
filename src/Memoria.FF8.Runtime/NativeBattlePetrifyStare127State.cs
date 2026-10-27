using System.Buffers.Binary;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattlePetrifyStare127Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattlePetrifyStare127State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattlePetrifyStare127State([property: JsonRequired] NativeBattleEffectArena Globals)
{
    public const uint BaseAddress = 0x2521270;
    public const int Length = 0x50;

    public static NativeBattlePetrifyStare127State FromColdProcess() => new(NativeBattleEffectArena.Clear());
    public static NativeBattlePetrifyStare127State FromUnknown() => new(NativeBattleEffectArena.Clear().MarkUnknown(0, Length));

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
    public NativeBattlePetrifyStare127State WriteDword(uint address, uint value)
    {
        Span<byte> encoded = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(encoded, value);
        Globals.Write(Require(address, 4), encoded);
        return this;
    }

    private static int Require(uint address, int length)
    {
        if (address < BaseAddress || length < 0 || (ulong)address + (uint)length > BaseAddress + Length || address < 0x252129C && (ulong)address + (uint)length > 0x2521288 || address < 0x25212C0 && (ulong)address + (uint)length > 0x25212A0)
        {
            throw new InvalidDataException("Petrify Stare globals exclude the original task bank and descriptors.");
        }
        return checked((int)(address - BaseAddress));
    }
}
