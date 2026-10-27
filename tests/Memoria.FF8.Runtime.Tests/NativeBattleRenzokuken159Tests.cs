using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using Xunit;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRenzokuken159Tests
{
    [Theory]
    [InlineData(41, 0x6027C0u, 1536, 4, 20)]
    [InlineData(50, 0x6027C0u, 1536, 4, 20)]
    [InlineData(51, 0x602910u, 4864, 16, 96)]
    public void NativeBurstUsesExactFrameMeshAndRandomConsumption(int counter, uint callback, int scale, int count, int draws)
    {
        NativeBattleRenzokuken159Frame frame = Frame();
        (NativeBattleEffectTaskPool pool, int slot) = Controller(counter);
        NativeBattleRenzokuken159ChildStep step = NativeBattleRenzokuken159Controller.Advance600CC0(frame, pool, slot, Inputs(), Resources());
        Assert.Equal(callback, step.Pool.Slots[1].Callback);
        Assert.Equal(scale, step.Pool.Slots[1].ReadWord(28));
        Assert.Equal(100, step.Pool.Slots[1].ReadWord(16));
        Assert.Equal(300, step.Pool.Slots[1].ReadWord(20));
        Assert.Equal(Advance(frame.Random, draws), step.Frame.Random);
        ImmutableArray<byte> debris = step.Frame.Memory.Renzokuken159!.Debris;
        Assert.Equal(1600, debris.Length);
        Assert.Equal(0, Word(debris, (count - 1) * 20));
        Assert.Equal(-1, Word(debris, count * 20));
        Assert.Equal(counter + 1, step.Pool.Slots[slot].ReadWord(12));
    }

    [Fact]
    public void LateTargetBurstUsesEightStaggeredTasksAndCurrentTargetAnchor()
    {
        NativeBattleRenzokuken159Frame frame = Frame();
        (NativeBattleEffectTaskPool pool, int slot) = Controller(84);
        int target = -1;
        NativeBattleRenzokuken159Inputs inputs = Inputs() with { ReadAnchor = actor =>
            {
                target = actor;
                return new(1000, -2000, 3000, 0);
            } };
        NativeBattleRenzokuken159ChildStep step = NativeBattleRenzokuken159Controller.Advance600CC0(frame, pool, slot, inputs, Resources());
        Assert.Equal(3, target);
        Assert.Equal(Advance(frame.Random, 32), step.Frame.Random);
        for (int index = 0; index < 8; index++)
        {
            NativeBattleModulePoolTask child = step.Pool.Slots[index + 1];
            Assert.Equal(0x602A50u, child.Callback);
            Assert.Equal(index * 2, child.ReadWord(14));
            Assert.Equal(0, child.ReadWord(18));
            Assert.InRange(child.ReadWord(16), 100, 1899);
            Assert.InRange(child.ReadWord(20), 2100, 3899);
            Assert.Equal(child.ReadWord(28), child.ReadWord(30));
            Assert.Equal(child.ReadWord(32), child.ReadWord(34));
        }
    }

    [Fact]
    public void FirstFrameSwapsActorAndTargetInNativeOrder()
    {
        List<NativeBattleRenzokuken159Operation> operations = [];
        (NativeBattleEffectTaskPool pool, int slot) = Controller(1);
        NativeBattleRenzokuken159Inputs inputs = Inputs() with { Execute = (frame, operation) =>
            {
                operations.Add(operation);
                return frame;
            } };
        NativeBattleRenzokuken159Controller.Advance600CC0(Frame(), pool, slot, inputs, Resources());
        Assert.IsType<NativeBattleRenzokuken159ActorResource>(operations[0]);
        Assert.Equal(1, ((NativeBattleRenzokuken159ActorResource)operations[0]).Sequence);
        Assert.IsType<NativeBattleRenzokuken159TargetResource>(operations[1]);
    }

    [Fact]
    public void LastControllerFrameRestoresStageColorAndOriginalActorFlag()
    {
        List<NativeBattleRenzokuken159Operation> operations = [];
        (NativeBattleEffectTaskPool pool, int slot) = Controller(100);
        pool = NativeBattleTaskFixture.EditPool(pool, data => data with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteWord(14, 0x800)) });
        NativeBattleRenzokuken159Inputs inputs = Inputs() with { ReadModel = _ => new(0, 0x0101), Execute = (frame, operation) =>
            {
                operations.Add(operation);
                return frame;
            } };
        NativeBattleRenzokuken159ChildStep step = NativeBattleRenzokuken159Controller.Advance600CC0(Frame(), pool, slot, inputs, Resources());
        Assert.Equal(2, step.Result);
        Assert.Equal(101, step.Pool.Slots[slot].ReadWord(12));
        Assert.Equal(new NativeBattleRenzokuken159Stage(0), operations[^3]);
        Assert.Equal(new NativeBattleRenzokuken159ActorColor(0x808080), operations[^2]);
        Assert.Equal(new NativeBattleRenzokuken159ActorFlags(0x0901), operations[^1]);
    }

    [Fact]
    public void ContactCreatesNativeRaysAndUsesAllEightyDebrisSlots()
    {
        NativeBattleRenzokuken159Frame frame = Frame();
        byte[] debris = frame.Memory.Renzokuken159!.Debris.ToArray();
        for (int index = 0; index < 40; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(debris.AsSpan(index * 20), 0);
        }
        frame = frame with { Memory = frame.Memory with { Renzokuken159 = frame.Memory.Renzokuken159 with { Debris = debris.ToImmutableArray() } } };
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x23D6C38, 0x23D6C48, 36, 100);
        (pool, int slot) = pool.Append(0x601FF0);
        NativeBattleRenzokuken159Resources resources = Resources() with { ContactLeadFrames = [1], ContactSpread = [180] };
        NativeBattleRenzokuken159ChildStep step = NativeBattleRenzokuken159Controller.Advance601FF0(frame, pool, slot, Inputs() with { AbSegments = [1, 99] }, resources);
        Assert.Equal(0x602390u, step.Pool.Slots[1].Callback);
        Assert.Equal(Advance(frame.Random, 121), step.Frame.Random);
        Assert.Equal(0, Word(step.Frame.Memory.Renzokuken159!.Debris, 51 * 20));
        Assert.Equal(-1, Word(step.Frame.Memory.Renzokuken159.Debris, 52 * 20));
        NativeCrtRandomState random = frame.Random.Draw(out ushort scale);
        Assert.Equal(scale % 768 + 1280, step.Pool.Slots[1].ReadWord(28));
        random = random.Draw(out _).Draw(out ushort extent);
        Assert.Equal(extent % 3072 + 3072, Word(step.Frame.Memory.Renzokuken159.ImpactStates, 60));
        Assert.Equal(1000 * (extent % 3072 + 3072) >> 12, Word(step.Frame.Memory.Renzokuken159.ImpactStates, 92));
    }

    [Fact]
    public void SnapshotRetainsBanksAndPausedControllerDoesNotConsumeRandom()
    {
        NativeBattleRenzokuken159Frame frame = Frame();
        (NativeBattleEffectTaskPool pool, int slot) = Controller(51);
        NativeBattleRenzokuken159ChildStep paused = NativeBattleRenzokuken159Controller.Advance600CC0(frame, pool, slot, Inputs() with { GraphicsFlags = 0x200 }, Resources());
        Assert.Same(frame, paused.Frame);
        Assert.Same(pool, paused.Pool);
        NativeBattleRenzokuken159ChildStep step = NativeBattleRenzokuken159Controller.Advance600CC0(frame, pool, slot, Inputs(), Resources());
        NativeBattleRenzokuken159State actual = step.Frame.Memory.Renzokuken159!;
        NativeBattleRenzokuken159State restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleRenzokuken159State>(NativeBattleTestSnapshots.Json(actual))!;
        restored.Validate();
        Assert.Equal(actual.Debris.ToArray(), restored.Debris.ToArray());
        Assert.Equal(actual.TrailHistory.ToArray(), restored.TrailHistory.ToArray());
        Assert.Equal(actual.Origin, restored.Origin);
    }

    private static NativeBattleRenzokuken159Frame Frame()
    {
        NativeBattleRenzokuken159State state = NativeBattleRenzokuken159State.FromColdProcess().Enter(0, 0, 3, false) with { Duration = 100, Origin = new(100, -200, 300), Direction = new(4096, 0, 0) };
        return new(NativeBattleEffectMemory.FromColdProcess() with { Renzokuken159 = state }, new(159, 158, new(159, 0x600BC0, 0x600BA0), null, null), NativeCrtRandomState.FromColdProcess());
    }

    private static NativeBattleRenzokuken159Inputs Inputs() => new(0, [], _ => new(0, 0), (frame, _) => frame);
    private static NativeBattleRenzokuken159Resources Resources() => new(new("animation", []), null!, [], [], [], [], 0, 0x808080);
    private static short Word(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan().Slice(offset));

    private static (NativeBattleEffectTaskPool Pool, int Slot) Controller(int counter)
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x23D6C38, 0x23D6C48, 36, 100);
        (pool, int slot) = pool.Append(0x600CC0);
        return (NativeBattleTaskFixture.EditPool(pool, data => data with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteWord(12, (ushort)counter)) }), slot);
    }

    private static NativeCrtRandomState Advance(NativeCrtRandomState random, int count)
    {
        for (int index = 0; index < count; index++)
        {
            random = random.Draw(out _);
        }
        return random;
    }
}

