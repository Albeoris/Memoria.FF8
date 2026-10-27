using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBio22LaterEmitterTests
{
    [Theory]
    [InlineData(false, 24, 0x89CE60u)]
    [InlineData(true, 20, 0x89D120u)]
    public void Final_table_row_appends_before_one_CRT_per_child_and_uses_fresh_primary_scale(bool second, int maximum, uint childCallback)
    {
        NativeBattleBio22Resources resources = Counts(second, maximum, 2);
        NativeBattleBio22EmitterFrame frame = Frame(second);
        frame = frame with { Task = frame.Task.WriteByte(0x29, 1).WriteWord(0x24, (ushort)maximum), Random = new(6) };
        List<string> order = [];
        List<NativeBattleModulePoolTask> children = [];
        NativeBattleBio22LaterEmitterInputs inputs = new((current, parentAddress, callback) =>
        {
            order.Add("append");
            Assert.Equal(0x123456u, parentAddress);
            Assert.Equal(childCallback, callback);
            Assert.Equal(Draws(frame.Random, children.Count), current.Random);
            return new(current with { Task = current.Task.WriteByte(0x28, (byte)(current.Task.ReadByte(0x28) + 1)), Primary = current.Primary.WriteWord(0x86, unchecked((ushort)-4097)) }, (uint)(1 + children.Count), new NativeBattleModulePoolTask(1, 0, null, callback, new byte[0x70 - 12].ToImmutableArray()).WriteWord(0x22, 0xBEEF));
        }, (current, address, child) =>
        {
            order.Add("publish");
            NativeCrtRandomState random = Draws(frame.Random, children.Count).Draw(out ushort value);
            Assert.Equal(random, current.Random);
            uint scale = unchecked((uint)((value & 1) != 0 ? -4097 : -2048));
            Assert.Equal(scale, child.ReadDword(0x30));
            Assert.Equal(scale, child.ReadDword(0x34));
            Assert.Equal(scale, child.ReadDword(0x38));
            Assert.Equal((ushort)0xBEEF, child.ReadWord(0x22));
            children.Add(child);
            return current;
        });
        NativeBattleBio22EmitterStep step = Advance(second, frame, resources, inputs);
        Assert.Equal(new[] { "append", "publish", "append", "publish" }, order);
        Assert.Equal(Draws(frame.Random, 2), step.Frame.Random);
        Assert.Equal((byte)2, step.Frame.Task.ReadByte(0x28));
        Assert.Equal((ushort)(maximum + 1), step.Frame.Task.ReadWord(0x24));
        Assert.False(step.Result == 2);
    }

    [Theory]
    [InlineData(false, 24)]
    [InlineData(true, 20)]
    public void Past_limit_and_terminal_do_not_consume_tables_services_or_random_and_hold_actual_children(bool second, int maximum)
    {
        NativeBattleBio22EmitterFrame frame = Frame(second);
        frame = frame with { Task = frame.Task.WriteByte(0x29, 1).WriteWord(0x24, (ushort)(maximum + 1)).WriteByte(0x28, 1) };
        NativeBattleBio22EmitterStep held = Advance(second, frame, null!, new(RejectAppend, RejectPublish));
        Assert.Equal(0u, held.Result);
        Assert.Equal(frame.Random, held.Frame.Random);
        Assert.Equal((byte)2, held.Frame.Task.ReadByte(0x29));
        NativeBattleBio22EmitterStep complete = Advance(second, held.Frame with { Task = held.Frame.Task.WriteByte(0x28, 0) }, null!, new(RejectAppend, RejectPublish));
        Assert.Equal(2u, complete.Result);
        Assert.Equal(frame.Random, complete.Frame.Random);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Initialization_copies_primary_raw_position_and_zero_count_has_no_RNG_or_append_with_JSON_next(bool second)
    {
        NativeBattleBio22Resources resources = Counts(second, 1, 0);
        NativeBattleBio22EmitterFrame frame = Frame(second);
        frame = frame with { Primary = frame.Primary.WriteWord(0x78, 11).WriteWord(0x7A, unchecked((ushort)-21)).WriteWord(0x7C, 31).WriteWord(0x7E, 0xBEEF) };
        NativeBattleBio22EmitterStep initialized = Advance(second, frame, null!, new(RejectAppend, RejectPublish));
        Assert.Equal(frame.Primary.ReadDword(0x78), initialized.Frame.Task.ReadDword(0x1C));
        Assert.Equal(frame.Primary.ReadDword(0x7C), initialized.Frame.Task.ReadDword(0x20));
        NativeBattleBio22EmitterFrame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleBio22EmitterFrame>(NativeBattleTestSnapshots.Json(initialized.Frame))!;
        NativeBattleBio22EmitterStep next = Advance(second, initialized.Frame, resources, new(RejectAppend, RejectPublish));
        Assert.Equal(NativeBattleTestSnapshots.Json(next), NativeBattleTestSnapshots.Json(Advance(second, restored, resources, new(RejectAppend, RejectPublish))));
        Assert.Equal(frame.Random, next.Frame.Random);
    }

    [Fact]
    public void Mandatory_failed_append_is_not_silently_skipped()
    {
        NativeBattleBio22EmitterFrame frame = Frame(false);
        frame = frame with { Task = frame.Task.WriteByte(0x29, 1).WriteWord(0x24, 1) };
        Assert.Throws<NotSupportedException>(() => Advance(false, frame, Counts(false, 1, 1), new((current, parent, callback) => new(current, 0, current.Task), RejectPublish)));
    }

    private static NativeBattleBio22EmitterStep Advance(bool second, NativeBattleBio22EmitterFrame frame, NativeBattleBio22Resources resources, NativeBattleBio22LaterEmitterInputs inputs) => second ? NativeBattleBio22LaterEmitters.Advance89D020(0x123456, frame, resources, inputs) : NativeBattleBio22LaterEmitters.Advance89CD60(0x123456, frame, resources, inputs);
    private static NativeBattleBio22EmitterSpawn RejectAppend(NativeBattleBio22EmitterFrame frame, uint parent, uint callback) => throw new InvalidOperationException("Unexpected append.");
    private static NativeBattleBio22EmitterFrame RejectPublish(NativeBattleBio22EmitterFrame frame, uint address, NativeBattleModulePoolTask task) => throw new InvalidOperationException("Unexpected publish.");
    private static NativeBattleBio22EmitterFrame Frame(bool second) => new(new(0, 0, null, second ? 0x89D020u : 0x89CD60u, new byte[0x38 - 12].ToImmutableArray()), new(0, 0, null, 0x89A8D0, new byte[0x90].ToImmutableArray()), NativeBattleEffectMemory.FromColdProcess(), NativeCrtRandomState.FromColdProcess());
    private static NativeBattleBio22Resources Counts(bool second, int index, byte count)
    {
        NativeBattleBio22Resources resources = NativeBattleBio22GeometryTaskTests.Fixture().Resources;
        byte[] bytes = new byte[second ? 21 : 25];
        bytes[index] = count;
        return second ? resources with { LaterBillboardCounts = new("FF8_EN:16269AC", bytes.ToImmutableArray()) } : resources with { LaterPlanarCounts = new("FF8_EN:162698C", bytes.ToImmutableArray()) };
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
