using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;
using Xunit;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleFatedCircle162ControllerTests
{
    [Theory]
    [InlineData(1u, 0, 17)]
    [InlineData(0x200u, -1, 17)]
    [InlineData(0x200u, 0, 18)]
    public void ControllerUsesItsActualSignedIoPauseCondition(uint flags, int pending, int expected)
    {
        (NativeBattleFatedCircle162Frame frame, NativeBattleEffectTaskPool pool) = Frame(17);
        NativeBattleFatedCircle162ChildStep step = NativeBattleFatedCircle162Controller.Advance5F91D0(frame, pool, 0, Inputs(flags, pending));
        Assert.Equal(expected, step.Pool.Slots[0].ReadWord(12));
    }

    [Fact]
    public void Native108WaitDoesNotAdvanceOrAppendTheMainTextureTask()
    {
        (NativeBattleFatedCircle162Frame frame, NativeBattleEffectTaskPool pool) = Frame(108);
        NativeBattleFatedCircle162ChildStep wait = NativeBattleFatedCircle162Controller.Advance5F91D0(frame, pool, 0, Inputs(0, -1));
        Assert.Equal(108, wait.Pool.Slots[0].ReadWord(12));
        Assert.Null(wait.Frame.Effect.Pools[0].Slots[0].Next);
        List<NativeBattleFatedCircle162Operation> operations = [];
        NativeBattleFatedCircle162ChildStep ready = NativeBattleFatedCircle162Controller.Advance5F91D0(frame, pool, 0, Inputs(0, 0, operations));
        Assert.Equal(109, ready.Pool.Slots[0].ReadWord(12));
        Assert.Single(operations);
        Assert.IsType<NativeBattleFatedCircle162EnqueueTexture>(operations[0]);
        Assert.Equal(0x5FCFF0u, ready.Frame.Effect.Pools[0].Slots[1].Callback);
        Assert.Equal(1, ready.Frame.Effect.Pools[0].Slots[0].Next);
    }

    [Fact]
    public void NativeTextureCompletionKeepsMainOwnerAliveAfterTheLastVisualChild()
    {
        (NativeBattleFatedCircle162Frame frame, _) = Frame(0);
        NativeBattleEffectTaskPool main = frame.Effect.Pools[0];
        (main, int loader) = main.Append(0x5FCFF0);
        main = main with { Slots = main.Slots.SetItem(loader, main.Slots[loader].WriteWord(12, 2)) };
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(0, main) } };
        uint completion = 0;
        NativeBattleFatedCircle162Frame step = NativeBattleFatedCircle162Controller.Advance5FD090(frame, (current, pool, _) => new(current, pool, 2), (current, address) =>
        {
            completion = address;
            return current;
        });
        Assert.Equal(0x23CE63Eu, completion);
        Assert.Equal(0x23CE640u, step.Effect.ReturnedDescriptor);
        Assert.Equal(loader, step.Effect.Pools[0].Head);
        Assert.Equal(3, step.Effect.Pools[0].Slots[loader].ReadWord(12));
        main = step.Effect.Pools[0];
        main = main with { Slots = main.Slots.SetItem(loader, main.Slots[loader].WriteWord(14, 1)) };
        step = step with { Effect = step.Effect with { Pools = step.Effect.Pools.SetItem(0, main) } };
        step = NativeBattleFatedCircle162Controller.Advance5FD090(step, (_, _, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException());
        Assert.Equal(0u, step.Effect.ReturnedDescriptor);
        Assert.Null(step.Effect.Pools[0].Head);
    }

    [Fact]
    public void NativeMorphUsesSignedDifferenceTruncationAndPreservesPadding()
    {
        byte[] source = [0xFF, 0x7F, 0x00, 0x80, 7, 0, 0xAA, 0xBB];
        byte[] target = [0x00, 0x80, 0xFF, 0x7F, 0, 0, 0xCC, 0xDD];
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(0xDA2350, source).Write(0xDA3770, target).Write(0x23D0254, Enumerable.Repeat((byte)0x91, 16).ToArray());
        staging = NativeBattleFatedCircle162Controller.PrepareMorph5FD030(staging, 0xDA2350, 0xDA3770, 1, 24);
        Assert.Equal(-2730, BinaryPrimitives.ReadInt16LittleEndian(staging.Read(0x23D0258, 2)));
        Assert.Equal(2730, BinaryPrimitives.ReadInt16LittleEndian(staging.Read(0x23D025A, 2)));
        Assert.Equal(0, BinaryPrimitives.ReadInt16LittleEndian(staging.Read(0x23D025C, 2)));
        Assert.Equal(new byte[] { 0x91, 0x91 }, staging.Read(0x23D025E, 2));
        Assert.Equal(source, staging.Read(0xDA2350, 8));
    }

    [Fact]
    public void Native501330DereferencesTheOriginalStaticSoundDescriptor()
    {
        (NativeBattleFatedCircle162Frame frame, NativeBattleEffectTaskPool pool) = Frame(1);
        frame = frame with { Staging = frame.Staging.Write(0xDA8C68, new byte[] { 0x34, 0x12, 0x05, 0 }) };
        List<NativeBattleFatedCircle162Operation> operations = [];
        _ = NativeBattleFatedCircle162Controller.Advance5F91D0(frame, pool, 0, Inputs(0, 0, operations));
        Assert.Equal(0x51234u, Assert.IsType<NativeBattleFatedCircle162Sound>(operations[^1]).Address);
    }

    private static NativeBattleFatedCircle162Inputs Inputs(uint flags, int pending, List<NativeBattleFatedCircle162Operation>? operations = null) => new(flags, NativeBattleActionPacketsState.Fresh(), () => pending, _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), (frame, operation) =>
    {
        operations?.Add(operation);
        return frame;
    }, (_, _) => throw new InvalidOperationException());

    private static (NativeBattleFatedCircle162Frame Frame, NativeBattleEffectTaskPool Pool) Frame(ushort counter)
    {
        NativeBattleEffectTaskPool main = NativeBattleEffectTaskPool.Initialize(0x23CE640, 0x23CE620, 16, 2);
        (main, _) = main.Append(0x5FD090);
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(0x23CF430, 0x23CF440, 36, 100);
        (children, _) = children.Append(0x5F91D0);
        children = children with { Slots = children.Slots.SetItem(0, children.Slots[0].WriteWord(12, counter)) };
        NativeBattleEffectParameters parameters = new(0, 0, 8, 0, 0, 0);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { FatedCircle162 = new(true, parameters, null) };
        NativeBattleOriginalEffectState effect = new(162, 161, new(162, 0x5F90F0, 0x5F90D0), parameters, 0x23CE640) { Pools = [main, children] };
        return (new(memory, effect, NativeBattleFileStaging.Unknown(), NativeCrtRandomState.FromColdProcess()), children);
    }
}
