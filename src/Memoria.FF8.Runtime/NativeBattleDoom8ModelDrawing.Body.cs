using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleDoom8ModelDrawing
{
    private static NativeBattleDoom8ModelDrawStep DrawBody(NativeBattleDoom8Frame frame, NativeBattleFileStaging staging, NativeBattleEffectOwnedModelResource resource, NativeBattleEffectArena temporary, uint temporaryAddress, NativeBattleDoom8ModelDrawInputs inputs, uint packetAddress)
    {
        NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Doom body requires its current renderer process.");
        var gate = process.Enter5099D0(inputs.Capabilities.HardwareEnabled ?? throw new NotSupportedException("Doom body requires its actual hardware capability."));
        process = gate.Process;
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        if (gate.Draw)
        {
            uint color = BinaryPrimitives.ReadUInt32LittleEndian(temporary.Read(0x3C, 4));
            process = process.PublishModelColor(color);
            uint mask = BinaryPrimitives.ReadUInt32LittleEndian(temporary.Read(0x40, 4));
            Dictionary<int, (ImmutableArray<ushort> Counts, int First, int End)> metadata = [];
            ImmutableArray<NativeBattleRawMesh> meshes = ReadGeometry(staging, resource, metadata);
            ImmutableArray<NativeBattlePackedMatrix32> packedBones = CaptureBones(staging, resource);
            ImmutableArray<NativeBattleActionMatrix> bones = packedBones.Select(bone => bone.ReadMatrix()).ToImmutableArray();
            for (int objectIndex = 0; objectIndex < meshes.Length; objectIndex++)
            {
                if ((mask & (1u << (objectIndex & 31))) == 0)
                {
                    continue;
                }
                NativeBattleRawMesh mesh = meshes[objectIndex];
                if (mesh.Vertices.Length * 8 > ProjectedLength || mesh.Polygons.Any(face => face.Bytes.Length != (face.Quad ? 20 : 16)))
                {
                    throw new NotSupportedException("Doom body requires its bounded original plain FT3 and FT4 model objects.");
                }
                NativeBattleGeometryRegisterStep registers = Registers(frame.Memory);
                foreach (NativeBattleRawVertexGroup group in mesh.PackedVertexGroups ?? throw new NotSupportedException("Doom body requires its actual packed vertex groups."))
                {
                    NativeBattlePackedMatrix32 matrix = packedBones[group.Bone];
                    short? padding = matrix.Known[18] && matrix.Known[19] ? BinaryPrimitives.ReadInt16LittleEndian(matrix.Bytes.AsSpan().Slice(18, 2)) : null;
                    registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, matrix.ReadMatrix(), true, true, padding) };
                }
                frame = frame with { Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } };
                var layout = metadata[objectIndex];
                uint geometry = checked(resource.Address + (uint)resource.Section(1));
                temporary = Dword(temporary, 0x20, checked(geometry + (uint)layout.First));
                for (int group = 0; group < 4; group++)
                {
                    temporary = Word(temporary, 0x28 + group * 2, layout.Counts[group]);
                }
                (byte[] previous, _) = frame.State.Globals.ReadSnapshot(checked((int)(ProjectedAddress - NativeBattleDoom8State.BaseAddress)), ProjectedLength);
                NativeBattleRawModelBodyDraw body = NativeBattleRawModelBodyDrawing.Draw5099D0(mesh, bones, inputs.Projection, color, 1, previous.ToImmutableArray(), inputs.Colors, 4, new(0, 0, 320, 216), 17, 0);
                if (body.ProjectedBytesWritten != 0)
                {
                    frame = frame with { State = frame.State.Write(ProjectedAddress, body.ProjectedScratch.AsSpan().Slice(0, body.ProjectedBytesWritten)) };
                }
                attempts.AddRange(body.Packets.Attempts.Select(attempt => attempt with { ByteOffsetRelative = checked(attempt.ByteOffsetRelative + allocated) }));
                allocated = checked(allocated + body.Packets.AllocationBytes);
                process = process.Publish50FDF0(checked(packetAddress + (uint)allocated), checked(geometry + (uint)layout.End));
            }
        }
        frame = frame with { Memory = frame.Memory with { PrimitiveProcess = process }, State = frame.State.WriteDword(NativeBattleDoom8ReaperAppearance.CursorAddress, checked(packetAddress + (uint)allocated)) };
        staging = NativeBattleTemporaryMemory.PublishView(staging, temporaryAddress, temporary, 0x4C);
        return new(frame, staging, new(attempts.ToImmutable(), allocated));
    }

    private static ImmutableArray<NativeBattleRawMesh> ReadGeometry(NativeBattleFileStaging staging, NativeBattleEffectOwnedModelResource resource, Dictionary<int, (ImmutableArray<ushort> Counts, int First, int End)>? metadata = null)
    {
        uint geometry = checked(resource.Address + (uint)resource.Section(1));
        int end = resource.Section(2);
        int length = checked(end - resource.Section(1));
        return NativeBattleRawGeometryReader.Read((offset, count) =>
        {
            if (offset < 0 || count < 0 || offset > length - count)
            {
                throw new InvalidDataException("Doom body read leaves its actual original geometry section.");
            }
            return staging.Read(checked(geometry + (uint)offset), count);
        }, resource.PoseSource.BoneCount, (index, counts, first, last) => metadata?.Add(index, (counts, first, last)));
    }
}
