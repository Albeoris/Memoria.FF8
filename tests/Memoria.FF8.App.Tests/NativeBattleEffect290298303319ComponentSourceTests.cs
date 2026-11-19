using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect290298303319ComponentSourceTests
{
    [Theory]
    [InlineData(290, 431, 126, 109, 28, 291, 14, 0)]
    [InlineData(298, 194, 52, 102, 45, 119, 14, 0)]
    [InlineData(303, 363, 96, 11, 46, 258, 13, 4)]
    [InlineData(319, 372, 140, 38, 36, 279, 14, 0)]
    public void Installed_component_retains_original_rig_and_native_companion_activation(ushort effect, int formation, int stageId, int model, int bones, ushort action, byte sequence, byte targetIndex)
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
        Assert.Equal((byte)8, admission.Command);
        Assert.Equal(action, admission.Action);
        BattlePresentationProcess process = source.Presentation!;
        BattleCombatant actor = Assert.Single(source.Combatants, candidate => candidate.NativeActorIndex == 3);
        Assert.Equal(model, actor.ModelId);
        NativeBattleGraphicsResource resource = world.BattleResources.ReadNativeGraphicsResource(source.GetCombatantDefinition(actor.Id));
        Assert.Equal(bones, resource.PoseSource!.BoneCount);
        Assert.Equal(bones, NativeBattleBodyBoneMatrices.Require(process.Graphics.ActorModels.Get(admission.Actor), resource.PoseSource).Length);
        BattleActionHeaderReference reference = new(admission.HeaderIndex);
        NativeBattleActionHeaderSlot header = process.ActionPackets.ReadHeader(reference);
        Assert.Equal(sequence, header.Sequence);
        Assert.Contains(actor.NativeAi!.Abilities, ability => ability.Command == 8 && ability.Id == action && ability.Sequence == header.Sequence);
        NativeBattleContactPacket geometry = Assert.Single(process.ActionPackets.ReadHeaderContacts(reference));
        Assert.Equal(process.FindOriginalActor(NativeBattleModelSlot.Decode(targetIndex)), geometry.Primary.Recipient);
        Assert.Equal(0, geometry.Primary.Amount6);
        Assert.Equal(source.NativePresentationStatusMemory.Rows[targetIndex].Saved, geometry.Primary.SavedStatuses4);
        Assert.Equal(source.NativePresentationStatusMemory.Rows[targetIndex].Battle, geometry.Primary.BattleStatuses8);
        Assert.Null(geometry.Secondary);
        BattleCombatant? companion = effect == 303 ? Assert.Single(source.Combatants, candidate => candidate.NativeActorIndex == 4) : null;
        if (companion is not null)
        {
            Assert.Equal(12, companion.ModelId);
            Assert.True(companion.Hidden && companion.Suspended);
            BattleModelOwner owner = process.FindOriginalActor(NativeBattleModelSlot.Decode(4))!;
            NativeBattlePoseSource companionPose = world.BattleResources.ReadNativeGraphicsResource(source.GetCombatantDefinition(companion.Id)).PoseSource!;
            Assert.Equal(46, NativeBattleBodyBoneMatrices.Require(process.Graphics.ActorModels.Get(owner), companionPose).Length);
        }
        NativeBattleGraphicsState graphics = process.Graphics;
        NativeBattleStageState stage = process.NativeStage!;
        long? callback = source.NativeHostCallbacks;
        int events = source.Events.Count;
        var combat = source.Combatants.Select(candidate => (candidate.Id, candidate.CurrentHp, candidate.NativeStatus0, candidate.NativeStatus1, candidate.IsActive, candidate.Hidden, candidate.Untargetable, candidate.Suspended)).ToArray();
        BattleSession fork = source.ForkForContinuation();
        int firstEvent = fork.Events.Count;
        bool entered = false;
        bool reached = false;
        for (int frame = 0; frame < 2400 && !reached; frame++)
        {
            fork.AdvanceNativeGraphicsPresentation(BattlePresentationTiming.ElapsedForCallbacks(1));
            NativeBattleOriginalEffectState? current = fork.Presentation!.Graphics.OriginalEffect;
            entered |= current?.RequestedEffect == effect;
            reached = entered && (current is null || current.ReturnedDescriptor == 0);
        }
        Assert.True(reached, "The original callback must pass actual original skeleton and current model cache consumers and complete.");
        long reachedCallback = fork.Presentation!.Graphics.Callback;
        fork.AdvanceNativeGraphicsPresentation(BattlePresentationTiming.ElapsedForCallbacks(1));
        Assert.True(fork.Presentation.Graphics.Callback > reachedCallback);
        if (companion is not null)
        {
            for (int frame = 0; frame < 2400 && !fork.Events.Skip(firstEvent).Any(item => item.Kind == "Activated" && item.ActorId == companion.Id); frame++)
            {
                fork.AdvanceNativeGraphicsPresentation(BattlePresentationTiming.ElapsedForCallbacks(1));
            }
            Assert.Single(fork.Events.Skip(firstEvent), item => item.Kind == "Activated" && item.ActorId == companion.Id);
            BattleCombatant activated = Assert.Single(fork.Combatants, candidate => candidate.Id == companion.Id);
            Assert.True(activated.IsActive);
            long activatedCallback = fork.Presentation.Graphics.Callback;
            fork.AdvanceNativeGraphicsPresentation(BattlePresentationTiming.ElapsedForCallbacks(1));
            Assert.True(fork.Presentation.Graphics.Callback > activatedCallback);
        }
        Assert.Equal(source.Combatants.Select(candidate => (candidate.Id, candidate.CurrentHp, candidate.NativeStatus0, candidate.NativeStatus1)).ToArray(), fork.Combatants.Select(candidate => (candidate.Id, candidate.CurrentHp, candidate.NativeStatus0, candidate.NativeStatus1)).ToArray());
        Assert.Same(graphics, process.Graphics);
        Assert.Same(stage, process.NativeStage);
        Assert.Equal(callback, source.NativeHostCallbacks);
        Assert.Equal(events, source.Events.Count);
        Assert.Equal(header, process.ActionPackets.ReadHeader(reference));
        Assert.Equal(combat, source.Combatants.Select(candidate => (candidate.Id, candidate.CurrentHp, candidate.NativeStatus0, candidate.NativeStatus1, candidate.IsActive, candidate.Hidden, candidate.Untargetable, candidate.Suspended)).ToArray());
    }
}
