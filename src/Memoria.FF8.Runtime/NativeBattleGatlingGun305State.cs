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
        foreach (NativeBattleEffectMemoryRegion region in Globals.Regions)
        {
            Require(checked(BaseAddress + (uint)region.Offset), region.Data.Length);
        }
        if (Globals.UnknownRegions.Any(region => region.Offset < 0 || region.Length < 0 || region.Offset > Length - region.Length) || Packets.Regions.Any(region => region.Offset < 0 || region.Offset > NativeBattleGatlingGun305Profile.PacketBytes - region.Data.Length) || Packets.UnknownRegions.Any(region => region.Offset < 0 || region.Length < 0 || region.Offset > NativeBattleGatlingGun305Profile.PacketBytes - region.Length))
        {
            throw new InvalidDataException("Gatling Gun retained memory leaves its actual scalar or fixed packet owner.");
        }
    }
    public uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));
    public ushort ReadWord(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(Read(address, 2));
    public byte[] Read(uint address, int length) => Globals.Read(Require(address, length), length);
    public NativeBattleGatlingGun305State WriteDword(uint address, uint value) => Write(address, BitConverter.GetBytes(value));
    public NativeBattleGatlingGun305State WriteWord(uint address, ushort value) => Write(address, BitConverter.GetBytes(value));
    public NativeBattleGatlingGun305State Write(uint address, ReadOnlySpan<byte> bytes) => this with { Globals = Globals.Write(Require(address, bytes.Length), bytes) };
    public NativeBattleEffectArena ReadView(uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(Globals, Require(address, length), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    public NativeBattleGatlingGun305State WriteView(uint address, NativeBattleEffectArena source, int length) => this with { Globals = NativeBattleEffectNodeContextMemory.Copy(source, 0, Globals, Require(address, length), length) };
    public NativeBattleResourcePointer? ReadResourcePointer(uint address) => Globals.ReadResourcePointer(Require(address, 4));
    public NativeBattleGatlingGun305State WriteResourcePointer(uint address, NativeBattleResourcePointer? pointer) => this with { Globals = Globals.WriteResourcePointer(Require(address, 4), pointer) };
    public byte[] ReadPacket(uint address, int length) => Packets.Read(PacketOffset(address, length), length);
    public NativeBattleGatlingGun305State WritePacket(uint address, ReadOnlySpan<byte> bytes) => this with { Packets = Packets.Write(PacketOffset(address, bytes.Length), bytes) };
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
