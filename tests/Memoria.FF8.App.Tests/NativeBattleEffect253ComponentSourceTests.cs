using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect253ComponentSourceTests
{
    [Theory]
    [InlineData(253, 105, 3, 283, 16)]
    [InlineData(254, 107, 5, 281, 17)]
    [InlineData(255, 106, 4, 282, 17)]
    public void Installed_counter_component_passes_actual_child_creation_with_its_original_trio_ring(ushort effect, int model, byte physical, ushort action, byte sequence)
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(game is not null, "Select installed original resources with MEMORIA_FF8_GAME_DIR.");
        GameSession session = GameWorld.CreateColdProcess(game!);
        session.StartNewGame();
        using GameWorld world = new(session, game!);
        BattleSession source = NativeBattleComponentSourceTests.PrepareComponentSource(world, NativeBattleComponentSourceTests.CreateComponentRequest(effect));
        Assert.Equal(794, source.ReadDefinition().EncounterId);
        Assert.Equal(65, source.ReadDefinition().StageId);
        BattleDiagnosticComponentAdmission admission = GameWorld.ValidateEffectComponentSource(source, effect);
        Assert.Equal(physical, admission.OriginalActorIndex);
        Assert.Equal((byte)8, admission.Command);
        Assert.Equal(action, admission.Action);
        BattlePresentationProcess process = source.Presentation!;
        BattleCombatant actor = Assert.Single(source.Combatants, candidate => candidate.NativeActorIndex == physical);
        Assert.Equal(model, actor.ModelId);
        NativeBattleGraphicsResource resource = world.BattleResources.ReadNativeGraphicsResource(source.GetCombatantDefinition(actor.Id));
        Assert.Equal(3, NativeBattleModelRings.Walk(process.Graphics.ActorModels, admission.Actor).Count());
        BattleActionHeaderReference reference = new(admission.HeaderIndex);
        NativeBattleActionHeaderSlot header = process.ActionPackets.ReadHeader(reference);
        Assert.Equal(sequence, header.Sequence);
        Assert.Contains(actor.NativeAi!.Abilities, ability => ability.Command == 8 && ability.Id == action && ability.Sequence == header.Sequence);
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
        bool entered = false;
        bool reached = false;
        for (int frame = 0; frame < 2400 && !reached; frame++)
        {
            fork.AdvanceNativeGraphicsPresentation(BattlePresentationTiming.ElapsedForCallbacks(1));
            NativeBattleOriginalEffectState? current = fork.Presentation!.Graphics.OriginalEffect;
            entered |= current?.RequestedEffect == effect;
            reached = entered && (current is null || current.ReturnedDescriptor == 0);
        }
        Assert.True(reached, "The original callback must pass actual child creation with its typed parent model owner and complete.");
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
