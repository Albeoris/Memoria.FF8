using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleStare45Controller
{
    public static NativeBattleStare45Frame DrawProgrammeNode45(NativeBattleStare45Frame frame, NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattlePackedMatrix32 caller, int depthBias, NativeBattleStare45Resources resources, NativeBattleStare45Inputs inputs, NativeBattlePilotVisualIdentity? pilotIdentity = null)
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
            throw new InvalidDataException("Stare programme shape leaves its original geometry.");
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
            frame = MorphProgramme45(frame, mesh, first, second, ratio);
            vertices = 0x2655428;
        }
        NativeBattleActionMatrix rotation = (node.Header & 0x40000) != 0 ? NativeBattleGfMatrices.Euler7015B0(node.Vector16) : NativeBattleGfMatrices.Euler701310(node.Vector16);
        NativeBattleActionMatrix callerMatrix = caller.ReadMatrix();
        bool billboard = (node.Header & 0x1000) != 0;
        bool direct = !billboard && (node.Header & 0x200) != 0;
        int x = node.Vector8.X;
        int y = node.Vector8.Y;
        int z = node.Vector8.Z;
        if (!direct)
        {
            NativeBattlePackedMatrix32 packed = billboard ? ReadProgrammeBasis45(frame) : caller;
            frame = LoadMatrix(frame, packed, true, false);
            NativeBattleGeometryRegisterStep registers = new(frame.Memory.GeometryRegisters!, frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Stare node transform requires its arithmetic owner."));
            registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(registers, 0, node.Vector8, null), false);
            x = registers.Colors.Mac[0]!.Value;
            y = registers.Colors.Mac[1]!.Value;
            z = registers.Colors.Mac[2]!.Value;
            frame = frame with { Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } };
            rotation = NativeBattleGfMatrices.Multiply56C090(packed.ReadMatrix(), rotation);
        }
        rotation = rotation with { X = unchecked(callerMatrix.X + x), Y = unchecked(callerMatrix.Y + y), Z = unchecked(callerMatrix.Z + z) };
        if (scale is not { X: 4096, Y: 4096, Z: 4096 })
        {
            rotation = NativeBattlePoseMatrices.Scale56BEF0(rotation, scale.X, scale.Y, scale.Z);
        }
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(rotation, null));
        NativeBattleEffectArena context = Context(frame, temporary, 104).Write(0, BitConverter.GetBytes(geometryAddress)).Write(4, BitConverter.GetBytes(vertices)).Write(12, BitConverter.GetBytes((int)node.Scalar36)).Write(16, BitConverter.GetBytes(depthBias)).Write(20, BitConverter.GetBytes(node.Scalar36 == 0 ? 0x2030u : 0x20F0u));
        if (node.Scalar36 != 0)
        {
            context = context.Write(8, BitConverter.GetBytes(node.Color32));
        }
        for (int offset = 0x18; offset <= 0x2A; offset += 2)
        {
            context = context.Write(offset, BitConverter.GetBytes((ushort)(offset is 0x20 or 0x22 or 0x28 or 0x2A ? 256 : 0)));
        }
        frame = PublishContext(frame, temporary, context, 104);
        frame = inputs.Execute(frame, new NativeBattleStare45DrawMesh104(temporary, geometryAddress, vertices) { PilotIdentity = pilotIdentity });
        return Release(frame, 104);
    }

    private static NativeBattlePackedMatrix32 ReadProgrammeBasis45(NativeBattleStare45Frame frame)
    {
        NativeBattleEffectArena basis = ReadBytes(frame, 0x2655758, 32);
        (byte[] bytes, bool[] known) = basis.ReadSnapshot(0, 32);
        return new(bytes.ToImmutableArray(), known.ToImmutableArray());
    }

    private static NativeBattleStare45Frame MorphProgramme45(NativeBattleStare45Frame frame, NativeBattleStaticMeshResource mesh, int first, int second, int ratio)
    {
        int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4));
        NativeBattleGeometryRegisterStep registers = new(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Stare morph requires its geometry owner."), frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Stare morph requires its arithmetic owner."));
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
            frame = frame with { State = frame.State.WriteView(checked(0x2655428 + (uint)(vertex * 8)), result, 6) };
        }
        return frame with { Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } };

        ushort ShapeWord(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(mesh.Bytes.AsSpan().Slice(offset));
    }

}
