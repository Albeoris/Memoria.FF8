using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect248ComponentSourceTests
{
    [Fact]
    public void Installed_component_retains_actual_death_allocation_and_initialization_actor_order()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(game is not null, "Select installed original resources with MEMORIA_FF8_GAME_DIR.");
        GameSession session = GameWorld.CreateColdProcess(game!);
        session.StartNewGame();
        using GameWorld world = new(session, game!);
        string records = JsonSerializer.Serialize(session.State.CharacterVitals!.Records);
        BattleSession source = NativeBattleComponentSourceTests.PrepareComponentSource(world, NativeBattleComponentSourceTests.CreateComponentRequest(248));
        Assert.Equal(records, JsonSerializer.Serialize(session.State.CharacterVitals!.Records));
        Assert.Equal(164, source.ReadDefinition().EncounterId);
        Assert.Equal(25, source.ReadDefinition().StageId);
        BattleCombatant defeated = Assert.Single(source.Combatants, actor => actor.ModelId == 101 && actor.SceneSlot == 0);
        Assert.False(defeated.IsAlive);
        Assert.Contains(source.Events, entry => entry.Kind == "Damage" && entry.TargetId == defeated.Id && entry.Amount > 0);
        BattleDiagnosticComponentAdmission admission = GameWorld.ValidateEffectComponentSource(source, 248);
        Assert.Equal((byte)252, admission.Command);
        Assert.Equal((ushort)1, admission.Action);
        BattlePresentationProcess process = source.Presentation!;
        BattleModelOwner[] actors = Enumerable.Range(3, 3).Select(index => process.FindOriginalActor(NativeBattleModelSlot.Decode(checked((byte)index))) ?? throw new InvalidDataException("The source lacks its authored model operand.")).ToArray();
        Assert.Equal(new[] { 87, 87, 88 }, actors.Select(owner => (int?)process.Graphics.ActorModels.Get(owner).ModelId4 ?? -1).Order().ToArray());
        Assert.Contains(admission.Actor, actors);
        Assert.Equal((byte)87, process.Graphics.ActorModels.Get(admission.Actor).ModelId4);
        BattleActionHeaderReference reference = new(admission.HeaderIndex);
        NativeBattleActionHeaderSlot header = process.ActionPackets.ReadHeader(reference);
        NativeBattleContactPacket contact = Assert.Single(process.ActionPackets.ReadHeaderContacts(reference));
        Assert.Equal(admission.Actor, contact.Primary.Recipient);
        Assert.Equal(0, contact.Primary.Amount6);
        NativeBattleGraphicsState graphics = process.Graphics;
        NativeBattleStageState stage = process.NativeStage!;
        long? callback = source.NativeHostCallbacks;
        int events = source.Events.Count;
        var combat = source.Combatants.Select(actor => (actor.Id, actor.ModelId, actor.SceneSlot, actor.CurrentHp, actor.NativeStatus0, actor.NativeStatus1, actor.IsActive, actor.Hidden, actor.Suspended, actor.Untargetable)).ToArray();
        BattleSession fork = source.ForkForContinuation();
        bool ordered = false;
        for (int frame = 0; frame < 1200 && !ordered; frame++)
        {
            fork.AdvanceNativeGraphicsPresentation(BattlePresentationTiming.ElapsedForCallbacks(1));
            NativeBattleCinematicActorContext? context = fork.Presentation!.NativeEffectMemory?.ScriptedEffects?.PresentationActors;
            ordered = context is not null && context.CurrentActors.SequenceEqual(actors);
        }
        Assert.True(ordered, "The original VM9A must resolve its authored physical actor order3/4/5 through actual native allocations.");
        long orderedCallback = fork.Presentation!.Graphics.Callback;
        fork.AdvanceNativeGraphicsPresentation(BattlePresentationTiming.ElapsedForCallbacks(1));
        Assert.True(fork.Presentation.Graphics.Callback > orderedCallback);
        Assert.Same(graphics, process.Graphics);
        Assert.Same(stage, process.NativeStage);
        Assert.Equal(callback, source.NativeHostCallbacks);
        Assert.Equal(events, source.Events.Count);
        Assert.Equal(header, process.ActionPackets.ReadHeader(reference));
        Assert.Equal(combat, source.Combatants.Select(actor => (actor.Id, actor.ModelId, actor.SceneSlot, actor.CurrentHp, actor.NativeStatus0, actor.NativeStatus1, actor.IsActive, actor.Hidden, actor.Suspended, actor.Untargetable)).ToArray());
    }

    [Fact]
    public void Original_component_rejects_a_different_context_before_world_initialization()
    {
        Assert.Throws<InvalidDataException>(() => NativeBattleComponentSourceTests.PrepareOriginalEffect248Component(null!, new(0, 0, 248)));
    }
}
