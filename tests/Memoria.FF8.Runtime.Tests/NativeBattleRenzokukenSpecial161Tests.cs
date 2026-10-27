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
        _ = Advance(frame.Random, 18).Draw(out _).Draw(out ushort angle).Draw(out ushort speed);
        Assert.Equal(unchecked((short)(((speed % 200 + 220) * NativeBattleCameraMath.Cosine(angle % 4096)) >> 12)), Read(burst, 12));
        Assert.Equal(unchecked((short)(((speed % 200 + 220) * NativeBattleCameraMath.Sine(angle % 4096)) >> 12)), Read(burst, 16));
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

    [Fact]
    public void Special341CameraWritesNegative200AtPhaseZeroEvenWhilePaused()
    {
        NativeBattleRenzokuken160Frame frame = Frame();
        (NativeBattleEffectTaskPool pool, int slot) = Pool(0);
        List<NativeBattleRenzokuken160Operation> operations = [];
        NativeBattleRenzokuken160Inputs inputs = new(0x201, [], _ => throw new InvalidOperationException(), (current, operation) =>
        {
            operations.Add(operation);
            return current;
        });
        NativeBattleRenzokuken160ChildStep step = NativeBattleRenzokukenSpecialController.Advance57B880(frame, pool, slot, inputs);
        Assert.Equal(new NativeBattleRenzokukenSpecialCameraWord(-200), Assert.Single(operations));
        Assert.Same(pool, step.Pool);
        Assert.Equal(0, step.Result);
    }

    [Fact]
    public void Special342ContactStartsItsOwnTaggedParticleTaskAfterTheImpact()
    {
        NativeBattleRenzokuken160Resources resources = Resources() with { ContactLeadFrames = new int[8].ToImmutableArray(), ContactOffsets = Enumerable.Repeat(new NativeBattleVector(0, 0, 0), 8).ToImmutableArray() };
        NativeBattleRenzokukenSpecialState state = NativeBattleRenzokukenSpecialController.ReadState(Frame()) with { Resources = resources, ExtraStorage = new byte[3200].ToImmutableArray() };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { RenzokukenSpecials = ImmutableDictionary<ushort, NativeBattleRenzokukenSpecialState>.Empty.Add(342, state) };
        NativeBattleRenzokuken160Frame frame = new(memory, new(342, 341, new(342, 0x5776D0, 0x5776B0), null, null), NativeCrtRandomState.FromColdProcess());
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x21E4CF8, 0x21E4D08, 36, 100);
        (pool, int slot) = pool.Append(0x578A00);
        pool = NativeBattleTaskFixture.EditPool(pool, data => data with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteWord(12, 10)) });
        NativeBattleActionMatrix matrix = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        NativeBattleRenzokuken160Inputs inputs = new(0, [5, 5, 5, 5, 5, 5, 5, 5, 5], _ => new(0, 0) { GeometryWorldMatrix = matrix }, (current, _) => current);
        NativeBattleRenzokuken160ChildStep step = NativeBattleRenzokukenSpecialController.Advance578A00(frame, pool, slot, inputs, resources);
        Assert.Equal(0x578DF0u, step.Pool.Slots[1].Callback);
        Assert.Equal(0x5790D0u, step.Pool.Slots[2].Callback);
        Assert.Equal(1, step.Pool.Slots[2].ReadWord(14));
        Assert.Equal(2, NativeBattleRenzokukenSpecialController.ReadState(step.Frame).ContactPadding.Impacts);
        Assert.Equal(Advance(frame.Random, 121), step.Frame.Random);
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
        return (NativeBattleTaskFixture.EditPool(pool, data => data with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteWord(12, counter)) }), slot);
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
