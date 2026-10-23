using System.Buffers.Binary;
using System.Text.Json.Serialization;
using static Memoria.FF8.Runtime.NativeBattlePoisonGas171Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattlePoisonGas171State([property: JsonRequired] NativeBattleEffectArena Globals, [property: JsonRequired] NativeBattleEffectArena? RootBankStorage, [property: JsonRequired] NativeBattleEffectArena? ChildBankStorage)
{
    private static readonly (uint Address, int Length)[] GlobalRanges = [(FirstTargetGlobal, 4), (ParticleABase, ParticleBankBytes * 2), (ParametersGlobal, 20)];

    public static NativeBattlePoisonGas171State FromColdProcess() => new(NativeBattleEffectArena.Clear(), NativeBattleEffectArena.Clear(), NativeBattleEffectArena.Clear());
    public static NativeBattlePoisonGas171State FromUnknown()
    {
        NativeBattleEffectArena globals = NativeBattleEffectArena.Clear();
        foreach ((uint address, int length) in GlobalRanges)
        {
            globals = globals.MarkUnknown(checked((int)(address - BaseAddress)), length);
        }
        return new(globals, NativeBattleEffectArena.Clear().MarkUnknown(0, RootBankLength), NativeBattleEffectArena.Clear().MarkUnknown(0, ChildBankLength));
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
        if (RootBankStorage is { } root)
        {
            ValidateBounded(root, RootBankLength);
        }
        if (ChildBankStorage is { } child)
        {
            ValidateBounded(child, ChildBankLength);
        }
    }

    public byte[] Read(uint address, int length) => Globals.Read(Require(address, length), length);
    public byte[] ReadBytes(uint address, int length) => Read(address, length);
    public uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));
    public ushort ReadWord(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(Read(address, 2));
    public NativeBattlePoisonGas171State Write(uint address, ReadOnlySpan<byte> bytes) => this with { Globals = Globals.Write(Require(address, bytes.Length), bytes) };
    public NativeBattlePoisonGas171State WriteBytes(uint address, ReadOnlySpan<byte> bytes) => Write(address, bytes);
    public NativeBattlePoisonGas171State WriteDword(uint address, uint value) => Write(address, BitConverter.GetBytes(value));
    public NativeBattlePoisonGas171State WriteWord(uint address, ushort value) => Write(address, BitConverter.GetBytes(value));
    public NativeBattleEffectArena ReadView(uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(Globals, Require(address, length), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    public NativeBattlePoisonGas171State WriteView(uint address, NativeBattleEffectArena source, int length) => this with { Globals = NativeBattleEffectNodeContextMemory.Copy(source, 0, Globals, Require(address, length), length) };

    private static int Require(uint address, int length)
    {
        ulong end = (ulong)address + (uint)Math.Max(length, 0);
        if (length < 0 || !GlobalRanges.Any(range => address >= range.Address && end <= (ulong)range.Address + (uint)range.Length))
        {
            throw new InvalidDataException("PoisonGas171 carrier bytes exclude its physical task banks, descriptors, COMMON packets, unused gaps.");
        }
        return checked((int)(address - BaseAddress));
    }
    private static void ValidateBounded(NativeBattleEffectArena arena, int length)
    {
        arena.Validate();
        if (arena.Regions.Any(region => region.Offset < 0 || region.Offset > length - region.Data.Length) || arena.UnknownRegions.Any(region => region.Offset < 0 || region.Length < 0 || region.Offset > length - region.Length))
        {
            throw new InvalidDataException("PoisonGas171 retained bank bytes leave their single physical owner.");
        }
    }
}
