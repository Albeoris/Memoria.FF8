using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect289Controller
{
    public static NativeBattleEffect289Frame WarpVertices6790E0(NativeBattleEffect289Frame frame, uint sourceAddress, int angle, int amplitudeA, int amplitudeB, int referenceW, int scale, NativeBattleEffect289Resources resources)
    {
        uint scratch = checked(frame.State.ReadDword(NativeBattleEffect289Profile.ArenaGlobal) + NativeBattleEffect289Profile.ScratchOffset);
        return frame with { Memory = NativeBattleEffect289WarpVertices.Execute6790E0(frame.Memory, resources.ReadMesh(frame.State, sourceAddress), resources.ReadLookup, unchecked((short)angle), amplitudeA, amplitudeB, referenceW, scale, scratch) };
    }

    public static NativeBattleEffect289Frame DrawProgrammeNode6780F0(NativeBattleEffect289Frame frame, NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleEffectArena caller40, NativeBattleEffect289Resources resources, NativeBattleEffect289Inputs inputs)
    {
        NativeBattleVector scale = node.Vector24;
        if (scale is { X: 0, Y: 0, Z: 0 } || node.Scalar36 >= 4096 && node.Color32 == 0)
        {
            return frame;
        }
        int geometryIndex = unchecked((short)node.GeometryIndex);
        if (geometryIndex < 0 || geometryIndex >= BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan()))
        {
            throw new InvalidDataException("Effect289 programme geometry leaves its selected source directory.");
        }
        int geometryOffset = BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(8 + geometryIndex * 4));
        uint geometryAddress = checked(source.Address + (uint)geometryOffset);
        NativeBattleStaticMeshResource mesh = resources.ReadGeometry(geometryAddress);
        int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4));
        int shapes = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(8));
        int first = node.Word40;
        int second = node.Word42;
        int ratio = node.Scalar38;
        if (first < 0 || second < 0 || first >= shapes || second >= shapes)
        {
            throw new InvalidDataException("Effect289 programme shape leaves its current selected geometry.");
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
            vertices = ReadDword289(caller40, 0x24);
            frame = MorphProgramme289(frame, mesh, first, second, ratio, vertices);
        }
        (byte[] callerBytes, bool[] callerKnown) = caller40.ReadSnapshot(0, 32);
        NativeBattlePackedMatrix32 caller = new(callerBytes.ToImmutableArray(), callerKnown.ToImmutableArray());
        NativeBattleActionMatrix basis = caller.ReadMatrix();
        NativeBattleActionMatrix rotation = (node.Header & 0x40000) != 0 ? NativeBattleGfMatrices.Euler7015B0(node.Vector16) : NativeBattleGfMatrices.Euler701310(node.Vector16);
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
            NativeBattleGeometryRegisterStep registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(RegistersBody289(frame), 0, point, null), false);
            x = registers.Colors.Mac[0]!.Value;
            y = registers.Colors.Mac[1]!.Value;
            z = registers.Colors.Mac[2]!.Value;
            frame = PublishRegistersBody289(frame, registers);
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
        NativeBattleEffectArena context = Context(frame, temporary, 88).Write(0, BitConverter.GetBytes(geometryAddress)).Write(4, BitConverter.GetBytes(vertices)).Write(12, BitConverter.GetBytes((int)node.Scalar36)).Write(24, caller40.Read(0x20, 4)).Write(28, BitConverter.GetBytes(node.Scalar36 == 0 ? 0x2030u : 0x20F0u));
        if (node.Scalar36 != 0)
        {
            context = context.Write(8, BitConverter.GetBytes(node.Color32));
        }
        frame = PublishContext(frame, temporary, context, 88);
        frame = inputs.Execute(frame, new NativeBattleEffect289DrawMesh7043B0(temporary, geometryAddress, vertices));
        return Release(frame, 88);
    }

    private static NativeBattleEffect289Frame MorphProgramme289(NativeBattleEffect289Frame frame, NativeBattleStaticMeshResource mesh, int first, int second, int ratio, uint destination)
    {
        uint scratch = checked(frame.State.ReadDword(NativeBattleEffect289Profile.ArenaGlobal) + NativeBattleEffect289Profile.ScratchOffset);
        int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4));
        if (destination != scratch || count < 1 || count > 68 || count * 8 > NativeBattleEffect289Profile.ScratchLength)
        {
            throw new InvalidDataException("Effect289 morph requires its actual selected COMMON scratch.");
        }
        NativeBattleGeometryRegisterStep registers = RegistersBody289(frame);
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
        return PublishRegistersBody289(frame, registers);

        ushort ShapeWord(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(mesh.Bytes.AsSpan().Slice(offset, 2));
    }

    private static uint ReadDword289(NativeBattleEffectArena value, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(value.Read(offset, 4));
    private static NativeBattleGeometryRegisterStep RegistersBody289(NativeBattleEffect289Frame frame) => new(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Effect289 requires current geometry registers."), frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect289 requires current color registers."));
    private static NativeBattleEffect289Frame PublishRegistersBody289(NativeBattleEffect289Frame frame, NativeBattleGeometryRegisterStep registers) => frame with { Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } };
}
