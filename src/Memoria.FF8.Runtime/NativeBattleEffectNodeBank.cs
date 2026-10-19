using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>A callback's current addressed executable view, published back to the same typed retained bank after its exact stores.</summary>
internal sealed class NativeBattleEffectNodeBank
{
    private readonly Func<uint, int, byte[]>? readExternal;
    internal uint Address { get; }
    internal ImmutableArray<byte> Data { get; private set; }
    internal int Length => Data.Length;

    internal NativeBattleEffectNodeBank(uint address, ImmutableArray<byte> data, Func<uint, int, byte[]>? readExternal = null)
    {
        if (data.IsDefaultOrEmpty || (ulong)address + (uint)data.Length > uint.MaxValue)
        {
            throw new InvalidDataException("Native node executable view requires its actual current addressed bytes.");
        }
        Address = address;
        Data = data;
        this.readExternal = readExternal;
    }

    internal byte[] Read(uint address, int length)
    {
        if (length < 0 || (ulong)address + (uint)length > (1UL << 32))
        {
            throw new InvalidDataException("Native node executable read leaves its own current resource bank.");
        }
        ulong end = (ulong)address + (uint)length;
        ulong bankEnd = (ulong)Address + (uint)Data.Length;
        if (address >= Address && end <= bankEnd)
        {
            return Data.AsSpan().Slice(checked((int)(address - Address)), length).ToArray();
        }
        if (readExternal is null || address < bankEnd && end > Address)
        {
            throw new InvalidDataException("Native node executable read leaves its own current resource bank.");
        }
        byte[] result = readExternal(address, length);
        if (result is null || result.Length != length)
        {
            throw new InvalidDataException("Native node external owner must supply exactly the requested addressed bytes.");
        }
        return result;
    }

    internal void ReplaceData(ImmutableArray<byte> data)
    {
        if (data.IsDefault || data.Length != Data.Length)
        {
            throw new InvalidDataException("Native node executable writes cannot change the retained bank's extent.");
        }
        Data = data;
    }
}

internal sealed record NativeBattleEffectNodeInputs(NativeBattleActionMatrix? CopiedCamera, Func<int, NativeBattleGraphicsModel> ReadModel, Func<uint, sbyte, sbyte, int?> AllocateEmitter, Func<int, sbyte, int?> AllocateGeometry)
{
    public Func<NativeBattleEffectNodeBank, uint, int, byte, ImmutableArray<byte>>? ResourceUpdate { get; init; }
    public NativeBattleModelScaleVector? ActorScale { get; init; }
}
internal delegate NativeBattleGfGeometryBatch NativeBattleEffectNodeMeshInstance(ImmutableArray<byte> resource, NativeBattleActionMatrix matrix);
internal delegate NativeBattleEffectNodeMeshInstance NativeBattleEffectNodePrepareMesh(int node, byte[] source);
