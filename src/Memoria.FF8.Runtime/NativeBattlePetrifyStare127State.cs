using System.Buffers.Binary;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattlePetrifyStare127Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattlePetrifyStare127State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattlePetrifyStare127State([property: JsonRequired] NativeBattleEffectArena Globals)
{
    public const uint BaseAddress = 0x2521270;
    public const int Length = 0x50;

    public static NativeBattlePetrifyStare127State FromColdProcess() => new(NativeBattleEffectArena.Clear());
    public static NativeBattlePetrifyStare127State FromUnknown() => new(NativeBattleEffectArena.Clear().MarkUnknown(0, Length));

    public void Validate()
    {
        Globals.Validate();
        foreach (NativeBattleEffectMemoryRegion region in Globals.Regions)
        {
            Require(checked(BaseAddress + (uint)region.Offset), region.Data.Length);
        }
        if (Globals.UnknownRegions.Any(region => region.Offset < 0 || region.Length < 0 || region.Offset > Length - region.Length))
        {
            throw new InvalidDataException("Petrify Stare retained globals leave their original scalar owner.");
        }
    }

    public uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Globals.Read(Require(address, 4), 4));
    public NativeBattlePetrifyStare127State WriteDword(uint address, uint value) => this with { Globals = Globals.Write(Require(address, 4), BitConverter.GetBytes(value)) };

    private static int Require(uint address, int length)
    {
        if (address < BaseAddress || length < 0 || (ulong)address + (uint)length > BaseAddress + Length || address < 0x252129C && (ulong)address + (uint)length > 0x2521288 || address < 0x25212C0 && (ulong)address + (uint)length > 0x25212A0)
        {
            throw new InvalidDataException("Petrify Stare globals exclude the original task bank and descriptors.");
        }
        return checked((int)(address - BaseAddress));
    }
}
