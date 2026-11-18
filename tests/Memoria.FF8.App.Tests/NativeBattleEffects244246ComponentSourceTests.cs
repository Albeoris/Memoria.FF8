using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffects244246ComponentSourceTests
{
    [Theory]
    [InlineData(244, 148)]
    [InlineData(245, 149)]
    [InlineData(246, 150)]
    public void Installed_component_retains_original_encounter_bones_and_finishes_its_native_script(ushort effect, ushort action)
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(game is not null, "Select installed original resources with MEMORIA_FF8_GAME_DIR.");
        GameSession session = GameWorld.CreateColdProcess(game!);
        session.StartNewGame();
        using GameWorld world = new(session, game!);
        string records = JsonSerializer.Serialize(session.State.CharacterVitals!.Records);
        BattleSession source = NativeBattleComponentSourceTests.PrepareComponentSource(world, NativeBattleComponentSourceTests.CreateComponentRequest(effect));
        Assert.Equal(records, JsonSerializer.Serialize(session.State.CharacterVitals!.Records));
        Assert.Equal(164, source.ReadDefinition().EncounterId);
        Assert.Equal(25, source.ReadDefinition().StageId);
        BattleDiagnosticComponentAdmission admission = GameWorld.ValidateEffectComponentSource(source, effect);
        Assert.Equal((byte)3, admission.OriginalActorIndex);
        Assert.Equal((byte)8, admission.Command);
        Assert.Equal(action, admission.Action);
        BattlePresentationProcess process = source.Presentation!;
        BattleCombatant actor = Assert.Single(source.Combatants, candidate => candidate.NativeActorIndex == 3);
        Assert.Equal(101, actor.ModelId);
        Assert.Equal(0, actor.SceneSlot);
        Assert.True(actor.IsActive);
        Assert.False(actor.Hidden);
        Assert.False(actor.Suspended);
        NativeBattleGraphicsResource resource = world.BattleResources.ReadNativeGraphicsResource(source.GetCombatantDefinition(actor.Id));
        Assert.Equal(45, resource.PoseSource!.BoneCount);
        Assert.Equal(45, NativeBattleBodyBoneMatrices.Require(process.Graphics.ActorModels.Get(admission.Actor), resource.PoseSource).Length);
        BattleActionHeaderReference reference = new(admission.HeaderIndex);
        NativeBattleActionHeaderSlot header = process.ActionPackets.ReadHeader(reference);
        Assert.Equal(Assert.Single(actor.NativeAi!.Abilities.Where(ability => ability.Command == 8 && ability.Id == action).Select(ability => ability.Sequence).Distinct()), header.Sequence);
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
        var combat = source.Combatants.Select(candidate => (candidate.Id, candidate.ModelId, candidate.SceneSlot, candidate.CurrentHp, candidate.NativeStatus0, candidate.NativeStatus1, candidate.IsActive, candidate.Hidden, candidate.Untargetable, candidate.Suspended)).ToArray();
        BattleSession fork = source.ForkForContinuation();
        bool entered = false;
        bool completed = false;
        for (int frame = 0; frame < 2400 && !completed; frame++)
        {
            fork.AdvanceNativeGraphicsPresentation(BattlePresentationTiming.ElapsedForCallbacks(1));
            NativeBattleOriginalEffectState? current = fork.Presentation!.Graphics.OriginalEffect;
            if (current?.RequestedEffect == effect)
            {
                entered = true;
                completed = current.ReturnedDescriptor == 0;
            }
        }
        Assert.True(entered && completed, "The original component must pass its actor bone anchors and complete its actual native script.");
        long completedCallback = fork.Presentation!.Graphics.Callback;
        fork.AdvanceNativeGraphicsPresentation(BattlePresentationTiming.ElapsedForCallbacks(1));
        Assert.True(fork.Presentation.Graphics.Callback > completedCallback);
        Assert.Same(graphics, process.Graphics);
        Assert.Same(stage, process.NativeStage);
        Assert.Equal(callback, source.NativeHostCallbacks);
        Assert.Equal(events, source.Events.Count);
        Assert.Equal(header, process.ActionPackets.ReadHeader(reference));
        Assert.Equal(combat, source.Combatants.Select(candidate => (candidate.Id, candidate.ModelId, candidate.SceneSlot, candidate.CurrentHp, candidate.NativeStatus0, candidate.NativeStatus1, candidate.IsActive, candidate.Hidden, candidate.Untargetable, candidate.Suspended)).ToArray());
    }
}
