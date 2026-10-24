using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect64Controller;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect64Programme
{
    public static (NativeBattleEffect64Frame Frame, uint Next) Birth6E61E0(NativeBattleEffect64Frame frame, uint script, uint model, uint offsets, bool forceYZero, NativeBattleEffect64Resources resources, NativeBattleEffect64Inputs inputs)
    {
        short[] words = Enumerable.Range(0, 6).Select(index => NativeBattleEffect64Script.ReadWord(frame, script + (uint)(index * 2))).ToArray();
        (frame, uint address) = Append(frame, 0x2545038, 0x6E6310);
        if (address == 0)
        {
            return (frame, script + 12);
        }
        frame = inputs.Execute(frame, new NativeBattleEffect64ApplyOffsetPose662C00(model, offsets));
        NativeBattleEffect64PointStep bone = inputs.ReadBone502170(frame, model, words[0], words[1], NativeBattleEffectArena.Clear().MarkUnknown(0, 8));
        frame = bone.Frame;
        NativeBattleEffectArena point = AddOffset(frame, bone.Point8, offsets, words[0]);
        if (forceYZero)
        {
            point = point.Write(2, new byte[2]);
        }
        NativeBattleModulePoolTask task = CopyTask(Read(frame, address), 0xC, point, 6).WriteWord(0x12, unchecked((ushort)words[3])).WriteWord(0x14, unchecked((ushort)words[4])).WriteWord(0x16, unchecked((ushort)words[5])).WriteWord(0x18, 0);
        if ((uint)words[2] >= 2)
        {
            throw new InvalidDataException("Effect64 programme descriptor leaves its original table.");
        }
        int descriptor = words[2] * 12;
        uint sourceAddress = BinaryPrimitives.ReadUInt32LittleEndian(resources.ProgrammeDescriptors.AsSpan().Slice(descriptor));
        int clearBytes = BinaryPrimitives.ReadInt32LittleEndian(resources.ProgrammeDescriptors.AsSpan().Slice(descriptor + 4));
        task = task.WriteDword(0x1C, checked(0x13473D0u + (uint)descriptor));
        frame = Write(frame, address, task);
        frame = inputs.Execute(frame, new NativeBattleEffect64RestorePose508C90(model));
        NativeBattleGfAnimationWorkspace workspace = new(new byte[clearBytes].ToImmutableArray());
        workspace = workspace.Initialize7016B0(resources.Act(sourceAddress), clearBytes);
        frame = WriteBytes(frame, address + 0x20, NativeBattleEffectArena.Clear().Write(0, workspace.Data.AsSpan()), clearBytes);
        return (frame, script + 12);
    }

    public static NativeBattleEffect64Step Advance6E6310(NativeBattleEffect64Frame frame, uint address, NativeBattleEffect64Resources resources, NativeBattleEffect64Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        NativeBattleActionMatrix local = NativeBattlePandemona291Restoration.Rotation701270(Signed(task, 0x14)) with { X = Signed(task, 0xC), Y = Signed(task, 0xE), Z = Signed(task, 0x10) };
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local);
        int scale = Signed(task, 0x12);
        NativeBattleEffectArena caller = PackedArena(NativeBattlePackedMatrix32.FromMatrix(matrix, null)).Write(0x20, BitConverter.GetBytes(scale)).Write(0x24, BitConverter.GetBytes(scale)).Write(0x28, BitConverter.GetBytes(scale)).Write(0x2C, BitConverter.GetBytes(1)).Write(0x30, BitConverter.GetBytes(task.ReadDword(0x1C))).Write(0x34, BitConverter.GetBytes(task.ReadWord(0x16))).Write(0x36, BitConverter.GetBytes(unchecked((ushort)(task.ReadWord(0x18) << 6)))).Write(0x40, BitConverter.GetBytes(checked(frame.State.ReadDword(0x2545098) + 0x26F20)));
        uint sourceAddress = task.ReadDword(0x20);
        NativeBattleGfAnimationSource source = resources.Act(sourceAddress);
        int length = sourceAddress switch { 0x13433C8 => 0x98, 0x1343C30 => 0x578, _ => throw new NotSupportedException("Effect64 programme requires its selected current source.") };
        NativeBattleGfAnimationWorkspace Workspace() => new(ReadBytes(frame, address + 0x20, length).Read(0, length).ToImmutableArray());
        NativeBattleGfAnimationAdvance advanced = Workspace().Advance701970(source, 0, (current, node) =>
        {
            frame = WriteBytes(frame, address + 0x20, NativeBattleEffectArena.Clear().Write(0, current.Data.AsSpan()), length);
            frame = DrawNode6E63E0(frame, source, node, caller, resources, inputs);
            return Workspace();
        });
        frame = WriteBytes(frame, address + 0x20, NativeBattleEffectArena.Clear().Write(0, advanced.Workspace.Data.AsSpan()), length);
        if (advanced.Remaining == 0)
        {
            return new(frame, 2);
        }
        task = Read(frame, address);
        return new(Write(frame, address, task.WriteWord(0x18, unchecked((ushort)(task.ReadWord(0x18) + 1)))), 0);
    }

    public static NativeBattleEffect64Frame DrawNode6E63E0(NativeBattleEffect64Frame frame, NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleEffectArena caller68, NativeBattleEffect64Resources resources, NativeBattleEffect64Inputs inputs)
    {
        if (!NativeBattleActMeshRendering.Visible(node))
        {
            return frame;
        }
        int offset = BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(8 + node.GeometryIndex * 4));
        uint geometryAddress = checked(source.Address + (uint)offset);
        NativeBattleStaticMeshResource mesh = resources.ReadGeometry(geometryAddress);
        int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4));
        int shapes = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(8));
        if (node.Word40 < 0 || node.Word42 < 0 || node.Word40 >= shapes || node.Word42 >= shapes)
        {
            throw new InvalidDataException("Effect64 ACT shape leaves its original selected geometry.");
        }
        if (node.Word40 != node.Word42 && node.Scalar38 is not (0 or 4096))
        {
            throw new NotSupportedException("Effect64 interpolation requires an observed writable morph owner.");
        }
        int selectedShape = node.Word40 != node.Word42 && node.Scalar38 == 4096 ? node.Word42 : node.Word40;
        uint vertices = checked(geometryAddress + 12 + (uint)(selectedShape * count * 8));
        (frame, uint temporary) = Allocate(frame, 108);
        NativeBattleActionMatrix rotation = (node.Header & 0x40000) != 0 ? NativeBattleGfMatrices.Euler7015B0(node.Vector16) : NativeBattleGfMatrices.Euler701310(node.Vector16);
        (byte[] bytes, bool[] known) = caller68.ReadSnapshot(0, 32);
        NativeBattlePackedMatrix32 caller = new(bytes.ToImmutableArray(), known.ToImmutableArray());
        NativeBattleActionMatrix basis = caller.ReadMatrix();
        int x = node.Vector8.X;
        int y = node.Vector8.Y;
        int z = node.Vector8.Z;
        if (Dword(caller68, 0x2C) != 0)
        {
            int sx = unchecked((int)Dword(caller68, 0x20));
            int sy = unchecked((int)Dword(caller68, 0x24));
            int sz = unchecked((int)Dword(caller68, 0x28));
            x = unchecked((short)(unchecked(x * sx) >> 12));
            y = unchecked((short)(unchecked(y * sy) >> 12));
            z = unchecked((short)(unchecked(z * sz) >> 12));
            rotation = NativeBattlePoseMatrices.Scale56BEF0(rotation, sx, sy, sz);
        }
        if ((node.Header & 0x200) == 0)
        {
            frame = LoadMatrix(frame, caller, true, false);
            NativeBattleGeometryRegisterStep registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(Registers(frame), 0, new(unchecked((short)x), unchecked((short)y), unchecked((short)z)), null), false);
            x = registers.Colors.Mac[0]!.Value;
            y = registers.Colors.Mac[1]!.Value;
            z = registers.Colors.Mac[2]!.Value;
            frame = Publish(frame, registers);
            rotation = NativeBattleGfMatrices.Multiply56C090(basis, rotation);
        }
        rotation = rotation with { X = unchecked(basis.X + x), Y = unchecked(basis.Y + y), Z = unchecked(basis.Z + z) };
        NativeBattleVector scale = node.Vector24;
        if (scale is not { X: 4096, Y: 4096, Z: 4096 })
        {
            if ((node.Header & 0x100) != 0)
            {
                rotation = rotation with { Rotation = NativeBattleGfMatrices.Multiply56C090(rotation, new([scale.X, 0, 0, 0, scale.Y, 0, 0, 0, scale.Z], 0, 0, 0, 4096)).Rotation };
            }
            else
            {
                rotation = NativeBattlePoseMatrices.Scale56BEF0(rotation, scale.X, scale.Y, scale.Z);
            }
        }
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(rotation, null));
        NativeBattleEffectArena context = Context(frame, temporary, 108).Write(0, BitConverter.GetBytes(geometryAddress)).Write(4, BitConverter.GetBytes(vertices)).Write(12, BitConverter.GetBytes((int)node.Scalar36)).Write(0x18, BitConverter.GetBytes((int)BinaryPrimitives.ReadInt16LittleEndian(caller68.Read(0x34, 2)))).Write(0x1C, BitConverter.GetBytes(node.Scalar36 == 0 ? 0x2030u : 0x20F0u));
        if (node.Scalar36 != 0)
        {
            context = context.Write(8, BitConverter.GetBytes(node.Color32));
        }
        context = context.Write(0x20, new byte[8]).Write(0x28, BitConverter.GetBytes((ushort)256)).Write(0x2A, BitConverter.GetBytes((ushort)256));
        byte selector = resources.ReadStatic(checked(Dword(caller68, 0x30) + node.GeometryIndex), 1)[0];
        if (selector == (byte)'1')
        {
            context = context.Write(0x2C, BitConverter.GetBytes((ushort)0)).Write(0x2E, BitConverter.GetBytes((ushort)128)).Write(0x30, BitConverter.GetBytes((ushort)256)).Write(0x32, BitConverter.GetBytes((ushort)128)).Write(0x22, BitConverter.GetBytes((ushort)(caller68.Read(0x36, 1)[0] & 0x7F)));
        }
        NativeBattleWindowedMeshContext108Step drawn = NativeBattleWindowedMeshContext108.DrawLoaded(frame.Memory, mesh, inputs.ReadProjection(frame), context, inputs.Colors, 17, 2, count, vertexOffset =>
        {
            ReadOnlySpan<byte> row = source.Data.AsSpan().Slice(checked((int)(vertices - source.Address)) + vertexOffset, 8);
            return (new NativeBattleVector(BinaryPrimitives.ReadInt16LittleEndian(row), BinaryPrimitives.ReadInt16LittleEndian(row[2..]), BinaryPrimitives.ReadInt16LittleEndian(row[4..])), BinaryPrimitives.ReadInt16LittleEndian(row[6..]));
        });
        frame = PublishContext(frame with { Memory = drawn.Memory }, temporary, drawn.Context108, 108);
        frame = inputs.Execute(frame, new NativeBattleEffect64DeliverBatch(drawn.Batch, 0x254508C));
        return Release(frame, 108);
    }

    public static NativeBattleEffectArena AddOffset(NativeBattleEffect64Frame frame, NativeBattleEffectArena point8, uint offsets, int bone)
    {
        NativeBattleEffectArena row = ReadBytes(frame, unchecked(offsets + (uint)(bone * 16)), 6);
        for (int axis = 0; axis < 3; axis++)
        {
            short value = BinaryPrimitives.ReadInt16LittleEndian(point8.Read(axis * 2, 2));
            short extra = BinaryPrimitives.ReadInt16LittleEndian(row.Read(axis * 2, 2));
            point8 = point8.Write(axis * 2, BitConverter.GetBytes(unchecked((short)(value + extra))));
        }
        return point8;
    }
    private static short Signed(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static uint Dword(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
}
