using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>A callback's current addressed executable view, published back to the same typed retained bank after its exact stores.</summary>
internal sealed class NativeBattleEffectNodeBank
{
    internal uint Address { get; }
    internal ImmutableArray<byte> Data { get; private set; }
    internal int Length => Data.Length;

    internal NativeBattleEffectNodeBank(uint address, ImmutableArray<byte> data)
    {
        if (data.IsDefaultOrEmpty || (ulong)address + (uint)data.Length > uint.MaxValue)
        {
            throw new InvalidDataException("Native node executable view requires its actual current addressed bytes.");
        }
        Address = address;
        Data = data;
    }

    internal byte[] Read(uint address, int length)
    {
        if (address < Address || length < 0 || address - Address > Data.Length || length > Data.Length - (address - Address))
        {
            throw new InvalidDataException("Native node executable read leaves its own current resource bank.");
        }
        return Data.AsSpan().Slice(checked((int)(address - Address)), length).ToArray();
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

internal sealed record NativeBattleEffectNodeInputs(NativeBattleActionMatrix? CopiedCamera, Func<int, NativeBattleGraphicsModel> ReadModel, Func<uint, sbyte, sbyte, int?> AllocateEmitter, Func<int, sbyte, int?> AllocateGeometry);
internal delegate NativeBattleGfGeometryBatch NativeBattleEffectNodeMeshInstance(ImmutableArray<byte> resource, NativeBattleActionMatrix matrix);
internal delegate NativeBattleEffectNodeMeshInstance NativeBattleEffectNodePrepareMesh(int node, byte[] source);
