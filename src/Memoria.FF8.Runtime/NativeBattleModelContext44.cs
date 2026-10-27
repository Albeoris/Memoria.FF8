using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleModelContext44Step(NativeBattleEffectMemory Memory, NativeBattleEffectArena Context44, NativeBattleEffectArena ProjectedScratch, NativeBattleGfGeometryBatch Batch);

public static class NativeBattleModelContext44
{
    public static NativeBattleModelContext44Step Draw(NativeBattleEffectMemory memory, NativeBattleEffectArena context44, IReadOnlyList<NativeBattlePackedMatrix32> bones, Func<int, int, byte[]> readGeometry, uint geometryAddress, NativeBattleEffectArena projectedScratch, int projectedLength, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, bool hardware, int tableBaseBucket, byte depthShift, uint? packetPhysicalStart = null)
    {
        RequireTemporary(context44);
        if (geometryAddress == 0 || projectedLength < 0 || projectedLength > NativeBattleEffectArena.Length || bones.Count == 0 || depthShift > 31)
        {
            throw new InvalidDataException("Model body context requires its actual current geometry, bones and bounded shared projection workspace.");
        }
        NativeBattleEffectArena temporary = context44;
        NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Model body context requires its retained renderer process.");
        var gate = process.Enter5099D0(hardware);
        memory = memory with { PrimitiveProcess = gate.Process };
        if (!gate.Draw)
        {
            return new(memory, temporary, projectedScratch, new([], 0));
        }
        uint groupColor = Dword(temporary, 0x1C);
        (byte[] cueBytes, bool[] cueKnown) = temporary.ReadSnapshot(0x24, 2);
        short? groupDepth = cueKnown.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(cueBytes) : null;
        process = gate.Process.PublishModelColor(groupColor);
        memory = memory with { PrimitiveProcess = process };
        Dictionary<int, (ImmutableArray<ushort> Counts, int First, int End)> layouts = [];
        ImmutableArray<NativeBattleRawMesh> meshes = NativeBattleRawGeometryReader.Read(readGeometry, bones.Count, (index, counts, first, end) => layouts.Add(index, (counts, first, end)), Dword(temporary, 0x20));
        ImmutableArray<NativeBattleActionMatrix> matrices = bones.Select(bone => bone.ReadMatrix()).ToImmutableArray();
        uint objectMask = Dword(temporary, 0x20);
        NativeBattleGeometryViewport viewport = new(Short(temporary, 0x14), Short(temporary, 0x16), Short(temporary, 0x18), Short(temporary, 0x1A));
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        for (int objectIndex = 0; objectIndex < meshes.Length; objectIndex++)
        {
            if ((objectMask & (1u << (objectIndex & 31))) == 0)
            {
                continue;
            }
            NativeBattleRawMesh mesh = meshes[objectIndex];
            int bytes = checked(mesh.Vertices.Length * 8);
            if (bytes > projectedLength)
            {
                throw new InvalidDataException("Model body context leaves its canonical shared projection workspace.");
            }
            NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? NativeBattleGeometryRegisters.Unknown(), memory.ScriptedColorRegisters ?? NativeBattleScriptedColorRegisters.Unknown());
            foreach (NativeBattleRawVertexGroup group in mesh.PackedVertexGroups ?? throw new NotSupportedException("Model body context requires its original packed bone directory."))
            {
                NativeBattlePackedMatrix32 matrix = bones[group.Bone];
                short? padding = matrix.Known[18] && matrix.Known[19] ? BinaryPrimitives.ReadInt16LittleEndian(matrix.Bytes.AsSpan().Slice(18, 2)) : null;
                registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, matrix.ReadMatrix(), true, true, padding) };
            }
            memory = memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors };
            var layout = layouts[objectIndex];
            temporary = Dword(temporary, 0, checked(geometryAddress + (uint)layout.First));
            for (int group = 0; group < 4; group++)
            {
                temporary = Word(temporary, 8 + group * 2, layout.Counts[group]);
            }
            (byte[] previous, _) = projectedScratch.ReadSnapshot(0, bytes);
            NativeBattleRawModelBodyDraw body = NativeBattleRawModelBodyDrawing.Draw5099D0(mesh, matrices, projection, groupColor, 1, previous.ToImmutableArray(), colors, depthShift, viewport, tableBaseBucket, groupDepth);
            if (body.ProjectedBytesWritten != 0)
            {
                projectedScratch = projectedScratch.Write(0, body.ProjectedScratch.AsSpan().Slice(0, body.ProjectedBytesWritten));
            }
            memory = PublishAcceptedColors(memory, mesh, body.ProjectedScratch, temporary, groupColor, groupDepth, tableBaseBucket, depthShift, colors);
            attempts.AddRange(body.Packets.Attempts.Select(attempt => attempt with { ByteOffsetRelative = checked(allocated + attempt.ByteOffsetRelative) }));
            allocated = checked(allocated + body.Packets.AllocationBytes);
            process = (memory.PrimitiveProcess ?? throw new NotSupportedException("Model body context lost its model renderer process.")).Publish50FDF0(packetPhysicalStart is uint start ? checked(start + (uint)allocated) : null, checked(geometryAddress + (uint)layout.End));
            memory = memory with { PrimitiveProcess = process };
        }
        return new(memory, temporary, projectedScratch, new(attempts.ToImmutable(), allocated));
    }

    internal static NativeBattleEffectMemory PublishAcceptedColors(NativeBattleEffectMemory memory, NativeBattleRawMesh mesh, ImmutableArray<byte> scratch, NativeBattleEffectArena temporary, uint color, short? depth, int bucket, byte depthShift, NativeBattleSummonColors constants)
    {
        if (depth == 0)
        {
            return memory;
        }
        NativeBattleProjectedVertex[] projected = Enumerable.Range(0, mesh.Vertices.Length).Select(index => new NativeBattleProjectedVertex(BinaryPrimitives.ReadUInt32LittleEndian(scratch.AsSpan().Slice(index * 8, 4)), BinaryPrimitives.ReadUInt32LittleEndian(scratch.AsSpan().Slice(index * 8 + 4, 4)))).ToArray();
        foreach (NativeBattleRawPolygon face in mesh.Polygons)
        {
            int plainLength = face.Quad ? 20 : 16;
            if (face.Bytes.Length == plainLength)
            {
                continue;
            }
            NativeBattleGeometryPrimitive? primitive = NativeBattleGeometryPackets.EmitRaw50FDF0(face.Bytes.AsSpan(), projected, face.Quad, color, bucket, depthShift, depth, constants);
            if (primitive is null)
            {
                continue;
            }
            (byte[] bytes, bool[] known) = temporary.ReadSnapshot(0x24, 4);
            uint cue = BinaryPrimitives.ReadUInt32LittleEndian(bytes);
            bool completeCue = known.All(value => value);
            if (face.Quad)
            {
                cue |= 0x08000000;
            }
            uint material = BinaryPrimitives.ReadUInt32LittleEndian(face.Bytes.AsSpan().Slice(plainLength, 4));
            NativeBattleScriptedColorRegisters registers = memory.ScriptedColorRegisters ?? NativeBattleScriptedColorRegisters.Unknown();
            NativeBattleModelRendererRegisters far = memory.PrimitiveProcess?.ModelRendererRegisters ?? throw new NotSupportedException("Model body rendering requires its actual far color producer.");
            NativeBattleScriptedDpcsResult shaded = NativeBattleScriptedDpcs.Transform45F270(registers, material, cue, far, constants);
            if (shaded.PackedRgb != primitive.Words[1])
            {
                throw new InvalidDataException("Model body rendering color publication differs from its emitted original polygon.");
            }
            memory = memory with { ScriptedColorRegisters = completeCue ? shaded.Registers : shaded.Registers with { Ir0 = null } };
        }
        return memory;
    }

    private static short Short(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
    private static uint Dword(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static NativeBattleEffectArena Word(NativeBattleEffectArena arena, int offset, ushort value) => arena.Write(offset, BitConverter.GetBytes(value));
    private static NativeBattleEffectArena Dword(NativeBattleEffectArena arena, int offset, uint value) => arena.Write(offset, BitConverter.GetBytes(value));
    private static void RequireTemporary(NativeBattleEffectArena temporary)
    {
        temporary.Validate();
        temporary.ValidateExtent(44);
    }
}
