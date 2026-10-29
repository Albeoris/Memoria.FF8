using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace Memoria.FF8.Runtime;

public static class NativeBattleGeometry
{
    public static NativeBattleGeometryRegisterStep LoadDiagonal45DC70(NativeBattleGeometryRegisterStep step, NativeBattleVector vector)
    {
        Validate(step);
        byte?[] rotation = step.Geometry.Rotation20.ToArray();
        short[] values = [vector.X, vector.Y, vector.Z];
        for (int index = 0; index < 3; index++)
        {
            PutWord(rotation, index * 8, values[index]);
            PutWord(rotation, index * 8 + 2, values[index] < 0 ? (short)-1 : (short)0);
        }
        return step with { Geometry = step.Geometry with { Rotation20 = rotation.ToImmutableArray() } };
    }

    public static NativeBattleGeometryRegisterStep LoadSignedIr45DC40(NativeBattleGeometryRegisterStep step, NativeBattleVector vector)
    {
        Validate(step);
        return step with { Colors = step.Colors with { Ir = [(int)vector.X, (int)vector.Y, (int)vector.Z] } };
    }

    public static NativeBattleGeometryRegisterStep OuterProduct45E820(NativeBattleGeometryRegisterStep step)
    {
        Validate(step);
        double x = ReadWord(step.Geometry.Rotation20, 0) / 4096d;
        double y = ReadWord(step.Geometry.Rotation20, 8) / 4096d;
        double z = ReadWord(step.Geometry.Rotation20, 16) / 4096d;
        int vx = unchecked((short)Require(step.Colors.Ir[0]));
        int vy = unchecked((short)Require(step.Colors.Ir[1]));
        int vz = unchecked((short)Require(step.Colors.Ir[2]));
        return step with { Colors = PublishMac(step.Colors, NativePrecision53.RoundInteger(y * vz - z * vy), NativePrecision53.RoundInteger(z * vx - x * vz), NativePrecision53.RoundInteger(x * vy - y * vx), 0) };
    }

    public static NativeBattleGeometryRegisterStep OuterProduct45E670(NativeBattleGeometryRegisterStep step)
    {
        Validate(step);
        int x = ReadWord(step.Geometry.Rotation20, 0);
        int y = ReadWord(step.Geometry.Rotation20, 8);
        int z = ReadWord(step.Geometry.Rotation20, 16);
        int vx = unchecked((short)Require(step.Colors.Ir[0]));
        int vy = unchecked((short)Require(step.Colors.Ir[1]));
        int vz = unchecked((short)Require(step.Colors.Ir[2]));
        return step with { Colors = PublishMac(step.Colors, unchecked(y * vz - z * vy), unchecked(z * vx - x * vz), unchecked(x * vy - y * vx), 0) };
    }

    public static NativeBattleGeometryRegisters LoadMatrix(NativeBattleGeometryRegisters geometry, NativeBattleActionMatrix matrix, bool rotation, bool translation, short? padding)
    {
        geometry.Validate();
        if (!rotation && !translation)
        {
            return geometry;
        }
        matrix.Validate();
        if (rotation)
        {
            byte?[] bytes = new byte?[20];
            for (int index = 0; index < 9; index++)
            {
                PutWord(bytes, index * 2, matrix.Rotation[index]);
            }
            PutWord(bytes, 18, padding);
            geometry = geometry with { Rotation20 = bytes.ToImmutableArray() };
        }
        if (translation)
        {
            geometry = geometry with { Translation = [matrix.X, matrix.Y, matrix.Z] };
        }
        return geometry;
    }

    public static NativeBattleGeometryRegisterStep LoadVector(NativeBattleGeometryRegisterStep step, int slot, NativeBattleVector vector, short? w)
    {
        Span<byte?> bytes = stackalloc byte?[8];
        PutWord(bytes, 0, unchecked((short)vector.X));
        PutWord(bytes, 2, unchecked((short)vector.Y));
        PutWord(bytes, 4, unchecked((short)vector.Z));
        PutWord(bytes, 6, w);
        return LoadPackedVector(step, slot, bytes);
    }

    public static NativeBattleGeometryRegisters LoadAnchorScratchXYZ(NativeBattleGeometryRegisters geometry, NativeBattleVector vector)
    {
        geometry.Validate();
        byte?[] bytes = geometry.AnchorScratch8.ToArray();
        PutWord(bytes, 0, unchecked((short)vector.X));
        PutWord(bytes, 2, unchecked((short)vector.Y));
        PutWord(bytes, 4, unchecked((short)vector.Z));
        return geometry with { AnchorScratch8 = bytes.ToImmutableArray() };
    }

    public static NativeBattleGeometryRegisters LoadAnchorScratchZ(NativeBattleGeometryRegisters geometry, short z)
    {
        geometry.Validate();
        byte?[] bytes = geometry.AnchorScratch8.ToArray();
        PutWord(bytes, 4, z);
        return geometry with { AnchorScratch8 = bytes.ToImmutableArray() };
    }

    public static NativeBattleGeometryRegisterStep LoadAnchorVector(NativeBattleGeometryRegisterStep step) => LoadPackedVector(step, 0, step.Geometry.AnchorScratch8.AsSpan());

    public static NativeBattleGeometryRegisterStep LoadIrColumn(NativeBattleGeometryRegisterStep step, ushort x, ushort y, ushort z)
    {
        Validate(step);
        return step with { Colors = step.Colors with { Ir = [(int)x, (int)y, (int)z] } };
    }

    public static NativeBattleGeometryRegisterStep Transform(NativeBattleGeometryRegisterStep step, bool translation = true, bool irInput = false)
    {
        Validate(step);
        NativeBattleVector input = irInput
            ? new(unchecked((short)Require(step.Colors.Ir[0])), unchecked((short)Require(step.Colors.Ir[1])), unchecked((short)Require(step.Colors.Ir[2])))
            : ReadVector(step, 0);
        NativeBattleFullMacVertex mac = NativeBattleGfMatrices.TransformMac460840(ReadMatrix(step.Geometry, translation), input);
        return step with { Colors = PublishMac(step.Colors, mac.X, mac.Y, mac.Z, 0) };
    }

    public static NativeBattleGeometryRegisterStep ProjectSingle(NativeBattleGeometryRegisterStep step, NativeBattleCameraProjection projection, short dqa, int dqb)
    {
        Validate(step);
        NativeBattleModuleProjectionObservation observed = NativeBattleModuleProjection.Observe45FE10(ReadMatrix(step.Geometry, true), ReadVector(step, 0), projection);
        NativeBattleGeometryRegisters geometry = step.Geometry with
        {
            Sxy = [step.Geometry.Sxy[1], step.Geometry.Sxy[2], observed.Projected.Screen],
            Sz = [step.Geometry.Sz[1], step.Geometry.Sz[2], step.Geometry.Sz[3], observed.Projected.Depth]
        };
        return PublishProjection(step with { Geometry = geometry }, observed, observed.Projected.Flags, dqa, dqb);
    }

    public static NativeBattleGeometryRegisterStep ProjectTriple(NativeBattleGeometryRegisterStep step, NativeBattleCameraProjection projection, short dqa, int dqb)
    {
        Validate(step);
        NativeBattleActionMatrix matrix = ReadMatrix(step.Geometry, true);
        NativeBattleModuleProjectionObservation first = NativeBattleModuleProjection.Observe45FE10(matrix, ReadVector(step, 0), projection);
        NativeBattleModuleProjectionObservation second = NativeBattleModuleProjection.Observe45FE10(matrix, ReadVector(step, 1), projection);
        NativeBattleModuleProjectionObservation third = NativeBattleModuleProjection.Observe45FE10(matrix, ReadVector(step, 2), projection);
        NativeBattleGeometryRegisters geometry = step.Geometry with
        {
            Sxy = [first.Projected.Screen, second.Projected.Screen, third.Projected.Screen],
            Sz = [step.Geometry.Sz[3], first.Projected.Depth, second.Projected.Depth, third.Projected.Depth]
        };
        return PublishProjection(step with { Geometry = geometry }, third, first.Projected.Flags | second.Projected.Flags | third.Projected.Flags, dqa, dqb);
    }

    public static NativeBattleGeometryRegisterStep Clip45EE10(NativeBattleGeometryRegisterStep step)
    {
        Validate(step);
        uint a = Require(step.Geometry.Sxy[0]);
        uint b = Require(step.Geometry.Sxy[1]);
        uint c = Require(step.Geometry.Sxy[2]);
        int x0 = unchecked((short)a);
        int x1 = unchecked((short)b);
        int x2 = unchecked((short)c);
        int y0 = unchecked((short)(a >> 16));
        int y1 = unchecked((short)(b >> 16));
        int y2 = unchecked((short)(c >> 16));
        uint mac = unchecked((uint)(x0 * (y1 - y2) + x1 * (y2 - y0) + x2 * (y0 - y1)));
        return step with { Geometry = step.Geometry with { Mac0 = mac }, Colors = step.Colors with { Flags = 0 } };
    }

    public static NativeBattleGeometryRegisterStep Average45E5C0(NativeBattleGeometryRegisterStep step, short zsf3)
    {
        Validate(step);
        int sum = 0;
        for (int index = 1; index < 4; index++)
        {
            uint value = step.Geometry.Sz[index] ?? throw new NotSupportedException("Native three-depth averaging requires its actual three SZ values.");
            sum += unchecked((ushort)value);
        }
        uint result = unchecked((uint)(sum * zsf3)) >> 12;
        return step with { Geometry = step.Geometry with { Mac0 = result, Otz = result }, Colors = step.Colors with { Flags = 0 } };
    }

    public static NativeBattleGeometryRegisterStep Average45E610(NativeBattleGeometryRegisterStep step, short zsf4)
    {
        Validate(step);
        int sum = 0;
        foreach (uint? value in step.Geometry.Sz)
        {
            sum += unchecked((ushort)Require(value));
        }
        uint result = unchecked((uint)(sum * zsf4)) >> 12;
        return step with { Geometry = step.Geometry with { Mac0 = result, Otz = result }, Colors = step.Colors with { Flags = 0 } };
    }

    private static NativeBattleGeometryRegisterStep PublishProjection(NativeBattleGeometryRegisterStep step, NativeBattleModuleProjectionObservation observed, uint flags, short dqa, int dqb)
    {
        double cue = (double)dqa * observed.Factor;
        cue *= 65536d;
        cue += dqb;
        int ir0 = NativePrecision53.RoundInteger(cue * (1d / 4096d));
        if (ir0 is < 0 or > 4095)
        {
            flags |= 0x1000;
        }
        NativeBattleScriptedColorRegisters colors = PublishMac(step.Colors, observed.X, observed.Y, observed.Z, flags) with { Ir0 = (uint)Math.Clamp(ir0, 0, 4095) };
        return new(step.Geometry with { Mac0 = unchecked((uint)NativePrecision53.RoundInteger(cue)) }, colors);
    }

    private static NativeBattleScriptedColorRegisters PublishMac(NativeBattleScriptedColorRegisters colors, int x, int y, int z, uint flags)
    {
        ReadOnlySpan<int> values = stackalloc int[] { x, y, z };
        ReadOnlySpan<uint> limits = stackalloc uint[] { 0x81000000, 0x80800000, 0x00400000 };
        for (int lane = 0; lane < 3; lane++)
        {
            if (values[lane] is < short.MinValue or > short.MaxValue)
            {
                flags |= limits[lane];
            }
        }
        return colors with { Mac = [(int?)x, y, z], Ir = [(int?)Math.Clamp(x, short.MinValue, short.MaxValue), Math.Clamp(y, short.MinValue, short.MaxValue), Math.Clamp(z, short.MinValue, short.MaxValue)], Flags = flags };
    }

    private static NativeBattleGeometryRegisterStep LoadPackedVector(NativeBattleGeometryRegisterStep step, int slot, ReadOnlySpan<byte?> bytes)
    {
        Validate(step);
        if (bytes.Length != 8)
        {
            throw new InvalidDataException("Native vector load requires its complete packed input width.");
        }
        if (slot == 0)
        {
            byte[] normal = new byte[8];
            bool[] known = new bool[8];
            for (int index = 0; index < bytes.Length; index++)
            {
                normal[index] = bytes[index] ?? 0;
                known[index] = bytes[index].HasValue;
            }
            return step with { Colors = step.Colors with { NormalInput = ImmutableCollectionsMarshal.AsImmutableArray(normal), NormalInputKnown = ImmutableCollectionsMarshal.AsImmutableArray(known) } };
        }
        return slot switch
        {
            1 => step with { Geometry = step.Geometry with { V1 = bytes.ToImmutableArray() } },
            2 => step with { Geometry = step.Geometry with { V2 = bytes.ToImmutableArray() } },
            _ => throw new ArgumentOutOfRangeException(nameof(slot))
        };
    }

    private static NativeBattleVector ReadVector(NativeBattleGeometryRegisterStep step, int slot)
    {
        if (slot == 0)
        {
            ImmutableArray<byte> normal = step.Colors.NormalInput ?? throw new NotSupportedException("Native geometry requires its loaded V0 input.");
            ImmutableArray<bool>? mask = step.Colors.NormalInputKnown;
            return new(ReadNormalWord(normal, mask, 0), ReadNormalWord(normal, mask, 2), ReadNormalWord(normal, mask, 4));
        }
        ImmutableArray<byte?> bytes = slot == 1 ? step.Geometry.V1 : step.Geometry.V2;
        return new(ReadWord(bytes, 0), ReadWord(bytes, 2), ReadWord(bytes, 4));
    }

    public static NativeBattleActionMatrix CaptureMatrix(NativeBattleGeometryRegisters geometry) => ReadMatrix(geometry, true);

    private static NativeBattleActionMatrix ReadMatrix(NativeBattleGeometryRegisters geometry, bool translation)
    {
        short[] rotation = new short[9];
        for (int index = 0; index < rotation.Length; index++)
        {
            rotation[index] = ReadWord(geometry.Rotation20, index * 2);
        }
        return new(ImmutableCollectionsMarshal.AsImmutableArray(rotation), translation ? Require(geometry.Translation[0]) : 0, translation ? Require(geometry.Translation[1]) : 0, translation ? Require(geometry.Translation[2]) : 0, 4096);
    }

    private static void PutWord(Span<byte?> bytes, int offset, short? value)
    {
        bytes[offset] = value is { } word ? unchecked((byte)word) : null;
        bytes[offset + 1] = value is { } high ? unchecked((byte)(high >> 8)) : null;
    }

    private static short ReadWord(ImmutableArray<byte?> bytes, int offset) => unchecked((short)(Require(bytes[offset]) | (Require(bytes[offset + 1]) << 8)));

    private static short ReadNormalWord(ImmutableArray<byte> bytes, ImmutableArray<bool>? mask, int offset)
    {
        byte low = Require(mask is { } knownLow && !knownLow[offset] ? (byte?)null : bytes[offset]);
        byte high = Require(mask is { } knownHigh && !knownHigh[offset + 1] ? (byte?)null : bytes[offset + 1]);
        return unchecked((short)(low | (high << 8)));
    }

    private static T Require<T>(T? value) where T : struct => value ?? throw new NotSupportedException("Native geometry requires the actual retained input lane.");

    private static void Validate(NativeBattleGeometryRegisterStep step)
    {
        step.Geometry.Validate();
        step.Colors.Validate();
    }
}
