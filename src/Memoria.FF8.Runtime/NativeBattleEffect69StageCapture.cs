using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect69Controller;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect69StageCapture
{
    public static NativeBattleEffect69StageStep StageCapture6FE300(NativeBattleEffect69Frame frame, int group, NativeBattlePackedMatrix32 cpu, uint metricPointer, NativeBattleEffect69Inputs inputs)
    {
        uint temporary;
        (frame, temporary) = Allocate(frame, 16);
        frame = (inputs.PrepareStage ?? throw new NotSupportedException("Effect69 stage capture requires its actual local-pose restore and CPU matrix service."))(frame, group, cpu);
        NativeBattleEffect69StageSource source = NativeBattleEffect69StageKernel.Source(frame, group, inputs);
        NativeBattleEffectArena context = ReadBytes(frame, temporary, 16).Write(4, BitConverter.GetBytes(source.ScratchAddress)).Write(8, BitConverter.GetBytes(metricPointer));
        NativeBattleGeometryRegisterStep registers = Registers(frame);
        for (int item = 0; item < source.RawGeometry.Length; item++)
        {
            NativeBattleRawMesh mesh = source.RawGeometry[item];
            NativeBattleEffect69StageKernel.RequireScratch(source, mesh.Vertices.Length, 4);
            int vertex = 0;
            foreach (NativeBattleRawVertexGroup packed in NativeBattleEffect69StageKernel.Groups(mesh))
            {
                registers = NativeBattleEffect69StageKernel.LoadBone(registers, source, packed.Bone);
                for (int index = 0; index < packed.Count; index++)
                {
                    NativeBattleVector point = NativeBattleEffect69StageKernel.Vector(packed.Bytes.AsSpan().Slice(index * 6, 6));
                    registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(registers, 0, point, 0));
                    registers = Square45F9E0(registers);
                    int metric = unchecked(registers.Colors.Mac[0]!.Value + registers.Colors.Mac[1]!.Value + registers.Colors.Mac[2]!.Value);
                    frame = NativeBattleEffect69StageKernel.WriteStaging(frame, checked(source.ScratchAddress + (uint)(vertex * 4)), BitConverter.GetBytes(metric));
                    vertex++;
                }
            }
            uint faceAddress = NativeBattleEffect69StageKernel.FaceAddress(frame, source, item, mesh);
            context = NativeBattleEffect69StageKernel.Dword(context, 0, faceAddress);
            context = NativeBattleEffect69StageKernel.Word(context, 12, mesh.Polygons.Count(face => !face.Quad));
            context = NativeBattleEffect69StageKernel.Word(context, 14, mesh.Polygons.Count(face => face.Quad));
            foreach (NativeBattleRawPolygon face in mesh.Polygons)
            {
                int total = 0;
                int count = face.Quad ? 4 : 3;
                for (int corner = 0; corner < count; corner++)
                {
                    int index = NativeBattleEffect69StageKernel.FaceIndex(face, corner, vertex);
                    int metric = BinaryPrimitives.ReadInt32LittleEndian(NativeBattleEffect69StageKernel.Staging(frame).Read(checked(source.ScratchAddress + (uint)(index * 4)), 4));
                    total = unchecked(total + metric);
                }
                frame = WriteBytes(frame, metricPointer, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(total / count)), 4);
                metricPointer = unchecked(metricPointer + 4);
                context = NativeBattleEffect69StageKernel.Dword(context, 8, metricPointer);
            }
            context = NativeBattleEffect69StageKernel.Word(NativeBattleEffect69StageKernel.Word(context, 12, -1), 14, -1);
        }
        frame = Publish(frame, registers);
        frame = WriteBytes(frame, temporary, context, 16);
        frame = Release(frame, 16);
        return new(frame, metricPointer);
    }

    public static NativeBattleGeometryRegisterStep Square45F9E0(NativeBattleGeometryRegisterStep registers)
    {
        int[] mac = new int[3];
        int[] ir = new int[3];
        uint flags = 0;
        uint[] limits = [0x81000000, 0x80800000, 0x00400000];
        for (int lane = 0; lane < 3; lane++)
        {
            int input = unchecked((short)(registers.Colors.Ir[lane] ?? throw new NotSupportedException("Native shifted SQR requires its current IR lanes.")));
            mac[lane] = unchecked(input * input) >> 12;
            ir[lane] = Math.Clamp(mac[lane], short.MinValue, short.MaxValue);
            if (mac[lane] > short.MaxValue)
            {
                flags |= limits[lane];
            }
        }
        return registers with { Colors = registers.Colors with { Mac = [(int?)mac[0], mac[1], mac[2]], Ir = [(int?)ir[0], ir[1], ir[2]], Flags = flags } };
    }
}

internal static class NativeBattleEffect69StageKernel
{
    public static NativeBattleEffect69StageSource Source(NativeBattleEffect69Frame frame, int group, NativeBattleEffect69Inputs inputs)
    {
        NativeBattleEffect69StageSource source = (inputs.ReadStage ?? throw new NotSupportedException("Effect69 requires its current stage group and addressed raw geometry."))(frame, group);
        if (source is null || source.Group is null || source.RawGeometry.IsDefault || source.GeometryAddress == 0 || source.ScratchAddress == 0 || source.ScratchLength < 0)
        {
            throw new InvalidDataException("Effect69 requires its current stage geometry and physical scratch addresses.");
        }
        ImmutableArray<NativeBattleRawMesh> current = NativeBattleRawGeometryReader.Read((offset, length) => Staging(frame).Read(checked(source.GeometryAddress + (uint)offset), length), source.Group.Matrices.Length);
        source = source with { RawGeometry = current };
        foreach (NativeBattleRawMesh mesh in source.RawGeometry)
        {
            mesh.Validate();
            if (mesh.Polygons.Any(face => face.Bytes.Length != (face.Quad ? 24 : 20)))
            {
                throw new NotSupportedException("Effect69 stage capture and drawing require the reached raw triangle20 and quad24 groups.");
            }
        }
        return source;
    }

    public static ImmutableArray<NativeBattleRawVertexGroup> Groups(NativeBattleRawMesh mesh)
    {
        ImmutableArray<NativeBattleRawVertexGroup> groups = mesh.PackedVertexGroups ?? throw new NotSupportedException("Effect69 requires its current packed bone-group order, including empty groups.");
        if (groups.Any(group => group.Count < 0 || group.Bytes.IsDefault || group.Bytes.Length < group.Count * 6) || groups.Sum(group => group.Count) != mesh.Vertices.Length)
        {
            throw new InvalidDataException("Effect69 packed vertex groups leave their current object dimensions.");
        }
        return groups;
    }

    public static void RequireScratch(NativeBattleEffect69StageSource source, int vertices, int stride)
    {
        if (vertices < 0 || vertices > source.ScratchLength / stride)
        {
            throw new InvalidDataException("Effect69 stage object exceeds its actual shared scratch allocation.");
        }
    }

    public static uint FaceAddress(NativeBattleEffect69Frame frame, NativeBattleEffect69StageSource source, int item, NativeBattleRawMesh mesh)
    {
        int offset = BinaryPrimitives.ReadInt32LittleEndian(Staging(frame).Read(checked(source.GeometryAddress + 4 + (uint)(item * 4)), 4));
        uint cursor = unchecked(source.GeometryAddress + (uint)offset + 2);
        foreach (NativeBattleRawVertexGroup group in Groups(mesh))
        {
            cursor = checked(cursor + 4 + (uint)(group.Count * 6));
        }
        return checked(((cursor + 3) & ~3u) + 12);
    }

    public static NativeBattleGeometryRegisterStep LoadBone(NativeBattleGeometryRegisterStep registers, NativeBattleEffect69StageSource source, ushort bone)
    {
        if (bone >= source.Group.Matrices.Length)
        {
            throw new InvalidDataException("Effect69 stage group selects an absent current bone matrix.");
        }
        return registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, source.Group.Matrices[bone], true, true, null) };
    }

    public static int FaceIndex(NativeBattleRawPolygon face, int corner, int vertices)
    {
        int index = BinaryPrimitives.ReadUInt16LittleEndian(face.Bytes.AsSpan().Slice(corner * 2, 2)) & 0xFFF;
        if (index >= vertices)
        {
            throw new InvalidDataException("Effect69 stage face leaves its current object vertex rows.");
        }
        return index;
    }

    public static NativeBattleFileStaging Staging(NativeBattleEffect69Frame frame) => frame.Staging ?? throw new NotSupportedException("Effect69 requires its current addressed shared scratch.");
    public static NativeBattleEffect69Frame WriteStaging(NativeBattleEffect69Frame frame, uint address, ReadOnlySpan<byte> bytes) => frame with { Staging = Staging(frame).Write(address, bytes) };
    public static NativeBattleVector Vector(ReadOnlySpan<byte> bytes) => new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(2)), BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(4)));
    public static NativeBattleEffectArena Word(NativeBattleEffectArena data, int offset, int value) => data.Write(offset, BitConverter.GetBytes(unchecked((short)value)));
    public static NativeBattleEffectArena Dword(NativeBattleEffectArena data, int offset, uint value) => data.Write(offset, BitConverter.GetBytes(value));
}
