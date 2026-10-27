namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDuel212SceneTests
{
    [Fact]
    public void NativeSceneIncrementsBeforeItsTerminalComparisonAndDoesNotReadAbsentOwners()
    {
        var fixture = Fixture(181);
        NativeBattleDuel212Inputs drawing = new(Identity(), (_, _) => throw new InvalidOperationException());
        NativeBattleDuel212Step step = NativeBattleDuel212Controller.Advance6A1660(fixture.Frame, fixture.Pool, 0, new(drawing, (_, _, _) => throw new InvalidOperationException(), _ => throw new InvalidOperationException()));
        Assert.Equal(2, step.Result);
        Assert.Equal((ushort)182, step.Pool.Slots[0].ReadWord(12));
        Assert.Equal(fixture.Frame.Random, step.Frame.Random);
    }

    [Fact]
    public void ContactPhaseRestoresModelsBeforeDisablingStageAndSkipsCamera()
    {
        var fixture = Fixture(173);
        List<NativeBattleDuel212Operation> operations = [];
        NativeBattleDuel212Inputs drawing = new(Identity(), (frame, operation) =>
        {
            operations.Add(operation);
            return frame;
        });
        NativeBattleDuel212Step step = NativeBattleDuel212Controller.Advance6A1660(fixture.Frame, fixture.Pool, 0, new(drawing, (_, _, _) => throw new InvalidOperationException(), _ => throw new InvalidOperationException()));
        Assert.Collection(operations, operation => Assert.IsType<NativeBattleDuel212Contacts>(operation), operation => Assert.IsType<NativeBattleDuel212RestoreModels>(operation), operation => Assert.Equal(new NativeBattleDuel212Stage(0), operation));
        Assert.Equal(0, step.Result);
        Assert.Equal((ushort)174, step.Pool.Slots[0].ReadWord(12));
    }

    [Fact]
    public void CameraUsesNativeIntervalEndpointsAndPreservesGaps()
    {
        NativeBattleCameraState camera = NativeBattleCameraState.FromColdProcess() with { CurrentPose = new(new(1, 2, 3), new(4, 5, 6), 77, 9), FocalLength = 77, Roll = 9 };
        NativeBattleCameraState first = NativeBattleDuel212Controller.AdvanceCamera6A1660(camera, 1, 100, 200, 0, 0);
        Assert.Equal(new NativeBattleCameraPoint(-25, -771, -532), first.CurrentPose!.Eye);
        Assert.Equal((short)288, first.FocalLength);
        Assert.Equal((short)0, first.Roll);
        Assert.Same(first, NativeBattleDuel212Controller.AdvanceCamera6A1660(first, 17, 100, 200, 0, 0));
        NativeBattleCameraState start = NativeBattleDuel212Controller.AdvanceCamera6A1660(first, 18, 100, 200, 0, 0);
        Assert.Equal(new NativeBattleCameraPoint(5, -738, -430), start.CurrentPose!.Eye);
        Assert.Same(start, NativeBattleDuel212Controller.AdvanceCamera6A1660(start, 42, 100, 200, 0, 0));
        NativeBattleCameraState final = NativeBattleDuel212Controller.AdvanceCamera6A1660(start, 134, 100, 200, 0, 0);
        Assert.Equal(new NativeBattleCameraPoint(-575, -2226, -4454), final.CurrentPose!.Eye);
        Assert.Equal(new NativeBattleCameraPoint(985, -167, 1259), final.CurrentPose.Target);
    }

    private static NativeBattleActionMatrix Identity() => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);

    private static (NativeBattleDuel212Frame Frame, NativeBattleEffectTaskPool Pool) Fixture(ushort counter)
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(NativeBattleDuel212Controller.Targets, 0x20DFAB8, 3280, 1);
        (pool, int slot) = pool.Append(0x6A1660);
        pool = NativeBattleTaskFixture.EditPool(pool, data => data with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteWord(12, counter)) });
        NativeBattleOriginalEffectState effect = new(212, 211, new(212, 0x6A0D20, 0x6A0D30), new(0, 0, 8, 0, 0, 0), NativeBattleDuel212Controller.Root) { Pools = [pool] };
        NativeBattleDuel212Frame frame = new(NativeBattleEffectMemory.FromColdProcess(), effect, NativeBattleFileStaging.Unknown(), NativeCrtRandomState.FromColdProcess());
        return (frame, pool);
    }
}
