using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRenzokukenKnownEmptyContactTests
{
    private static readonly NativeBattleActionMatrix Identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);

    [Theory]
    [InlineData(160, 0, 1, 0)]
    [InlineData(340, 0, 1, 0)]
    [InlineData(160, 2, 3, 2)]
    [InlineData(340, 2, 3, 2)]
    [InlineData(160, 32767, 32768, 0)]
    [InlineData(340, 65535, 0, 0)]
    public void Known_empty_bank_keeps_native_signed_counter_and_lifetime(ushort effect, ushort counter, ushort expectedCounter, int expectedResult)
    {
        NativeBattleRenzokuken160Frame frame = Frame(effect);
        (NativeBattleEffectTaskPool pool, int slot) = ContactPool(counter);
        string before = JsonSerializer.Serialize(Core(frame));
        NativeBattleRenzokuken160Inputs inputs = Inputs([], []);
        NativeBattleRenzokuken160ChildStep step = NativeBattleRenzokuken160Controller.Advance600110(frame, pool, slot, inputs, Resources(effect), readOrigin: _ => throw new InvalidOperationException("A zero-count loop cannot read contact origins."));
        Assert.Equal(expectedCounter, step.Pool.Slots[slot].ReadWord(12));
        Assert.Equal(expectedResult, step.Result);
        Assert.Equal(frame.Random, step.Frame.Random);
        Assert.Equal(before, JsonSerializer.Serialize(Core(step.Frame)));
        Assert.Null(step.Pool.Slots[slot].Next);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(512)]
    public void Native_graphics_gate_precedes_even_unknown_contact_inputs(uint flags)
    {
        NativeBattleRenzokuken160Frame frame = Frame(340);
        (NativeBattleEffectTaskPool pool, int slot) = ContactPool(2);
        NativeBattleRenzokuken160ChildStep step = NativeBattleRenzokuken160Controller.Advance600110(frame, pool, slot, Inputs(default, []) with { GraphicsFlags = flags }, Resources(340) with { ContactLeadFrames = default });
        Assert.Same(frame, step.Frame);
        Assert.Equal((ushort)2, step.Pool.Slots[slot].ReadWord(12));
        Assert.Equal(0, step.Result);
    }

    [Fact]
    public void Unknown_AB_unknown_leads_and_short_actual_leads_still_fail_without_advancing()
    {
        NativeBattleRenzokuken160Frame frame = Frame(340);
        (NativeBattleEffectTaskPool pool, int slot) = ContactPool(0);
        NativeBattleRenzokuken160Resources resources = Resources(340);
        Assert.Throws<NotSupportedException>(() => NativeBattleRenzokuken160Controller.Advance600110(frame, pool, slot, Inputs(default, []), resources));
        Assert.Throws<NotSupportedException>(() => NativeBattleRenzokuken160Controller.Advance600110(frame, pool, slot, Inputs([], []), resources with { ContactLeadFrames = default }));
        Assert.Throws<NotSupportedException>(() => NativeBattleRenzokuken160Controller.Advance600110(frame, pool, slot, Inputs([18, 8, 7, 7, 7, 10, 8, 10], []), resources with { ContactLeadFrames = new int[6].ToImmutableArray() }));
        Assert.Equal((ushort)0, pool.Slots[slot].ReadWord(12));
        Assert.Equal(NativeCrtRandomState.FromColdProcess(), frame.Random);
    }

    [Fact]
    public void Cold_340_contact_child_runs_in_the_birth_pass_before_delayed_AB_publication()
    {
        NativeBattleRenzokuken160Frame frame = Frame(340);
        NativeBattleRenzokukenSpecialProfile profile = NativeBattleRenzokukenSpecialProfiles.ForEffect(340);
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(profile.ChildDescriptor, profile.ChildBank, 36, 100);
        (pool, int controller) = pool.Append(profile.ControllerCallback);
        pool = pool.WriteSlot(controller, pool.Slots[controller].WriteWord(12, 2));
        List<NativeBattleRenzokuken160Operation> operations = [];
        List<uint> visited = [];
        NativeCrtRandomState? afterController = null;
        int contactSlot = -1;
        NativeBattleEffectTaskStep advanced = pool.Advance((current, slot) =>
        {
            uint callback = current.Slots[slot].Callback;
            visited.Add(callback);
            if (callback == profile.ControllerCallback)
            {
                NativeBattleRenzokuken160ChildStep parent = NativeBattleRenzokukenSpecialController.AdvanceLateSpecialChild(frame, current, slot, Inputs([], operations), Resources(340), _ => new(0, 0, 0, 0), Identity, null!, null, null!, 0);
                frame = parent.Frame;
                afterController = frame.Random;
                return new(parent.Pool, parent.Result);
            }
            if (callback == 0x57D000)
            {
                contactSlot = slot;
                Assert.False(Core(frame).AbSegments.IsDefault);
                Assert.Empty(Core(frame).AbSegments);
                Assert.Equal(4, Core(frame).Duration);
                NativeBattleRenzokuken160ChildStep contact = NativeBattleRenzokuken160Controller.Advance600110(frame, current, slot, Inputs(Core(frame).AbSegments, operations), Resources(340), 0x57D3D0, _ => throw new InvalidOperationException("AB has not reached its source producer yet."));
                frame = contact.Frame;
                return new(contact.Pool, contact.Result);
            }
            // The unrelated trail and spiral draw callbacks are outside this contact traversal assertion.
            return new(current, 0);
        });
        Assert.Equal(new uint[] { profile.ControllerCallback, 0x57C950, 0x57CED0, 0x57D000 }, visited);
        Assert.Equal(4, advanced.Survivors);
        Assert.Equal((ushort)3, advanced.Pool.Slots[controller].ReadWord(12));
        Assert.Equal((ushort)1, advanced.Pool.Slots[contactSlot].ReadWord(12));
        Assert.Equal(afterController, frame.Random);
        Assert.Empty(Core(frame).AbSegments);
        Assert.Single(operations.OfType<NativeBattleRenzokuken160ActorResource>());
        Assert.DoesNotContain(operations, operation => operation is NativeBattleRenzokukenSpecialTargetSequence);

        ImmutableArray<byte> reached = [18, 8, 7, 7, 7, 10, 8, 10];
        NativeBattleRenzokuken160ChildStep next = NativeBattleRenzokukenSpecialController.AdvanceLateSpecialChild(frame, advanced.Pool, controller, Inputs(reached, operations), Resources(340), _ => new(0, 0, 0, 0), Identity, null!, null, null!, 0);
        Assert.Equal(reached, Core(next.Frame).AbSegments);
        Assert.Equal(75, Core(next.Frame).Duration);
    }

    [Fact]
    public void Reached_contact_keeps_original_lead_timing_allocation_and_random_draw_count()
    {
        NativeBattleRenzokuken160Frame frame = Frame(340, 100);
        (NativeBattleEffectTaskPool pool, int slot) = ContactPool(16);
        NativeBattleRenzokuken160Inputs inputs = Inputs([18, 10], []);
        int origins = 0;
        NativeBattleRenzokuken160ChildStep before = NativeBattleRenzokuken160Controller.Advance600110(frame, pool, slot, inputs, Resources(340), 0x57D3D0, _ => throw new InvalidOperationException("Contact has not reached its lead threshold."));
        Assert.Equal(frame.Random, before.Frame.Random);
        Assert.Null(before.Pool.Slots[slot].Next);
        NativeBattleRenzokuken160ChildStep contact = NativeBattleRenzokuken160Controller.Advance600110(before.Frame, before.Pool, slot, inputs, Resources(340), 0x57D3D0, index =>
        {
            Assert.Equal(0, index);
            origins++;
            return new(new(10, 20, 30), 0);
        });
        Assert.Equal(1, origins);
        Assert.Equal((ushort)18, contact.Pool.Slots[slot].ReadWord(12));
        int child = Assert.IsType<int>(contact.Pool.Slots[slot].Next);
        Assert.Equal(0x57D3D0u, contact.Pool.Slots[child].Callback);
        Assert.Equal((ushort)0, contact.Pool.Slots[child].ReadWord(12));
        Assert.Equal((ushort)0, contact.Pool.Slots[child].ReadWord(14));
        NativeCrtRandomState expectedRandom = frame.Random;
        const int contactDraws = 1 + 16 * 3 + 3 * 4 + 12 * 5;
        for (int draw = 0; draw < contactDraws; draw++)
        {
            expectedRandom = expectedRandom.Draw(out _);
        }
        Assert.Equal(expectedRandom, contact.Frame.Random);
    }

    private static NativeBattleRenzokuken160Inputs Inputs(ImmutableArray<byte> segments, List<NativeBattleRenzokuken160Operation> operations) => new(0, segments, _ => new(0, 0) { GeometryWorldMatrix = Identity }, (frame, operation) =>
    {
        operations.Add(operation);
        return frame;
    });

    private static (NativeBattleEffectTaskPool Pool, int Slot) ContactPool(ushort counter)
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x21EA9C8, 0x21EA9D8, 36, 100);
        (pool, int slot) = pool.Append(0x57D000);
        return (pool.WriteSlot(slot, pool.Slots[slot].WriteWord(12, counter)), slot);
    }

    private static NativeBattleRenzokuken160Resources Resources(ushort effect) => new(new("animation", []), new(effect, 0, 0, [], [], new([]), 0, 1, [], new([], 0)) { TargetResources = [new(0, [], [], new([]))] }, [], [], [1, 1, 1, 1, 1, 1, 1], [], 0, 0);

    private static NativeBattleRenzokuken160Frame Frame(ushort effect, int duration = 4)
    {
        NativeBattleRenzokuken160State core = NativeBattleRenzokuken160State.FromColdProcess().Enter(0, 0, 3, true) with { Duration = duration };
        NativeBattleEffectMemory memory = effect == 160 ? NativeBattleEffectMemory.FromColdProcess() with { Renzokuken160 = core } : NativeBattleEffectMemory.FromColdProcess() with { RenzokukenSpecials = ImmutableDictionary<ushort, NativeBattleRenzokukenSpecialState>.Empty.Add(effect, new(core, Resources(effect)) { ExtraStorage = new byte[3200].ToImmutableArray() }) };
        NativeBattleOriginalEffectState original = new(effect, effect - 1, new(effect, 0, 0), null, null);
        return new(memory, original, NativeCrtRandomState.FromColdProcess());
    }

    private static NativeBattleRenzokuken160State Core(NativeBattleRenzokuken160Frame frame) => frame.Effect.Binding.Effect == 160 ? frame.Memory.Renzokuken160! : NativeBattleRenzokukenSpecialController.ReadState(frame).Core;
}
