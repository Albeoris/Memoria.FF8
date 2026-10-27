using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleUltraWavesStageCapture84Step(NativeBattleEffectMemory Memory, NativeBattleEffectArena Temporary84, NativeBattleEffectArena ProjectedScratch, NativeBattleGfGeometryBatch Batch);

public static class NativeBattleUltraWavesStageCapture84
{
    public static NativeBattleEffectArena Prepare(NativeBattleEffectArena retained84, uint projectedScratchAddress, NativeBattleEffectArena retainedFlags4)
    {
        RequireTemporary(retained84);
        NativeBattleEffectArena result = Dword(retained84, 0x2C, projectedScratchAddress);
        result = NativeBattleEffectNodeContextMemory.Copy(retainedFlags4, 0, result, 0x38, 4);
        result = Word(Word(Word(Word(result, 0x3C, 0), 0x3E, 0), 0x40, 320), 0x42, 216);
        return Dword(result, 0x48, uint.MaxValue);
    }

    public static NativeBattleUltraWavesStageCapture84Step DrawGroup(NativeBattleEffectMemory memory, NativeBattleEffectArena retained84, uint groupColor, short groupDepth, IReadOnlyList<NativeBattlePackedMatrix32> bones, Func<int, int, byte[]> readGeometry, uint geometryAddress, NativeBattleEffectArena projectedScratch, int projectedLength, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, bool hardware, int tableBaseBucket, byte depthShift, uint? packetPhysicalStart = null)
    {
        RequireTemporary(retained84);
        if (geometryAddress == 0 || projectedLength < 0 || projectedLength > NativeBattleEffectArena.Length || bones.Count == 0 || depthShift > 31)
        {
            throw new InvalidDataException("Ultra Waves capture requires its actual current geometry, bones and bounded shared projection workspace.");
        }
        NativeBattleEffectArena temporary = Word(Dword(retained84, 0x44, groupColor), 0x4C, unchecked((ushort)groupDepth));
        NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Ultra Waves capture requires its retained renderer process.");
        var gate = process.Enter5099D0(hardware);
        memory = memory with { PrimitiveProcess = gate.Process };
        if (!gate.Draw)
        {
            return new(memory, temporary, projectedScratch, new([], 0));
        }
        process = gate.Process.PublishModelColor(groupColor);
        memory = memory with { PrimitiveProcess = process };
        Dictionary<int, (ImmutableArray<ushort> Counts, int First, int End)> layouts = [];
        ImmutableArray<NativeBattleRawMesh> meshes = NativeBattleRawGeometryReader.Read(readGeometry, bones.Count, (index, counts, first, end) => layouts.Add(index, (counts, first, end)));
        ImmutableArray<NativeBattleActionMatrix> matrices = bones.Select(bone => bone.ReadMatrix()).ToImmutableArray();
        uint objectMask = Dword(temporary, 0x48);
        NativeBattleGeometryViewport viewport = new(Short(temporary, 0x3C), Short(temporary, 0x3E), Short(temporary, 0x40), Short(temporary, 0x42));
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
                throw new InvalidDataException("Ultra Waves capture leaves its canonical shared projection workspace.");
            }
            NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? NativeBattleGeometryRegisters.Unknown(), memory.ScriptedColorRegisters ?? NativeBattleScriptedColorRegisters.Unknown());
            foreach (NativeBattleRawVertexGroup group in mesh.PackedVertexGroups ?? throw new NotSupportedException("Ultra Waves capture requires its original packed bone directory."))
            {
                NativeBattlePackedMatrix32 matrix = bones[group.Bone];
                short? padding = matrix.Known[18] && matrix.Known[19] ? BinaryPrimitives.ReadInt16LittleEndian(matrix.Bytes.AsSpan().Slice(18, 2)) : null;
                registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, matrix.ReadMatrix(), true, true, padding) };
            }
            memory = memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors };
            var layout = layouts[objectIndex];
            temporary = Dword(temporary, 0x28, checked(geometryAddress + (uint)layout.First));
            for (int group = 0; group < 4; group++)
            {
                temporary = Word(temporary, 0x30 + group * 2, layout.Counts[group]);
            }
            (byte[] previous, _) = projectedScratch.ReadSnapshot(0, bytes);
            NativeBattleRawModelBodyDraw body = NativeBattleRawModelBodyDrawing.Draw5099D0(mesh, matrices, projection, groupColor, 1, previous.ToImmutableArray(), colors, depthShift, viewport, tableBaseBucket, groupDepth);
            if (body.ProjectedBytesWritten != 0)
            {
                projectedScratch = projectedScratch.Write(0, body.ProjectedScratch.AsSpan().Slice(0, body.ProjectedBytesWritten));
            }
            memory = NativeBattleModelContext44.PublishAcceptedColors(memory, mesh, body.ProjectedScratch, NativeBattleEffectNodeContextMemory.Copy(temporary, 0x4C, NativeBattleEffectArena.Clear().MarkUnknown(0, 44), 0x24, 4), groupColor, groupDepth, tableBaseBucket, depthShift, colors);
            attempts.AddRange(body.Packets.Attempts.Select(attempt => attempt with { ByteOffsetRelative = checked(allocated + attempt.ByteOffsetRelative) }));
            allocated = checked(allocated + body.Packets.AllocationBytes);
            process = (memory.PrimitiveProcess ?? throw new NotSupportedException("Ultra Waves capture lost its model renderer process.")).Publish50FDF0(packetPhysicalStart is uint start ? checked(start + (uint)allocated) : null, checked(geometryAddress + (uint)layout.End));
            memory = memory with { PrimitiveProcess = process };
        }
        return new(memory, temporary, projectedScratch, new(attempts.ToImmutable(), allocated));
    }

    private static short Short(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
    private static uint Dword(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static NativeBattleEffectArena Word(NativeBattleEffectArena arena, int offset, ushort value) => arena.Write(offset, BitConverter.GetBytes(value));
    private static NativeBattleEffectArena Dword(NativeBattleEffectArena arena, int offset, uint value) => arena.Write(offset, BitConverter.GetBytes(value));
    private static void RequireTemporary(NativeBattleEffectArena temporary)
    {
        temporary.Validate();
        temporary.ValidateExtent(84);
    }
}
