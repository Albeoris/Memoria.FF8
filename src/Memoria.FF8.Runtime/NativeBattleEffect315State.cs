using System.Buffers.Binary;
using System.Text.Json.Serialization;
using static Memoria.FF8.Runtime.NativeBattleEffect315Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect315State([property: JsonRequired] NativeBattleEffectArena Globals, [property: JsonRequired] NativeBattleEffectArena ParticleStorage, [property: JsonRequired] NativeBattleEffectArena? RootBankStorage, [property: JsonRequired] NativeBattleEffectArena? ChildBankStorage)
{
    private static readonly (uint Address, int Length)[] GlobalRanges = [(TargetGlobal, 4), (ParametersGlobal, 24)];

    public static NativeBattleEffect315State FromColdProcess() => new(NativeBattleEffectArena.Clear(), NativeBattleEffectArena.Clear(), NativeBattleEffectArena.Clear(), NativeBattleEffectArena.Clear());

    public static NativeBattleEffect315State FromUnknown()
    {
        NativeBattleEffectArena globals = NativeBattleEffectArena.Clear();
        foreach ((uint address, int length) in GlobalRanges)
        {
            globals = globals.MarkUnknown(checked((int)(address - BaseAddress)), length);
        }
        return new(globals, NativeBattleEffectArena.Clear().MarkUnknown(0, ParticleLength), NativeBattleEffectArena.Clear().MarkUnknown(0, RootBankLength), NativeBattleEffectArena.Clear().MarkUnknown(0, ChildBankLength));
    }

    public void Validate()
    {
        Globals.Validate();
        foreach (NativeBattleEffectMemoryRegion region in Globals.Regions)
        {
            RequireGlobal(checked(BaseAddress + (uint)region.Offset), region.Data.Length);
        }
        foreach (NativeBattleEffectUnknownRegion region in Globals.UnknownRegions)
        {
            RequireGlobal(checked(BaseAddress + (uint)region.Offset), region.Length);
        }
        ValidateBank(ParticleStorage, ParticleLength);
        if (RootBankStorage is { } root)
        {
            ValidateBank(root, RootBankLength);
        }
        if (ChildBankStorage is { } children)
        {
            ValidateBank(children, ChildBankLength);
        }
    }

    public byte[] Read(uint address, int length) => ReadView(address, length).Read(0, length);
    public byte[] ReadBytes(uint address, int length) => Read(address, length);
    public ushort ReadWord(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(Read(address, 2));
    public uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));
    public NativeBattleEffect315State Write(uint address, ReadOnlySpan<byte> bytes) => WriteView(address, NativeBattleEffectArena.Clear().Write(0, bytes), bytes.Length);
    public NativeBattleEffect315State WriteBytes(uint address, ReadOnlySpan<byte> bytes) => Write(address, bytes);
    public NativeBattleEffect315State WriteWord(uint address, ushort value) => Write(address, BitConverter.GetBytes(value));
    public NativeBattleEffect315State WriteDword(uint address, uint value) => Write(address, BitConverter.GetBytes(value));

    public NativeBattleEffectArena ReadView(uint address, int length)
    {
        (NativeBattleEffectArena bank, int offset, int _) = Owner(address, length);
        return NativeBattleEffectNodeContextMemory.Copy(bank, offset, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    }

    public NativeBattleEffect315State WriteView(uint address, NativeBattleEffectArena source, int length)
    {
        (NativeBattleEffectArena bank, int offset, int owner) = Owner(address, length);
        NativeBattleEffectArena result = NativeBattleEffectNodeContextMemory.Copy(source, 0, bank, offset, length);
        return owner switch
        {
            0 => this with { Globals = result },
            1 => this with { ParticleStorage = result },
            2 => this with { RootBankStorage = result },
            _ => this with { ChildBankStorage = result }
        };
    }

    private (NativeBattleEffectArena Bank, int Offset, int Owner) Owner(uint address, int length)
    {
        if (Contains(ParticleAddress, ParticleLength, address, length))
        {
            return (ParticleStorage, checked((int)(address - ParticleAddress)), 1);
        }
        if (Contains(RootBank, RootBankLength, address, length))
        {
            return (RootBankStorage ?? throw new NotSupportedException("Effect315 root bytes belong to its active task pool."), checked((int)(address - RootBank)), 2);
        }
        if (Contains(ChildBank, ChildBankLength, address, length))
        {
            return (ChildBankStorage ?? throw new NotSupportedException("Effect315 child bytes belong to its active task pool."), checked((int)(address - ChildBank)), 3);
        }
        return (Globals, RequireGlobal(address, length), 0);
    }

    private static bool Contains(uint start, int size, uint address, int length) => length >= 0 && address >= start && (ulong)address + (uint)length <= (ulong)start + (uint)size;

    private static int RequireGlobal(uint address, int length)
    {
        if (!GlobalRanges.Any(range => Contains(range.Address, range.Length, address, length)))
        {
            throw new InvalidDataException("Effect315 scalar storage excludes descriptors, gaps and separately owned banks.");
        }
        return checked((int)(address - BaseAddress));
    }

    private static void ValidateBank(NativeBattleEffectArena bank, int length)
    {
        bank.Validate();
        if (bank.Regions.Any(region => region.Offset < 0 || region.Offset > length - region.Data.Length) || bank.UnknownRegions.Any(region => region.Offset < 0 || region.Length < 0 || region.Offset > length - region.Length))
        {
            throw new InvalidDataException("Effect315 retained bytes leave their physical bank.");
        }
    }
}
