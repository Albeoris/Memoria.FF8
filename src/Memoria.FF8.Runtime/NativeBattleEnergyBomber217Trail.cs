using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEnergyBomber217TrailStep(NativeBattleEffectMemory Memory, NativeBattleFileStaging Staging, NativeBattleGfGeometryBatch Batch);

public static class NativeBattleEnergyBomber217Trail
{
    public static NativeBattleEnergyBomber217TrailStep Draw(NativeBattleEffectMemory memory, NativeBattleFileStaging staging, uint temporary120, NativeBattleVector point, NativeBattleStaticMeshResource mesh, NativeBattlePackedMatrix32 camera, NativeBattleCameraProjection projection, NativeBattleSummonColors constants, int cue)
    {
        NativeBattleEffectArena temporary = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, temporary120, 120), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 120), 0, 120);
        NativeBattleActionMatrix view = camera.ReadMatrix();
        (int x, int y, int z) = NativeBattleGfMatrices.Rotate56C3D0(view, point);
        temporary = temporary.Write(0x6C, BitConverter.GetBytes(unchecked(x + view.X))).Write(0x70, BitConverter.GetBytes(unchecked(y + view.Y))).Write(0x74, BitConverter.GetBytes(unchecked(z + view.Z)));
        NativeBattlePackedMatrix32 identity = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null);
        temporary = temporary.Write(0x58, identity.Bytes.AsSpan(0, 18));
        (byte[] rotation, bool[] rotationKnown) = temporary.ReadSnapshot(0x58, 20);
        NativeBattleGeometryRegisters geometry = (memory.GeometryRegisters ?? NativeBattleGeometryRegisters.Unknown()) with
        {
            Rotation20 = Enumerable.Range(0, 20).Select(index => rotationKnown[index] ? (byte?)rotation[index] : null).ToImmutableArray(),
            Translation = Enumerable.Range(0, 3).Select(axis => (int?)BinaryPrimitives.ReadInt32LittleEndian(temporary.Read(0x6C + axis * 4, 4))).ToImmutableArray()
        };
        temporary = temporary.Write(0, BitConverter.GetBytes(mesh.Address)).Write(8, new byte[4]).Write(12, BitConverter.GetBytes(cue)).Write(0x1C, BitConverter.GetBytes(0x30u));
        NativeBattleEffectArena context = NativeBattleEffectNodeContextMemory.Copy(temporary, 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 88), 0, 88);
        NativeBattleStaticMeshContext88Step drawn = NativeBattleStaticMeshContext88.DrawLoaded(memory with { GeometryRegisters = geometry }, mesh, projection, context, constants);
        temporary = NativeBattleEffectNodeContextMemory.Copy(drawn.Context, 0, temporary, 0, 88);
        return new(drawn.Memory, NativeBattleTemporaryMemory.PublishView(staging, temporary120, temporary, 120), drawn.Batch);
    }
}
