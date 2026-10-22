using System.Buffers.Binary;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleElectrocute275Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleElectrocute275State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleElectrocute275State([property: JsonRequired] NativeBattleEffectArena Globals, [property: JsonRequired] uint? FlashThreshold, [property: JsonRequired] bool ColdSourcesPending)
{
    public const uint BaseAddress = 0x2508348;
    public const int Length = 0xC8;
    public static NativeBattleElectrocute275State FromColdProcess() => new(NativeBattleEffectArena.Clear(), null, true);
    public static NativeBattleElectrocute275State FromUnknown() => new(NativeBattleEffectArena.Clear().MarkUnknown(0, Length), null, false);
    public void Validate()
    {
        Globals.Validate();
        foreach (NativeBattleEffectMemoryRegion region in Globals.Regions)
        {
            Require(checked(BaseAddress + (uint)region.Offset), region.Data.Length);
        }
        if (Globals.UnknownRegions.Any(region => region.Offset < 0 || region.Length < 0 || region.Offset > Length - region.Length))
        {
            throw new InvalidDataException("Electrocute retained globals leave their original scalar owner.");
        }
    }
    public NativeBattleElectrocute275State SeedColdSources(NativeBattleElectrocute275Resources resources)
    {
        if (!ColdSourcesPending)
        {
            return this;
        }
        if (FlashThreshold.HasValue)
        {
            throw new InvalidDataException("Electrocute cold source seed must not overwrite an existing mutable threshold.");
        }
        return this with { FlashThreshold = resources.InitialFlashThreshold, ColdSourcesPending = false };
    }
    public uint ReadFlashThreshold() => FlashThreshold ?? throw new NotSupportedException("Electrocute requires its retained mutable flash threshold.");
    public uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Globals.Read(Require(address, 4), 4));
    public ushort ReadWord(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(Globals.Read(Require(address, 2), 2));
    public NativeBattleElectrocute275State WriteDword(uint address, uint value) => this with { Globals = Globals.Write(Require(address, 4), BitConverter.GetBytes(value)) };
    public NativeBattleElectrocute275State WriteWord(uint address, ushort value) => this with { Globals = Globals.Write(Require(address, 2), BitConverter.GetBytes(value)) };
    public NativeBattleEffectArena ReadView(uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(Globals, Require(address, length), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    public NativeBattleElectrocute275State WriteView(uint address, NativeBattleEffectArena source, int length) => this with { Globals = NativeBattleEffectNodeContextMemory.Copy(source, 0, Globals, Require(address, length), length) };
    private static int Require(uint address, int length)
    {
        if (address < BaseAddress || length < 0 || (ulong)address + (uint)length > BaseAddress + Length || address < 0x2508374 && (ulong)address + (uint)length > 0x2508360 || address < 0x2508408 && (ulong)address + (uint)length > 0x25083A8)
        {
            throw new InvalidDataException("Electrocute globals exclude the original task bank and descriptors.");
        }
        return checked((int)(address - BaseAddress));
    }
}
