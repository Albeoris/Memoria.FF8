using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect303Controller
{
    public static (NativeBattleEffect303Frame Frame, int Remaining) DrawProgramme303(NativeBattleEffect303Frame frame, uint taskAddress, int workspaceOffset, int workspaceBytes, NativeBattleGfAnimationSource source, NativeBattleEffectArena caller32, NativeBattleEffect303Resources resources, NativeBattleEffect303Inputs inputs)
    {
        NativeBattleGfAnimationWorkspace Workspace()
        {
            NativeBattleModulePoolTask task = Read(frame, taskAddress);
            task.RequireKnownPayload(workspaceOffset, workspaceBytes);
            return NativeBattleGfAnimationWorkspace.Borrow(task, 12 + (workspaceOffset - 12), workspaceBytes);
        }
        NativeBattleGfAnimationAdvance advanced = Workspace().Advance701970(source, 0, (current, node) =>
        {
            NativeBattleEffectTaskMemory.PublishAnimationWorkspace(frame.Memory, frame.Effect.Pools, taskAddress + (uint)workspaceOffset, current);
            frame = DrawProgrammeNode66AE20(frame, source, node, caller32, resources, inputs);
            return Workspace();
        });
        NativeBattleEffectTaskMemory.PublishAnimationWorkspace(frame.Memory, frame.Effect.Pools, taskAddress + (uint)workspaceOffset, advanced.Workspace);
        return (frame, advanced.Remaining);
    }

    public static NativeBattleEffect303Frame DrawProgrammeNode66AE20(NativeBattleEffect303Frame frame, NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleEffectArena caller32, NativeBattleEffect303Resources resources, NativeBattleEffect303Inputs inputs)
    {
        NativeBattleVector scale = node.Vector24;
        if (scale is { X: 0, Y: 0, Z: 0 } || node.Scalar36 >= 4096 && node.Color32 == 0)
        {
            return frame;
        }
        int geometryOffset = BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(8 + node.GeometryIndex * 4));
        uint geometryAddress = checked(source.Address + (uint)geometryOffset);
        NativeBattleStaticMeshResource mesh = resources.ReadGeometry(geometryAddress);
        int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4));
        int shapes = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(8));
        int first = node.Word40;
        int second = node.Word42;
        int ratio = node.Scalar38;
        if (first < 0 || second < 0 || first >= shapes || second >= shapes)
        {
            throw new InvalidDataException("Effect303 programme shape leaves its original geometry.");
        }
        (frame, uint temporary) = Allocate(frame, 88);
        uint vertices;
        if (first == second || ratio == 0 || ratio == 4096)
        {
            int selected = ratio == 4096 && first != second ? second : first;
            vertices = checked(geometryAddress + 12 + (uint)(selected * count * 8));
        }
        else
        {
            frame = MorphProgramme303(frame, mesh, first, second, ratio, BinaryPrimitives.ReadUInt32LittleEndian(caller32.Read(28, 4)));
            vertices = BinaryPrimitives.ReadUInt32LittleEndian(caller32.Read(28, 4));
        }
        NativeBattleActionMatrix rotation = (node.Header & 0x40000) != 0 ? NativeBattleGfMatrices.Euler7015B0(node.Vector16) : NativeBattleGfMatrices.Euler701310(node.Vector16);
        NativeBattleVector local = node.Vector8;
        if (BinaryPrimitives.ReadInt32LittleEndian(caller32.Read(20, 4)) != 0)
        {
            int[] callerScale = Enumerable.Range(0, 3).Select(axis => BinaryPrimitives.ReadInt32LittleEndian(caller32.Read(8 + axis * 4, 4))).ToArray();
            local = new(unchecked((short)(unchecked(local.X * callerScale[0]) >> 12)), unchecked((short)(unchecked(local.Y * callerScale[1]) >> 12)), unchecked((short)(unchecked(local.Z * callerScale[2]) >> 12)));
            rotation = NativeBattlePoseMatrices.Scale56BEF0(rotation, callerScale[0], callerScale[1], callerScale[2]);
        }
        NativeBattlePackedMatrix32 view = inputs.ReadView(frame);
        NativeBattleActionMatrix camera = view.ReadMatrix();
        NativeBattleVector anchor = Vector303(caller32);
        bool direct = (node.Header & 0x200) != 0;
        NativeBattleVector point = direct ? anchor : new(unchecked((short)(anchor.X + local.X)), unchecked((short)(anchor.Y + local.Y)), unchecked((short)(anchor.Z + local.Z)));
        frame = LoadMatrix(frame, view, true, false);
        NativeBattleGeometryRegisterStep registers = Registers303(frame);
        registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(registers, 0, point, direct ? W303(caller32) : null), false);
        int x = registers.Colors.Mac[0]!.Value;
        int y = registers.Colors.Mac[1]!.Value;
        int z = registers.Colors.Mac[2]!.Value;
        frame = Publish303(frame, registers);
        if (direct)
        {
            x = unchecked(x + local.X);
            y = unchecked(y + local.Y);
            z = unchecked(z + local.Z);
        }
        else
        {
            rotation = NativeBattleGfMatrices.Multiply56C090(camera, rotation);
        }
        rotation = rotation with { X = unchecked(camera.X + x), Y = unchecked(camera.Y + y), Z = unchecked(camera.Z + z) };
        if (scale is not { X: 4096, Y: 4096, Z: 4096 })
        {
            rotation = NativeBattlePoseMatrices.Scale56BEF0(rotation, scale.X, scale.Y, scale.Z);
        }
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(rotation, null));
        int depthBias = BinaryPrimitives.ReadInt32LittleEndian(caller32.Read(24, 4));
        NativeBattleEffectArena context = Context(frame, temporary, 88).Write(0, BitConverter.GetBytes(geometryAddress)).Write(4, BitConverter.GetBytes(vertices)).Write(12, BitConverter.GetBytes((int)node.Scalar36)).Write(24, BitConverter.GetBytes(depthBias)).Write(28, BitConverter.GetBytes(node.Scalar36 == 0 ? 0x2030u : 0x20F0u));
        if (node.Scalar36 != 0)
        {
            context = context.Write(8, BitConverter.GetBytes(node.Color32));
        }
        frame = PublishContext(frame, temporary, context, 88);
        frame = inputs.Execute(frame, new NativeBattleEffect303DrawMesh7043B0(temporary, geometryAddress, vertices));
        return Release(frame, 88);
    }

    private static NativeBattleEffect303Frame MorphProgramme303(NativeBattleEffect303Frame frame, NativeBattleStaticMeshResource mesh, int first, int second, int ratio, uint destination)
    {
        if (destination != NativeBattleEffect303Profile.ArenaAddress + 0x3688)
        {
            throw new InvalidDataException("Effect303 morph requires its current programme scratch.");
        }
        int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4));
        if (count < 1 || count * 8 > 384)
        {
            throw new InvalidDataException("Effect303 morph leaves its actual current scratch extent.");
        }
        NativeBattleGeometryRegisterStep registers = new(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Effect303 morph requires its geometry owner."), frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect303 morph requires its arithmetic owner."));
        for (int vertex = 0; vertex < count; vertex++)
        {
            int a = 12 + (first * count + vertex) * 8;
            int b = 12 + (second * count + vertex) * 8;
            registers = NativeBattleGeometry.LoadIrColumn(registers with { Colors = registers.Colors with { Ir0 = unchecked((uint)(4096 - ratio)) } }, ShapeWord(a), ShapeWord(a + 2), ShapeWord(a + 4));
            registers = NativeBattleRaldo19BallRendering.Weighted(registers, false, NativeBattleGteColorInterpolation.CodeConstants45F270);
            registers = NativeBattleGeometry.LoadIrColumn(registers with { Colors = registers.Colors with { Ir0 = unchecked((uint)ratio) } }, ShapeWord(b), ShapeWord(b + 2), ShapeWord(b + 4));
            registers = NativeBattleRaldo19BallRendering.Weighted(registers, true, NativeBattleGteColorInterpolation.CodeConstants45F270);
            NativeBattleEffectArena result = NativeBattleEffectArena.Clear();
            for (int axis = 0; axis < 3; axis++)
            {
                result = result.Write(axis * 2, BitConverter.GetBytes(unchecked((short)registers.Colors.Ir[axis]!.Value)));
            }
            frame = WriteBytes(frame, checked(destination + (uint)(vertex * 8)), result, 6);
        }
        return frame with { Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } };

        ushort ShapeWord(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(mesh.Bytes.AsSpan().Slice(offset));
    }

}
