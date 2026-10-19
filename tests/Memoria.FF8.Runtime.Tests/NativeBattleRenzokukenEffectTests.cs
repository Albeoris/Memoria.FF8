using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;
using Xunit;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRenzokukenEffectTests
{
    [Fact]
    public void EntryClearsOnlyNativeParticleMarkers()
    {
        NativeBattleRenzokuken160State state = NativeBattleRenzokuken160State.FromColdProcess();
        state = state with { Sparks = Enumerable.Repeat((byte)0x5A, 480).ToImmutableArray(), Debris = Enumerable.Repeat((byte)0x6B, 800).ToImmutableArray(), Spirals = Enumerable.Repeat((byte)0x7C, 960).ToImmutableArray(), TrailHistory = Enumerable.Repeat((byte)0x8D, 140).ToImmutableArray() };
        NativeBattleRenzokuken160State entered = state.Enter(1, 2, 3, true);
        Assert.Equal(4, entered.Duration);
        Assert.Equal(3, entered.FirstTarget);
        Assert.Equal(state.TrailHistory, entered.TrailHistory);
        Check(entered.Sparks, 12, 0x5A);
        Check(entered.Debris, 20, 0x6B);
        Check(entered.Spirals, 16, 0x7C);

        static void Check(ImmutableArray<byte> bytes, int stride, byte untouched)
        {
            for (int offset = 0; offset < bytes.Length; offset += stride)
            {
                Assert.Equal(-1, BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan().Slice(offset)));
                Assert.All(bytes.AsSpan().Slice(offset + 2, stride - 2).ToArray(), value => Assert.Equal(untouched, value));
            }
        }
    }

    [Fact]
    public void SpiralEmitterSkipsBoneRngForEmptyOriginalBoneBank()
    {
        NativeBattleRenzokuken160Frame frame = Frame();
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x23D4778, 0x23D4788, 36, 100);
        (pool, int slot) = pool.Append(0x5FFFE0);
        int calls = 0;
        NativeBattleRenzokuken160Inputs inputs = new(0, [], _ => new(0, 0), (current, _) => current)
        {
            ReadBoneCount = _ => 0,
            ReadBone = (_, bone, distance) =>
            {
                Assert.Equal(0, bone);
                Assert.Equal(0, distance);
                calls++;
                return new(100, 200, 300);
            }
        };
        NativeBattleRenzokuken160ChildStep step = NativeBattleRenzokuken160Controller.Advance5FFFE0(frame, pool, slot, inputs);
        NativeCrtRandomState expected = frame.Random;
        for (int index = 0; index < 6; index++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, step.Frame.Random);
        Assert.Equal(3, calls);
        Assert.Equal(1, step.Pool.Slots[slot].ReadWord(12));
        Assert.Equal(1, step.Pool.Slots[slot].ReadWord(14));
        ImmutableArray<byte> particles = step.Frame.Memory.Renzokuken160!.Spirals;
        Assert.Equal(0, BinaryPrimitives.ReadInt16LittleEndian(particles.AsSpan().Slice(32)));
        Assert.Equal(-1, BinaryPrimitives.ReadInt16LittleEndian(particles.AsSpan().Slice(48)));
    }

    [Fact]
    public void PausedSpiralEmitterPreservesTaskAndRng()
    {
        NativeBattleRenzokuken160Frame frame = Frame();
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x23D4778, 0x23D4788, 36, 100);
        (pool, int slot) = pool.Append(0x5FFFE0);
        NativeBattleRenzokuken160Inputs inputs = new(0x200, [], _ => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException());
        NativeBattleRenzokuken160ChildStep step = NativeBattleRenzokuken160Controller.Advance5FFFE0(frame, pool, slot, inputs);
        Assert.Same(frame, step.Frame);
        Assert.Same(pool, step.Pool);
        Assert.Equal(0, step.Result);
    }

    private static NativeBattleRenzokuken160Frame Frame()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Renzokuken160 = NativeBattleRenzokuken160State.FromColdProcess().Enter(0, 0, 3, false) with { Duration = 100 } };
        NativeBattleOriginalEffectState effect = new(160, 159, new(160, 0x5FF080, 0x5FF060), null, null);
        return new(memory, effect, NativeCrtRandomState.FromColdProcess());
    }

    [Fact]
    public void ControllerPreservesChildrenAppendedToItsOwnTail()
    {
        NativeBattleRenzokuken160Frame frame = Frame();
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x23D4778, 0x23D4788, 36, 100);
        (pool, int slot) = pool.Append(0x5FF180);
        pool = pool with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteWord(12, 2)) };
        NativeBattleRenzokuken160Resources resources = new(new("animation", []), null!, [], [], [], [], 0, 0);
        NativeBattleRenzokuken160Inputs inputs = new(0, [10, 20, 30, 40, 10], _ => new(0, 0), (current, _) => current);
        NativeBattleRenzokuken160ChildStep step = NativeBattleRenzokuken160Controller.Advance5FF180(frame, pool, slot, inputs, resources);
        Assert.Equal(110, step.Frame.Memory.Renzokuken160!.Duration);
        Assert.Equal(1, step.Pool.Slots[0].Next);
        Assert.Equal(2, step.Pool.Slots[1].Next);
        Assert.Equal(3, step.Pool.Slots[2].Next);
        Assert.Equal(0x600110u, step.Pool.Slots[3].Callback);
    }

    [Fact]
    public void ReturnedTaskClearsWhenTheLastNativeChildReleases()
    {
        NativeBattleRenzokuken160Frame frame = Frame();
        NativeBattleEffectTaskPool main = NativeBattleEffectTaskPool.Initialize(0x23D4230, 0x23D4220, 16, 1);
        (main, _) = main.Append(0x600930);
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(0x23D4778, 0x23D4788, 36, 100);
        (children, _) = children.Append(0x5FF180);
        frame = frame with { Effect = frame.Effect with { Pools = [main, children], ReturnedDescriptor = main.DescriptorAddress } };
        int draws = 0;
        NativeBattleRenzokuken160Frame result = NativeBattleRenzokuken160Controller.Advance600930(frame, (current, pool, _) => new(current, pool, 2), current =>
        {
            draws++;
            return current;
        });
        Assert.Equal(1, draws);
        Assert.Null(result.Effect.Pools[0].Head);
        Assert.Null(result.Effect.Pools[1].Head);
        Assert.Equal(1, result.Effect.Pools[0].Slots[0].ReadWord(12));
        Assert.Equal(0u, result.Effect.ReturnedDescriptor);
    }

    [Fact]
    public void PollingAnAlreadyEmptyNativeRootPublishesNull()
    {
        NativeBattleRenzokuken160Frame frame = Frame();
        NativeBattleEffectTaskPool main = NativeBattleEffectTaskPool.Initialize(0x23D4230, 0x23D4220, 16, 1);
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(0x23D4778, 0x23D4788, 36, 100);
        frame = frame with { Effect = frame.Effect with { Pools = [main, children], ReturnedDescriptor = main.DescriptorAddress } };
        NativeBattleRenzokuken160Frame result = NativeBattleRenzokuken160Controller.Advance600930(frame, (_, _, _) => throw new InvalidOperationException(), _ => throw new InvalidOperationException());
        Assert.Equal(0u, result.Effect.ReturnedDescriptor);
    }

    [Fact]
    public void SharedEmitterPreservesItsAppendedStreakLinksAndPayloadTails()
    {
        NativeBattleModuleState modules = NativeBattleModuleState.Clear();
        (modules, int slot) = modules.AppendCallback(0x5FF4E0);
        byte[] payload = modules.Slots[slot].Payload.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(payload, 4);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(14), 0);
        modules = modules with { Slots = modules.Slots.SetItem(slot, modules.Slots[slot] with { Payload = payload.ToImmutableArray() }) };
        NativeBattleModuleInputs inputs = new(0)
        {
            CrtRandom = NativeCrtRandomState.FromColdProcess(),
            ReadModelAnchor = _ => new(0, 0, 0, 0),
            ReadModelBoneCount = _ => 0,
            ReadModelBone = (_, _, _) => new(0, -700, 0)
        };
        NativeBattleModuleCallback step = NativeBattleRenzokuken160Controller.AdvanceShared5FF4E0(modules, slot, inputs);
        Assert.Equal(1, step.State.Slots[slot].Next);
        Assert.Equal(6, step.State.Tail);
        Assert.All(step.State.Slots.Skip(1).Take(6), child => Assert.Equal(0x5FF770u, child.Callback));
        Assert.Equal(5, BinaryPrimitives.ReadUInt16LittleEndian(step.State.Slots[slot].Payload.AsSpan()));
    }
}
