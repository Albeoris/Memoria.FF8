using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleQuake38Tests
{
    [Fact]
    public void Wave_initialization_consumes_eighteen_actual_CRT_draws_and_retains_original_zone_ranges()
    {
        NativeCrtRandomState original = NativeCrtRandomState.FromColdProcess();
        NativeCrtRandomState expected = original;
        for (int index = 0; index < 18; index++)
        {
            expected = expected.Draw(out _);
        }
        NativeBattleQuake38WaveStep step = NativeBattleQuake38Waves.Initialize8DA580(NativeBattleQuake38State.FromColdProcess(), original);
        Assert.Equal(expected, step.Random);
        Assert.Equal(0, step.State.Word(0x278C916));
        Assert.Equal(0, step.State.Word(0x278C918));
        Assert.InRange(step.State.Word(0x278C968), (short)256, (short)511);
        Assert.InRange(step.State.Word(0x278C978), (short)1024, (short)2047);
        Assert.InRange(step.State.Word(0x278C916 + 16), (short)256, (short)383);
    }

    [Fact]
    public void Saved_uninitialized_dormant_actor_bytes_remain_unknown_on_restore()
    {
        NativeBattleGraphicsModel original = new(6, 0);
        NativeBattleQuake38SavedActor saved = NativeBattleQuake38SavedActor.Capture8DBBA0(original);
        NativeBattleGraphicsModel restored = saved.Restore8DBE40(original);
        Assert.Null(saved.Position28);
        Assert.Null(saved.RotationPadding18);
        Assert.Equal(original, restored);
    }

    [Fact]
    public void Zone_lookup_uses_thirty_two_columns_and_word_wrap_before_clamping()
    {
        byte[] bytes = new byte[1024];
        bytes[16 * 32 + 16] = 3;
        bytes[0] = 4;
        bytes[31 * 32 + 31] = 5;
        bytes[16] = 6;
        ImmutableArray<byte> map = bytes.ToImmutableArray();
        Assert.Equal(3, NativeBattleQuake38Zones.Read8D9A80(map, 0, 0, 8192, 8192, 0));
        Assert.Equal(4, NativeBattleQuake38Zones.Read8D9A80(map, 8192, -8192, 8192, 8192, 0));
        Assert.Equal(5, NativeBattleQuake38Zones.Read8D9A80(map, -8192, 8192, 8192, 8192, 0));
        Assert.Equal(6, NativeBattleQuake38Zones.Read8D9A80(map, 0, short.MaxValue, 8192, 8192, -1));
        Assert.Equal(4, NativeBattleQuake38Zones.Read8D9A80(map, short.MinValue, short.MinValue, 8192, 8192, 0));
    }

    [Fact]
    public void Triangle_builder_averages_full_signed_sums_and_unsigned_uvs_without_touching_padding()
    {
        byte[] vertices = new byte[24];
        for (int index = 0; index < 3; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(vertices.AsSpan(index * 8), (short)(-3 + index));
            BinaryPrimitives.WriteInt16LittleEndian(vertices.AsSpan(index * 8 + 2), 30000);
            BinaryPrimitives.WriteInt16LittleEndian(vertices.AsSpan(index * 8 + 4), -30000);
        }
        byte[] source = new byte[20];
        BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(4), 2);
        source[8] = 200;
        source[9] = 210;
        source[12] = 220;
        source[13] = 230;
        source[6] = 240;
        source[7] = 250;
        source[16] = 255;
        source[17] = 200;
        source[18] = 130;
        ImmutableArray<byte> actual = NativeBattleQuake38GroundEntries.Build(1, 50, new(false, source.ToImmutableArray()), vertices.ToImmutableArray(), Enumerable.Repeat((byte)0xA5, 112).ToImmutableArray());
        Assert.Equal(-2, Word(24));
        Assert.Equal(30000, Word(26));
        Assert.Equal(-30000, Word(28));
        Assert.Equal(401, Word(58));
        Assert.Equal(220, actual[103]);
        Assert.Equal(230, actual[108]);
        Assert.Equal(new byte[] { 153, 120, 78, 0x2C }, actual.AsSpan().Slice(44, 4).ToArray());
        Assert.Equal(0xA5, actual[6]);
        Assert.Equal(0xA5, actual[30]);
        Assert.Equal(0xA5, actual[111]);
        short Word(int offset) => BinaryPrimitives.ReadInt16LittleEndian(actual.AsSpan().Slice(offset));
    }

    [Fact]
    public void Enemy_restoration_uses_original_shifted_source_and_its_own_visibility_gate()
    {
        ImmutableArray<uint> saved = [0x1800, 4, 0, 0, 0x1020, 4, 0];
        Assert.Equal((ushort)0x1000, NativeBattleQuake38ActorFlags.Restore8DBE80(3, 0x1020, saved, false));
        Assert.Equal((ushort)4, NativeBattleQuake38ActorFlags.Restore8DBE80(4, 0, saved, true));
        Assert.Equal((ushort)0x1020, NativeBattleQuake38ActorFlags.Restore8DBE80(5, 0x1020, saved, false));
        Assert.Equal((ushort)0x800, NativeBattleQuake38ActorFlags.Restore8DC180(3, 0, saved, true));
        Assert.Equal((ushort)0, NativeBattleQuake38ActorFlags.Restore8DC180(3, 0, saved, false));
        Assert.Equal((ushort)0x800, NativeBattleQuake38ActorFlags.Restore8DC180(5, 0x800, saved, true));
    }

    [Fact]
    public void Initial_ground_transform_saturates_signed_xyz_and_preserves_each_padding_word()
    {
        NativeBattleRawMesh mesh = new([new(new(100, -200, 300), 0), new(new(-100, 200, -300), 1)], []);
        ImmutableArray<short> identity = [4096, 0, 0, 0, 4096, 0, 0, 0, 4096];
        ImmutableArray<NativeBattleActionMatrix> bones = [new(identity, 32700, -32700, 400, 4096), new(identity, 20, 30, 40, 4096)];
        ImmutableArray<byte> retained = Enumerable.Range(1, 24).Select(value => (byte)value).ToImmutableArray();
        ImmutableArray<byte> actual = NativeBattleQuake38GroundVertices.Transform8D8B10(mesh, bones, retained);
        Assert.Equal(short.MaxValue, Word(0));
        Assert.Equal(short.MinValue, Word(2));
        Assert.Equal(700, Word(4));
        Assert.Equal(-80, Word(8));
        Assert.Equal(230, Word(10));
        Assert.Equal(-260, Word(12));
        Assert.Equal(retained.AsSpan().Slice(6, 2).ToArray(), actual.AsSpan().Slice(6, 2).ToArray());
        Assert.Equal(retained.AsSpan().Slice(14, 10).ToArray(), actual.AsSpan().Slice(14, 10).ToArray());
        short Word(int offset) => BinaryPrimitives.ReadInt16LittleEndian(actual.AsSpan().Slice(offset));
    }
}
