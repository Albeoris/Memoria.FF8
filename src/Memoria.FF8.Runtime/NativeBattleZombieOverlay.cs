using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleZombieOverlayDraw(NativeBattleFileStaging Staging, NativeBattleGfGeometryBatch Packets);

public static class NativeBattleZombieOverlay
{
    public static NativeBattleProjectedVertex Project(NativeBattleModuleDepthCueVertex vertex, short originX, ImmutableArray<short> wave, short phase, short amplitude)
    {
        if (wave.IsDefault || wave.Length != 4096)
        {
            throw new InvalidDataException("Native Zombie requires its original4096-WORD wave table.");
        }
        if (vertex.Projected.Depth == 0)
        {
            return new(0, 0x100000);
        }
        int index = unchecked((ushort)phase + unchecked((ushort)vertex.Ir2) * 4) & 4095;
        short x = unchecked((short)vertex.Projected.Screen);
        short y = unchecked((short)(vertex.Projected.Screen >> 16));
        int displacement = unchecked(wave[index] * amplitude) >> 12;
        displacement = unchecked(displacement * (x - originX)) >> 11;
        short shifted = unchecked((short)(x + displacement));
        uint flags = shifted < 0 ? 1u : shifted >= 320 * 8 ? 2u : 0u;
        flags |= y < 0 ? 4u : y >= 216 * 8 ? 8u : 0u;
        return new(unchecked((ushort)shifted) | unchecked((uint)y << 16), vertex.Projected.Depth | flags << 16);
    }

    public static NativeBattleZombieOverlayDraw Draw(ImmutableArray<NativeBattleRawMesh> meshes, ImmutableArray<NativeBattleActionMatrix> bones, NativeBattleCameraProjection projection, uint color, uint mask, NativeBattleModelTextureBinding? binding, uint workspace, int capacity, NativeBattleFileStaging staging, ImmutableArray<short> wave, short originX, short phase, short amplitude, short dqa, int dqb)
    {
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        for (int meshIndex = 0; meshIndex < meshes.Length; meshIndex++)
        {
            if ((mask & (1u << (meshIndex & 31))) == 0)
            {
                continue;
            }
            NativeBattleRawMesh mesh = meshes[meshIndex];
            mesh.Validate();
            if ((long)mesh.Vertices.Length * 8 > capacity)
            {
                throw new InvalidDataException("Native Zombie overlay exceeds its actual loaded CPU projection maximum.");
            }
            NativeBattleProjectedVertex[] vertices = new NativeBattleProjectedVertex[mesh.Vertices.Length];
            byte[] projected = new byte[checked(mesh.Vertices.Length * 8)];
            int writeStart = 0;
            for (int index = 0; index < mesh.Vertices.Length; index++)
            {
                NativeBattleRawVertex vertex = mesh.Vertices[index];
                NativeBattleActionMatrix bone = vertex.Bone < bones.Length ? bones[vertex.Bone] : throw new InvalidDataException("Native Zombie overlay lost its actual selected bone.");
                NativeBattleModuleDepthCueVertex native = NativeBattleModuleProjection.ProjectWithCue45FAA0(bone, vertex.Position, projection, dqa, dqb);
                vertices[index] = Project(native, originX, wave, phase, amplitude);
                int offset = index * 8;
                if (native.Projected.Depth == 0)
                {
                    // Rejected vertices only write their clip WORD; retain all six untouched bytes.
                    Flush(offset);
                    writeStart = offset + 6;
                    BinaryPrimitives.WriteUInt16LittleEndian(projected.AsSpan(writeStart), 0x10);
                }
                else
                {
                    BinaryPrimitives.WriteUInt32LittleEndian(projected.AsSpan(offset), vertices[index].Screen);
                    BinaryPrimitives.WriteUInt32LittleEndian(projected.AsSpan(offset + 4), vertices[index].DepthAndClip);
                }
            }
            Flush(projected.Length);
            NativeBattleGfGeometryBatch body = NativeBattleEffect107Geometry.Emit6BCB50(mesh.Polygons, vertices, color | 0x02000000, binding, 17, 0);
            attempts.AddRange(body.Attempts.Select(attempt => attempt with { ByteOffsetRelative = checked(allocated + attempt.ByteOffsetRelative) }));
            allocated = checked(allocated + body.AllocationBytes);

            void Flush(int end)
            {
                if (end > writeStart)
                {
                    staging = staging.Write(checked(workspace + (uint)writeStart), projected.AsSpan(writeStart, end - writeStart));
                }
            }
        }
        return new(staging, new(attempts.ToImmutable(), allocated));
    }
}
