using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleNoMercy200TargetsTests
{
    [Fact]
    public void LockedSingleTargetWaitsWithoutAdvancingItsPhaseButRetainsHeaderClock()
    {
        var fixture = Fixture(0, 0);
        NativeBattleNoMercy200ChildStep step = NativeBattleNoMercy200Controller.AdvanceTarget(fixture.Frame, fixture.Pool, 0, 0, Inputs());
        Assert.Equal((ushort)0, step.Pool.Slots[0].ReadWord(12));
        Assert.Equal((ushort)1, step.Pool.Slots[0].ReadWord(34));
        Assert.Equal(fixture.Frame.Random, step.Frame.Random);
    }

    [Fact]
    public void SingleTargetBirthsKeepFourOriginalDelayRangesAndCrtDrawOrderAcrossRestore()
    {
        var fixture = Fixture(1, 0);
        NativeBattleNoMercy200Frame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleNoMercy200Frame>(NativeBattleTestSnapshots.Json(fixture.Frame))!;
        NativeBattleNoMercy200ChildStep first = NativeBattleNoMercy200Controller.AdvanceTarget(fixture.Frame, fixture.Pool, 0, 0, Inputs());
        NativeBattleNoMercy200ChildStep second = NativeBattleNoMercy200Controller.AdvanceTarget(restored, restored.Effect.Pools[0], 0, 0, Inputs());
        Assert.Equal(NativeBattleTestSnapshots.Json(first), NativeBattleTestSnapshots.Json(second));
        NativeBattleModulePoolTask[] children = first.Pool.Slots.Where(task => task.Callback == 0x639A60 && (task.Flags & 1) != 0).ToArray();
        Assert.Equal(4, children.Length);
        first.Pool.Validate();
        Assert.Equal(1, first.Pool.Slots[0].Next);
        for (int index = 0; index < children.Length; index++)
        {
            Assert.InRange(children[index].ReadWord(14), 2 * index, 2 * index + 5);
            Assert.InRange(children[index].ReadWord(28), 3328, 4863);
            Assert.Equal((ushort)0, children[index].ReadWord(22));
        }
        NativeCrtRandomState random = fixture.Frame.Random;
        for (int index = 0; index < 20; index++)
        {
            random = random.Draw(out _);
        }
        Assert.Equal(random, first.Frame.Random);
    }

    [Fact]
    public void PausedTargetDoesNotReadModelOrChangeTaskAndTerminalPhaseReleasesItsLock()
    {
        var fixture = Fixture(21, 1);
        NativeBattleNoMercy200TargetInputs inputs = Inputs() with { ReadAnchor = _ => throw new InvalidOperationException() };
        NativeBattleNoMercy200ChildStep paused = NativeBattleNoMercy200Controller.AdvanceTarget(fixture.Frame, fixture.Pool, 0, 1, inputs);
        Assert.Equal(fixture.Frame, paused.Frame);
        Assert.Equal(fixture.Pool, paused.Pool);
        NativeBattleNoMercy200ChildStep completed = NativeBattleNoMercy200Controller.AdvanceTarget(fixture.Frame, fixture.Pool, 0, 0, inputs);
        Assert.Equal(2, completed.Result);
        Assert.Equal(1, BinaryPrimitives.ReadInt32LittleEndian(completed.Frame.Staging.Read(0xE33D7C, 4)));
    }

    private static NativeBattleNoMercy200TargetInputs Inputs() => new(NativeBattleActionPacketsState.FromColdLoaderStorage(new byte[640], new byte[1176]), _ => new(100, -3000, 200, 0), _ => 4096, _ => 0);

    private static (NativeBattleNoMercy200Frame Frame, NativeBattleEffectTaskPool Pool) Fixture(ushort phase, byte locked)
    {
        NativeBattleEffectParameters parameters = new(2, 0, 8, 0, 0, 0);
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(NativeBattleNoMercy200Controller.Children, 0x24F99A0, 36, 250);
        (pool, _) = pool.Append(0x639FD0);
        pool = NativeBattleTaskFixture.EditPool(pool, data => data with { Slots = pool.Slots.SetItem(0, pool.Slots[0].WriteWord(12, phase).WriteWord(14, 0).WriteWord(32, 3).WriteWord(34, 0)) });
        NativeBattleOriginalEffectState effect = new(200, 199, new(200, 0x6391D0, 0x575530), parameters, NativeBattleNoMercy200Controller.Root) { Pools = [pool] };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { NoMercy200 = new(true) { Parameters = parameters } };
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(0xE33D7C, new byte[] { locked, 0, 0, 0 });
        return (new(memory, effect, staging, NativeCrtRandomState.FromColdProcess()), pool);
    }
}
