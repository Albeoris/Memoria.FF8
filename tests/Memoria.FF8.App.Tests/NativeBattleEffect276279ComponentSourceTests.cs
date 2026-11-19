using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect276279ComponentSourceTests
{
    [Fact]
    public void Installed_Xatm_component_passes_original_sixth_death_rig()
    {
        ValidateInstalledComponent(276, 9, 92, 145, 19);
    }

    [Fact]
    public void Installed_MegaSpark_component_passes_actual_composed_stack_producer()
    {
        ValidateInstalledComponent(279, 377, 30, 106, 14);
    }

    private static void ValidateInstalledComponent(ushort effect, int formation, int model, ushort action, byte sequence)
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(game is not null, "Select installed original resources with MEMORIA_FF8_GAME_DIR.");
        GameSession session = GameWorld.CreateColdProcess(game!);
        session.StartNewGame();
        using GameWorld world = new(session, game!);
        BattleSession source = NativeBattleComponentSourceTests.PrepareComponentSource(world, NativeBattleComponentSourceTests.CreateComponentRequest(effect));
        Assert.Equal(formation, source.ReadDefinition().EncounterId);
        BattleDiagnosticComponentAdmission admission = GameWorld.ValidateEffectComponentSource(source, effect);
        Assert.Equal((byte)3, admission.OriginalActorIndex);
        Assert.Equal((byte)8, admission.Command);
        Assert.Equal(action, admission.Action);
        BattlePresentationProcess process = source.Presentation!;
        BattleCombatant actor = Assert.Single(source.Combatants, candidate => candidate.NativeActorIndex == 3);
        Assert.Equal(model, actor.ModelId);
        NativeBattleGraphicsResource resource = world.BattleResources.ReadNativeGraphicsResource(source.GetCombatantDefinition(actor.Id));
        Assert.Equal(resource.PoseSource!.BoneCount, NativeBattleBodyBoneMatrices.Require(process.Graphics.ActorModels.Get(admission.Actor), resource.PoseSource).Length);
        if (effect == 279)
        {
            Assert.Equal(127, source.ReadDefinition().StageId);
            Assert.Equal(46, resource.PoseSource.BoneCount);
        }
        else
        {
            Assert.Equal(0, actor.CurrentHp);
        }
        BattleActionHeaderReference reference = new(admission.HeaderIndex);
        NativeBattleActionHeaderSlot header = process.ActionPackets.ReadHeader(reference);
        Assert.Equal(sequence, header.Sequence);
        Assert.Contains(actor.NativeAi!.Abilities, ability => ability.Command == 8 && ability.Id == action && ability.Sequence == header.Sequence);
        NativeBattleContactPacket geometry = Assert.Single(process.ActionPackets.ReadHeaderContacts(reference));
        byte recipient = effect == 276 ? (byte)3 : (byte)0;
        Assert.Equal(process.FindOriginalActor(NativeBattleModelSlot.Decode(recipient)), geometry.Primary.Recipient);
        Assert.Equal(0, geometry.Primary.Amount6);
        Assert.Equal(source.NativePresentationStatusMemory.Rows[recipient].Saved, geometry.Primary.SavedStatuses4);
        Assert.Equal(source.NativePresentationStatusMemory.Rows[recipient].Battle, geometry.Primary.BattleStatuses8);
        Assert.Null(geometry.Secondary);
        NativeBattleGraphicsState graphics = process.Graphics;
        NativeBattleStageState stage = process.NativeStage!;
        long? callback = source.NativeHostCallbacks;
        int events = source.Events.Count;
        var combat = source.Combatants.Select(candidate => (candidate.Id, candidate.CurrentHp, candidate.NativeStatus0, candidate.NativeStatus1, candidate.IsActive, candidate.Hidden, candidate.Untargetable, candidate.Suspended)).ToArray();
        BattleSession fork = source.ForkForContinuation();
        bool entered = false;
        bool reached = false;
        for (int frame = 0; frame < 2400 && !reached; frame++)
        {
            fork.AdvanceNativeGraphicsPresentation(BattlePresentationTiming.ElapsedForCallbacks(1));
            NativeBattleOriginalEffectState? current = fork.Presentation!.Graphics.OriginalEffect;
            entered |= current?.RequestedEffect == effect;
            reached = entered && (current is null || current.ReturnedDescriptor == 0);
        }
        Assert.True(reached, "The original callback must pass original death skeleton or actual composed stack-byte consumers and complete.");
        long reachedCallback = fork.Presentation!.Graphics.Callback;
        fork.AdvanceNativeGraphicsPresentation(BattlePresentationTiming.ElapsedForCallbacks(1));
        Assert.True(fork.Presentation.Graphics.Callback > reachedCallback);
        Assert.Equal(source.Combatants.Select(candidate => (candidate.Id, candidate.CurrentHp, candidate.NativeStatus0, candidate.NativeStatus1)).ToArray(), fork.Combatants.Select(candidate => (candidate.Id, candidate.CurrentHp, candidate.NativeStatus0, candidate.NativeStatus1)).ToArray());
        Assert.Same(graphics, process.Graphics);
        Assert.Same(stage, process.NativeStage);
        Assert.Equal(callback, source.NativeHostCallbacks);
        Assert.Equal(events, source.Events.Count);
        Assert.Equal(header, process.ActionPackets.ReadHeader(reference));
        Assert.Equal(combat, source.Combatants.Select(candidate => (candidate.Id, candidate.CurrentHp, candidate.NativeStatus0, candidate.NativeStatus1, candidate.IsActive, candidate.Hidden, candidate.Untargetable, candidate.Suspended)).ToArray());
    }
}
