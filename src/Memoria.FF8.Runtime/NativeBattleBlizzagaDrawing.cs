using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleBlizzagaDrawing
{
    public static NativeBattleGfGeometryBatch EmitMainMesh(ImmutableArray<byte> clone, ImmutableArray<byte> companion, ImmutableArray<byte> taskVertices, int phase, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection)
    {
        if (clone.IsDefault || clone.Length != 0x2328 || companion.IsDefault || companion.Length != 0x1928 || taskVertices.IsDefault || phase < 28 || phase > 97 || taskVertices.Length != (phase < 33 ? 177 : 960) * 8)
        {
            throw new InvalidDataException("Native Blizzaga requires its complete current geometry, companion and phase-owned vertex bank.");
        }
        (int partnerStart, int partnerCount) = NativeBattleIceMeshVertices.Group(companion.AsSpan(), 2);
        int faces = 0;
        NativeBattleGfGeometryBatch batch = NativeBattleEffectMeshPackets.EmitBatch(clone, matrix, projection, 0x2000, offset =>
        {
            if (offset < 0 || (offset & 7) != 0 || offset > taskVertices.Length - 8)
            {
                throw new InvalidDataException("Native Blizzaga selector leaves its actual current vertex bank.");
            }
            ReadOnlySpan<byte> bytes = taskVertices.AsSpan().Slice(offset);
            return new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes[2..]), BinaryPrimitives.ReadInt16LittleEndian(bytes[4..]));
        }, (group, face, project, offset) =>
        {
            if (group != 6 || faces >= partnerCount)
            {
                throw new InvalidDataException("Native Blizzaga main and companion primitive ownership is inconsistent.");
            }
            ImmutableArray<byte> partner = companion.AsSpan().Slice(partnerStart + faces++ * 20, 20).ToArray().ToImmutableArray();
            NativeBattleGfGeometryBatch emitted = EmitMainFace(face.ToArray().ToImmutableArray(), partner, [project(0), project(1), project(2)], phase);
            return emitted with { Attempts = emitted.Attempts.Select(attempt => attempt with { ByteOffsetRelative = checked(attempt.ByteOffsetRelative + offset) }).ToImmutableArray() };
        });
        if (faces != 320 || partnerCount != 320)
        {
            throw new InvalidDataException("Native Blizzaga requires all320 current main and companion faces.");
        }
        return batch;
    }

    public static NativeBattleGfGeometryBatch EmitMainFace(ImmutableArray<byte> source, ImmutableArray<byte> companion, ImmutableArray<NativeBattleModuleProjectedVertex> corners, int phase)
    {
        if (source.IsDefault || source.Length != 28 || companion.IsDefault || companion.Length != 20 || corners.IsDefault || corners.Length != 3)
        {
            throw new InvalidDataException("Native Blizzaga requires its current28-byte GT3,20-byte FT3 and three projected vertices.");
        }
        NativeBattleModuleMeshContext context = new(source[23] == 0 ? 0u : 0x20u, null, null, null, null, 0, 17, 2);
        NativeBattleGfGeometryAttempt main = NativeBattleModulePackets.EmitFace572200(source.AsSpan(), corners, 6, context);
        if (main.Primitive is not { } polygon || source[27] == 0)
        {
            return new([main], main.Primitive is null ? 0 : 40);
        }
        byte[] partner = companion.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(partner, 0x26000000u | (uint)source[27] * 0x10101);
        NativeBattleEffectMeshWindow window = new(0, unchecked((ushort)((phase & 31) << 2)), new(0, 0, 256, 256), new(0, 128, 64, 128), true);
        NativeBattleEffectMeshPacketContext partnerContext = new(0, null, null, null, null, 0, false, null, 17, 2, window);
        NativeBattleGfGeometryBatch partnerBatch = NativeBattleEffectMeshPackets.EmitTextured(partner, corner => corners[corner], false, false, partnerContext, null, 40, NativeBattleEffectMeshClip.None);
        if (partnerBatch.AllocationBytes != 56)
        {
            throw new InvalidDataException("Native Blizzaga companion lost its already-admitted main projection.");
        }
        SkinWrites linkMain = new(40, 0);
        return new([main with { Primitive = null }, .. partnerBatch.Attempts, linkMain.Attempt(polygon)], 96);
    }
}
