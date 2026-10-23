using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBladeShot12GeometryTests
{
    [Fact]
    public void Spherical_direction_uses_signed_angles_and_retains_destination_W()
    {
        var angles = NativeBattleEffectArena.Clear().Write(0, new byte[6]).Write(2, BitConverter.GetBytes((short)1024));
        var actual = NativeBattleBladeShot12Geometry.SphericalDirection6E99F0(angles, NativeBattleEffectArena.Clear().Write(6, BitConverter.GetBytes((ushort)0xBEEF)));
        Assert.Equal((short)-4096, S(actual, 0));
        Assert.Equal((short)0, S(actual, 2));
        Assert.Equal((short)0, S(actual, 4));
        Assert.Equal(unchecked((short)0xBEEF), S(actual, 6));
    }

    [Fact]
    public void XZ_rotation_combines_products_before_arithmetic_shift_and_retains_unknown_W()
    {
        var source = NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((short)-7)).Write(2, BitConverter.GetBytes((short)23)).Write(4, BitConverter.GetBytes((short)11));
        var actual = NativeBattleBladeShot12Geometry.RotateXZ6E9A50(source, NativeBattleEffectArena.Clear().MarkUnknown(0, 8), 512);
        int sine = NativeBattleCameraMath.Sine(512);
        int cosine = NativeBattleCameraMath.Cosine(512);
        Assert.Equal(unchecked((short)((-7 * cosine + 11 * sine) >> 12)), S(actual, 0));
        Assert.Equal((short)23, S(actual, 2));
        Assert.Equal(unchecked((short)((7 * sine + 11 * cosine) >> 12)), S(actual, 4));
        Assert.Throws<NotSupportedException>(() => actual.Read(6, 2));
    }

    [Fact]
    public void Rotation_writes_only_nine_rotation_WORDs_preserving_padding_and_translation_knownness()
    {
        byte[] retained = Enumerable.Repeat((byte)0xA5, 32).ToArray();
        bool[] known = Enumerable.Repeat(true, 32).ToArray();
        known[19] = false;
        known[25] = false;
        var result = NativeBattleBladeShot12Geometry.Rotation6E9AB0(NativeBattleEffectArena.Clear().Write(0, new byte[6]), new(retained.ToImmutableArray(), known.ToImmutableArray()));
        Assert.Equal(NativeBattleCuragaMatrices.Identity8DD770().Rotation, Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadInt16LittleEndian(result.Bytes.AsSpan().Slice(index * 2))).ToImmutableArray());
        Assert.Equal(retained.Skip(18), result.Bytes.Skip(18));
        Assert.Equal(known.Skip(18), result.Known.Skip(18));
    }

    [Fact]
    public void Zero_fill_returns_before_touching_any_physical_owner()
    {
        Assert.Null(NativeBattleBladeShot12Geometry.FillDwords6DA380(null!, uint.MaxValue, 123, 0));
        Assert.Throws<InvalidDataException>(() => NativeBattleBladeShot12Geometry.FillDwords6DA380(null!, 0, 0, -1));
    }
    [Fact]
    public void Point_blend_increments_accumulator_first_publishes_GPF_GPL_and_retains_W()
    {
        var pool = NativeBattleEffectTaskPool.Initialize(0x2556110, 0x2545188, 100, 1);
        (pool, _) = pool.Append(0x6E8C90);
        NativeBattleOriginalEffectState effect = new(12, 11, new(12, 0x6E8AE0, 0x6E8AC0), null, 0x2556110) { Pools = [pool] };
        var frame = new NativeBattleBladeShot12Frame(NativeBattleBladeShot12MeshContext88Tests.Memory(2000), effect, new(123), NativeBattleBladeShot12State.FromColdProcess());
        var point = NativeBattleEffectArena.Clear().Write(0, new byte[24]).Write(6, BitConverter.GetBytes((ushort)0xBEEF)).Write(22, BitConverter.GetBytes((ushort)1024));
        for (int axis = 0; axis < 3; axis++)
        {
            point = point.Write(8 + axis * 2, BitConverter.GetBytes((ushort)(100 + axis * 100))).Write(16 + axis * 2, BitConverter.GetBytes((ushort)(300 + axis * 100)));
        }
        frame = NativeBattleBladeShot12Controller.WriteBytes(frame, 0x254519C, point, 24);
        var actual = NativeBattleBladeShot12Geometry.BlendPoint6E9B00(frame, 0x254519C);
        point = NativeBattleBladeShot12Controller.ReadBytes(actual, 0x254519C, 24);
        Assert.Equal(new short[] { 150, 250, 350 }, new[] { S(point, 0), S(point, 2), S(point, 4) });
        Assert.Equal(unchecked((short)0xBEEF), S(point, 6));
        Assert.Equal((short)1024, S(point, 14));
        Assert.Equal(1024u, actual.Memory.ScriptedColorRegisters!.Ir0);
        Assert.Equal(frame.Random, actual.Random);
        actual = NativeBattleBladeShot12Geometry.FillDwords6DA380(actual, 0x254519C, 0x12345678, 2);
        Assert.Equal(0x12345678u, BitConverter.ToUInt32(NativeBattleBladeShot12Controller.ReadBytes(actual, 0x254519C, 4).Read(0, 4)));
        Assert.Equal((short)100, S(NativeBattleBladeShot12Controller.ReadBytes(actual, 0x254519C, 24), 8));
    }

    private static short S(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
}
