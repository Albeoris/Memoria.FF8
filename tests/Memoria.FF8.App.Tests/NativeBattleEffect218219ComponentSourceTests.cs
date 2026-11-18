using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect218219ComponentSourceTests
{
    [Theory]
    [InlineData(218, 795, 68, 128, 236, 330)]
    [InlineData(219, 462, 121, 114, 8, 311)]
    public void Installed_component_retains_original_text_or_bone_owner_through_the_real_callback_boundary(ushort effect, int formation, int stageId, int model, byte command, ushort action)
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(game is not null, "Select installed original resources with MEMORIA_FF8_GAME_DIR.");
        GameSession session = GameWorld.CreateColdProcess(game!);
        session.StartNewGame();
        using GameWorld world = new(session, game!);
        BattleSession source = NativeBattleComponentSourceTests.PrepareComponentSource(world, NativeBattleComponentSourceTests.CreateComponentRequest(effect));
        Assert.Equal(formation, source.ReadDefinition().EncounterId);
        Assert.Equal(stageId, source.ReadDefinition().StageId);
        BattleDiagnosticComponentAdmission admission = GameWorld.ValidateEffectComponentSource(source, effect);
        Assert.Equal((byte)3, admission.OriginalActorIndex);
        Assert.Equal(command, admission.Command);
        Assert.Equal(action, admission.Action);
        BattlePresentationProcess process = source.Presentation!;
        BattleCombatant actor = Assert.Single(source.Combatants, candidate => candidate.NativeActorIndex == 3);
        Assert.Equal(model, actor.ModelId);
        NativeBattleGraphicsResource resource = world.BattleResources.ReadNativeGraphicsResource(source.GetCombatantDefinition(actor.Id));
        NativeBattlePoseSource pose = resource.PoseSource!;
        Assert.Equal(pose.BoneCount, NativeBattleBodyBoneMatrices.Require(process.Graphics.ActorModels.Get(admission.Actor), pose).Length);
        Assert.True(effect == 218 ? pose.BoneCount == 41 : pose.BoneCount > 60);
        if (effect == 218)
        {
            Assert.Contains(source.Combatants, companion => companion.ModelId == 82 && companion.NativeActorIndex == 4);
        }
        BattleActionHeaderReference reference = new(admission.HeaderIndex);
        NativeBattleActionHeaderSlot header = process.ActionPackets.ReadHeader(reference);
        Assert.Contains(actor.NativeAi!.Abilities, ability => ability.Command == command && ability.Id == action && ability.Sequence == header.Sequence);
        byte targetIndex = effect == 218 ? (byte)3 : (byte)0;
        NativeBattleContactPacket contact = Assert.Single(process.ActionPackets.ReadHeaderContacts(reference));
        Assert.Equal(process.FindOriginalActor(NativeBattleModelSlot.Decode(targetIndex)), contact.Primary.Recipient);
        Assert.Equal(0, contact.Primary.Amount6);
        Assert.Equal(source.NativePresentationStatusMemory.Rows[targetIndex].Saved, contact.Primary.SavedStatuses4);
        Assert.Equal(source.NativePresentationStatusMemory.Rows[targetIndex].Battle, contact.Primary.BattleStatuses8);
        Assert.Null(contact.Secondary);
        NativeBattleGraphicsState graphics = process.Graphics;
        NativeBattleStageState stage = process.NativeStage!;
        long? callback = source.NativeHostCallbacks;
        int events = source.Events.Count;
        var combat = source.Combatants.Select(candidate => (candidate.Id, candidate.CurrentHp, candidate.NativeStatus0, candidate.NativeStatus1, candidate.IsActive, candidate.Hidden, candidate.Untargetable, candidate.Suspended)).ToArray();
        BattleSession fork = source.ForkForContinuation();
        int firstEvent = fork.Events.Count;
        string? expectedText = effect == 218 ? resource.ReadText91(0) : null;
        bool entered = false;
        bool reached = false;
        for (int frame = 0; frame < 1200 && !reached; frame++)
        {
            fork.AdvanceNativeGraphicsPresentation(BattlePresentationTiming.ElapsedForCallbacks(1));
            NativeBattleOriginalEffectState? current = fork.Presentation!.Graphics.OriginalEffect;
            entered |= current?.RequestedEffect == effect;
            reached = effect == 218
                ? fork.Events.Skip(firstEvent).Any(item => item.Kind == "NativeText" && item.ActorId == actor.Id && item.Message == expectedText && item.Amount == 45 && item.NativeMessageMode == 3)
                : entered && (current is null || current.ReturnedDescriptor == 0);
        }
        Assert.True(reached, "The actual original callback must pass its text-bank or cinematic bone60 boundary.");
        long reachedCallback = fork.Presentation!.Graphics.Callback;
        fork.AdvanceNativeGraphicsPresentation(BattlePresentationTiming.ElapsedForCallbacks(1));
        Assert.True(fork.Presentation.Graphics.Callback > reachedCallback);
        Assert.Same(graphics, process.Graphics);
        Assert.Same(stage, process.NativeStage);
        Assert.Equal(callback, source.NativeHostCallbacks);
        Assert.Equal(events, source.Events.Count);
        Assert.Equal(header, process.ActionPackets.ReadHeader(reference));
        Assert.Equal(combat, source.Combatants.Select(candidate => (candidate.Id, candidate.CurrentHp, candidate.NativeStatus0, candidate.NativeStatus1, candidate.IsActive, candidate.Hidden, candidate.Untargetable, candidate.Suspended)).ToArray());
    }
}
