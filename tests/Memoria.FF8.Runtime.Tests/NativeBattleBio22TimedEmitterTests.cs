using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBio22TimedEmitterTests
{
    [Fact]
    public void Initialization_copies_primary_raw_fourth_position_word_without_random_or_service_reads()
    {
        var fixture = Fixture();
        NativeBattleBio22EmitterStep step = NativeBattleBio22TimedEmitter.Advance89BA80(1, fixture.Frame, null!, null!);
        Assert.Equal(0xCAFE002Au, step.Frame.Task.ReadDword(0x20));
        Assert.Equal(0xFFF6000Bu, step.Frame.Task.ReadDword(0x1C));
        Assert.Equal((byte)1, step.Frame.Task.ReadByte(0x29));
        Assert.Equal((ushort)1, step.Frame.Task.ReadWord(0x24));
        Assert.Equal(fixture.Frame.Random, step.Frame.Random);
    }

    [Fact]
    public void Planar_then_billboard_preserve_three_then_five_random_calls_and_actual_append_height_order()
    {
        var fixture = Fixture();
        byte[] counts = new byte[21];
        counts[1] = 1;
        NativeBattleBio22Resources resources = fixture.Resources with { PlanarCounts = fixture.Resources.PlanarCounts with { Data = counts.ToImmutableArray() }, BillboardCounts = fixture.Resources.BillboardCounts with { Data = counts.ToImmutableArray() } };
        NativeBattleBio22EmitterFrame frame = fixture.Frame with { Task = fixture.Frame.Task.WriteByte(0x29, 1).WriteWord(0x24, 1) };
        List<uint> callbacks = [];
        List<NativeBattleModulePoolTask> children = [];
        NativeBattleBio22EmitterInputs inputs = new((current, parent, callback) =>
        {
            Assert.Equal(0x123400u, parent);
            Assert.Equal(Draws(frame.Random, callbacks.Count == 0 ? 2 : 5), current.Random);
            callbacks.Add(callback);
            NativeBattleModulePoolTask child = new NativeBattleModulePoolTask(1, 0, null, callback, new byte[0x70 - 12].ToImmutableArray()).WriteWord(0x1C, 100).WriteWord(0x1E, unchecked((ushort)-50)).WriteWord(0x20, 200).WriteDword(0x3C, 0xABCDEF01);
            return new(current with { Task = current.Task.WriteByte(0x28, (byte)callbacks.Count) }, (uint)callbacks.Count, child);
        }, (current, address, child) =>
        {
            Assert.Equal((uint)(children.Count + 1), address);
            children.Add(child);
            return current;
        }, current =>
        {
            Assert.Equal(Draws(frame.Random, 6), current.Random);
            Assert.Single(children);
            return -512;
        });
        NativeBattleBio22EmitterStep step = NativeBattleBio22TimedEmitter.Advance89BA80(0x123400, frame, resources, inputs);
        Assert.Equal(new uint[] { 0x89BF20, 0x89BD20 }, callbacks);
        Assert.Equal(Draws(frame.Random, 8), step.Frame.Random);
        Assert.Equal((byte)2, step.Frame.Task.ReadByte(0x28));
        Assert.Equal((ushort)0, children[0].ReadWord(0x1E));
        Assert.Equal(unchecked((ushort)-410), children[1].ReadWord(0x1E));
        Assert.Equal(4096u, children[0].ReadDword(0x30));
        Assert.Equal(3410u, children[1].ReadDword(0x30));
        Assert.All(children, child => Assert.Equal(0xABCDEF01u, child.ReadDword(0x3C)));
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(1, 0)]
    public void Counter_twenty_one_skips_resources_random_and_services_but_waits_for_children(byte children, uint result)
    {
        var fixture = Fixture();
        NativeBattleBio22EmitterFrame frame = fixture.Frame with { Task = fixture.Frame.Task.WriteByte(0x29, 1).WriteWord(0x24, 21).WriteByte(0x28, children) };
        NativeBattleBio22EmitterStep step = NativeBattleBio22TimedEmitter.Advance89BA80(1, frame, null!, null!);
        Assert.Equal(result, step.Result);
        Assert.Equal((byte)2, step.Frame.Task.ReadByte(0x29));
        Assert.Equal(frame.Random, step.Frame.Random);
    }

    [Fact]
    public void Zero_count_tables_consume_no_services_and_independent_JSON_continuation_is_equal()
    {
        var fixture = Fixture();
        NativeBattleBio22EmitterFrame frame = fixture.Frame with { Task = fixture.Frame.Task.WriteByte(0x29, 1).WriteWord(0x24, 20) };
        NativeBattleBio22EmitterFrame restored = JsonSerializer.Deserialize<NativeBattleBio22EmitterFrame>(JsonSerializer.Serialize(frame))!;
        NativeBattleBio22EmitterStep expected = NativeBattleBio22TimedEmitter.Advance89BA80(1, frame, fixture.Resources, null!);
        Assert.Equal(frame.Random, expected.Frame.Random);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(NativeBattleBio22TimedEmitter.Advance89BA80(1, restored, fixture.Resources, null!)));
    }

    private static (NativeBattleBio22EmitterFrame Frame, NativeBattleBio22Resources Resources) Fixture()
    {
        var fixture = NativeBattleBio22GeometryTaskTests.Fixture();
        NativeBattleModulePoolTask task = new(1, 0, null, 0x89BA80, new byte[0x38 - 12].ToImmutableArray());
        NativeBattleModulePoolTask primary = fixture.Parent.WriteDword(0x78, 0xFFF6000B).WriteDword(0x7C, 0xCAFE002A).WriteWord(0x86, 4096);
        return (new(task, primary, fixture.Memory, new(1)), fixture.Resources);
    }

    private static NativeCrtRandomState Draws(NativeCrtRandomState random, int count)
    {
        for (int index = 0; index < count; index++)
        {
            random = random.Draw(out _);
        }
        return random;
    }
}
