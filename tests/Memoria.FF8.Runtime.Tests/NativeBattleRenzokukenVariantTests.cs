using System.Collections.Immutable;
using Memoria.FF8.Runtime;
using Xunit;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRenzokukenVariantTests
{
    [Fact]
    public void VariantSnapshotRejectsAnUnsupportedOwnerAndMissingEntryResources()
    {
        NativeBattleRenzokuken160Frame frame = Frame();
        Assert.Throws<InvalidDataException>(() => frame.Memory.ValidateRenzokukenVariants());
        NativeBattleRenzokukenVariantState state = NativeBattleRenzokukenVariantController.ReadState(frame);
        NativeBattleEffectMemory unknown = frame.Memory with { RenzokukenVariants = ImmutableDictionary<ushort, NativeBattleRenzokukenVariantState>.Empty.Add(259, state) };
        Assert.Throws<InvalidDataException>(() => unknown.ValidateRenzokukenVariants());
        NativeBattleEffectMemory.FromColdProcess().ValidateRenzokukenVariants();
    }

    [Fact]
    public void Native141LateImpactCapturesOriginalTrailAndConsumesOneRandomDraw()
    {
        NativeBattleRenzokuken160Frame frame = Frame();
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x24C2388, 0x24C2398, 36, 100);
        (pool, int slot) = pool.Append(0x61DF70);
        pool = pool with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteWord(12, 51)) };
        NativeBattleRenzokuken160Resources resources = new(new("animation", []), null!, [], [], [], [], 0, 0);
        NativeBattleRenzokuken160Inputs inputs = new(0, [], _ => throw new InvalidOperationException(), (current, _) => current);
        NativeBattleRenzokuken160ChildStep step = NativeBattleRenzokukenVariantController.Advance61DF70(frame, pool, slot, inputs, resources);
        NativeCrtRandomState expected = frame.Random.Draw(out ushort value);
        Assert.Equal(expected, step.Frame.Random);
        Assert.Equal(1, step.Pool.Slots[slot].Next);
        Assert.Equal(0x61F590u, step.Pool.Slots[1].Callback);
        Assert.Equal((value % 2560 + 7168) / 3, step.Pool.Slots[1].ReadWord(28));
        Assert.Equal(step.Pool.Slots[1].ReadWord(28), step.Pool.Slots[1].ReadWord(30));
        NativeBattleRenzokukenVariantState state = NativeBattleRenzokukenVariantController.ReadState(step.Frame);
        Assert.Equal(state.Core.Origin, state.LateOrigin);
        Assert.Equal(state.Core.Direction, state.LateDirection);
        Assert.Same(frame.Memory.Renzokuken160, step.Frame.Memory.Renzokuken160);
    }

    [Fact]
    public void AuthenticatedSpiralHelperWritesOnlyItsSelectedNativeOwner()
    {
        NativeBattleRenzokuken160Frame frame = Frame();
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x24C2388, 0x24C2398, 36, 100);
        (pool, int slot) = pool.Append(0x61EDF0);
        NativeBattleRenzokuken160Inputs inputs = new(0, [], _ => new(0, 0), (current, _) => current)
        {
            ReadBoneCount = _ => 0,
            ReadBone = (_, _, _) => new(100, -200, 300)
        };
        NativeBattleRenzokuken160ChildStep step = NativeBattleRenzokuken160Controller.Advance5FFFE0(frame, pool, slot, inputs);
        Assert.Same(frame.Memory.Renzokuken160, step.Frame.Memory.Renzokuken160);
        Assert.NotEqual(NativeBattleRenzokukenVariantController.ReadState(frame).Core.Spirals, NativeBattleRenzokukenVariantController.ReadState(step.Frame).Core.Spirals);
        Assert.Null(step.Frame.Effect.ReturnedDescriptor);
    }

    [Fact]
    public void Native141ControllerPublishesActualCallbackAddressesInOrder()
    {
        NativeBattleRenzokuken160Frame frame = Frame();
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x24C2388, 0x24C2398, 36, 100);
        (pool, int slot) = pool.Append(0x61DF70);
        pool = pool with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteWord(12, 2)) };
        NativeBattleRenzokuken160Resources resources = new(new("animation", []), null!, [], [], [], [], 0, 0);
        NativeBattleRenzokuken160Inputs inputs = new(0, [10, 20, 30, 40, 10, 5], _ => new(0, 0), (current, _) => current);
        NativeBattleRenzokuken160ChildStep step = NativeBattleRenzokukenVariantController.Advance61DF70(frame, pool, slot, inputs, resources);
        Assert.Equal(115, NativeBattleRenzokukenVariantController.ReadState(step.Frame).Core.Duration);
        Assert.Equal(new uint[] { 0x61DF70, 0x61E8B0, 0x61EDF0, 0x61EF20 }, step.Pool.Slots.Take(4).Select(task => task.Callback));
        Assert.Equal(1, step.Pool.Slots[0].Next);
        Assert.Equal(2, step.Pool.Slots[1].Next);
        Assert.Equal(3, step.Pool.Slots[2].Next);
    }

    private static NativeBattleRenzokuken160Frame Frame()
    {
        NativeBattleRenzokuken160State core = NativeBattleRenzokuken160State.FromColdProcess().Enter(0, 0, 3, false) with { Duration = 100, Origin = new(100, -200, 300), Direction = new(0, 0, -4096) };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { RenzokukenVariants = ImmutableDictionary<ushort, NativeBattleRenzokukenVariantState>.Empty.Add(141, new(core, default, default, null)) };
        NativeBattleOriginalEffectState effect = new(141, 140, new(141, 0x61DE70, 0x61DE50), null, null);
        return new(memory, effect, NativeCrtRandomState.FromColdProcess());
    }
}
