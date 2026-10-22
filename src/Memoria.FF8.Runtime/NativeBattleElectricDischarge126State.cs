using System.Buffers.Binary;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleElectricDischarge126State([property: JsonRequired] NativeBattleEffectArena Globals, [property: JsonRequired] uint? FlashThreshold, [property: JsonRequired] bool? ColdSourcesPending)
{
    public const uint BaseAddress = 0x25212C0;
    public const int Length = 0xA8;
    public static NativeBattleElectricDischarge126State FromColdProcess() => new(NativeBattleEffectArena.Clear(), null, true);
    public static NativeBattleElectricDischarge126State FromUnknown() => new(NativeBattleEffectArena.Clear().MarkUnknown(0, Length), null, null);
    public void Validate()
    {
        Globals.Validate();
        foreach (NativeBattleEffectMemoryRegion region in Globals.Regions)
        {
            Require(checked(BaseAddress + (uint)region.Offset), region.Data.Length);
        }
        if (Globals.UnknownRegions.Any(region => region.Offset < 0 || region.Length < 0 || region.Offset > Length - region.Length))
        {
            throw new InvalidDataException("Electric Discharge retained globals leave their original scalar owner.");
        }
    }
    public NativeBattleElectricDischarge126State SeedColdSources(NativeBattleElectricDischarge126Resources resources)
    {
        if (ColdSourcesPending is null)
        {
            throw new NotSupportedException("Electric Discharge requires its observed mutable flash threshold source.");
        }
        if (ColdSourcesPending == false)
        {
            return this;
        }
        if (FlashThreshold.HasValue)
        {
            throw new InvalidDataException("Electric Discharge cold source seed must not overwrite an existing mutable threshold.");
        }
        return this with { FlashThreshold = resources.InitialFlashThreshold, ColdSourcesPending = false };
    }
    public uint ReadFlashThreshold() => FlashThreshold ?? throw new NotSupportedException("Electric Discharge requires its retained mutable flash threshold.");
    public uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Globals.Read(Require(address, 4), 4));
    public ushort ReadWord(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(Globals.Read(Require(address, 2), 2));
    public NativeBattleElectricDischarge126State WriteDword(uint address, uint value) => this with { Globals = Globals.Write(Require(address, 4), BitConverter.GetBytes(value)) };
    public NativeBattleElectricDischarge126State WriteWord(uint address, ushort value) => this with { Globals = Globals.Write(Require(address, 2), BitConverter.GetBytes(value)) };
    public NativeBattleEffectArena ReadView(uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(Globals, Require(address, length), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    public NativeBattleElectricDischarge126State WriteView(uint address, NativeBattleEffectArena source, int length) => this with { Globals = NativeBattleEffectNodeContextMemory.Copy(source, 0, Globals, Require(address, length), length) };
    private static int Require(uint address, int length)
    {
        ulong end = (ulong)address + (uint)Math.Max(0, length);
        if (length < 0 || !(address >= 0x25212C0 && end <= 0x25212D4 || address >= 0x25212EC && end <= 0x25212FC || address >= 0x2521360 && end <= 0x2521368))
        {
            throw new InvalidDataException("Electric Discharge globals exclude its actual task bank, descriptors and unconsumed gaps.");
        }
        return checked((int)(address - BaseAddress));
    }
}
