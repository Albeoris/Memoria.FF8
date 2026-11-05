using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCommonModuleMotionTests
{
    [Theory]
    [InlineData(0, 31, 1115)]
    [InlineData(1, 38, 26)]
    [InlineData(2, 31, 370)]
    [InlineData(3, 31, 370)]
    [InlineData(4, 27, 75)]
    [InlineData(5, 27, 75)]
    public void Every_casting_selector_has_transient_draw_owners_without_changing_native_payloads_or_random_draws(int selector, int expectedCallbacks, int expectedRandomDraws)
    {
        NativeBattleModuleState state = NativeBattleModuleState.Clear().AppendModelEffect(0, (byte)selector);
        NativeCrtRandomState initial = new(17);
        NativeCrtRandomState random = initial;
        Dictionary<int, NativeBattlePilotVisualIdentity> liveOwners = [];
        int callbacks = 0;
        int geometricDraws = 0;
        int stableOwners = 0;
        while (state.Head is not null && callbacks < 64)
        {
            NativeBattleModuleState restored = JsonSerializer.Deserialize<NativeBattleModuleState>(JsonSerializer.Serialize(state))!;
            NativeBattleModuleInputs inputs = Inputs(selector, random);
            NativeBattleModuleStep step = state.Advance(inputs);
            NativeBattleModuleStep resumed = restored.Advance(inputs);
            Assert.Equal(step.CrtRandomAfter, resumed.CrtRandomAfter);
            Assert.Equal(JsonSerializer.Serialize(step.State), JsonSerializer.Serialize(resumed.State));
            Assert.Equal(NativeDraws(step.Draws), NativeDraws(resumed.Draws));
            foreach (NativeBattleModuleDraw draw in step.Draws.Where(draw => draw.Kind is NativeBattleModuleDrawKind.Mesh or NativeBattleModuleDrawKind.Sprite))
            {
                NativeBattlePilotVisualIdentity identity = Assert.IsType<NativeBattlePilotVisualIdentity>(draw.PilotIdentity);
                Assert.Equal((ushort)0, identity.Effect);
                Assert.Equal(NativeBattleModuleState.DescriptorAddress, identity.Bank);
                Assert.Null(draw.PresentationOwnerEffect);
                geometricDraws++;
            }
            Assert.All(resumed.Draws.Where(draw => draw.Kind is NativeBattleModuleDrawKind.Mesh or NativeBattleModuleDrawKind.Sprite), draw => Assert.NotNull(draw.PilotIdentity));
            for (int slot = 0; slot < state.Slots.Length; slot++)
            {
                NativeBattleModuleTask before = state.Slots[slot];
                NativeBattleModuleTask after = step.State.Slots[slot];
                if (after.PilotIdentity is not { } owner)
                {
                    liveOwners.Remove(slot);
                    continue;
                }
                if (before.PilotIdentity is { } previous && previous == owner && liveOwners.TryGetValue(slot, out NativeBattlePilotVisualIdentity retained))
                {
                    Assert.Equal(retained, owner);
                    stableOwners++;
                }
                if (after.Flags == 0)
                {
                    liveOwners.Remove(slot);
                    continue;
                }
                liveOwners[slot] = owner;
            }
            state = step.State;
            random = step.CrtRandomAfter!;
            callbacks++;
        }
        Assert.Equal(expectedCallbacks, callbacks);
        Assert.Null(state.Head);
        Assert.True(geometricDraws > 0);
        Assert.True(stableOwners > 0);
        NativeCrtRandomState expected = initial;
        for (int draw = 0; draw < expectedRandomDraws; draw++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, random);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Reused_geometric_task_slot_receives_a_new_allocation_owner(int selector)
    {
        NativeBattleModuleState state = NativeBattleModuleState.Clear().AppendModelEffect(0, (byte)selector);
        NativeCrtRandomState random = new(7);
        NativeBattleModuleDraw? selected = null;
        for (int callback = 0; callback < 8 && selected is null; callback++)
        {
            NativeBattleModuleStep step = state.Advance(Inputs(selector, random));
            selected = step.Draws.FirstOrDefault(draw => draw.Kind is NativeBattleModuleDrawKind.Mesh or NativeBattleModuleDrawKind.Sprite);
            state = step.State;
            random = step.CrtRandomAfter!;
        }
        NativeBattlePilotVisualIdentity identity = Assert.IsType<NativeBattlePilotVisualIdentity>(Assert.IsType<NativeBattleModuleDraw>(selected).PilotIdentity);
        NativeBattleModuleTask task = state.Slots[identity.Slot];
        NativeBattleModuleState released = state with { Slots = state.Slots.SetItem(identity.Slot, task with { Flags = 0 }), Head = null, Tail = null };
        (NativeBattleModuleState reused, int slot) = released.Append(task.Kind);
        Assert.Equal(identity.Slot, slot);
        NativeBattleModuleStep replacement = reused.Advance(Inputs(selector, random));
        Assert.NotNull(replacement.State.Slots[slot].PilotIdentity);
        Assert.NotEqual(task.PilotIdentity, replacement.State.Slots[slot].PilotIdentity);
        Assert.DoesNotContain(replacement.Draws, draw => draw.PilotIdentity == identity);
    }

    private static string NativeDraws(IEnumerable<NativeBattleModuleDraw> draws)
    {
        return JsonSerializer.Serialize(draws.Select(draw => draw with { PilotIdentity = null }));
    }

    private static NativeBattleModuleInputs Inputs(int selector, NativeCrtRandomState random)
    {
        return new(0)
        {
            CrtRandom = random,
            ReadModelAnchor = _ => new(10, 20, 30, 40),
            ReadModelTransform = _ => new(new(0, 0, 0), new(0, 0, 0), 0, 700, 4096),
            ReadModelBoneCount = _ => 47,
            ReadModelBone = (_, _, _) => new(100, 200, 300),
            Resources = [Sprite(4, 8), Sprite(5, selector >= 4 ? 2 : 10), Sprite(6, 11)]
        };
    }

    private static NativeBattleModuleResource Sprite(int id, int frames)
    {
        int start = 12 + frames * 2;
        byte[] bytes = new byte[start + frames * 4];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), (ushort)frames);
        for (int frame = 0; frame < frames; frame++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10 + frame * 2), (ushort)(start + frame * 4));
        }
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10 + frames * 2), ushort.MaxValue);
        return new((byte)id, (uint)(id + 1), 0x570CC0, 0x570BC0, bytes.ToImmutableArray());
    }
}
