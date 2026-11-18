using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect227ComponentSourceTests
{
    [Fact]
    public void Installed_component_retains_original_bone_text_and_dormant_companion_owners()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(game is not null, "Select installed original resources with MEMORIA_FF8_GAME_DIR.");
        GameSession session = GameWorld.CreateColdProcess(game!);
        session.StartNewGame();
        using GameWorld world = new(session, game!);
        BattleSession source = NativeBattleComponentSourceTests.PrepareComponentSource(world, NativeBattleComponentSourceTests.CreateComponentRequest(227));
        Assert.Equal(511, source.ReadDefinition().EncounterId);
        Assert.Equal(142, source.ReadDefinition().StageId);
        BattleDiagnosticComponentAdmission admission = GameWorld.ValidateEffectComponentSource(source, 227);
        Assert.Equal((byte)3, admission.OriginalActorIndex);
        Assert.Equal((byte)8, admission.Command);
        Assert.Equal((ushort)339, admission.Action);
        BattlePresentationProcess process = source.Presentation!;
        BattleCombatant actor = Assert.Single(source.Combatants, candidate => candidate.NativeActorIndex == 3);
        BattleCombatant companion = Assert.Single(source.Combatants, candidate => candidate.NativeActorIndex == 4);
        Assert.Equal(121, actor.ModelId);
        Assert.Equal(123, companion.ModelId);
        Assert.True(companion.Hidden && companion.Suspended && companion.Untargetable);
        NativeBattleGraphicsResource resource = world.BattleResources.ReadNativeGraphicsResource(source.GetCombatantDefinition(actor.Id));
        Assert.Equal(69, resource.PoseSource!.BoneCount);
        Assert.Equal(69, NativeBattleBodyBoneMatrices.Require(process.Graphics.ActorModels.Get(admission.Actor), resource.PoseSource).Length);
        BattleActionHeaderReference reference = new(admission.HeaderIndex);
        NativeBattleActionHeaderSlot header = process.ActionPackets.ReadHeader(reference);
        Assert.Equal((byte)13, header.Sequence);
        NativeBattleContactPacket geometry = Assert.Single(process.ActionPackets.ReadHeaderContacts(reference));
        Assert.Equal(process.FindOriginalActor(NativeBattleModelSlot.Decode(0)), geometry.Primary.Recipient);
        Assert.Equal(0, geometry.Primary.Amount6);
        Assert.Equal(source.NativePresentationStatusMemory.Rows[0].Saved, geometry.Primary.SavedStatuses4);
        Assert.Equal(source.NativePresentationStatusMemory.Rows[0].Battle, geometry.Primary.BattleStatuses8);
        Assert.Null(geometry.Secondary);
        NativeBattleGraphicsState graphics = process.Graphics;
        NativeBattleStageState stage = process.NativeStage!;
        long? callback = source.NativeHostCallbacks;
        int events = source.Events.Count;
        var combat = source.Combatants.Select(candidate => (candidate.Id, candidate.CurrentHp, candidate.NativeStatus0, candidate.NativeStatus1, candidate.IsActive, candidate.Hidden, candidate.Untargetable, candidate.Suspended)).ToArray();
        BattleSession fork = source.ForkForContinuation();
        int firstEvent = fork.Events.Count;
        string expected = resource.ReadText91(0);
        bool reached = false;
        for (int frame = 0; frame < 1200 && !reached; frame++)
        {
            fork.AdvanceNativeGraphicsPresentation(BattlePresentationTiming.ElapsedForCallbacks(1));
            reached = fork.Events.Skip(firstEvent).Any(item => item.Kind == "NativeText" && item.ActorId == actor.Id && item.Message == expected && item.Amount == 42 && item.NativeMessageMode == 3);
        }
        Assert.True(reached, "The original callback must pass its actual bone50 and first DAT text boundary.");
        long reachedCallback = fork.Presentation!.Graphics.Callback;
        fork.AdvanceNativeGraphicsPresentation(BattlePresentationTiming.ElapsedForCallbacks(1));
        Assert.True(fork.Presentation.Graphics.Callback > reachedCallback);
        Assert.Equal(combat, fork.Combatants.Select(candidate => (candidate.Id, candidate.CurrentHp, candidate.NativeStatus0, candidate.NativeStatus1, candidate.IsActive, candidate.Hidden, candidate.Untargetable, candidate.Suspended)).ToArray());
        Assert.Same(graphics, process.Graphics);
        Assert.Same(stage, process.NativeStage);
        Assert.Equal(callback, source.NativeHostCallbacks);
        Assert.Equal(events, source.Events.Count);
        Assert.Equal(header, process.ActionPackets.ReadHeader(reference));
        Assert.Equal(combat, source.Combatants.Select(candidate => (candidate.Id, candidate.CurrentHp, candidate.NativeStatus0, candidate.NativeStatus1, candidate.IsActive, candidate.Hidden, candidate.Untargetable, candidate.Suspended)).ToArray());
    }
}
