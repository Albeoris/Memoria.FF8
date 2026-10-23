using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect283Controller
{
    public static NativeBattleEffect283Frame DrawProgrammeNode67C8A0(NativeBattleEffect283Frame frame, NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleEffectArena caller32, NativeBattleEffect283Resources resources, NativeBattleEffect283Inputs inputs)
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
            throw new InvalidDataException("Effect283 programme shape leaves its original geometry.");
        }
        (frame, uint temporary) = Allocate(frame, 108);
        uint vertices;
        if (first == second || ratio == 0 || ratio == 4096)
        {
            int selected = ratio == 4096 && first != second ? second : first;
            vertices = checked(geometryAddress + 12 + (uint)(selected * count * 8));
        }
        else
        {
            frame = MorphProgramme283(frame, mesh, first, second, ratio, BinaryPrimitives.ReadUInt32LittleEndian(caller32.Read(28, 4)));
            vertices = BinaryPrimitives.ReadUInt32LittleEndian(caller32.Read(28, 4));
        }
        NativeBattleActionMatrix rotation = NativeBattleGfMatrices.Euler701310(node.Vector16);
        NativeBattleVector local = node.Vector8;
        if (BinaryPrimitives.ReadInt32LittleEndian(caller32.Read(20, 4)) != 0)
        {
            int[] callerScale = Enumerable.Range(0, 3).Select(axis => BinaryPrimitives.ReadInt32LittleEndian(caller32.Read(8 + axis * 4, 4))).ToArray();
            local = new(unchecked((short)(unchecked(local.X * callerScale[0]) >> 12)), unchecked((short)(unchecked(local.Y * callerScale[1]) >> 12)), unchecked((short)(unchecked(local.Z * callerScale[2]) >> 12)));
            rotation = NativeBattlePoseMatrices.Scale56BEF0(rotation, callerScale[0], callerScale[1], callerScale[2]);
        }
        NativeBattlePackedMatrix32 view = inputs.ReadView(frame);
        NativeBattleActionMatrix camera = view.ReadMatrix();
        NativeBattleVector anchor = Vector283(caller32);
        NativeBattlePackedMatrix32 basis = view;
        frame = LoadMatrix(frame, view, true, false);
        NativeBattleGeometryRegisterStep registers = Registers283(frame);
        bool separate = (node.Header & 0x1000) != 0;
        NativeBattleVector point = separate ? anchor : new(unchecked((short)(anchor.X + local.X)), unchecked((short)(anchor.Y + local.Y)), unchecked((short)(anchor.Z + local.Z)));
        registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(registers, 0, point, separate ? W283(caller32) : null), false);
        int x = registers.Colors.Mac[0]!.Value;
        int y = registers.Colors.Mac[1]!.Value;
        int z = registers.Colors.Mac[2]!.Value;
        frame = Publish283(frame, registers);
        if (separate)
        {
            (byte[] bytes, bool[] known) = ReadBytes(frame, 0x2507E70, 32).ReadSnapshot(0, 32);
            basis = new(bytes.ToImmutableArray(), known.ToImmutableArray());
            frame = LoadMatrix(frame, basis, true, false);
            registers = Registers283(frame);
            registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(registers, 0, local, null), false);
            x = unchecked(x + registers.Colors.Mac[0]!.Value);
            y = unchecked(y + registers.Colors.Mac[1]!.Value);
            z = unchecked(z + registers.Colors.Mac[2]!.Value);
            frame = Publish283(frame, registers);
        }
        if ((node.Header & 0x8000) == 0)
        {
            rotation = NativeBattleGfMatrices.Multiply56C090(basis.ReadMatrix(), rotation);
        }
        rotation = rotation with { X = unchecked(camera.X + x), Y = unchecked(camera.Y + y), Z = unchecked(camera.Z + z) };
        if (scale is not { X: 4096, Y: 4096, Z: 4096 })
        {
            rotation = NativeBattlePoseMatrices.Scale56BEF0(rotation, scale.X, scale.Y, scale.Z);
        }
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(rotation, null));
        int depthBias = BinaryPrimitives.ReadInt16LittleEndian(caller32.Read(24, 2));
        NativeBattleEffectArena context = Context(frame, temporary, 108).Write(0, BitConverter.GetBytes(geometryAddress)).Write(4, BitConverter.GetBytes(vertices)).Write(12, BitConverter.GetBytes((int)node.Scalar36)).Write(24, BitConverter.GetBytes(depthBias)).Write(28, BitConverter.GetBytes(node.Scalar36 == 0 ? 0x2030u : 0x20F0u)).Write(0x20, BitConverter.GetBytes((ushort)0));
        if (node.Scalar36 != 0)
        {
            context = context.Write(8, BitConverter.GetBytes(node.Color32));
        }
        ushort window = BinaryPrimitives.ReadUInt16LittleEndian(caller32.Read(26, 2));
        context = context.Write(0x22, BitConverter.GetBytes(node.GeometryIndex == 1 ? window : (ushort)0));
        if (window != 0 && node.GeometryIndex == 1)
        {
            context = context.Write(0x24, BitConverter.GetBytes((ushort)0)).Write(0x26, BitConverter.GetBytes((ushort)0)).Write(0x28, BitConverter.GetBytes((ushort)256)).Write(0x2A, BitConverter.GetBytes((ushort)256)).Write(0x2C, BitConverter.GetBytes((ushort)0)).Write(0x2E, BitConverter.GetBytes((ushort)0)).Write(0x30, BitConverter.GetBytes((ushort)256)).Write(0x32, BitConverter.GetBytes((ushort)128));
        }
        frame = PublishContext(frame, temporary, context, 108);
        frame = inputs.Execute(frame, new NativeBattleEffect283DrawWindow108(temporary, geometryAddress, vertices));
        return Release(frame, 108);
    }

    private static NativeBattleEffect283Frame MorphProgramme283(NativeBattleEffect283Frame frame, NativeBattleStaticMeshResource mesh, int first, int second, int ratio, uint destination)
    {
        if (destination != NativeBattleEffect283Profile.ArenaAddress + NativeBattleEffect283Profile.ScratchOffset)
        {
            throw new InvalidDataException("Effect283 morph requires its current programme scratch.");
        }
        int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4));
        if (count < 1 || count * 8 > NativeBattleEffect283Profile.ScratchLength)
        {
            throw new InvalidDataException("Effect283 morph leaves its actual current scratch extent.");
        }
        NativeBattleGeometryRegisterStep registers = new(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Effect283 morph requires its geometry owner."), frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect283 morph requires its arithmetic owner."));
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
