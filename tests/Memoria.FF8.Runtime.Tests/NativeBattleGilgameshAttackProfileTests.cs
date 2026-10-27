using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGilgameshAttackProfileTests
{
    [Theory]
    [InlineData(329, 0x591250u, 0)]
    [InlineData(330, 0x5920E0u, 1)]
    public void Initial_flare_selects_the_exact_material_without_initializing_other_words(int effect, uint callback, int material)
    {
        NativeBattleGilgameshAttackProfile profile = NativeBattleGilgameshAttackProfile.ForEffect(effect);
        Assert.Equal(callback, profile.Callback);
        NativeBattleGilgameshAttackStep step = Step(effect, 0);
        NativeBattleModulePoolTask child = step.Children.Slots[0];
        Assert.Equal(0x591470u, child.Callback);
        Assert.Equal((ushort)material, child.ReadWord(14));
        Assert.Equal((ushort)0xA5A5, child.ReadWord(20));
        Assert.Equal((ushort)0xA5A5, child.ReadWord(34));
        Assert.Equal((ushort)1, step.Task.ReadWord(12));
        Assert.Equal(new NativeCrtRandomState(0x12345678), step.Random);
    }

    [Fact]
    public void Excaliber_and_Excalipoor_emit_identical_contact_children_except_the_two_material_words()
    {
        NativeBattleGilgameshAttackStep excaliber = Step(329, 18);
        NativeBattleGilgameshAttackStep excalipoor = Step(330, 18);
        Assert.Equal(excalipoor.Random, excaliber.Random);
        Assert.Equal(new uint[] { 0x5915C0, 0x591850, 0x591B80, 0x591F50, 0x5915C0, 0x591850, 0x591B80, 0x591F50 }, excaliber.Children.Slots.Take(8).Select(child => child.Callback));
        for (int index = 0; index < 8; index++)
        {
            NativeBattleModulePoolTask first = excaliber.Children.Slots[index];
            NativeBattleModulePoolTask second = excalipoor.Children.Slots[index];
            if (index % 4 < 2)
            {
                Assert.Equal((ushort)0, first.ReadWord(14));
                Assert.Equal((ushort)1, second.ReadWord(14));
                first = first.WriteWord(14, 1);
            }
            else
            {
                Assert.Equal((ushort)(index % 4 == 2 ? 0xA5A5 : 1), first.ReadWord(14));
            }
            Assert.Equal(NativeBattleTestSnapshots.Json(second), NativeBattleTestSnapshots.Json(first));
        }
        NativeBattleModulePoolTask main = excaliber.Children.Slots[0];
        Assert.Equal((ushort)500, main.ReadWord(16));
        Assert.Equal(unchecked((ushort)-200), main.ReadWord(18));
        Assert.Equal(unchecked((ushort)-300), main.ReadWord(20));
        Assert.Equal((ushort)1, main.ReadWord(22));
        Assert.Equal((ushort)0x200, excaliber.Children.Slots[5].ReadWord(22));
    }

    [Theory]
    [InlineData(19, 0u, 20, true, 0)]
    [InlineData(49, 0u, 50, false, 2)]
    [InlineData(32767, 0u, 32768, false, 0)]
    [InlineData(65535, 0u, 0, false, 0)]
    [InlineData(18, 0x200u, 18, false, 0)]
    [InlineData(19, 1u, 19, false, 0)]
    public void Excaliber_retains_pause_contact_and_signed_terminal_timing(int counter, uint flags, int next, bool contact, int result)
    {
        NativeBattleGilgameshAttackStep step = Step(329, (ushort)counter, flags);
        Assert.Equal((ushort)next, step.Task.ReadWord(12));
        Assert.Equal(contact, step.Contact);
        Assert.Equal(result, step.Result);
        Assert.All(step.Children.Slots, child => Assert.Equal(0u, child.Callback));
        Assert.Equal(new NativeCrtRandomState(0x12345678), step.Random);
    }

    [Fact]
    public void Unsupported_or_mismatched_profiles_fail_before_producing_children()
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleGilgameshAttackProfile.ForEffect(328));
        Assert.Throws<InvalidDataException>(() => new NativeBattleGilgameshAttackProfile(329, 0x5920E0, 0).Validate());
        Assert.Throws<InvalidDataException>(() => new NativeBattleGilgameshAttackProfile(329, 0x591250, 1).Validate());
        Assert.Throws<InvalidDataException>(() => NativeBattleGilgameshExcalipoorAttack.Advance(NativeBattleGilgameshAttackProfile.ForEffect(329), Task(330, 18), Pool(), new(1), 0, [], _ => throw new InvalidOperationException()));
    }

    private static NativeBattleGilgameshAttackStep Step(int effect, ushort counter, uint flags = 0) => NativeBattleGilgameshExcalipoorAttack.Advance(NativeBattleGilgameshAttackProfile.ForEffect(effect), Task(effect, counter), Pool(), new(0x12345678), flags, [5, 3], actor => new((short)(actor * 100), -200, -300, -999));
    private static NativeBattleModulePoolTask Task(int effect, ushort counter) => new NativeBattleModulePoolTask(1, 0, null, NativeBattleGilgameshAttackProfile.ForEffect(effect).Callback, Enumerable.Repeat((byte)0xA5, 24).ToImmutableArray()).WriteWord(12, counter);

    private static NativeBattleEffectTaskPool Pool()
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x21FFD58, 0x21FFD68, 36, 100);
        return NativeBattleTaskFixture.EditPool(pool, data => data with { Slots = pool.Slots.Select(task => NativeBattleTaskFixture.EditTask(task, taskData => taskData with { Payload = Enumerable.Repeat((byte)0xA5, 24).ToImmutableArray() })).ToImmutableArray() });
    }
}
