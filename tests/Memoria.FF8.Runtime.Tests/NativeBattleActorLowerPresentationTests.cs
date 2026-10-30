namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleActorLowerPresentationTests
{
    [Theory]
    [InlineData(181)]
    [InlineData(187)]
    [InlineData(200)]
    public void Physical_row_birth_and_resource_changes_do_not_bridge_task_ownership(ushort effect)
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x2400000, 0x2400020, 36, 2);
        (pool, int slot) = pool.Append(0x123456);
        NativeBattleModulePoolTask task = pool.Slots[slot];
        NativeBattlePilotVisualIdentity owner = NativeBattlePilotVisualLifetimes.Identity(effect, task);
        NativeBattleShotVisualLifetimes.Allocated(effect, task, 24);
        NativeBattlePilotVisualIdentity first = NativeBattleShotVisualLifetimes.Identity(effect, task, owner, 24);
        Assert.Equal(first, NativeBattleShotVisualLifetimes.Identity(effect, task, owner, 24));
        NativeBattleShotVisualLifetimes.Allocated(effect, task, 24);
        NativeBattlePilotVisualIdentity replacement = NativeBattleShotVisualLifetimes.Identity(effect, task, owner, 24);
        Assert.NotEqual(first, replacement);
        Assert.Equal(first.Bank, replacement.Bank);
        Assert.Equal(first.Generation, replacement.Generation);
        Assert.NotEqual(NativeBattleShotVisualLifetimes.Resource(first, 100), NativeBattleShotVisualLifetimes.Resource(first, 101));
        Assert.NotEqual(replacement, NativeBattleShotVisualLifetimes.Identity(effect, task, owner, 48));
    }

    [Fact]
    public void NoMercy_scrolling_mesh_capture_keeps_callback_native_state_and_CRT()
    {
        (NativeBattleNoMercy200ChildStep plain, NativeBattleNoMercy200MeshDraw operation) = Step(false);
        string detachedNative = NativeBattleTestSnapshots.Json(plain);
        string detachedOperation = NativeBattleTestSnapshots.Json(operation);
        (NativeBattleNoMercy200ChildStep captured, NativeBattleNoMercy200MeshDraw capturedOperation) = Step(true);
        Assert.Equal(detachedNative, NativeBattleTestSnapshots.Json(captured));
        Assert.Equal(detachedOperation, NativeBattleTestSnapshots.Json(capturedOperation));
        Assert.Null(operation.PilotIdentity);
        NativeBattlePilotVisualIdentity identity = Assert.IsType<NativeBattlePilotVisualIdentity>(capturedOperation.PilotIdentity);
        Assert.Equal((ushort)200, identity.Effect);
        Assert.Equal(0xE316B4L, identity.ResourceGeneration);
        Assert.True(identity.Generation > 0);
        Assert.NotNull(capturedOperation.Scrolling);

        static (NativeBattleNoMercy200ChildStep, NativeBattleNoMercy200MeshDraw) Step(bool capture)
        {
            NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(NativeBattleNoMercy200Controller.Children, 0x24F99A0, 36, 2);
            (pool, int slot) = pool.Append(0x63AD20);
            pool = pool.WriteSlot(slot, pool.Slots[slot].WriteWord(12, 3).WriteWord(28, 4096).WriteWord(32, 4096));
            NativeBattleOriginalEffectState effect = new(200, 199, new(200, 0x6391D0, 0x575530), new(2, 0, 8, 0, 0, 0), NativeBattleNoMercy200Controller.Root) { Pools = [pool] };
            NativeBattleNoMercy200Frame frame = new(NativeBattleEffectMemory.FromColdProcess(), effect, NativeBattleFileStaging.Unknown(), NativeCrtRandomState.FromColdProcess());
            NativeBattleNoMercy200MeshDraw? operation = null;
            NativeBattleNoMercy200Inputs inputs = new(0, NativeBattleCuragaMatrices.Identity8DD770(), () => throw new InvalidOperationException(), (current, draw) =>
            {
                operation = Assert.IsType<NativeBattleNoMercy200MeshDraw>(draw);
                return current;
            }) { CapturePresentation = capture };
            NativeBattleNoMercy200ChildStep result = NativeBattleNoMercy200Controller.AdvanceSimpleMesh(frame, pool, slot, inputs);
            return (result, operation!);
        }
    }
}
