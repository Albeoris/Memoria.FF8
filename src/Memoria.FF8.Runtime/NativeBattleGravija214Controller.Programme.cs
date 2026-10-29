using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleGravija214Controller
{
    public static NativeBattleGravija214Frame DrawProgramme69DC40(NativeBattleGravija214Frame frame, uint address, NativeBattleGravija214Resources resources, NativeBattleGravija214Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        NativeBattleVector position = new(Signed(task, 0x28), Signed(task, 0x2A), Signed(task, 0x2C));
        NativeBattleGfAnimationWorkspace Workspace()
        {
            NativeBattleModulePoolTask current = Read(frame, address);
            current.RequireKnownPayload(0x30, 756);
            return NativeBattleGfAnimationWorkspace.Borrow(current, 0x30, 756);
        }
        NativeBattleGfAnimationAdvance advanced = Workspace().Advance701970(resources.Programme, 0, (current, node) =>
        {
            NativeBattleEffectTaskMemory.PublishAnimationWorkspace(frame.Memory, frame.Effect.Pools, address + 0x30, current);
            frame = DrawProgrammeNode214(frame, resources.Programme, node, position, -128, resources, inputs, Signed(Read(frame, address), 0x2E), inputs.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(214, Read(frame, address), node.ResourceOffset) with { ResourceGeneration = ((long)resources.Programme.Address << 32) | 0x30 } : null);
            return Workspace();
        });
        NativeBattleEffectTaskMemory.PublishAnimationWorkspace(frame.Memory, frame.Effect.Pools, address + 0x30, advanced.Workspace);
        return frame;
    }

    public static NativeBattleGravija214Frame DrawProgrammeNode214(NativeBattleGravija214Frame frame, NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleVector callerPosition, int depthBias, NativeBattleGravija214Resources resources, NativeBattleGravija214Inputs inputs, short? callerW = null, NativeBattlePilotVisualIdentity? pilotIdentity = null)
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
            throw new InvalidDataException("Gravija programme shape leaves its original geometry.");
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
            frame = MorphProgramme214(frame, mesh, first, second, ratio);
            vertices = 0x20E04DC;
        }
        NativeBattleActionMatrix rotation = (node.Header & 0x400) != 0 ? NativeBattleGfMatrices.Euler701430(node.Vector16) : (node.Header & 0x40000) != 0 ? NativeBattleGfMatrices.Euler7015B0(node.Vector16) : NativeBattleGfMatrices.Euler701310(node.Vector16);
        NativeBattlePackedMatrix32 view = inputs.ReadView(frame);
        NativeBattleActionMatrix camera = view.ReadMatrix();
        NativeBattleVector operand = (node.Header & 0x200) != 0 ? callerPosition : new(unchecked((short)(callerPosition.X + node.Vector8.X)), unchecked((short)(callerPosition.Y + node.Vector8.Y)), unchecked((short)(callerPosition.Z + node.Vector8.Z)));
        frame = LoadMatrix(frame, view, true, false);
        NativeBattleGeometryRegisterStep registers = new(frame.Memory.GeometryRegisters!, frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Gravija node requires its current arithmetic owner."));
        registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(registers, 0, operand, (node.Header & 0x200) != 0 ? callerW : null), false);
        int x = registers.Colors.Mac[0]!.Value;
        int y = registers.Colors.Mac[1]!.Value;
        int z = registers.Colors.Mac[2]!.Value;
        frame = frame with { Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } };
        if ((node.Header & 0x200) != 0)
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
            rotation = NativeBattlePoseMatrices.Scale56BEF0(rotation, scale.X, scale.Y, scale.Z);
        }
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(rotation, null));
        NativeBattleEffectArena context = Context(frame, temporary, 88).Write(0, BitConverter.GetBytes(geometryAddress)).Write(4, BitConverter.GetBytes(vertices)).Write(12, BitConverter.GetBytes((int)node.Scalar36)).Write(24, BitConverter.GetBytes(depthBias)).Write(28, BitConverter.GetBytes(node.Scalar36 == 0 ? 0x2030u : 0x20F0u));
        if (node.Scalar36 != 0)
        {
            context = context.Write(8, BitConverter.GetBytes(node.Color32));
        }
        frame = PublishContext(frame, temporary, context, 88);
        frame = inputs.Execute(frame, new NativeBattleGravija214DrawMesh7043B0(temporary, geometryAddress, vertices) { PilotIdentity = pilotIdentity });
        return Release(frame, 88);
    }

    private static NativeBattleGravija214Frame MorphProgramme214(NativeBattleGravija214Frame frame, NativeBattleStaticMeshResource mesh, int first, int second, int ratio)
    {
        int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4));
        NativeBattleGeometryRegisterStep registers = new(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Gravija morph requires its geometry owner."), frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Gravija morph requires its arithmetic owner."));
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
            frame = WriteBytes(frame, checked(0x20E04DC + (uint)(vertex * 8)), result, 6);
        }
        return frame with { Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } };

        ushort ShapeWord(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(mesh.Bytes.AsSpan().Slice(offset));
    }

}
