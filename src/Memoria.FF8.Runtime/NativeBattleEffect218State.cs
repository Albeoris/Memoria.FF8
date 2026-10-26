using System.Buffers.Binary;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect218State([property: JsonRequired] NativeBattleEffectArena Globals, [property: JsonRequired] NativeBattleEffectArena ParticleGlobals)
{
    public const uint GlobalsAddress = 0x24F2340;
    public const int GlobalsLength = 0x12D8;
    public const uint ParticleGlobalsAddress = 0xE257E0;
    public const uint EmbeddedSkeletonAddress = 0xF1DD6C;
    public const int EmbeddedSkeletonLength = 0x70;
    public const uint CaptureQuadAddress = 0xE25AD8;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleEffectArena? CaptureQuad { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleEffectArena? EmbeddedSkeleton { get; init; }

    public bool ColdSourcePending { get; init; }

    public static NativeBattleEffect218State FromColdProcess() => new(NativeBattleEffectArena.Clear(), NativeBattleEffectArena.Clear().MarkUnknown(0, 12)) { ColdSourcePending = true };

    public static NativeBattleEffect218State FromUnknown() => new(NativeBattleEffectArena.Clear().MarkUnknown(0, GlobalsLength), NativeBattleEffectArena.Clear().MarkUnknown(0, 12));

    public NativeBattleEffect218State InitializeSource(NativeBattleEffect218Resources resources)
    {
        Validate();
        if (EmbeddedSkeleton is not null && CaptureQuad is not null)
        {
            return this;
        }
        if (!ColdSourcePending)
        {
            throw new NotSupportedException("Effect218 requires its current embedded skeleton or an explicit original cold process source.");
        }
        resources.Validate();
        return this with { EmbeddedSkeleton = NativeBattleEffectArena.Clear().Write(0, resources.EmbeddedModel.PoseSource.Skeleton.AsSpan()), CaptureQuad = NativeBattleEffectArena.Clear().Write(0, resources.CaptureClearQuad.AsSpan()), ColdSourcePending = false };
    }

    public void Validate()
    {
        if (ColdSourcePending && (EmbeddedSkeleton is not null || CaptureQuad is not null))
        {
            throw new InvalidDataException("Effect218 cold source cannot replace a current embedded skeleton.");
        }
        Require(Globals, GlobalsLength);
        Require(ParticleGlobals, 12);
        if (EmbeddedSkeleton is not null)
        {
            Require(EmbeddedSkeleton, EmbeddedSkeletonLength);
        }
        if (CaptureQuad is not null)
        {
            Require(CaptureQuad, 24);
        }
    }

    public uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(ReadView(address, 4).Read(0, 4));
    public byte ReadByte(uint address) => ReadView(address, 1).Read(0, 1)[0];
    public ushort ReadWord(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(ReadView(address, 2).Read(0, 2));
    public NativeBattleEffect218State WriteByte(uint address, byte value) => WriteView(address, NativeBattleEffectArena.Clear().Write(0, [value]), 1);
    public NativeBattleEffect218State WriteDword(uint address, uint value) => WriteView(address, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(value)), 4);
    public NativeBattleEffect218State WriteWord(uint address, ushort value) => WriteView(address, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(value)), 2);

    public NativeBattleEffectArena ReadView(uint address, int length)
    {
        var (storage, offset, _) = Locate(address, length);
        return NativeBattleEffectNodeContextMemory.Copy(storage, offset, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    }

    public NativeBattleEffect218State WriteView(uint address, NativeBattleEffectArena source, int length)
    {
        (NativeBattleEffectArena storage, int offset, int owner) = Locate(address, length);
        storage = NativeBattleEffectNodeContextMemory.Copy(source, 0, storage, offset, length);
        return owner switch
        {
            0 => this with { Globals = storage },
            1 => this with { ParticleGlobals = storage },
            2 => this with { EmbeddedSkeleton = storage },
            3 => this with { CaptureQuad = storage },
            _ => throw new InvalidDataException("Effect218 memory has an unsupported physical owner.")
        };
    }

    private (NativeBattleEffectArena Storage, int Offset, int Owner) Locate(uint address, int length)
    {
        if (length < 0)
        {
            throw new InvalidDataException("Effect218 memory has a negative extent.");
        }
        if (address >= GlobalsAddress && (ulong)address + (uint)length <= (ulong)GlobalsAddress + GlobalsLength)
        {
            return (Globals, checked((int)(address - GlobalsAddress)), 0);
        }
        if (address >= ParticleGlobalsAddress && (ulong)address + (uint)length <= (ulong)ParticleGlobalsAddress + 12)
        {
            return (ParticleGlobals, checked((int)(address - ParticleGlobalsAddress)), 1);
        }
        if (address >= EmbeddedSkeletonAddress && (ulong)address + (uint)length <= (ulong)EmbeddedSkeletonAddress + EmbeddedSkeletonLength)
        {
            return (EmbeddedSkeleton ?? throw new NotSupportedException("Effect218 embedded skeleton has no current source."), checked((int)(address - EmbeddedSkeletonAddress)), 2);
        }
        if (address >= CaptureQuadAddress && (ulong)address + (uint)length <= (ulong)CaptureQuadAddress + 24)
        {
            return (CaptureQuad ?? throw new NotSupportedException("Effect218 capture quad has no current retained source."), checked((int)(address - CaptureQuadAddress)), 3);
        }
        throw new InvalidDataException("Effect218 memory access leaves its actual retained physical owners.");
    }

    private static void Require(NativeBattleEffectArena arena, int length)
    {
        arena.Validate();
        if (arena.Regions.Any(region => region.Offset < 0 || region.Offset > length - region.Data.Length) || arena.UnknownRegions.Any(region => region.Offset < 0 || region.Offset > length - region.Length))
        {
            throw new InvalidDataException("Effect218 retained memory leaves its actual physical owner.");
        }
    }
}
