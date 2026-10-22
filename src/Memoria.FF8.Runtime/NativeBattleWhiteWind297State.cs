using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleWhiteWind297Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleWhiteWind297State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}
public sealed record NativeBattleWhiteWind297AnchorStep(NativeBattleWhiteWind297Frame Frame, NativeBattleModuleAnchor Anchor);
public abstract record NativeBattleWhiteWind297Operation;
public sealed record NativeBattleWhiteWind297BindCaster(byte Actor, uint SaveAddress, uint ResourceAddress) : NativeBattleWhiteWind297Operation;
public sealed record NativeBattleWhiteWind297RingSequence(byte Actor, byte Sequence) : NativeBattleWhiteWind297Operation;
public sealed record NativeBattleWhiteWind297Sound(uint Value, int Pan) : NativeBattleWhiteWind297Operation;
public sealed record NativeBattleWhiteWind297Contact(int Index) : NativeBattleWhiteWind297Operation;
public sealed record NativeBattleWhiteWind297Inputs(NativeBattleActionPacketsState Packets, Func<int, NativeBattleGraphicsModel> ReadModel, Func<NativeBattleWhiteWind297Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleWhiteWind297Frame, int, NativeBattleWhiteWind297AnchorStep> ReadAnchor571400, Func<NativeBattleWhiteWind297Frame, NativeBattleWhiteWind297Operation, NativeBattleWhiteWind297Frame> Execute, Func<NativeBattleWhiteWind297Frame, NativeBattleGfAnimationSource, NativeBattleEffectArena, NativeBattleGfAnimationNode, NativeBattleWhiteWind297Frame> EmitNode);

public sealed record NativeBattleWhiteWind297State([property: JsonRequired] NativeBattleEffectArena Globals)
{
    public const uint BaseAddress = 0x2507908;
    public const int Length = 0x80;
    public static NativeBattleWhiteWind297State FromColdProcess() => new(NativeBattleEffectArena.Clear());
    public static NativeBattleWhiteWind297State FromUnknown() => new(NativeBattleEffectArena.Clear().MarkUnknown(0, Length));

    public void Validate()
    {
        if (Globals is null)
        {
            throw new InvalidDataException("Blue Magic White Wind requires its canonical retained globals.");
        }
        Globals.Validate();
        foreach (NativeBattleEffectMemoryRegion region in Globals.Regions)
        {
            Require(checked(BaseAddress + (uint)region.Offset), region.Data.Length);
        }
        if (Globals.UnknownRegions.Any(region => region.Offset < 0 || region.Length < 0 || region.Offset > Length - region.Length))
        {
            throw new InvalidDataException("Blue Magic White Wind unknown globals leave the module.");
        }
    }

    public uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Globals.Read(Require(address, 4), 4));
    public NativeBattleWhiteWind297State WriteDword(uint address, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return this with { Globals = Globals.Write(Require(address, 4), bytes) };
    }
    public NativeBattleEffectArena ReadView(uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(Globals, Require(address, length), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    public NativeBattleWhiteWind297State WriteView(uint address, NativeBattleEffectArena source, int length) => this with { Globals = NativeBattleEffectNodeContextMemory.Copy(source, 0, Globals, Require(address, length), length) };
    private static int Require(uint address, int length)
    {
        if (length < 0 || address < BaseAddress || (ulong)address + (uint)length > BaseAddress + Length || address < 0x250795C && (ulong)address + (uint)length > 0x2507918)
        {
            throw new InvalidDataException("Blue Magic White Wind globals exclude its physical task banks and descriptors.");
        }
        return checked((int)(address - BaseAddress));
    }
}
