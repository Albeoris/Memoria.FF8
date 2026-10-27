using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect79Controller;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect79ProgrammeStep(NativeBattleEffect79Frame Frame, int Remaining);

public static class NativeBattleEffect79Programme
{
    public static NativeBattleEffect79Frame Initialize7016B0(NativeBattleEffect79Frame frame, uint taskAddress, uint sourceAddress, int sizeBytes, NativeBattleEffect79Resources resources)
    {
        NativeBattleModulePoolTask task = Read(frame, taskAddress);
        if (task.PayloadLength + 12 != 704 || task.ReadDword(0x74) != sourceAddress || unchecked((int)task.ReadDword(0x78)) != sizeBytes)
        {
            throw new InvalidDataException("Effect79 programme initialization requires its current source and actual task workspace clear size.");
        }
        NativeBattleGfAnimationSource source = CurrentSource(frame, sourceAddress, resources);
        NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(task, 0x94, task.PayloadLength - (0x94 - 12));
        workspace = workspace.Initialize7016B0(source, sizeBytes);
        return PublishWorkspace(frame, taskAddress, workspace);
    }

    public static NativeBattleEffect79ProgrammeStep Draw7B5C50(NativeBattleEffect79Frame frame, uint taskAddress, NativeBattleEffect79Resources resources, NativeBattleEffect79Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, taskAddress);
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770();
        short y = unchecked((short)task.ReadWord(0x62));
        short x = unchecked((short)task.ReadWord(0x60));
        short z = unchecked((short)task.ReadWord(0x64));
        if (y != 0)
        {
            matrix = NativeBattleCuragaMatrices.RotateY8DD8A0(matrix, y);
        }
        if (x != 0)
        {
            matrix = NativeBattleCuragaMatrices.RotateX8DD7E0(matrix, x);
        }
        if (z != 0)
        {
            matrix = NativeBattleCuragaMatrices.RotateZ8DD960(matrix, z);
        }
        matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, unchecked((int)task.ReadDword(0x50)), unchecked((int)task.ReadDword(0x54)), unchecked((int)task.ReadDword(0x58)));
        matrix = matrix with { X = unchecked((short)task.ReadWord(0x1C)), Y = unchecked((short)task.ReadWord(0x1E)), Z = unchecked((short)task.ReadWord(0x20)) };
        matrix = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), matrix);
        NativeBattleEffectArena caller = NativeBattleEffectArena.Clear().MarkUnknown(0, 92);
        caller = NativeBattleEffectNodeContextMemory.Copy(PackedArena(NativeBattlePackedMatrix32.FromMatrix(matrix, null)), 0, caller, 0, 32);
        caller = caller.Write(0x38, BitConverter.GetBytes(task.ReadDword(0x70))).Write(0x44, BitConverter.GetBytes(task.ReadDword(0x7C))).Write(0x48, BitConverter.GetBytes(0x25EAFA8u));
        int[] fields = [0x8C, 0x8E, 0x90, 0x92, 0x86, 0x80, 0x88];
        for (int index = 0; index < fields.Length; index++)
        {
            caller = caller.Write(0x4C + index * 2, BitConverter.GetBytes(task.ReadWord(fields[index])));
        }
        return Advance701970(frame, taskAddress, caller, resources, inputs);
    }

    public static NativeBattleEffect79ProgrammeStep Advance701970(NativeBattleEffect79Frame frame, uint taskAddress, NativeBattleEffectArena caller92, NativeBattleEffect79Resources resources, NativeBattleEffect79Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, taskAddress);
        NativeBattleGfAnimationSource source = CurrentSource(frame, task.ReadDword(0x74), resources);
        NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(task, 0x94, task.PayloadLength - (0x94 - 12));
        NativeBattleGfAnimationAdvance advanced = workspace.Advance701970(source, unchecked((int)frame.State.ReadDword(0x25E8954)), (current, node) =>
        {
            frame = PublishWorkspace(frame, taskAddress, current);
            frame = DrawNode7B5D80(frame, source, node, caller92, resources, inputs);
            return current;
        });
        return new(PublishWorkspace(frame, taskAddress, advanced.Workspace), advanced.Remaining);
    }

    private static NativeBattleGfAnimationSource CurrentSource(NativeBattleEffect79Frame frame, uint address, NativeBattleEffect79Resources resources)
    {
        NativeBattleGfAnimationSource template = resources.ReadProgramme(address);
        NativeBattleFileStaging staging = frame.Staging ?? throw new NotSupportedException("Effect79 programme requires its actual completed file staging.");
        return NativeBattleGfAnimationSource.Borrow(address, staging, template.Length);
    }

    private static NativeBattleEffect79Frame PublishWorkspace(NativeBattleEffect79Frame frame, uint taskAddress, NativeBattleGfAnimationWorkspace workspace)
    {
        NativeBattleModulePoolTask task = Read(frame, taskAddress);
        if (workspace.Length != task.PayloadLength - (0x94 - 12))
        {
            throw new InvalidDataException("Effect79 programme workspace leaves its current task payload.");
        }
        return Write(frame, taskAddress, workspace.CopyTo(task, 0x94, retainUnknown: true));
    }

    public static NativeBattleEffect79Frame DrawNode7B5D80(NativeBattleEffect79Frame frame, NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleEffectArena caller92, NativeBattleEffect79Resources resources, NativeBattleEffect79Inputs inputs)
    {
        NativeBattleVector scale = node.Vector24;
        if (scale is { X: 0, Y: 0, Z: 0 } || node.Scalar36 >= 4096 && node.Color32 == 0)
        {
            return frame;
        }
        int geometryIndex = unchecked((short)node.GeometryIndex);
        using NativeBattleGfSourceReader resource = source.OpenReader();
        if (geometryIndex < 0 || geometryIndex >= resource.Int(0))
        {
            throw new InvalidDataException("Effect79 programme geometry leaves its selected source directory.");
        }
        int geometryOffset = resource.Int(8 + geometryIndex * 4);
        uint geometryAddress = checked(source.Address + (uint)geometryOffset);
        using NativeBattleGfSourceReader mesh = resource.Slice(geometryOffset);
        int count = mesh.Int(4);
        int shapes = mesh.Int(8);
        int first = node.Word40;
        int second = node.Word42;
        int ratio = node.Scalar38;
        if (first < 0 || second < 0 || first >= shapes || second >= shapes)
        {
            throw new InvalidDataException("Effect79 programme shape leaves its current selected geometry.");
        }
        (frame, uint temporary) = Allocate(frame, 104);
        uint vertices;
        if (first == second || ratio == 0 || ratio == 4096)
        {
            int selected = ratio == 4096 && first != second ? second : first;
            vertices = checked(geometryAddress + 12 + (uint)(selected * count * 8));
        }
        else
        {
            vertices = Dword(caller92, 0x48);
            frame = MorphProgramme79(frame, mesh, first, second, ratio, vertices);
        }
        (byte[] callerBytes, bool[] callerKnown) = caller92.ReadSnapshot(0, 32);
        NativeBattlePackedMatrix32 caller = new(callerBytes.ToImmutableArray(), callerKnown.ToImmutableArray());
        NativeBattleActionMatrix basis = caller.ReadMatrix();
        NativeBattleActionMatrix rotation = NativeBattleGfMatrices.Euler701310(node.Vector16);
        NativeBattleVector point = node.Vector8;
        int x;
        int y;
        int z;
        if ((node.Header & 0x200) != 0)
        {
            x = point.X;
            y = point.Y;
            z = point.Z;
        }
        else
        {
            frame = LoadMatrix(frame, caller, true, false);
            NativeBattleGeometryRegisterStep registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(RegistersProgramme79(frame), 0, point, null), false);
            x = registers.Colors.Mac[0]!.Value;
            y = registers.Colors.Mac[1]!.Value;
            z = registers.Colors.Mac[2]!.Value;
            frame = PublishRegistersProgramme79(frame, registers);
            rotation = NativeBattleGfMatrices.Multiply56C090(basis, rotation);
        }
        rotation = rotation with { X = unchecked(basis.X + x), Y = unchecked(basis.Y + y), Z = unchecked(basis.Z + z) };
        if (scale is not { X: 4096, Y: 4096, Z: 4096 })
        {
            if ((node.Header & 0x100) != 0)
            {
                NativeBattleActionMatrix diagonal = new([scale.X, 0, 0, 0, scale.Y, 0, 0, 0, scale.Z], 0, 0, 0, 4096);
                rotation = rotation with { Rotation = NativeBattleGfMatrices.Multiply56C090(rotation, diagonal).Rotation };
            }
            else
            {
                rotation = NativeBattlePoseMatrices.Scale56BEF0(rotation, scale.X, scale.Y, scale.Z);
            }
        }
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(rotation, null));
        NativeBattleEffectArena context = Context(frame, temporary, 104).Write(0, BitConverter.GetBytes(geometryAddress)).Write(4, BitConverter.GetBytes(vertices)).Write(12, BitConverter.GetBytes((int)node.Scalar36)).Write(16, BitConverter.GetBytes((int)BinaryPrimitives.ReadInt16LittleEndian(caller92.Read(0x58, 2)))).Write(20, BitConverter.GetBytes(node.Scalar36 == 0 ? 0x2030u : 0x20F0u));
        if (node.Scalar36 != 0)
        {
            context = context.Write(8, BitConverter.GetBytes(node.Color32));
        }
        for (int offset = 0x18; offset <= 0x2A; offset += 2)
        {
            context = context.Write(offset, BitConverter.GetBytes((ushort)(offset is 0x20 or 0x22 or 0x28 or 0x2A ? 256 : 0)));
        }
        frame = PublishContext(frame, temporary, context, 104);
        NativeBattleEffect79Mesh104Step drawn = NativeBattleEffect79Mesh104.DrawLoaded(frame.Memory, geometryAddress, mesh, inputs.ReadProjection(frame), context, inputs.Colors, offset =>
        {
            NativeBattleEffectArena row = ReadBytes(frame, checked(vertices + (uint)offset), 8);
            (byte[] bytes, bool[] known) = row.ReadSnapshot(0, 8);
            ReadOnlySpan<byte> xyz = row.Read(0, 6);
            return (new NativeBattleVector(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz[2..]), BinaryPrimitives.ReadInt16LittleEndian(xyz[4..])), known[6] && known[7] ? BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(6)) : null);
        });
        frame = PublishContext(frame with { Memory = drawn.Memory }, temporary, drawn.Context104, 104);
        frame = inputs.Execute(frame, new NativeBattleEffect79DeliverBatch(drawn.Batch, 0x1D8E054));
        return Release(frame, 104);
    }

    private static NativeBattleEffect79Frame MorphProgramme79(NativeBattleEffect79Frame frame, NativeBattleGfSourceReader mesh, int first, int second, int ratio, uint destination)
    {
        const uint scratch = 0x25EAFA8;
        int count = mesh.Int(4);
        if (destination != scratch || count < 1 || count > 125 || count * 8 > 1000)
        {
            throw new InvalidDataException("Effect79 morph requires its actual addressed morph scratch.");
        }
        NativeBattleGeometryRegisterStep registers = RegistersProgramme79(frame);
        for (int index = 0; index < count; index++)
        {
            int a = 12 + (first * count + index) * 8;
            int b = 12 + (second * count + index) * 8;
            registers = NativeBattleGeometry.LoadIrColumn(registers with { Colors = registers.Colors with { Ir0 = unchecked((uint)(4096 - ratio)) } }, ShapeWord(a), ShapeWord(a + 2), ShapeWord(a + 4));
            registers = NativeBattleRaldo19BallRendering.Weighted(registers, false, NativeBattleGteColorInterpolation.CodeConstants45F270);
            registers = NativeBattleGeometry.LoadIrColumn(registers with { Colors = registers.Colors with { Ir0 = unchecked((uint)ratio) } }, ShapeWord(b), ShapeWord(b + 2), ShapeWord(b + 4));
            registers = NativeBattleRaldo19BallRendering.Weighted(registers, true, NativeBattleGteColorInterpolation.CodeConstants45F270);
            NativeBattleEffectArena row = NativeBattleEffectArena.Clear();
            for (int axis = 0; axis < 3; axis++)
            {
                row = row.Write(axis * 2, BitConverter.GetBytes(unchecked((short)registers.Colors.Ir[axis]!.Value)));
            }
            frame = WriteBytes(frame, checked(destination + (uint)(index * 8)), row, 6);
        }
        return PublishRegistersProgramme79(frame, registers);

        ushort ShapeWord(int offset) => mesh.UShort(offset);
    }

    private static uint Dword(NativeBattleEffectArena value, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(value.Read(offset, 4));
    private static NativeBattleGeometryRegisterStep RegistersProgramme79(NativeBattleEffect79Frame frame) => new(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Effect79 requires current geometry registers."), frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect79 requires current color registers."));
    private static NativeBattleEffect79Frame PublishRegistersProgramme79(NativeBattleEffect79Frame frame, NativeBattleGeometryRegisterStep registers) => frame with { Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } };
}
