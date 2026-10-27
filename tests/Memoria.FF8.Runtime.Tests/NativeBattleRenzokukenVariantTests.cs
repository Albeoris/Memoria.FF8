using System.Collections.Immutable;
using System.Buffers.Binary;
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
        NativeBattleEffectMemory unknown = frame.Memory with { RenzokukenVariants = ImmutableDictionary<ushort, NativeBattleRenzokukenVariantState>.Empty.Add(140, state) };
        Assert.Throws<InvalidDataException>(() => unknown.ValidateRenzokukenVariants());
        NativeBattleEffectMemory.FromColdProcess().ValidateRenzokukenVariants();
    }

    [Fact]
    public void Native141LateImpactCapturesOriginalTrailAndConsumesOneRandomDraw()
    {
        NativeBattleRenzokuken160Frame frame = Frame();
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x24C2388, 0x24C2398, 36, 100);
        (pool, int slot) = pool.Append(0x61DF70);
        pool = NativeBattleTaskFixture.EditPool(pool, data => data with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteWord(12, 51)) });
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
        pool = NativeBattleTaskFixture.EditPool(pool, data => data with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteWord(12, 2)) });
        NativeBattleRenzokuken160Resources resources = new(new("animation", []), null!, [], [], [], [], 0, 0);
        NativeBattleRenzokuken160Inputs inputs = new(0, [10, 20, 30, 40, 10, 5], _ => new(0, 0), (current, _) => current);
        NativeBattleRenzokuken160ChildStep step = NativeBattleRenzokukenVariantController.Advance61DF70(frame, pool, slot, inputs, resources);
        Assert.Equal(115, NativeBattleRenzokukenVariantController.ReadState(step.Frame).Core.Duration);
        Assert.Equal(new uint[] { 0x61DF70, 0x61E8B0, 0x61EDF0, 0x61EF20 }, step.Pool.Slots.Take(4).Select(task => task.Callback));
        Assert.Equal(1, step.Pool.Slots[0].Next);
        Assert.Equal(2, step.Pool.Slots[1].Next);
        Assert.Equal(3, step.Pool.Slots[2].Next);
    }

    [Theory]
    [InlineData(259, 58, 1, 0x5A8340, 0)]
    [InlineData(332, 78, 4, 0x58D130, 0x58D2D0)]
    [InlineData(333, 76, 2, 0x58B3A0, 0x58B520)]
    public void VariantLateBranchesRetainNativeRngOrderAndPhysicalLinks(int effect, int counter, int draws, uint first, uint second)
    {
        NativeBattleRenzokuken160Frame frame = Frame((ushort)effect);
        NativeBattleRenzokukenVariantProfile profile = NativeBattleRenzokukenVariantProfile.ForEffect((ushort)effect);
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(profile.ChildDescriptor, profile.ChildBank, 36, profile.ChildCapacity);
        (pool, int slot) = pool.Append(profile.ControllerCallback);
        pool = NativeBattleTaskFixture.EditPool(pool, data => data with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteWord(12, (ushort)counter)) });
        NativeBattleRenzokuken160Resources resources = new(new("animation", []), null!, [], [], [], [], 0, 0);
        NativeBattleActionMatrix matrix = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 11, 22, 33, 4096);
        NativeBattleRenzokuken160Inputs inputs = new(0, [], _ => new(0, 0) { GeometryWorldMatrix = matrix }, (current, _) => current);
        NativeBattleRenzokuken160ChildStep step = NativeBattleRenzokukenVariantController.AdvanceController(frame, pool, slot, inputs, resources, profile);
        NativeCrtRandomState expected = frame.Random;
        for (int index = 0; index < draws; index++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, step.Frame.Random);
        Assert.Equal(1, step.Pool.Slots[slot].Next);
        Assert.Equal(first, step.Pool.Slots[1].Callback);
        Assert.Equal(0, step.Pool.Slots[1].ReadWord(12));
        if (second != 0)
        {
            Assert.Equal(2, step.Pool.Slots[1].Next);
            Assert.Equal(second, step.Pool.Slots[2].Callback);
            Assert.Same(matrix, NativeBattleRenzokukenVariantController.ReadState(step.Frame).LateMatrix);
        }
        Assert.Same(frame.Memory.Renzokuken160, step.Frame.Memory.Renzokuken160);
    }

    [Theory]
    [InlineData(332)]
    [InlineData(333)]
    public void OffsetContactUsesReachedPositionAndRetainsIndeterminatePadding(int effect)
    {
        NativeBattleRenzokuken160Frame frame = Frame((ushort)effect);
        NativeBattleRenzokukenVariantProfile profile = NativeBattleRenzokukenVariantProfile.ForEffect((ushort)effect);
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(profile.ChildDescriptor, profile.ChildBank, 36, profile.ChildCapacity);
        (pool, int slot) = pool.Append(profile.ContactCallback);
        pool = NativeBattleTaskFixture.EditPool(pool, data => data with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteWord(12, 10)) });
        ImmutableArray<byte> segments = Enumerable.Repeat((byte)10, profile.Contacts + 1).ToImmutableArray();
        NativeBattleRenzokuken160Resources resources = new(new("animation", []), null!, [], [], Enumerable.Repeat(0, profile.Contacts).ToImmutableArray(), [], 0, 0);
        NativeBattleRenzokuken160Inputs inputs = new(0, segments, _ => throw new InvalidOperationException(), (current, _) => current);
        int calls = 0;
        NativeBattleRenzokuken160ChildStep step = NativeBattleRenzokuken160Controller.Advance600110(frame, pool, slot, inputs, resources, profile.ImpactCallback, contact =>
        {
            Assert.Equal(0, contact);
            calls++;
            return new(new(1234, -567, 2345), null);
        });
        Assert.Equal(1, calls);
        Assert.Equal(profile.ImpactCallback, step.Pool.Slots[1].Callback);
        NativeBattleRenzokukenVariantState state = NativeBattleRenzokukenVariantController.ReadState(step.Frame);
        Assert.Equal(1234, BinaryPrimitives.ReadInt16LittleEndian(state.Core.ImpactStates.AsSpan()));
        Assert.Equal(-567, BinaryPrimitives.ReadInt16LittleEndian(state.Core.ImpactStates.AsSpan().Slice(2)));
        Assert.Equal(2345, BinaryPrimitives.ReadInt16LittleEndian(state.Core.ImpactStates.AsSpan().Slice(4)));
        Assert.Equal((ushort)1, state.ContactPadding!.Impacts);
        Assert.Equal(7UL, state.ContactPadding.Sparks);
        Assert.Equal(4095UL, state.ContactPadding.Debris);
        Assert.Same(frame.Memory.Renzokuken160, step.Frame.Memory.Renzokuken160);
    }

    [Theory]
    [InlineData(332)]
    [InlineData(333)]
    public void OffsetContactFailsWithoutTheOriginalTransformProvider(int effect)
    {
        NativeBattleRenzokuken160Frame frame = Frame((ushort)effect);
        NativeBattleRenzokukenVariantProfile profile = NativeBattleRenzokukenVariantProfile.ForEffect((ushort)effect);
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(profile.ChildDescriptor, profile.ChildBank, 36, profile.ChildCapacity);
        (pool, int slot) = pool.Append(profile.ContactCallback);
        NativeBattleRenzokuken160Resources resources = new(new("animation", []), null!, [], [], [], [], 0, 0);
        NativeBattleRenzokuken160Inputs inputs = new(0, [], _ => throw new InvalidOperationException(), (current, _) => current);
        Assert.Throws<NotSupportedException>(() => NativeBattleRenzokuken160Controller.Advance600110(frame, pool, slot, inputs, resources, profile.ImpactCallback));
    }

    private static NativeBattleRenzokuken160Frame Frame(ushort selectedEffect = 141)
    {
        NativeBattleRenzokukenVariantProfile profile = NativeBattleRenzokukenVariantProfile.ForEffect(selectedEffect);
        NativeBattleRenzokuken160State core = NativeBattleRenzokuken160State.FromColdProcess().Enter(0, 0, 3, false) with { Duration = 100, Origin = new(100, -200, 300), Direction = new(0, 0, -4096) };
        NativeBattleRenzokukenVariantState state = new(core, default, default, null);
        if (profile.HasContactOffsets)
        {
            state = state with { ContactPadding = new(0, 0, 0), LateMatrix = new(new short[9].ToImmutableArray(), 0, 0, 0, 4096) };
        }
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { RenzokukenVariants = ImmutableDictionary<ushort, NativeBattleRenzokukenVariantState>.Empty.Add(selectedEffect, state) };
        NativeBattleOriginalEffectState effect = new(selectedEffect, selectedEffect - 1, new(selectedEffect, profile.Entry, profile.Initializer), null, null);
        return new(memory, effect, NativeCrtRandomState.FromColdProcess());
    }
}
