using System.Reflection;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class AttachedEncounterMagicTests
{
    [Fact]
    public void Installed_Edea_second_summon_file_failure_restores_both_owners()
    {
        string directory = TestResources.GameDirectory;
        using GameAssets game = new(directory);
        using BattleAssets assets = new(directory);
        NativeBattleGraphicsResource resource = assets.ReadEnemyNativeGraphicsResource(88);
        GameSession session = new(ExpandedBattleEffectMatrixTests.PrepareField(directory, game.ReadColdProcessNativeBattleMemory()));
        session.SetExamTimer(session.ExamTimer!.SetRemaining(600));
        using GameWorld world = new(session, directory, sharedAssets: game, applyEncounterMods: (definition, _) => BindSummonPolicy(definition, resource));
        ExpandedBattleInstalledEntry.Prepare(world, session, PrepareEdeaParty, 136, allies: 3, additionalEnemies: 0);
        world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: BattleAtbMode.TurnBased, NativeSeed: 4));
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        BattlePresentationProcess process = Assert.IsType<BattlePresentationProcess>(battle.Presentation);
        const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        BattleCombatant caster = battle.Combatants.Single(actor => actor.Id == battle.ReadDefinition().EncounterMod!.OriginalActorId);
        BattleCombatant readyParty = battle.Combatants.First(actor => actor.IsPlayer);
        foreach (BattleCombatant actor in battle.Combatants)
        {
            actor.Atb = actor == readyParty ? BattleSession.AtbMaximum : 0;
        }
        TickUntil(world, () => battle.NativeEntryReady && battle.QueuedActions.Count == 0 && battle.ActiveAction is null && battle.CanChoosePlayerCommand(readyParty.Id));
        string[] originalActors = battle.Combatants.Select(actor => actor.Id).ToArray();
        BattleModelOwner[] originalModels = process.PlannedModelRoster.Owners.ToArray();
        NativeBattleModelAliases originalAliases = process.Graphics.NativeBindings.CaptureAliases(process.Graphics.ActorModels);
        NativeBattleModelCpuResources originalCpu = process.ModelCpuResources;
        NativeBattleModelTextureCache originalCache = process.ModelTextureCache;
        string[] originalTextureOwners = process.Memory.Textures!.PresentationTextures!.Keys.Order().ToArray();
        NativeBattleFileIoState originalFileIo = process.NativeFileIo!;
        int? originalIoResult = process.NativeEffectIoResult;
        (byte[] Data, bool[] Known) originalStaging = process.NativeFileStaging!.ReadSnapshot(NativeBattleFileStaging.Address, 64);
        NativeBattleAllocationControl? originalAllocationControl = battle.NativeScriptState.AllocationControl;
        caster.Atb = BattleSession.AtbMaximum;

        TickUntil(world, () => battle.EncounterAllocation is { Phase: BattleEncounterAllocationPhase.Loading, LoadingIndex: 1 } batch &&
            process.NativeModelLoader?.Task is { Phase: 1, Owner: BattleEnemyActorModel enemy, File: not null } && enemy.Actor.ActorId == batch.ActorIds[1]);
        BattleEncounterAllocationState loading = battle.EncounterAllocation!;
        BattleModelOwner first = battle.ReadPresentationModelOwner(loading.ActorIds[0]);
        BattleModelOwner second = battle.ReadPresentationModelOwner(loading.ActorIds[1]);
        NativeBattleGraphicsModel loaded = process.Graphics.ActorModels.Get(first);
        Assert.Equal<byte?>(2, loaded.EffectCounter97);
        Assert.NotNull(loaded.ActiveResourceRow100);
        Assert.True(process.Graphics.ActorModels.Contains(second));
        Assert.All(loading.ActorIds, id =>
        {
            BattleCombatant actor = battle.Combatants.Single(actor => actor.Id == id);
            Assert.False(actor.IsActive);
            Assert.True(actor.Hidden && actor.Untargetable && actor.Suspended);
        });
        NativeBattleFileIoState io = process.NativeFileIo!;
        int slot = Assert.Single(Enumerable.Range(0, io.Rows.Length), index => io.Rows[index] is { SchedulerCallback: 0x482870, CompletionCallback: 0x508470 });
        Assert.True(io.Rows[slot].FileResult > 0);
        Assert.Equal(-1, process.NativeEffectIoResult);
        NativeBattleFileIoState failedRead = io with { Rows = io.Rows.SetItem(slot, io.Rows[slot] with { FileResult = -2, Countdown = 0 }) };
        typeof(BattlePresentationProcess).GetProperty(nameof(BattlePresentationProcess.NativeFileIo))!.SetValue(process, failedRead);
        NativeBattleRandomState randomBefore = Assert.IsType<NativeBattleRandomState>(typeof(BattleSession).GetField("random", PrivateInstance)!.GetValue(battle));
        NativeGlobalRandomChange? globalBefore = process.GlobalRandomChange;
        NativeCrtRandomState? crtBefore = process.CurrentCrtRandom;
        var uiRandomBefore = (NativeBattleUiRandomState?)typeof(BattleSession).GetField("nativeBattleUiRandom", PrivateInstance)!.GetValue(battle);
        long? hostCallbacksBefore = battle.NativeHostCallbacks;

        IOException fault = Assert.Throws<IOException>(() => world.ApplyBattleInput(new(BattleInputKind.Tick)));
        Assert.Contains(loading.ActorIds[1], fault.Message, StringComparison.Ordinal);
        BattleSnapshot rolledBack = battle.Capture();
        Assert.Null(battle.EncounterAllocation);
        Assert.Null(battle.EncounterPolicies!.PendingSummon);
        Assert.Equal(1ul, battle.EncounterPolicies.Cursors[caster.Id].CommittedTurns);
        Assert.Null(battle.ActiveAction);
        Assert.Empty(rolledBack.NativeAllocations);
        Assert.Equal(originalAllocationControl, rolledBack.NativeScriptState.AllocationControl);
        Assert.Null(rolledBack.NativeLastAllocatedTarget);
        Assert.Equal(originalActors, battle.Combatants.Select(actor => actor.Id));
        Assert.Equal(originalModels, process.PlannedModelRoster.Owners);
        Assert.Equal(originalModels, process.ActionPackets.Roster.Owners);
        NativeBattleModelAliases restoredAliases = process.Graphics.NativeBindings.CaptureAliases(process.Graphics.ActorModels);
        foreach (BattleModelOwner model in originalModels)
        {
            Assert.Equal(originalAliases.TryEncodeExact(model, out byte originalSlot), restoredAliases.TryEncodeExact(model, out byte restoredSlot));
            Assert.Equal(originalSlot, restoredSlot);
        }
        Assert.False(process.Graphics.ActorModels.Contains(first));
        Assert.False(process.Graphics.ActorModels.Contains(second));
        Assert.Equal(originalTextureOwners, process.Memory.Textures!.PresentationTextures!.Keys.Order());
        Assert.Equal(originalCpu.EnemyCursor, process.ModelCpuResources.EnemyCursor);
        Assert.Equal(originalCpu.EnemyLinkedSlots, process.ModelCpuResources.EnemyLinkedSlots);
        Assert.Equal(originalCpu.NextEnemyResourceId, process.ModelCpuResources.NextEnemyResourceId);
        Assert.Equal(originalCpu.Rows.ToArray(), process.ModelCpuResources.Rows.ToArray());
        Assert.Equal(originalCache.Rows.ToArray(), process.ModelTextureCache.Rows.ToArray());
        Assert.Equal(originalFileIo.Rows.ToArray(), process.NativeFileIo!.Rows.ToArray());
        Assert.Equal(originalFileIo.ActiveCount, process.NativeFileIo.ActiveCount);
        Assert.Equal(originalIoResult, process.NativeEffectIoResult);
        (byte[] Data, bool[] Known) restoredStaging = process.NativeFileStaging!.ReadSnapshot(NativeBattleFileStaging.Address, 64);
        Assert.Equal(originalStaging.Data, restoredStaging.Data);
        Assert.Equal(originalStaging.Known, restoredStaging.Known);
        Assert.Null(process.NativeModelLoader!.Task);
        Assert.Equal(0, process.Graphics.ModelLoadCount);
        Assert.True(process.IsControlPrefixRetired);
        Assert.Null(process.PendingCompletion);
        Assert.DoesNotContain(process.Graphics.Requests, request => request.Command is 102 or 103 || request.Command == 10 && request.State == 0);
        Assert.Equal(randomBefore.Lane, rolledBack.Random!.Lane);
        Assert.Equal(randomBefore.Indexes.ToArray(), rolledBack.Random.Indexes.ToArray());
        Assert.Equal(globalBefore, process.GlobalRandomChange);
        Assert.Equal(crtBefore, process.CurrentCrtRandom);
        Assert.Equal(uiRandomBefore, rolledBack.NativeBattleUiRandom);
        Assert.Equal(hostCallbacksBefore + 1, battle.NativeHostCallbacks);
        Assert.DoesNotContain(battle.PreparedPresentation!.Actors, actor => loading.ActorIds.Contains(actor.Id));

        world.ApplyBattleInput(new(BattleInputKind.Tick, Frames: 30));
        Assert.Null(battle.EncounterAllocation);
        Assert.Null(battle.EncounterPolicies.PendingSummon);
        Assert.Equal(1ul, battle.EncounterPolicies.Cursors[caster.Id].CommittedTurns);
        Assert.DoesNotContain(battle.Events, entry => entry.Kind == "EncounterSpawn");
        Assert.True(battle.CanChoosePlayerCommand(readyParty.Id));
    }
}
