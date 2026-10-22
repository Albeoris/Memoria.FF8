using System.Buffers.Binary;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleMicroMissiles299Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleMicroMissiles299State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}
public sealed record NativeBattleMicroMissiles299State([property: JsonRequired] NativeBattleEffectArena Globals)
{
    public const uint BaseAddress = 0x2507810;
    public const int Length = 0x6C;
    public static NativeBattleMicroMissiles299State FromColdProcess() => new(NativeBattleEffectArena.Clear());
    public static NativeBattleMicroMissiles299State FromUnknown() => new(NativeBattleEffectArena.Clear().MarkUnknown(0, Length));
    public uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Globals.Read(Require(address, 4), 4));
    public NativeBattleMicroMissiles299State WriteDword(uint address, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return this with { Globals = Globals.Write(Require(address, 4), bytes) };
    }
    public void Validate()
    {
        Globals.Validate();
        foreach (NativeBattleEffectMemoryRegion region in Globals.Regions)
        {
            Require(checked(BaseAddress + (uint)region.Offset), region.Data.Length);
        }
        if (Globals.UnknownRegions.Any(region => region.Offset < 0 || region.Length < 0 || region.Offset > Length - region.Length))
        {
            throw new InvalidDataException("Micro Missiles unknown globals leave their original module.");
        }
    }
    private static int Require(uint address, int length)
    {
        if (address < BaseAddress || length < 0 || (ulong)address + (uint)length > BaseAddress + Length || address < 0x2507874 && (ulong)address + (uint)length > 0x2507820)
        {
            throw new InvalidDataException("Micro Missiles globals exclude its physical descriptors and root task.");
        }
        return checked((int)(address - BaseAddress));
    }
}
