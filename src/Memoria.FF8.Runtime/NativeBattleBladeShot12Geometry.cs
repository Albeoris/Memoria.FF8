using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleBladeShot12Geometry
{
    public static NativeBattleBladeShot12Frame FillDwords6DA380(NativeBattleBladeShot12Frame frame, uint address, uint value, int count)
    {
        if (count < 0)
        {
            throw new InvalidDataException("Blade Shot fill requires its actual bounded DWORD count.");
        }
        if (count == 0)
        {
            return frame;
        }
        byte[] bytes = new byte[checked(count * 4)];
        for (int index = 0; index < count; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(index * 4), value);
        }
        return NativeBattleBladeShot12Controller.WriteBytes(frame, address, NativeBattleEffectArena.Clear().Write(0, bytes), bytes.Length);
    }

    public static NativeBattleBladeShot12Frame BlendPoint6E9B00(NativeBattleBladeShot12Frame frame, uint address)
    {
        NativeBattleEffectArena point = NativeBattleBladeShot12Controller.ReadBytes(frame, address, 24);
        ushort next = unchecked((ushort)(Word(point, 14) + Word(point, 22)));
        frame = NativeBattleBladeShot12Controller.WriteBytes(frame, address + 14, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(next)), 2);
        (frame, byte[] xyz) = Blend(frame, point, 8, point, 16, unchecked((short)next));
        return NativeBattleBladeShot12Controller.WriteBytes(frame, address, NativeBattleEffectArena.Clear().Write(0, xyz), 6);
    }

    public static NativeBattleBladeShot12Frame Morph6E9970(NativeBattleBladeShot12Frame frame, uint contextAddress, uint firstHeader, uint secondHeader, int factor, NativeBattleBladeShot12Resources resources)
    {
        uint destination = BinaryPrimitives.ReadUInt32LittleEndian(NativeBattleBladeShot12Controller.Context(frame, contextAddress, 88).Read(0, 4));
        NativeBattleStaticMeshResource mesh = resources.ReadMesh(destination);
        int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4));
        ImmutableArray<byte> first = resources.ReadMorphSource(firstHeader).Bytes;
        ImmutableArray<byte> second = resources.ReadMorphSource(secondHeader).Bytes;
        if (count < 0 || count != 49 || BinaryPrimitives.ReadInt32LittleEndian(first.AsSpan().Slice(4)) != count || BinaryPrimitives.ReadInt32LittleEndian(second.AsSpan().Slice(4)) != count || destination is not (0x1362E3C or 0x136372C))
        {
            throw new InvalidDataException("Blade Shot morph requires its actual paired shape and mutable destination bounds.");
        }
        NativeBattleEffectArena a = NativeBattleEffectArena.Clear().Write(0, first.AsSpan());
        NativeBattleEffectArena b = NativeBattleEffectArena.Clear().Write(0, second.AsSpan());
        for (int vertex = 0; vertex < count; vertex++)
        {
            int offset = checked(8 + vertex * 8);
            (frame, byte[] xyz) = Blend(frame, a, offset, b, offset, factor);
            frame = frame with { State = frame.State.WriteView(checked(destination + (uint)offset), NativeBattleEffectArena.Clear().Write(0, xyz), 6) };
        }
        return frame;
    }

    public static NativeBattleEffectArena SphericalDirection6E99F0(NativeBattleEffectArena angles, NativeBattleEffectArena output)
    {
        int a = unchecked((short)Word(angles, 2));
        int b = unchecked((short)Word(angles, 4));
        int sine = NativeBattleCameraMath.Sine(a);
        int x = unchecked(-unchecked(sine * NativeBattleCameraMath.Cosine(b))) >> 12;
        int y = unchecked(sine * NativeBattleCameraMath.Sine(b)) >> 12;
        int z = NativeBattleCameraMath.Cosine(a);
        return output.Write(0, BitConverter.GetBytes(unchecked((short)x))).Write(2, BitConverter.GetBytes(unchecked((short)y))).Write(4, BitConverter.GetBytes(unchecked((short)z)));
    }

    public static NativeBattleEffectArena RotateXZ6E9A50(NativeBattleEffectArena source, NativeBattleEffectArena output, int heading)
    {
        int sine = NativeBattleCameraMath.Sine(heading);
        int cosine = NativeBattleCameraMath.Cosine(heading);
        int x = unchecked((short)Word(source, 0));
        int z = unchecked((short)Word(source, 4));
        int resultX = unchecked(unchecked(x * cosine) + unchecked(z * sine)) >> 12;
        int resultZ = unchecked(unchecked(-x * sine) + unchecked(z * cosine)) >> 12;
        return output.Write(0, BitConverter.GetBytes(unchecked((short)resultX))).Write(2, source.Read(2, 2)).Write(4, BitConverter.GetBytes(unchecked((short)resultZ)));
    }

    public static NativeBattlePackedMatrix32 Rotation6E9AB0(NativeBattleEffectArena angles, NativeBattlePackedMatrix32 retained)
    {
        int x = unchecked((short)Word(angles, 0));
        short sine = unchecked((short)NativeBattleCameraMath.Sine(x));
        short cosine = unchecked((short)NativeBattleCameraMath.Cosine(x));
        NativeBattleActionMatrix basis = new([4096, 0, 0, 0, cosine, unchecked((short)-sine), 0, sine, cosine], 0, 0, 0, 4096);
        basis = NativeBattleGfMatrices.RotateZ56D090(basis, unchecked((short)Word(angles, 4)));
        NativeBattleActionMatrix y = new(NativeBattleDuelSharedPackets.Rotation6D9510(unchecked((short)Word(angles, 2))), 0, 0, 0, 4096);
        ImmutableArray<short> rotation = NativeBattleGfMatrices.Multiply56C090(y, basis).Rotation;
        byte[] bytes = retained.Bytes.ToArray();
        bool[] known = retained.Known.ToArray();
        for (int index = 0; index < 9; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(index * 2), rotation[index]);
            known[index * 2] = true;
            known[index * 2 + 1] = true;
        }
        return new(bytes.ToImmutableArray(), known.ToImmutableArray());
    }

    private static (NativeBattleBladeShot12Frame Frame, byte[] Xyz) Blend(NativeBattleBladeShot12Frame frame, NativeBattleEffectArena first, int firstOffset, NativeBattleEffectArena second, int secondOffset, int factor)
    {
        NativeBattleGeometryRegisterStep registers = new(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Blade Shot blend requires its current geometry owner."), frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Blade Shot blend requires its current arithmetic owner."));
        registers = NativeBattleGeometry.LoadIrColumn(registers with { Colors = registers.Colors with { Ir0 = unchecked((uint)(4096 - factor)) } }, Word(first, firstOffset), Word(first, firstOffset + 2), Word(first, firstOffset + 4));
        registers = NativeBattleRaldo19BallRendering.Weighted(registers, false, NativeBattleGteColorInterpolation.CodeConstants45F270);
        registers = NativeBattleGeometry.LoadIrColumn(registers with { Colors = registers.Colors with { Ir0 = unchecked((uint)factor) } }, Word(second, secondOffset), Word(second, secondOffset + 2), Word(second, secondOffset + 4));
        registers = NativeBattleRaldo19BallRendering.Weighted(registers, true, NativeBattleGteColorInterpolation.CodeConstants45F270);
        byte[] xyz = new byte[6];
        for (int axis = 0; axis < 3; axis++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(xyz.AsSpan(axis * 2), unchecked((short)(registers.Colors.Ir[axis] ?? throw new NotSupportedException("Blade Shot blend requires its produced IR value."))));
        }
        return (frame with { Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } }, xyz);
    }

    private static ushort Word(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(offset, 2));
}
