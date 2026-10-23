using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEvilEye282Controller
{
    public static (NativeBattleEvilEye282Frame Frame, int Remaining) DrawProgramme67D820(NativeBattleEvilEye282Frame frame, uint address, int workspaceOffset, int workspaceBytes, NativeBattlePackedMatrix32 caller, NativeBattleEvilEye282Resources resources, NativeBattleEvilEye282Inputs inputs)
    {
        if ((workspaceOffset, workspaceBytes) is not ((0x1C, 1352) or (0x18, 384)))
        {
            throw new InvalidDataException("Evil-Eye programme requires its actual workspace extent.");
        }
        NativeBattleGfAnimationWorkspace Workspace()
        {
            NativeBattleModulePoolTask current = Read(frame, address);
            current.RequireKnownPayload(workspaceOffset, workspaceBytes);
            return new(current.Payload.AsSpan().Slice(workspaceOffset - 12).ToArray().ToImmutableArray());
        }
        NativeBattleGfAnimationSource source = resources.ReadProgramme(Workspace().ResourceAddress);
        NativeBattleGfAnimationAdvance advanced = Workspace().Advance701970(source, 0, (current, node) =>
        {
            frame = WriteBytes(frame, address + (uint)workspaceOffset, NativeBattleEffectArena.Clear().Write(0, current.Data.AsSpan().Slice(0, workspaceBytes)), workspaceBytes);
            frame = DrawProgrammeNode282(frame, source, node, caller, -128, resources, inputs);
            return Workspace();
        });
        frame = WriteBytes(frame, address + (uint)workspaceOffset, NativeBattleEffectArena.Clear().Write(0, advanced.Workspace.Data.AsSpan().Slice(0, workspaceBytes)), workspaceBytes);
        return (frame, advanced.Remaining);
    }

    private static NativeBattlePackedMatrix32 ReadNormalized282(NativeBattleEvilEye282Frame frame)
    {
        (byte[] bytes, bool[] known) = frame.State.ReadView(0x2507F08, 32).ReadSnapshot(0, 32);
        return new(bytes.ToImmutableArray(), known.ToImmutableArray());
    }

    public static NativeBattleEvilEye282Frame DrawProgrammeNode282(NativeBattleEvilEye282Frame frame, NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattlePackedMatrix32 caller, int depthBias, NativeBattleEvilEye282Resources resources, NativeBattleEvilEye282Inputs inputs)
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
            throw new InvalidDataException("Evil-Eye programme shape leaves its original geometry.");
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
            frame = MorphProgramme282(frame, mesh, first, second, ratio);
            vertices = 0x20E0EE4;
        }
        NativeBattleActionMatrix rotation = (node.Header & 0x40000) != 0 ? NativeBattleGfMatrices.Euler7015B0(node.Vector16) : NativeBattleGfMatrices.Euler701310(node.Vector16);
        NativeBattleActionMatrix callerMatrix = caller.ReadMatrix();
        int x = node.Vector8.X;
        int y = node.Vector8.Y;
        int z = node.Vector8.Z;
        bool normalized = (node.Header & 0x1000) != 0;
        if (normalized || (node.Header & 0x200) == 0)
        {
            NativeBattlePackedMatrix32 parent = normalized ? ReadNormalized282(frame) : caller;
            frame = LoadMatrix(frame, parent, true, false);
            NativeBattleGeometryRegisterStep registers = new(frame.Memory.GeometryRegisters!, frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Evil-Eye node requires its current arithmetic owner."));
            registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(registers, 0, node.Vector8, null), false);
            x = registers.Colors.Mac[0]!.Value;
            y = registers.Colors.Mac[1]!.Value;
            z = registers.Colors.Mac[2]!.Value;
            frame = frame with { Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } };
            if ((node.Header & 0x8000) == 0)
            {
                rotation = NativeBattleGfMatrices.Multiply56C090(parent.ReadMatrix(), rotation);
            }
        }
        rotation = rotation with { X = unchecked(callerMatrix.X + x), Y = unchecked(callerMatrix.Y + y), Z = unchecked(callerMatrix.Z + z) };
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
        frame = inputs.Execute(frame, new NativeBattleEvilEye282DrawMesh7043B0(temporary, geometryAddress, vertices));
        return Release(frame, 88);
    }

    private static NativeBattleEvilEye282Frame MorphProgramme282(NativeBattleEvilEye282Frame frame, NativeBattleStaticMeshResource mesh, int first, int second, int ratio)
    {
        int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4));
        NativeBattleGeometryRegisterStep registers = new(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Evil-Eye morph requires its geometry owner."), frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Evil-Eye morph requires its arithmetic owner."));
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
            frame = WriteBytes(frame, checked(0x20E0EE4 + (uint)(vertex * 8)), result, 6);
        }
        return frame with { Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } };

        ushort ShapeWord(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(mesh.Bytes.AsSpan().Slice(offset));
    }

}
