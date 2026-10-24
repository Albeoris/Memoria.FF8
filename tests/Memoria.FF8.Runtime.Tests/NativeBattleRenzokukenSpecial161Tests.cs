using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;
using Xunit;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRenzokukenSpecial161Tests
{
    [Fact]
    public void TargetEmissionUsesBone240AndPreservesOtherModuleOwners()
    {
        NativeBattleRenzokuken160Frame frame = Frame();
        (NativeBattleEffectTaskPool pool, int slot) = Pool(28);
        List<short> fractions = [];
        NativeBattleRenzokuken160Inputs inputs = new(0, [], _ => throw new InvalidOperationException(), (current, _) => current)
        {
            ReadBone = (actor, bone, fraction) =>
            {
                Assert.Equal(3, actor);
                Assert.Equal(240, bone);
                fractions.Add(fraction);
                return new(100, -200, (short)(fraction == 0 ? 0 : 1000));
            }
        };
        NativeBattleRenzokuken160ChildStep step = NativeBattleRenzokukenSpecialController.Advance5FD230(frame, pool, slot, inputs, Resources(), _ => throw new InvalidOperationException());
        Assert.Equal(new short[] { 0, 4096 }, fractions);
        Assert.Equal(Advance(frame.Random, 18), step.Frame.Random);
        ImmutableArray<byte> particles = NativeBattleRenzokukenSpecialController.ReadState(step.Frame).TargetParticles;
        for (int index = 0; index < 3; index++)
        {
            Assert.Equal(0, Read(particles, index * 20));
            Assert.Equal(1000, Read(particles, index * 20 + 8));
            Assert.InRange(Read(particles, index * 20 + 18), 10, 27);
        }
        Assert.Equal(-1, Read(particles, 60));
        Assert.Same(frame.Memory.Renzokuken160, step.Frame.Memory.Renzokuken160);
    }

    [Fact]
    public void Frame46Emits28BurstSlotsAndUpdatesTheCameraWord()
    {
        NativeBattleRenzokuken160Frame frame = Frame();
        (NativeBattleEffectTaskPool pool, int slot) = Pool(46);
        List<NativeBattleRenzokuken160Operation> operations = [];
        NativeBattleRenzokuken160Inputs inputs = new(0, [], _ => throw new InvalidOperationException(), (current, operation) =>
        {
            operations.Add(operation);
            return current;
        }) { ReadBone = (_, _, _) => new(0, -100, 0) };
        NativeBattleRenzokuken160ChildStep step = NativeBattleRenzokukenSpecialController.Advance5FD230(frame, pool, slot, inputs, Resources(), actor => new(10, 20, 30, 40));
        Assert.Equal(Advance(frame.Random, 102), step.Frame.Random);
        Assert.Contains(new NativeBattleRenzokukenSpecialCameraRotation(32), operations);
        ImmutableArray<byte> burst = NativeBattleRenzokukenSpecialController.ReadState(step.Frame).BurstParticles;
        Assert.Equal(0, Read(burst, 27 * 20));
        Assert.Equal(-1, Read(burst, 28 * 20));
        Assert.Equal(10, Read(burst, 4));
        Assert.Equal(40, Read(burst, 6));
        Assert.Equal(30, Read(burst, 8));
        Assert.Equal(0x5FEA70u, step.Pool.Slots[1].Callback);
    }

    [Fact]
    public void PausePreservesTargetBanksTasksAndRandom()
    {
        NativeBattleRenzokuken160Frame frame = Frame();
        (NativeBattleEffectTaskPool pool, int slot) = Pool(46);
        NativeBattleRenzokuken160Inputs inputs = new(0x201, [], _ => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException());
        NativeBattleRenzokuken160ChildStep step = NativeBattleRenzokukenSpecialController.Advance5FD230(frame, pool, slot, inputs, Resources(), _ => throw new InvalidOperationException());
        Assert.Same(frame, step.Frame);
        Assert.Same(pool, step.Pool);
    }

    private static NativeBattleRenzokuken160Resources Resources() => new(new("animation", []), null!, [], [], [], [], 0, 0);

    private static NativeBattleRenzokuken160Frame Frame()
    {
        NativeBattleRenzokuken160State core = NativeBattleRenzokuken160State.FromColdProcess().Enter(0, 0, 3, false) with { Duration = 100 };
        NativeBattleRenzokukenSpecialState state = new(core, Resources()) { TargetParticles = Markers(800), BurstParticles = Markers(600) };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { RenzokukenSpecials = ImmutableDictionary<ushort, NativeBattleRenzokukenSpecialState>.Empty.Add(161, state) };
        return new(memory, new(161, 160, new(161, 0x5FD100, 0x5FD0E0), null, null), NativeCrtRandomState.FromColdProcess());
    }

    private static (NativeBattleEffectTaskPool Pool, int Slot) Pool(ushort counter)
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x23D25D8, 0x23D25E8, 36, 100);
        (pool, int slot) = pool.Append(0x5FD230);
        return (pool with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteWord(12, counter)) }, slot);
    }

    private static ImmutableArray<byte> Markers(int length)
    {
        byte[] bytes = new byte[length];
        for (int offset = 0; offset < length; offset += 20)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), -1);
        }
        return bytes.ToImmutableArray();
    }

    private static short Read(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan().Slice(offset));

    private static NativeCrtRandomState Advance(NativeCrtRandomState state, int count)
    {
        for (int index = 0; index < count; index++)
        {
            state = state.Draw(out _);
        }
        return state;
    }
}
