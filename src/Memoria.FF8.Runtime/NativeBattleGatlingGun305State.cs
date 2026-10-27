using System.Buffers.Binary;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleGatlingGun305Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleGatlingGun305State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleGatlingGun305State([property: JsonRequired] NativeBattleEffectArena Globals, [property: JsonRequired] NativeBattleEffectArena Packets)
{
    public const uint BaseAddress = 0x2245650;
    public const int Length = 0x12144;
    public static NativeBattleGatlingGun305State FromColdProcess() => new(NativeBattleEffectArena.Clear(), NativeBattleEffectArena.Clear());
    public static NativeBattleGatlingGun305State FromUnknown() => new(NativeBattleEffectArena.Clear().MarkUnknown(0, Length), NativeBattleEffectArena.Clear().MarkUnknown(0, NativeBattleGatlingGun305Profile.PacketBytes));
    public void Validate()
    {
        Globals.Validate();
        Packets.Validate();
        foreach (NativeBattleEffectStorageRange region in Globals.EnumerateDataRanges())
        {
            Require(checked(BaseAddress + (uint)region.Offset), region.Length);
        }
        if (Globals.EnumerateUnknownRanges().Any(region => region.Offset < 0 || region.Length < 0 || region.Offset > Length - region.Length) || Packets.EnumerateDataRanges().Any(region => region.Offset < 0 || region.Offset > NativeBattleGatlingGun305Profile.PacketBytes - region.Length) || Packets.EnumerateUnknownRanges().Any(region => region.Offset < 0 || region.Length < 0 || region.Offset > NativeBattleGatlingGun305Profile.PacketBytes - region.Length))
        {
            throw new InvalidDataException("Gatling Gun retained memory leaves its actual scalar or fixed packet owner.");
        }
    }
    public uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));
    public ushort ReadWord(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(Read(address, 2));
    public byte[] Read(uint address, int length) => Globals.Read(Require(address, length), length);
    public NativeBattleGatlingGun305State WriteDword(uint address, uint value)
    {
        Span<byte> encoded = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(encoded, value);
        return Write(address, encoded);
    }
    public NativeBattleGatlingGun305State WriteWord(uint address, ushort value)
    {
        Span<byte> encoded = stackalloc byte[2];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(encoded, value);
        return Write(address, encoded);
    }
    public NativeBattleGatlingGun305State Write(uint address, ReadOnlySpan<byte> bytes)
    {
        Globals.Write(Require(address, bytes.Length), bytes);
        return this;
    }
    public NativeBattleEffectArena ReadView(uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(Globals, Require(address, length), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    public NativeBattleGatlingGun305State WriteView(uint address, NativeBattleEffectArena source, int length)
    {
        NativeBattleEffectNodeContextMemory.Copy(source, 0, Globals, Require(address, length), length);
        return this;
    }
    public NativeBattleResourcePointer? ReadResourcePointer(uint address) => Globals.ReadResourcePointer(Require(address, 4));
    public NativeBattleGatlingGun305State WriteResourcePointer(uint address, NativeBattleResourcePointer? pointer)
    {
        Globals.WriteResourcePointer(Require(address, 4), pointer);
        return this;
    }
    public byte[] ReadPacket(uint address, int length) => Packets.Read(PacketOffset(address, length), length);
    public NativeBattleGatlingGun305State WritePacket(uint address, ReadOnlySpan<byte> bytes)
    {
        Packets.Write(PacketOffset(address, bytes.Length), bytes);
        return this;
    }
    public NativeBattleGatlingGun305State ClearSlotHeads()
    {
        NativeBattleGatlingGun305State state = this;
        foreach (uint bank in new[] { NativeBattleGatlingGun305Profile.SlotBankB, NativeBattleGatlingGun305Profile.SlotBankA })
        {
            for (int index = 0; index < NativeBattleGatlingGun305Profile.SlotCount; index++)
            {
                state = state.WriteDword(bank + (uint)(index * NativeBattleGatlingGun305Profile.SlotStride), 0);
            }
        }
        return state;
    }
    private static int Require(uint address, int length)
    {
        if (address < BaseAddress || length < 0 || (ulong)address + (uint)length > BaseAddress + Length || address < 0x2245678 && (ulong)address + (uint)length > 0x2245658 || address < 0x2247778 && (ulong)address + (uint)length > 0x2246958 || address < 0x2257790 && (ulong)address + (uint)length > 0x2247790)
        {
            throw new InvalidDataException("Gatling Gun globals exclude its actual task pools and fixed packet banks.");
        }
        return checked((int)(address - BaseAddress));
    }
    private static int PacketOffset(uint address, int length)
    {
        if (address < NativeBattleGatlingGun305Profile.PacketBase || length < 0 || (ulong)address + (uint)length > NativeBattleGatlingGun305Profile.PacketBase + NativeBattleGatlingGun305Profile.PacketBytes)
        {
            throw new InvalidDataException("Gatling Gun packet access leaves its original fixed allocation.");
        }
        return checked((int)(address - NativeBattleGatlingGun305Profile.PacketBase));
    }
}
