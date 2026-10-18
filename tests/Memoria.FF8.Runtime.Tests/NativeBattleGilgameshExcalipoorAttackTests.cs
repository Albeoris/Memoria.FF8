using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGilgameshExcalipoorAttackTests
{
    [Fact]
    public void Initial_flare_writes_only_original_fields_and_pause_performs_no_allocation()
    {
        NativeBattleEffectTaskPool pool = Pool();
        NativeBattleGilgameshAttackStep paused = Step(Task(0), pool, 0x200);
        Assert.Same(pool, paused.Children);
        Assert.Equal((ushort)0, paused.Task.ReadWord(12));
        NativeBattleGilgameshAttackStep step = Step(Task(0), pool);
        Assert.Equal(0x591470u, step.Children.Slots[0].Callback);
        NativeBattleModulePoolTask flare = step.Children.Slots[0];
        Assert.Equal((ushort)0xFF56, flare.ReadWord(16));
        Assert.Equal((ushort)0xFF9C, flare.ReadWord(18));
        Assert.Equal((ushort)0x968, flare.ReadWord(24));
        Assert.Equal((ushort)0x555, flare.ReadWord(28));
        Assert.Equal((ushort)0x555, flare.ReadWord(30));
        Assert.Equal((ushort)0x1000, flare.ReadWord(32));
        Assert.Equal((ushort)0xA5A5, flare.ReadWord(20));
        Assert.Equal((ushort)0xA5A5, flare.ReadWord(34));
        Assert.False(step.Contact);
        Assert.Equal(new NativeCrtRandomState(1), step.Random);
    }

    [Fact]
    public void Physical_contacts_produce_four_ordered_children_with_two_CRT_draws_each()
    {
        List<byte> reads = [];
        NativeCrtRandomState random = new(0xFEED1234);
        NativeBattleGilgameshAttackStep step = NativeBattleGilgameshExcalipoorAttack.Advance5920E0(Task(18), Pool(), random, 0, [5, 3], actor =>
        {
            reads.Add(actor);
            return new((short)(actor * 100), -200, -300, -999);
        });
        Assert.Equal(new byte[] { 5, 3 }, reads);
        for (int contact = 0; contact < 2; contact++)
        {
            NativeBattleModulePoolTask[] children = step.Children.Slots.Skip(contact * 4).Take(4).ToArray();
            Assert.Equal(new uint[] { 0x5915C0, 0x591850, 0x591B80, 0x591F50 }, children.Select(child => child.Callback));
            Assert.All(children, child =>
            {
                Assert.Equal(unchecked((ushort)(contact == 0 ? 500 : 300)), child.ReadWord(16));
                Assert.Equal(unchecked((ushort)-200), child.ReadWord(18));
                Assert.Equal(unchecked((ushort)-300), child.ReadWord(20));
            });
            Assert.Equal((ushort)(1 << contact), children[0].ReadWord(22));
            Assert.Equal((ushort)(0x100 << contact), children[1].ReadWord(22));
            Assert.Equal((ushort)(1 << contact), children[2].ReadWord(22));
            Assert.Equal((ushort)(1 << contact), children[3].ReadWord(22));
            Assert.Equal((ushort)0xA5A5, children[2].ReadWord(14));
            random = random.Draw(out ushort x);
            random = random.Draw(out ushort y);
            Assert.Equal((ushort)((x % 512 + 768) / 3), children[3].ReadWord(28));
            Assert.Equal(children[3].ReadWord(28), children[3].ReadWord(30));
            Assert.Equal((ushort)((y % 768 + 768) / 3), children[3].ReadWord(32));
            Assert.Equal(children[3].ReadWord(32), children[3].ReadWord(34));
        }
        Assert.Equal(random, step.Random);
        Assert.Equal((ushort)19, step.Task.ReadWord(12));
        Assert.False(step.Contact);
        Assert.True(Step(step.Task, step.Children).Contact);
        Assert.False(Step(Task(20), Pool()).Contact);
    }

    [Theory]
    [InlineData(48, 49, 0)]
    [InlineData(49, 50, 2)]
    [InlineData(32767, 32768, 0)]
    [InlineData(65535, 0, 0)]
    public void Counter_wrap_and_signed_terminal_comparison_are_preserved(int counter, int next, int result)
    {
        NativeBattleGilgameshAttackStep step = Step(Task((ushort)counter), Pool());
        Assert.Equal((ushort)next, step.Task.ReadWord(12));
        Assert.Equal(result, step.Result);
    }

    [Fact]
    public void Cold_JSON_before_emission_preserves_next_children_random_and_untouched_suffix()
    {
        NativeBattleModulePoolTask task = Task(18);
        NativeBattleEffectTaskPool pool = Pool();
        NativeBattleGilgameshAttackStep expected = Step(task, pool);
        NativeBattleGilgameshAttackStep restored = Step(JsonSerializer.Deserialize<NativeBattleModulePoolTask>(JsonSerializer.Serialize(task))!, JsonSerializer.Deserialize<NativeBattleEffectTaskPool>(JsonSerializer.Serialize(pool))!);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(restored));
    }

    [Fact]
    public void Exhausted_mandatory_bank_fails_instead_of_skipping_effect_children()
    {
        NativeBattleEffectTaskPool pool = Pool();
        for (int index = 0; index < 100; index++)
        {
            (pool, _) = pool.Append(0x591470);
        }
        Assert.Throws<InvalidOperationException>(() => Step(Task(18), pool));
    }

    private static NativeBattleGilgameshAttackStep Step(NativeBattleModulePoolTask task, NativeBattleEffectTaskPool pool, uint flags = 0) => NativeBattleGilgameshExcalipoorAttack.Advance5920E0(task, pool, new(1), flags, [3], _ => new(10, 20, 30, 40));
    private static NativeBattleModulePoolTask Task(ushort counter) => new NativeBattleModulePoolTask(1, 0, null, 0x5920E0, Enumerable.Repeat((byte)0xA5, 24).ToImmutableArray()).WriteWord(12, counter);

    private static NativeBattleEffectTaskPool Pool()
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x21FFD58, 0x21FFD68, 36, 100);
        return pool with { Slots = pool.Slots.Select(task => task with { Payload = Enumerable.Repeat((byte)0xA5, 24).ToImmutableArray() }).ToImmutableArray() };
    }
}
