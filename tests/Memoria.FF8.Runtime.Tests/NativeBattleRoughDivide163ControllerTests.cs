using Memoria.FF8.Runtime;
using Xunit;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRoughDivide163ControllerTests
{
    [Fact]
    public void PausedControllerStillComposesItsCapturedMatrixButDoesNotAdvance()
    {
        (NativeBattleRoughDivide163Frame frame, NativeBattleEffectTaskPool pool) = Frame(51);
        List<NativeBattleRoughDivide163Operation> operations = [];
        NativeBattleRoughDivide163ChildStep step = NativeBattleRoughDivide163Controller.Advance5F5C80(frame, pool, 0, Inputs(0x200, operations));
        Assert.Equal(51, step.Pool.Slots[0].ReadWord(12));
        Assert.IsType<NativeBattleRoughDivide163ComposeActor>(Assert.Single(operations));
        Assert.Equal(frame.Random, step.Frame.Random);
    }

    [Fact]
    public void BoneInitializerWritesOnlyXyzAndRetainsNativeTaskPadding()
    {
        (NativeBattleRoughDivide163Frame frame, NativeBattleEffectTaskPool pool) = Frame(4);
        pool = pool with { Slots = pool.Slots.SetItem(1, pool.Slots[1].WriteWord(22, 0xCAFE)) };
        NativeBattleRoughDivide163ChildStep step = NativeBattleRoughDivide163Controller.Advance5F5C80(frame, pool, 0, Inputs(0, []));
        NativeBattleModulePoolTask child = step.Pool.Slots[1];
        Assert.Equal(0x5F65E0u, child.Callback);
        Assert.Equal(123, child.ReadWord(16));
        Assert.Equal(0, child.ReadWord(18));
        Assert.Equal(789, child.ReadWord(20));
        Assert.Equal(0xCAFE, child.ReadWord(22));
        Assert.Equal(10240, child.ReadWord(28));
        Assert.Equal(10240, child.ReadWord(32));
        Assert.Equal(853, child.ReadWord(34));
        Assert.Equal(1, step.Pool.Slots[0].Next);
    }

    [Theory]
    [InlineData(51, 87, 15)]
    [InlineData(52, 17, 5)]
    public void NativeAppendBurstsConsumeTheirExactRandomDrawOrder(ushort counter, int draws, int allocated)
    {
        (NativeBattleRoughDivide163Frame frame, NativeBattleEffectTaskPool pool) = Frame(counter);
        frame = frame with { Staging = frame.Staging.Write(0x23CE280, new byte[8]) };
        NativeCrtRandomState expected = frame.Random;
        for (int index = 0; index < draws; index++)
        {
            expected = expected.Draw(out _);
        }
        NativeBattleRoughDivide163ChildStep step = NativeBattleRoughDivide163Controller.Advance5F5C80(frame, pool, 0, Inputs(0, []));
        Assert.Equal(expected, step.Frame.Random);
        Assert.Equal(allocated, step.Pool.Slots.Count(task => (task.Flags & 1) != 0));
    }

    [Fact]
    public void ActualTextureOwnerSurvivesVisualChildrenAndCompletionPreservesHighByte()
    {
        (NativeBattleRoughDivide163Frame frame, _) = Frame(0);
        uint completion = 0;
        frame = NativeBattleRoughDivide163Controller.Advance5F9020(frame, (current, pool, _) => new(current, pool, 2), (_, _) => throw new InvalidOperationException());
        Assert.Equal(0x23CA1C8u, frame.Effect.ReturnedDescriptor);
        Assert.Equal(1, frame.Effect.Pools[0].Head);
        Assert.Equal(1, frame.Effect.Pools[0].Slots[1].ReadWord(12));
        frame = NativeBattleRoughDivide163Controller.Advance5F9020(frame, (_, _, _) => throw new InvalidOperationException(), (current, address) =>
        {
            completion = address;
            return current;
        });
        Assert.Equal(0x23CA1C6u, completion);
        NativeBattleEffectTaskPool main = frame.Effect.Pools[0];
        main = main with { Slots = main.Slots.SetItem(1, main.Slots[1].WriteWord(14, 0xAB00)) };
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(0, main) } };
        frame = frame with { Effect = NativeBattleRoughDivide163Controller.WriteCompletionByte(frame.Effect, completion, 1) };
        Assert.Equal(0xAB01, frame.Effect.Pools[0].Slots[1].ReadWord(14));
        frame = NativeBattleRoughDivide163Controller.Advance5F9020(frame, (_, _, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException());
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
    }

    [Fact]
    public void FinalControllerRestoresAllEightCapturedActorPositionBytes()
    {
        (NativeBattleRoughDivide163Frame frame, NativeBattleEffectTaskPool pool) = Frame(85);
        pool = pool with { Slots = pool.Slots.SetItem(0, pool.Slots[0].WriteWord(16, 10).WriteWord(18, 20).WriteWord(20, 30).WriteWord(22, 0xCAFE)) };
        List<NativeBattleRoughDivide163Operation> operations = [];
        NativeBattleRoughDivide163ChildStep step = NativeBattleRoughDivide163Controller.Advance5F5C80(frame, pool, 0, Inputs(0, operations));
        Assert.Equal(2, step.Result);
        NativeBattleRoughDivide163SetPosition moved = Assert.IsType<NativeBattleRoughDivide163SetPosition>(operations[^1]);
        Assert.Equal(new NativeBattleModuleAnchor(10, 20, 30, unchecked((short)0xCAFE)), moved.Position);
        Assert.Equal(0, Assert.IsType<NativeBattleRoughDivide163Stage>(operations[^2]).Intensity);
    }

    [Fact]
    public void NativeSoundUsesTheDescriptorValueBeforeItsTrailAndParticleAppends()
    {
        (NativeBattleRoughDivide163Frame frame, NativeBattleEffectTaskPool pool) = Frame(1);
        frame = frame with { Staging = frame.Staging.Write(0xDA00A8, new byte[] { 0xA8, 0x57, 0x05, 0 }) };
        List<NativeBattleRoughDivide163Operation> operations = [];
        NativeBattleRoughDivide163ChildStep step = NativeBattleRoughDivide163Controller.Advance5F5C80(frame, pool, 0, Inputs(0, operations));
        Assert.IsType<NativeBattleRoughDivide163ComposeActor>(operations[0]);
        Assert.Equal(0x557A8u, Assert.IsType<NativeBattleRoughDivide163Sound>(operations[1]).Descriptor);
        Assert.Equal(0x5F5FE0u, step.Pool.Slots[1].Callback);
        Assert.Equal(0x5F67A0u, step.Pool.Slots[2].Callback);
    }

    private static NativeBattleRoughDivide163Inputs Inputs(uint flags, List<NativeBattleRoughDivide163Operation> operations) => new(flags, _ => new(123, 456, 789, 0), (_, _, _) => new(123, 456, 789, 0), (frame, operation) =>
    {
        operations.Add(operation);
        return frame;
    });

    private static (NativeBattleRoughDivide163Frame Frame, NativeBattleEffectTaskPool Pool) Frame(ushort counter)
    {
        NativeBattleEffectTaskPool main = NativeBattleEffectTaskPool.Initialize(0x23CA1C8, 0x23CA1A8, 16, 2);
        (main, _) = main.Append(0x5F9020);
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(0x23CD348, 0x23CD358, 36, 100);
        (children, _) = children.Append(0x5F5C80);
        children = children with { Slots = children.Slots.SetItem(0, children.Slots[0].WriteWord(12, counter)) };
        NativeBattleEffectParameters parameters = new(0, 0, 8, 0, 0, 0);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { RoughDivide163 = new(true, parameters, 3, null) };
        NativeBattleOriginalEffectState effect = new(163, 162, new(163, 0x5F5B80, 0x5F5B60), parameters, 0x23CA1C8) { Pools = [main, children] };
        return (new(memory, effect, NativeBattleFileStaging.Unknown(), NativeCrtRandomState.FromColdProcess()), children);
    }
}
