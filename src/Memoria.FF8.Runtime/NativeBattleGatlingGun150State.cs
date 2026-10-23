using System.Buffers.Binary;
using System.Text.Json.Serialization;
using static Memoria.FF8.Runtime.NativeBattleGatlingGun150Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleGatlingGun150State([property: JsonRequired] NativeBattleEffectArena Globals, [property: JsonRequired] NativeBattleEffectArena Packets, [property: JsonRequired] NativeBattleEffectArena? RootBankStorage, [property: JsonRequired] NativeBattleEffectArena? ChildBankStorage)
{
    private static readonly (uint Address, int Length)[] GlobalRanges = [(FirstTargetGlobal, 4), (ParticleABase, ParticleBankBytes * 2), (ParametersGlobal, 12), (CasterPointGlobal, 8), (CursorGlobal, 4)];

    public static NativeBattleGatlingGun150State FromColdProcess() => new(NativeBattleEffectArena.Clear(), NativeBattleEffectArena.Clear(), NativeBattleEffectArena.Clear(), NativeBattleEffectArena.Clear());
    public static NativeBattleGatlingGun150State FromUnknown()
    {
        NativeBattleEffectArena globals = NativeBattleEffectArena.Clear();
        foreach ((uint address, int length) in GlobalRanges)
        {
            globals = globals.MarkUnknown(checked((int)(address - BaseAddress)), length);
        }
        return new(globals, NativeBattleEffectArena.Clear().MarkUnknown(0, PacketBytes), NativeBattleEffectArena.Clear().MarkUnknown(0, RootBankLength), NativeBattleEffectArena.Clear().MarkUnknown(0, ChildBankLength));
    }

    public void Validate()
    {
        Globals.Validate();
        foreach (NativeBattleEffectMemoryRegion region in Globals.Regions)
        {
            Require(checked(BaseAddress + (uint)region.Offset), region.Data.Length);
        }
        foreach (NativeBattleEffectUnknownRegion region in Globals.UnknownRegions)
        {
            Require(checked(BaseAddress + (uint)region.Offset), region.Length);
        }
        ValidateBounded(Packets, PacketBytes);
        if (RootBankStorage is { } root)
        {
            ValidateBounded(root, RootBankLength);
        }
        if (ChildBankStorage is { } child)
        {
            ValidateBounded(child, ChildBankLength);
        }
    }

    public byte[] Read(uint address, int length) => IsPacket(address, length) ? Packets.Read(checked((int)(address - PacketBase)), length) : Globals.Read(Require(address, length), length);
    public uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));
    public ushort ReadWord(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(Read(address, 2));
    public byte[] ReadPacket(uint address, int length)
    {
        if (!IsPacket(address, length))
        {
            throw new InvalidDataException("Gatling Gun packet bytes leave their original fixed allocation.");
        }
        return Packets.Read(checked((int)(address - PacketBase)), length);
    }
    public NativeBattleGatlingGun150State Write(uint address, ReadOnlySpan<byte> bytes) => IsPacket(address, bytes.Length) ? this with { Packets = Packets.Write(checked((int)(address - PacketBase)), bytes) } : this with { Globals = Globals.Write(Require(address, bytes.Length), bytes) };
    public NativeBattleGatlingGun150State WriteDword(uint address, uint value) => Write(address, BitConverter.GetBytes(value));
    public NativeBattleGatlingGun150State WriteWord(uint address, ushort value) => Write(address, BitConverter.GetBytes(value));
    public NativeBattleEffectArena ReadView(uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(IsPacket(address, length) ? Packets : Globals, IsPacket(address, length) ? checked((int)(address - PacketBase)) : Require(address, length), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    public NativeBattleGatlingGun150State WriteView(uint address, NativeBattleEffectArena source, int length) => IsPacket(address, length) ? this with { Packets = NativeBattleEffectNodeContextMemory.Copy(source, 0, Packets, checked((int)(address - PacketBase)), length) } : this with { Globals = NativeBattleEffectNodeContextMemory.Copy(source, 0, Globals, Require(address, length), length) };

    private static bool IsPacket(uint address, int length) => length >= 0 && address >= PacketBase && (ulong)address + (uint)length <= PacketBase + PacketBytes;
    private static int Require(uint address, int length)
    {
        ulong end = (ulong)address + (uint)Math.Max(length, 0);
        if (length < 0 || !GlobalRanges.Any(range => address >= range.Address && end <= (ulong)range.Address + (uint)range.Length))
        {
            throw new InvalidDataException("Gatling Gun carrier bytes exclude its physical task banks, descriptors and unused gaps.");
        }
        return checked((int)(address - BaseAddress));
    }
    private static void ValidateBounded(NativeBattleEffectArena arena, int length)
    {
        arena.Validate();
        if (arena.Regions.Any(region => region.Offset < 0 || region.Offset > length - region.Data.Length) || arena.UnknownRegions.Any(region => region.Offset < 0 || region.Length < 0 || region.Offset > length - region.Length))
        {
            throw new InvalidDataException("Gatling Gun retained bank bytes leave their single physical owner.");
        }
    }
}
