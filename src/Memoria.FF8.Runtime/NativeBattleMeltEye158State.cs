using System.Buffers.Binary;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleMeltEye158State([property: JsonRequired] NativeBattleEffectArena Globals, [property: JsonRequired] NativeBattleEffectArena Packets)
{
    public const uint BaseAddress = 0x23D7B08;
    public const int Length = 0x13AB8;
    public static NativeBattleMeltEye158State FromColdProcess() => new(NativeBattleEffectArena.Clear(), NativeBattleEffectArena.Clear());
    public static NativeBattleMeltEye158State FromUnknown() => new(NativeBattleEffectArena.Clear().MarkUnknown(0, Length), NativeBattleEffectArena.Clear().MarkUnknown(0, NativeBattleMeltEye158Profile.PacketBytes));
    public void Validate()
    {
        Globals.Validate();
        Packets.Validate();
        foreach (NativeBattleEffectMemoryRegion region in Globals.Regions)
        {
            Require(checked(BaseAddress + (uint)region.Offset), region.Data.Length);
        }
        if (Globals.UnknownRegions.Any(region => region.Offset < 0 || region.Length < 0 || region.Offset > Length - region.Length) || Packets.Regions.Any(region => region.Offset < 0 || region.Offset > NativeBattleMeltEye158Profile.PacketBytes - region.Data.Length) || Packets.UnknownRegions.Any(region => region.Offset < 0 || region.Length < 0 || region.Offset > NativeBattleMeltEye158Profile.PacketBytes - region.Length))
        {
            throw new InvalidDataException("Melt-Eye retained memory leaves its actual scalar or fixed packet owner.");
        }
    }
    public uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));
    public ushort ReadWord(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(Read(address, 2));
    public byte[] Read(uint address, int length) => Globals.Read(Require(address, length), length);
    public NativeBattleMeltEye158State WriteDword(uint address, uint value) => Write(address, BitConverter.GetBytes(value));
    public NativeBattleMeltEye158State WriteWord(uint address, ushort value) => Write(address, BitConverter.GetBytes(value));
    public NativeBattleMeltEye158State Write(uint address, ReadOnlySpan<byte> bytes) => this with { Globals = Globals.Write(Require(address, bytes.Length), bytes) };
    public NativeBattleEffectArena ReadView(uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(Globals, Require(address, length), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    public NativeBattleMeltEye158State WriteView(uint address, NativeBattleEffectArena source, int length) => this with { Globals = NativeBattleEffectNodeContextMemory.Copy(source, 0, Globals, Require(address, length), length) };
    public NativeBattleResourcePointer? ReadResourcePointer(uint address) => Globals.ReadResourcePointer(Require(address, 4));
    public NativeBattleMeltEye158State WriteResourcePointer(uint address, NativeBattleResourcePointer? pointer) => this with { Globals = Globals.WriteResourcePointer(Require(address, 4), pointer) };
    public byte[] ReadPacket(uint address, int length) => Packets.Read(PacketOffset(address, length), length);
    public NativeBattleMeltEye158State WritePacket(uint address, ReadOnlySpan<byte> bytes) => this with { Packets = Packets.Write(PacketOffset(address, bytes.Length), bytes) };
    public NativeBattleMeltEye158State ClearSlotHeads()
    {
        NativeBattleMeltEye158State state = this;
        foreach ((uint bank, int count) in new[] { (NativeBattleMeltEye158Profile.SlotBankB, NativeBattleMeltEye158Profile.SlotCountB), (NativeBattleMeltEye158Profile.SlotBankA, NativeBattleMeltEye158Profile.SlotCountA) })
        {
            for (int index = 0; index < count; index++)
            {
                state = state.WriteDword(bank + (uint)(index * NativeBattleMeltEye158Profile.SlotStride), 0);
            }
        }
        return state;
    }
    private static int Require(uint address, int length)
    {
        ulong end = (ulong)address + (uint)Math.Max(0, length);
        bool allowed = address >= 0x23D7B08 && end <= 0x23D7B0C || address >= 0x23D7B30 && end <= 0x23D8C10 || address >= 0x23D9A30 && end <= 0x23DB3F8 || address >= 0x23DB5B0 && end <= 0x23DB5BC || address >= 0x23EB5BC && end <= 0x23EB5C0;
        if (length < 0 || !allowed)
        {
            throw new InvalidDataException("Melt-Eye globals require their exact scalar, slot or curve owner and exclude physical pools and packet bytes.");
        }
        return checked((int)(address - BaseAddress));
    }
    private static int PacketOffset(uint address, int length)
    {
        if (address < NativeBattleMeltEye158Profile.PacketBase || length < 0 || (ulong)address + (uint)length > NativeBattleMeltEye158Profile.PacketBase + NativeBattleMeltEye158Profile.PacketBytes)
        {
            throw new InvalidDataException("Melt-Eye packet access leaves its original fixed allocation.");
        }
        return checked((int)(address - NativeBattleMeltEye158Profile.PacketBase));
    }
}
