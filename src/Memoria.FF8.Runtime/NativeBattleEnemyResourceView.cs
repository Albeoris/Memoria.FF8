using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEnemyResourceView
{
    public static NativeBattleGraphicsResource Resolve(NativeBattleModelCpuResources cpu, NativeBattleFileStaging staging, int index, NativeBattleGraphicsResource original)
    {
        NativeBattleModelCpuResourceRow row = cpu.Rows[index];
        NativeBattleModelCpuReference skeleton = row.Require(12);
        int boneCount = NativeBattleModelEnemyHeap.Read(cpu, staging, skeleton, 1)[0];
        NativeBattlePoseSource pose = new(NativeBattleModelEnemyHeap.Read(cpu, staging, skeleton, checked(16 + boneCount * 48)).ToImmutableArray(), Block(20));
        bool camera = row.Fields.TryGetValue(44, out NativeBattleModelCpuReference? cameraReference) ? cameraReference is not null : throw new NotSupportedException("Native enemy action-camera pointer remains unknown.");
        NativeBattleGraphicsResource result = original with { PoseSource = pose, AnimationFrames = NativeBattlePoseDecoder.ReadFrameCounts(pose.Animations.AsSpan()), SequenceSection = Block(24), Geometry = Geometry(), GeometryRelocated507550 = true, HasActionCamera = camera, ActionCameraBank = camera ? new(Block(44)) : null, TextureAnimations = row.Fields.TryGetValue(48, out NativeBattleModelCpuReference? motion) && motion is not null ? Block(48) : [] };
        result.Validate();
        return result;

        ImmutableArray<byte> Block(int field)
        {
            int length = row.Lengths.TryGetValue(field, out int known) ? known : throw new NotSupportedException("Native enemy resource section extent remains unknown.");
            return NativeBattleModelEnemyHeap.Read(cpu, staging, row.Require(field), length).ToImmutableArray();
        }

        ImmutableArray<NativeBattleRawMesh> Geometry()
        {
            NativeBattleModelCpuReference reference = row.Require(16);
            byte[] bytes = NativeBattleModelEnemyHeap.Read(cpu, staging, reference, row.Lengths.TryGetValue(16, out int length) ? length : throw new NotSupportedException("Native enemy geometry extent remains unknown."));
            int count = I32(0);
            if (count is < 0 or > 256)
            {
                throw new InvalidDataException("Native enemy geometry object directory exceeds its original resource.");
            }
            var meshes = ImmutableArray.CreateBuilder<NativeBattleRawMesh>(count);
            for (int mesh = 0; mesh < count; mesh++)
            {
                int cursor = I32(4 + mesh * 4);
                int groups = U16(cursor);
                cursor += 2;
                var vertices = ImmutableArray.CreateBuilder<NativeBattleRawVertex>();
                var bones = ImmutableArray.CreateBuilder<short>();
                for (int group = 0; group < groups; group++)
                {
                    ushort bone = U16(cursor);
                    int total = U16(cursor + 2);
                    cursor += 4;
                    if (bone >= boneCount)
                    {
                        throw new InvalidDataException("Native enemy geometry references an absent skeleton bone.");
                    }
                    bones.Add(unchecked((short)bone));
                    for (int vertex = 0; vertex < total; vertex++)
                    {
                        vertices.Add(new(new(unchecked((short)U16(cursor)), unchecked((short)U16(cursor + 2)), unchecked((short)U16(cursor + 4))), bone));
                        cursor += 6;
                    }
                }
                cursor = checked((cursor + 3) & ~3);
                int triangles = unchecked((short)U16(cursor));
                int quads = unchecked((short)U16(cursor + 2));
                cursor += 12;
                var polygons = ImmutableArray.CreateBuilder<NativeBattleRawPolygon>();
                for (int face = 0; face < triangles + quads; face++)
                {
                    bool quad = face >= triangles;
                    int size = quad ? 20 : 16;
                    polygons.Add(new(quad, bytes.AsSpan(cursor, size).ToArray().ToImmutableArray()));
                    cursor += size;
                }
                meshes.Add(new(vertices.ToImmutable(), polygons.ToImmutable()) { BoneGroups = bones.ToImmutable() });
            }
            return meshes.ToImmutable();

            ushort U16(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, 2));
            int I32(int offset) => BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset, 4));
        }
    }
}
