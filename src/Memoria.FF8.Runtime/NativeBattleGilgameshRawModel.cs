using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleGilgameshRawModelDraw(NativeBattleCactuarRawModelState State, NativeBattleEffectArena Arena, NativeBattleGfGeometryBatch Packets);

/// <summary>Gilgamesh's explicit control bindings and single arena31800 projected prefix over the shared raw model kernel.</summary>
public static class NativeBattleGilgameshRawModel
{
    public static NativeBattleGilgameshRawModelDraw Draw595E40(NativeBattleCactuarRawModelState state, NativeBattleEffectOwnedModelResource resource, uint controlAddress, NativeBattleEffectArena arena, NativeBattleActionMatrix parent, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, byte depthShift = 4)
    {
        if (depthShift is not (3 or 4) || resource.Geometry.IsDefault || resource.Geometry.Length != 1 || state.ProjectedScratch is not null)
        {
            throw new InvalidDataException("Native Gilgamesh raw drawing requires its selected depth shift, one object and separate shared scratch owner.");
        }
        int length = checked(resource.Geometry[0].Vertices.Length * 8);
        ImmutableArray<byte> scratch = arena.Read(0x31800, length).ToImmutableArray();
        NativeBattleCactuarRawModelDraw draw = NativeBattleCactuarRawModel.Draw5A9010(state, resource, parent, projection, colors, new(controlAddress, depthShift), scratch);
        if (draw.ProjectedBytesWritten > 0)
        {
            arena = arena.Write(0x31800, draw.State.ProjectedScratch!.Value.AsSpan().Slice(0, draw.ProjectedBytesWritten));
        }
        return new(draw.State with { ProjectedScratch = null }, arena, draw.Packets);
    }
}
