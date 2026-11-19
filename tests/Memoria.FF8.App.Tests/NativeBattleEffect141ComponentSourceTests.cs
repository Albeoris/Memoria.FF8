using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect141ComponentSourceTests
{
    [Theory]
    [InlineData(141, 71, 36)]
    [InlineData(159, 9, 92)]
    [InlineData(160, 71, 36)]
    [InlineData(161, 738, 131)]
    [InlineData(259, 94, 61)]
    [InlineData(332, 94, 61)]
    [InlineData(333, 94, 61)]
    [InlineData(334, 326, 67)]
    [InlineData(336, 354, 90)]
    [InlineData(339, 796, 112)]
    [InlineData(340, 982, 126)]
    [InlineData(341, 712, 84)]
    [InlineData(342, 510, 124)]
    [InlineData(343, 509, 122)]
    public void Installed_component_preserves_real_Renzokuken_logical_owner_through_actual_widget_registration(ushort effect, int formation, int targetModel)
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(game is not null, "Select installed original resources with MEMORIA_FF8_GAME_DIR.");
        GameSession session = GameWorld.CreateColdProcess(game!);
        session.StartNewGame();
        using GameWorld world = new(session, game!);
        BattleSession source = NativeBattleComponentSourceTests.PrepareComponentSource(world, NativeBattleComponentSourceTests.CreateComponentRequest(effect));
        Assert.Equal(formation, source.ReadDefinition().EncounterId);
        BattleDiagnosticComponentAdmission admission = GameWorld.ValidateEffectComponentSource(source, effect);
        Assert.Equal((byte)0, admission.OriginalActorIndex);
        Assert.Equal((byte)5, admission.Command);
        Assert.Equal((ushort)65533, admission.Action);
        BattlePresentationProcess process = source.Presentation!;
        BattleCombatant actor = Assert.Single(source.Combatants, candidate => candidate.NativeActorIndex == 0);
        BattleCombatant target = source.Combatants.First(candidate => !candidate.IsPlayer && candidate.IsActive && candidate.ModelId == targetModel);
        Assert.Equal(0, actor.ModelId);
        Assert.Equal(targetModel, target.ModelId);
        Assert.True(target.IsActive && target.IsAlive);
        Assert.False(target.Untargetable || target.Suspended);
        Assert.Equal(effect, source.GetNativeRenzokuken(actor.Id)!.MagicId);
        Assert.Null(source.GetNativeRenzokuken(actor.Id)!.FinisherActionId);
        BattleSnapshot continuation = source.CaptureForContinuation();
        Assert.NotNull(continuation.LogicalContinuation!.Event!.Renzokuken);
        if (effect is 332 or 333)
        {
            Assert.Equal(effect == 332 ? (byte)3 : (byte)4, continuation.ActiveAction!.NativeRenzokuken!.CrisisLevel);
        }
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(continuation with { ActiveAction = null }));
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(continuation with { ActiveAction = continuation.ActiveAction! with { } }));
        BattleActionHeaderReference reference = new(admission.HeaderIndex);
        NativeBattleActionHeaderSlot header = process.ActionPackets.ReadHeader(reference);
        Assert.Equal((byte)21, header.Sequence);
        NativeBattleGraphicsState graphics = process.Graphics;
        NativeBattleStageState stage = process.NativeStage!;
        long? callback = source.NativeHostCallbacks;
        int events = source.Events.Count;
        var combat = source.Combatants.Select(candidate => (candidate.Id, candidate.CurrentHp, candidate.NativeStatus0, candidate.NativeStatus1, candidate.IsActive, candidate.Hidden, candidate.Untargetable, candidate.Suspended)).ToArray();
        BattleSession fork = source.ForkForContinuation();
        bool registered = false;
        for (int frame = 0; frame < 1200 && !registered; frame++)
        {
            fork.AdvanceNativeGraphicsPresentation(BattlePresentationTiming.ElapsedForCallbacks(1));
            registered = fork.Presentation!.TriggerWidget?.SourceMagicId == effect && fork.Presentation.LastFrameServices?.ReadOrderedReceipts().OfType<NativeBattleModelTriggerRegistered>().Any(receipt => receipt.Owner == admission.Actor && receipt.ActorId == actor.Id) == true;
        }
        Assert.True(registered, "The actual widget receipt must register under the retained ordinary Renzokuken logical owner.");
        Assert.Equal(source.ReadDefinition().NativeRenzokukenResources.Single(resource => resource.MagicId == effect).Timeline.InputThresholds.Length, fork.Presentation!.TriggerWidget!.Block.Timeline.InputThresholds.Length);
        if (effect is 332 or 333)
        {
            Assert.Equal(effect == 332 ? 7 : 8, fork.Presentation!.TriggerWidget!.Block.Timeline.InputThresholds.Length);
        }
        long registeredCallback = fork.Presentation!.Graphics.Callback;
        fork.AdvanceNativeGraphicsPresentation(BattlePresentationTiming.ElapsedForCallbacks(1));
        Assert.True(fork.Presentation.Graphics.Callback > registeredCallback);
        Assert.Equal(effect, fork.GetNativeRenzokuken(actor.Id)!.MagicId);
        Assert.Same(graphics, process.Graphics);
        Assert.Same(stage, process.NativeStage);
        Assert.Equal(callback, source.NativeHostCallbacks);
        Assert.Equal(events, source.Events.Count);
        Assert.Equal(header, process.ActionPackets.ReadHeader(reference));
        Assert.Equal(combat, source.Combatants.Select(candidate => (candidate.Id, candidate.CurrentHp, candidate.NativeStatus0, candidate.NativeStatus1, candidate.IsActive, candidate.Hidden, candidate.Untargetable, candidate.Suspended)).ToArray());
    }
}
