using System.Buffers.Binary;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDegenerator306Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleDegenerator306State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleDegenerator306State([property: JsonRequired] NativeBattleEffectArena Globals)
{
    public const uint BaseAddress = 0x2242590;
    public const int Length = 0x30BC;
    public static NativeBattleDegenerator306State FromColdProcess() => new(NativeBattleEffectArena.Clear());
    public static NativeBattleDegenerator306State FromUnknown() => new(NativeBattleEffectArena.Clear().MarkUnknown(0, Length));
    public void Validate()
    {
        Globals.Validate();
        foreach (NativeBattleEffectStorageRange region in Globals.EnumerateDataRanges())
        {
            Require(checked(BaseAddress + (uint)region.Offset), region.Length);
        }
        Globals.ValidateExtent(Length);
    }
    public uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));
    public ushort ReadWord(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(Read(address, 2));
    public byte[] Read(uint address, int length) => Globals.Read(Require(address, length), length);
    public NativeBattleDegenerator306State WriteDword(uint address, uint value)
    {
        Span<byte> encoded = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(encoded, value);
        return Write(address, encoded);
    }
    public NativeBattleDegenerator306State WriteWord(uint address, ushort value)
    {
        Span<byte> encoded = stackalloc byte[2];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(encoded, value);
        return Write(address, encoded);
    }
    public NativeBattleDegenerator306State Write(uint address, ReadOnlySpan<byte> bytes)
    {
        Globals.Write(Require(address, bytes.Length), bytes);
        return this;
    }
    public NativeBattleEffectArena ReadView(uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(Globals, Require(address, length), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    public NativeBattleDegenerator306State WriteView(uint address, NativeBattleEffectArena source, int length)
    {
        NativeBattleEffectNodeContextMemory.Copy(source, 0, Globals, Require(address, length), length);
        return this;
    }
    public NativeBattleResourcePointer? ReadResourcePointer(uint address) => Globals.ReadResourcePointer(Require(address, 4));
    public NativeBattleDegenerator306State WriteResourcePointer(uint address, NativeBattleResourcePointer? pointer)
    {
        Globals.WriteResourcePointer(Require(address, 4), pointer);
        return this;
    }
    public NativeBattleDegenerator306State ClearSlotHeads()
    {
        NativeBattleDegenerator306State state = this;
        for (int index = 0; index < NativeBattleDegenerator306Profile.SlotCount; index++)
        {
            state = state.WriteDword(NativeBattleDegenerator306Profile.SlotBank + (uint)(index * NativeBattleDegenerator306Profile.SlotStride), 0);
        }
        return state;
    }
    private static int Require(uint address, int length)
    {
        if (address < BaseAddress || length < 0 || (ulong)address + (uint)length > BaseAddress + Length || address < 0x22425C8 && (ulong)address + (uint)length > 0x2242598 || address < 0x2244168 && (ulong)address + (uint)length > 0x2243348)
        {
            throw new InvalidDataException("Degenerator globals exclude its physical native task pools and descriptors.");
        }
        return checked((int)(address - BaseAddress));
    }
}
