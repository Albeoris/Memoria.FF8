using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleBoomerangSword304Controller
{
    public static (NativeBattleBoomerangSword304Frame Frame, int Result) AdvanceProgramme66A3F0(NativeBattleBoomerangSword304Frame frame, uint address, NativeBattleBoomerangSword304Resources resources, NativeBattleBoomerangSword304Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        NativeBattleEffectArena caller = NativeBattleEffectNodeContextMemory.Copy(TaskView(task, 16, 8), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 16), 0, 8).Write(8, BitConverter.GetBytes(-128)).Write(12, BitConverter.GetBytes(NativeBattleBoomerangSword304Profile.ArenaAddress + 0x1558));
        (frame, int remaining) = PlayProgramme304(frame, address, 24, 460, resources.Programme, caller, resources, inputs);
        return (frame, remaining == 0 ? 2 : 0);
    }

    private static (NativeBattleBoomerangSword304Frame Frame, int Remaining) PlayProgramme304(NativeBattleBoomerangSword304Frame frame, uint address, int workspaceOffset, int workspaceBytes, NativeBattleGfAnimationSource source, NativeBattleEffectArena caller16, NativeBattleBoomerangSword304Resources resources, NativeBattleBoomerangSword304Inputs inputs)
    {
        NativeBattleGfAnimationWorkspace Workspace()
        {
            NativeBattleModulePoolTask task = Read(frame, address);
            task.RequireKnownPayload(workspaceOffset, workspaceBytes);
            return NativeBattleEffectTaskMemory.BorrowAnimationWorkspace(frame.Memory, frame.Effect.Pools, address + (uint)workspaceOffset, workspaceBytes);
        }
        NativeBattleGfAnimationAdvance advanced = Workspace().Advance701970(source, 0, (current, node) =>
        {
            NativeBattleEffectTaskMemory.PublishAnimationWorkspace(frame.Memory, frame.Effect.Pools, address + (uint)workspaceOffset, current);
            frame = DrawProgrammeNode66A440(frame, source, node, caller16, resources, inputs, pilotIdentity: inputs.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(304, Read(frame, address), node.ResourceOffset) with { ResourceGeneration = ((long)source.Address << 32) | (uint)workspaceOffset } : null);
            return Workspace();
        });
        NativeBattleEffectTaskMemory.PublishAnimationWorkspace(frame.Memory, frame.Effect.Pools, address + (uint)workspaceOffset, advanced.Workspace);
        return (frame, advanced.Remaining);
    }

    public static NativeBattleBoomerangSword304Frame DrawProgrammeNode66A440(NativeBattleBoomerangSword304Frame frame, NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleEffectArena caller16, NativeBattleBoomerangSword304Resources resources, NativeBattleBoomerangSword304Inputs inputs, NativeBattlePilotVisualIdentity? pilotIdentity = null)
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
            throw new InvalidDataException("Boomerang Sword programme shape leaves its original geometry.");
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
            frame = MorphProgramme304(frame, mesh, first, second, ratio, BinaryPrimitives.ReadUInt32LittleEndian(caller16.Read(12, 4)));
            vertices = BinaryPrimitives.ReadUInt32LittleEndian(caller16.Read(12, 4));
        }
        NativeBattleActionMatrix rotation = (node.Header & 0x40000) != 0 ? NativeBattleGfMatrices.Euler7015B0(node.Vector16) : NativeBattleGfMatrices.Euler701310(node.Vector16);
        NativeBattlePackedMatrix32 view = inputs.ReadView(frame);
        NativeBattleActionMatrix camera = view.ReadMatrix();
        NativeBattleVector anchor = new(BinaryPrimitives.ReadInt16LittleEndian(caller16.Read(0, 2)), BinaryPrimitives.ReadInt16LittleEndian(caller16.Read(2, 2)), BinaryPrimitives.ReadInt16LittleEndian(caller16.Read(4, 2)));
        NativeBattleVector point;
        short? w = null;
        bool direct = (node.Header & 0x200) != 0;
        if (direct)
        {
            point = anchor;
            (byte[] data, bool[] known) = caller16.ReadSnapshot(6, 2);
            w = known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(data) : null;
        }
        else
        {
            point = new(unchecked((short)(anchor.X + node.Vector8.X)), unchecked((short)(anchor.Y + node.Vector8.Y)), unchecked((short)(anchor.Z + node.Vector8.Z)));
        }
        frame = LoadMatrix(frame, view, true, false);
        NativeBattleGeometryRegisterStep registers = new(frame.Memory.GeometryRegisters!, frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Boomerang Sword node requires its current arithmetic owner."));
        registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(registers, 0, point, w), false);
        int x = registers.Colors.Mac[0]!.Value;
        int y = registers.Colors.Mac[1]!.Value;
        int z = registers.Colors.Mac[2]!.Value;
        frame = frame with { Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } };
        if (direct)
        {
            x = unchecked(x + node.Vector8.X);
            y = unchecked(y + node.Vector8.Y);
            z = unchecked(z + node.Vector8.Z);
        }
        else
        {
            rotation = NativeBattleGfMatrices.Multiply56C090(camera, rotation);
        }
        rotation = rotation with { X = unchecked(camera.X + x), Y = unchecked(camera.Y + y), Z = unchecked(camera.Z + z) };
        if (scale is not { X: 4096, Y: 4096, Z: 4096 })
        {
            if ((node.Header & 0x100) != 0)
            {
                NativeBattleActionMatrix scaled = NativeBattleGfMatrices.Multiply56C090(rotation, new([scale.X, 0, 0, 0, scale.Y, 0, 0, 0, scale.Z], 0, 0, 0, 4096));
                rotation = rotation with { Rotation = scaled.Rotation };
            }
            else
            {
                rotation = NativeBattlePoseMatrices.Scale56BEF0(rotation, scale.X, scale.Y, scale.Z);
            }
        }
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(rotation, null));
        int depthBias = BinaryPrimitives.ReadInt32LittleEndian(caller16.Read(8, 4));
        NativeBattleEffectArena context = Context(frame, temporary, 88).Write(0, BitConverter.GetBytes(geometryAddress)).Write(4, BitConverter.GetBytes(vertices)).Write(12, BitConverter.GetBytes((int)node.Scalar36)).Write(24, BitConverter.GetBytes(depthBias)).Write(28, BitConverter.GetBytes(node.Scalar36 == 0 ? 0x2030u : 0x20F0u));
        if (node.Scalar36 != 0)
        {
            context = context.Write(8, BitConverter.GetBytes(node.Color32));
        }
        frame = PublishContext(frame, temporary, context, 88);
        frame = inputs.Execute(frame, new NativeBattleBoomerangSword304DrawMesh7043B0(temporary, geometryAddress, vertices) { PilotIdentity = pilotIdentity });
        return Release(frame, 88);
    }

    private static NativeBattleBoomerangSword304Frame MorphProgramme304(NativeBattleBoomerangSword304Frame frame, NativeBattleStaticMeshResource mesh, int first, int second, int ratio, uint destination)
    {
        if (destination != NativeBattleBoomerangSword304Profile.ArenaAddress + 0x1558)
        {
            throw new InvalidDataException("Boomerang Sword morph requires its current programme scratch.");
        }
        int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4));
        if (count < 1 || count * 8 > 408)
        {
            throw new InvalidDataException("Boomerang Sword morph leaves its actual current scratch extent.");
        }
        NativeBattleGeometryRegisterStep registers = new(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Boomerang Sword morph requires its geometry owner."), frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Boomerang Sword morph requires its arithmetic owner."));
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
