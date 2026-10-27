using System.Buffers.Binary;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleAcid317Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleAcid317State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleAcid317State([property: JsonRequired] NativeBattleEffectArena Globals)
{
    public const uint BaseAddress = 0x2505490;
    public const int Length = 0x90;
    public static NativeBattleAcid317State FromColdProcess() => new(NativeBattleEffectArena.Clear());
    public static NativeBattleAcid317State FromUnknown() => new(NativeBattleEffectArena.Clear().MarkUnknown(0, Length));
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
    public ushort ReadWord(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(Globals.Read(Require(address, 2), 2));
    public NativeBattleAcid317State WriteDword(uint address, uint value)
    {
        Span<byte> encoded = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(encoded, value);
        Globals.Write(Require(address, 4), encoded);
        return this;
    }
    public NativeBattleAcid317State WriteWord(uint address, ushort value)
    {
        Span<byte> encoded = stackalloc byte[2];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(encoded, value);
        Globals.Write(Require(address, 2), encoded);
        return this;
    }
    public NativeBattleEffectArena ReadView(uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(Globals, Require(address, length), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    public NativeBattleAcid317State WriteView(uint address, NativeBattleEffectArena source, int length)
    {
        NativeBattleEffectNodeContextMemory.Copy(source, 0, Globals, Require(address, length), length);
        return this;
    }
    private static int Require(uint address, int length)
    {
        if (address < BaseAddress || length < 0 || (ulong)address + (uint)length > BaseAddress + Length || address < 0x25054B4 && (ulong)address + (uint)length > 0x25054A0 || address < 0x2505520 && (ulong)address + (uint)length > 0x25054E0)
        {
            throw new InvalidDataException("Acid globals exclude the original task bank and descriptors.");
        }
        return checked((int)(address - BaseAddress));
    }
}
