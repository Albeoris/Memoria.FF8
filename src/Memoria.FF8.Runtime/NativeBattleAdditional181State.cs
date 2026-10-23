using System.Buffers.Binary;
using System.Text.Json.Serialization;
using static Memoria.FF8.Runtime.NativeBattleAdditional181Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleAdditional181State([property: JsonRequired] NativeBattleEffectArena Globals, [property: JsonRequired] NativeBattleEffectArena? RootBankStorage, [property: JsonRequired] NativeBattleEffectArena? ChildBankStorage)
{
    private static readonly (uint Address, int Length)[] GlobalRanges = [(FirstTargetGlobal, 4), (ParticleBase, ParticleBankBytes), (ParametersGlobal, 20)];

    public static NativeBattleAdditional181State FromColdProcess() => new(NativeBattleEffectArena.Clear(), NativeBattleEffectArena.Clear(), NativeBattleEffectArena.Clear());
    public static NativeBattleAdditional181State FromUnknown()
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
    public NativeBattleAdditional181State Write(uint address, ReadOnlySpan<byte> bytes) => this with { Globals = Globals.Write(Require(address, bytes.Length), bytes) };
    public NativeBattleAdditional181State WriteBytes(uint address, ReadOnlySpan<byte> bytes) => Write(address, bytes);
    public NativeBattleAdditional181State WriteDword(uint address, uint value) => Write(address, BitConverter.GetBytes(value));
    public NativeBattleAdditional181State WriteWord(uint address, ushort value) => Write(address, BitConverter.GetBytes(value));
    public NativeBattleEffectArena ReadView(uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(Globals, Require(address, length), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    public NativeBattleAdditional181State WriteView(uint address, NativeBattleEffectArena source, int length) => this with { Globals = NativeBattleEffectNodeContextMemory.Copy(source, 0, Globals, Require(address, length), length) };

    private static int Require(uint address, int length)
    {
        ulong end = (ulong)address + (uint)Math.Max(length, 0);
        if (length < 0 || !GlobalRanges.Any(range => address >= range.Address && end <= (ulong)range.Address + (uint)range.Length))
        {
            throw new InvalidDataException("Additional181 carrier bytes exclude its physical task banks, descriptors, COMMON packets, unused gaps.");
        }
        return checked((int)(address - BaseAddress));
    }
    private static void ValidateBounded(NativeBattleEffectArena arena, int length)
    {
        arena.Validate();
        if (arena.Regions.Any(region => region.Offset < 0 || region.Offset > length - region.Data.Length) || arena.UnknownRegions.Any(region => region.Offset < 0 || region.Length < 0 || region.Offset > length - region.Length))
        {
            throw new InvalidDataException("Additional181 retained bank bytes leave their single physical owner.");
        }
    }
}
