using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect110ComponentSourceTests
{
    [Fact]
    public void Installed_component_retains_original_encounter_actors_and_both_process_owned_text_banks()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(game is not null, "Select installed original resources with MEMORIA_FF8_GAME_DIR.");
        GameSession session = GameWorld.CreateColdProcess(game!);
        session.StartNewGame();
        using GameWorld world = new(session, game!);
        BattleSession source = NativeBattleComponentSourceTests.PrepareComponentSource(world, NativeBattleComponentSourceTests.CreateComponentRequest(110));
        Assert.Equal(29, source.ReadDefinition().EncounterId);
        Assert.Equal(3, source.ReadDefinition().StageId);
        BattleDiagnosticComponentAdmission admission = GameWorld.ValidateEffectComponentSource(source, 110);
        Assert.Equal((byte)3, admission.OriginalActorIndex);
        Assert.Equal((byte)252, admission.Command);
        Assert.Equal((ushort)1, admission.Action);
        BattlePresentationProcess process = source.Presentation!;
        BattleCombatant biggs = Assert.Single(source.Combatants, actor => actor.NativeActorIndex == 3);
        BattleCombatant wedge = Assert.Single(source.Combatants, actor => actor.NativeActorIndex == 4);
        Assert.Equal(74, biggs.ModelId);
        Assert.Equal(73, wedge.ModelId);
        BattleActionHeaderReference reference = new(admission.HeaderIndex);
        NativeBattleActionHeaderSlot header = process.ActionPackets.ReadHeader(reference);
        Assert.Equal((byte)0, header.Sequence);
        NativeBattleContactPacket[] contacts = process.ActionPackets.ReadHeaderContacts(reference).ToArray();
        Assert.Equal(2, contacts.Length);
        for (byte ordinal = 0; ordinal < 2; ordinal++)
        {
            byte index = checked((byte)(3 + ordinal));
            Assert.Equal(process.FindOriginalActor(NativeBattleModelSlot.Decode(index)), contacts[ordinal].Primary.Recipient);
            Assert.Equal(0, contacts[ordinal].Primary.Amount6);
            Assert.Equal(source.NativePresentationStatusMemory.Rows[index].Saved, contacts[ordinal].Primary.SavedStatuses4);
            Assert.Equal(source.NativePresentationStatusMemory.Rows[index].Battle, contacts[ordinal].Primary.BattleStatuses8);
            Assert.Null(contacts[ordinal].Secondary);
        }
        NativeBattleGraphicsState graphics = process.Graphics;
        NativeBattleStageState stage = process.NativeStage!;
        long? callback = source.NativeHostCallbacks;
        int events = source.Events.Count;
        var actors = source.Combatants.Select(actor => (actor.Id, actor.IsActive, actor.Hidden, actor.Suspended, actor.Untargetable, actor.CurrentHp, actor.NativeStatus0, actor.NativeStatus1)).ToArray();
        BattleSession fork = source.ForkForContinuation();
        int firstEvent = fork.Events.Count;
        bool observed = false;
        for (int frame = 0; frame < 1200 && !observed; frame++)
        {
            fork.AdvanceNativeGraphicsPresentation(BattlePresentationTiming.ElapsedForCallbacks(1));
            observed = HasText(biggs.Id, biggs.NativeAi!.Texts[10]) && HasText(wedge.Id, wedge.NativeAi!.Texts[1]);
        }
        Assert.True(observed, "The actual target callbacks must publish both original actor messages.");
        Assert.Same(graphics, process.Graphics);
        Assert.Same(stage, process.NativeStage);
        Assert.Equal(callback, source.NativeHostCallbacks);
        Assert.Equal(events, source.Events.Count);
        Assert.Equal(header, process.ActionPackets.ReadHeader(reference));
        Assert.Equal(actors, source.Combatants.Select(actor => (actor.Id, actor.IsActive, actor.Hidden, actor.Suspended, actor.Untargetable, actor.CurrentHp, actor.NativeStatus0, actor.NativeStatus1)).ToArray());

        bool HasText(string actor, string text)
        {
            return fork.Events.Skip(firstEvent).Any(item => item.Kind == "NativeText" && item.ActorId == actor && item.Message == text && item.Amount == 30 && item.NativeMessageMode == 3);
        }
    }
}
