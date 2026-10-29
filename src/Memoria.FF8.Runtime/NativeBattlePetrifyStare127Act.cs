using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattlePetrifyStare127ActStep(NativeBattleEffectMemory Memory, NativeBattleEffectArena Temporary88, NativeBattleGfGeometryBatch Batch);

public static class NativeBattlePetrifyStare127Act
{
    public static bool Visible(NativeBattleGfAnimationNode node) => NativeBattleActMeshRendering.Visible(node);

    public static NativeBattlePetrifyStare127ActStep Draw(NativeBattleEffectMemory memory, NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleEffectArena context16, NativeBattlePackedMatrix32 camera, NativeBattleEffectArena temporary88, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, uint morphAddress, int morphLength = 1000, short? localVectorW = null, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        if (!Visible(node))
        {
            throw new InvalidOperationException("Petrify Stare ACT allocation requires a visible node.");
        }
        NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? throw new NotSupportedException("Petrify Stare requires its current geometry registers."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Petrify Stare requires its current color registers."));
        int geometry = Integer(checked(8 + node.GeometryIndex * 4));
        int count = Integer(checked(geometry + 4));
        int variants = Integer(checked(geometry + 8));
        if (count < 1 || variants < 1 || node.Word40 < 0 || node.Word40 >= variants || node.Word42 < 0 || node.Word42 >= variants)
        {
            throw new InvalidDataException("Petrify Stare ACT requires its original selected vertex variants.");
        }
        int first = checked(geometry + 12 + node.Word40 * count * 8);
        int second = checked(geometry + 12 + node.Word42 * count * 8);
        int selected = node.Word40 != node.Word42 && node.Scalar38 == 4096 ? second : first;
        bool interpolate = node.Word40 != node.Word42 && node.Scalar38 is not (0 or 4096);
        NativeBattleEffectArena? arena = memory.Arena;
        uint selectedAddress;
        int morphOffset = 0;
        if (interpolate)
        {
            if (arena is null)
            {
                throw new NotSupportedException("Petrify Stare interpolation requires its current effect arena.");
            }
            long offset = (long)morphAddress - NativeBattleEffectNodeMemory.ArenaAddress;
            if (morphLength != 1000 || count > morphLength / 8 || offset < 0 || offset > NativeBattleEffectArena.Length - morphLength || Dword(context16, 12) != morphAddress)
            {
                throw new InvalidDataException("Petrify Stare interpolation leaves its actual working scratch.");
            }
            morphOffset = checked((int)offset);
            for (int vertex = 0; vertex < count; vertex++)
            {
                NativeBattleVector a = Vector(checked(first + vertex * 8));
                NativeBattleVector b = Vector(checked(second + vertex * 8));
                registers = registers with { Colors = registers.Colors with { Ir0 = unchecked((uint)(4096 - node.Scalar38)), Ir = [unchecked((ushort)a.X), unchecked((ushort)a.Y), unchecked((ushort)a.Z)] } };
                registers = NativeBattleRaldo19BallRendering.Weighted(registers, false, colors);
                registers = registers with { Colors = registers.Colors with { Ir0 = unchecked((uint)node.Scalar38), Ir = [unchecked((ushort)b.X), unchecked((ushort)b.Y), unchecked((ushort)b.Z)] } };
                registers = NativeBattleRaldo19BallRendering.Weighted(registers, true, colors);
                byte[] xyz = new byte[6];
                for (int axis = 0; axis < 3; axis++)
                {
                    BinaryPrimitives.WriteInt16LittleEndian(xyz.AsSpan(axis * 2), unchecked((short)registers.Colors.Ir[axis]!.Value));
                }
                arena = arena.Write(checked(morphOffset + vertex * 8), xyz);
            }
            selectedAddress = morphAddress;
            memory = memory with { Arena = arena };
        }
        else
        {
            _ = Read(selected, checked(count * 8));
            selectedAddress = checked(source.Address + (uint)selected);
        }
        NativeBattleActionMatrix matrix = (node.Header & 0x40000) != 0 ? NativeBattleGfMatrices.Euler7015B0(node.Vector16) : NativeBattleGfMatrices.Euler701310(node.Vector16);
        NativeBattleActionMatrix view = camera.ReadMatrix();
        NativeBattleVector anchor = new(Signed(context16, 0), Signed(context16, 2), Signed(context16, 4));
        NativeBattleVector point = node.Vector8;
        (int X, int Y, int Z) translation;
        if ((node.Header & 0x200) != 0)
        {
            Transform(anchor, OptionalWord(context16, 6));
            translation = (unchecked(registers.Colors.Mac[0]!.Value + point.X), unchecked(registers.Colors.Mac[1]!.Value + point.Y), unchecked(registers.Colors.Mac[2]!.Value + point.Z));
        }
        else
        {
            point = new(unchecked((short)(point.X + anchor.X)), unchecked((short)(point.Y + anchor.Y)), unchecked((short)(point.Z + anchor.Z)));
            Transform(point, localVectorW);
            translation = (registers.Colors.Mac[0]!.Value, registers.Colors.Mac[1]!.Value, registers.Colors.Mac[2]!.Value);
            matrix = NativeBattleGfMatrices.Multiply56C090(view, matrix);
        }
        matrix = matrix with { X = unchecked(translation.X + view.X), Y = unchecked(translation.Y + view.Y), Z = unchecked(translation.Z + view.Z) };
        NativeBattleVector scale = node.Vector24;
        if (scale is not { X: 4096, Y: 4096, Z: 4096 })
        {
            matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, scale.X, scale.Y, scale.Z);
        }
        registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, matrix, true, true, null) };
        memory = memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors };
        temporary88 = Store(temporary88, 0, checked(source.Address + (uint)geometry));
        temporary88 = Store(temporary88, 4, selectedAddress);
        temporary88 = Store(temporary88, 12, unchecked((uint)(int)node.Scalar36));
        temporary88 = Store(temporary88, 0x1C, node.Scalar36 == 0 ? 0x2030u : 0x20F0u);
        if (node.Scalar36 != 0)
        {
            temporary88 = Store(temporary88, 8, node.Color32);
        }
        temporary88 = Store(temporary88, 0x18, Dword(context16, 8));
        NativeBattleEffectArena? selectedArena = arena;
        NativeBattleDefaultActMeshStep drawn = NativeBattleDefaultActMesh.Draw(memory, source, geometry, temporary88, projection, colors, ReadVertex, ReadW, pilotIdentity: pilotIdentity);
        return new(drawn.Memory, drawn.Context88, drawn.Batch);

        void Transform(NativeBattleVector vector, short? w)
        {
            registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, view, true, false, PackedPadding(camera)) };
            registers = NativeBattleGeometry.LoadVector(registers, 0, vector, w);
            registers = NativeBattleGeometry.Transform(registers, false);
        }

        NativeBattleVector ReadVertex(int offset)
        {
            RequireVertex(offset);
            if (!interpolate)
            {
                return Vector(checked(selected + offset));
            }
            byte[] xyz = selectedArena!.Read(checked(morphOffset + offset), 6);
            return new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(4)));
        }

        short? ReadW(int offset)
        {
            RequireVertex(offset);
            return interpolate ? OptionalWord(selectedArena!, checked(morphOffset + offset + 6)) : BinaryPrimitives.ReadInt16LittleEndian(Read(checked(selected + offset + 6), 2));
        }

        void RequireVertex(int offset)
        {
            if (offset < 0 || (offset & 7) != 0 || offset > count * 8 - 8)
            {
                throw new InvalidDataException("Petrify Stare face leaves its selected packed vertex owner.");
            }
        }

        int Integer(int offset) => BinaryPrimitives.ReadInt32LittleEndian(Read(offset, 4));
        NativeBattleVector Vector(int offset)
        {
            ReadOnlySpan<byte> bytes = Read(offset, 6);
            return new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes[2..]), BinaryPrimitives.ReadInt16LittleEndian(bytes[4..]));
        }
        ReadOnlySpan<byte> Read(int offset, int length)
        {
            if (source.Data.IsDefault || offset < 0 || length < 0 || offset > source.Data.Length - length)
            {
                throw new InvalidDataException("Petrify Stare ACT read leaves its immutable source.");
            }
            return source.Data.AsSpan().Slice(offset, length);
        }
    }

    private static short Signed(NativeBattleEffectArena memory, int offset) => BinaryPrimitives.ReadInt16LittleEndian(memory.Read(offset, 2));
    private static uint Dword(NativeBattleEffectArena memory, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(offset, 4));
    private static NativeBattleEffectArena Store(NativeBattleEffectArena memory, int offset, uint value) => memory.Write(offset, BitConverter.GetBytes(value));
    private static short? OptionalWord(NativeBattleEffectArena memory, int offset)
    {
        (byte[] bytes, bool[] known) = memory.ReadSnapshot(offset, 2);
        return known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null;
    }
    private static short? PackedPadding(NativeBattlePackedMatrix32 matrix) => matrix.Known[18] && matrix.Known[19] ? BinaryPrimitives.ReadInt16LittleEndian(matrix.Bytes.AsSpan().Slice(18)) : null;
}
