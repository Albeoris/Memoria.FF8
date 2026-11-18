using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect243ComponentSourceTests
{
    [Fact]
    public void Installed_component_passes_original_joint_anchor_and_mesh_mask_boundary()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(game is not null, "Select installed original resources with MEMORIA_FF8_GAME_DIR.");
        GameSession session = GameWorld.CreateColdProcess(game!);
        session.StartNewGame();
        using GameWorld world = new(session, game!);
        BattleSession source = NativeBattleComponentSourceTests.PrepareComponentSource(world, NativeBattleComponentSourceTests.CreateComponentRequest(243));
        Assert.Equal(164, source.ReadDefinition().EncounterId);
        Assert.Equal(25, source.ReadDefinition().StageId);
        BattleDiagnosticComponentAdmission admission = GameWorld.ValidateEffectComponentSource(source, 243);
        Assert.Equal((byte)3, admission.OriginalActorIndex);
        Assert.Equal((byte)8, admission.Command);
        Assert.Equal((ushort)147, admission.Action);
        BattlePresentationProcess process = source.Presentation!;
        BattleCombatant actor = Assert.Single(source.Combatants, candidate => candidate.NativeActorIndex == 3);
        Assert.Equal(101, actor.ModelId);
        NativeBattleGraphicsResource resource = world.BattleResources.ReadNativeGraphicsResource(source.GetCombatantDefinition(actor.Id));
        Assert.Equal(45, resource.PoseSource!.BoneCount);
        Assert.Equal(45, NativeBattleBodyBoneMatrices.Require(process.Graphics.ActorModels.Get(admission.Actor), resource.PoseSource).Length);
        BattleActionHeaderReference reference = new(admission.HeaderIndex);
        NativeBattleActionHeaderSlot header = process.ActionPackets.ReadHeader(reference);
        Assert.Contains(actor.NativeAi!.Abilities, ability => ability.Command == 8 && ability.Id == 147 && ability.Sequence == header.Sequence);
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
        uint initialMask = process.Graphics.ActorModels.Get(admission.Actor).SequenceState!.MeshMask124;
        Assert.NotEqual(0u, initialMask & 8);
        bool reached = false;
        for (int frame = 0; frame < 1200 && !reached; frame++)
        {
            fork.AdvanceNativeGraphicsPresentation(BattlePresentationTiming.ElapsedForCallbacks(1));
            reached = fork.Presentation!.Graphics.ActorModels.Get(admission.Actor).SequenceState!.MeshMask124 == (initialMask & ~8u);
        }
        Assert.True(reached, "The original callback must pass its joint-anchor and actual808D mesh-mask boundary.");
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
