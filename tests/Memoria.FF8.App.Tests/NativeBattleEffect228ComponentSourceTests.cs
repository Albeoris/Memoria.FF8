using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect228ComponentSourceTests
{
    [Fact]
    public void Installed_component_retains_original_Edea_limit_and_survives_actual_actor_dat_binding()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(game is not null, "Select installed original resources with MEMORIA_FF8_GAME_DIR.");
        GameSession session = GameWorld.CreateColdProcess(game!);
        session.StartNewGame();
        using GameWorld world = new(session, game!);
        string records = JsonSerializer.Serialize(session.State.CharacterVitals!.Records);
        BattleSession source = NativeBattleComponentSourceTests.PrepareComponentSource(world, NativeBattleComponentSourceTests.CreateComponentRequest(228));
        Assert.Equal(records, JsonSerializer.Serialize(session.State.CharacterVitals!.Records));
        BattleNativeAction action = Assert.Single(world.BattleResources.NativeActions, candidate => candidate.Command == 18 && candidate.CharacterId == 7 && candidate.MagicId == 228 && candidate.KernelSection == 18);
        BattleDiagnosticComponentAdmission admission = GameWorld.ValidateEffectComponentSource(source, 228);
        Assert.Equal((byte)0, admission.OriginalActorIndex);
        Assert.Equal(action.Command, admission.Command);
        Assert.Equal(action.ActionId, admission.Action);
        BattlePresentationProcess process = source.Presentation!;
        BattleCombatant caster = Assert.Single(source.Combatants, actor => actor.NativeActorIndex == 0);
        Assert.Equal(7, caster.ModelId);
        BattleActionHeaderReference reference = new(admission.HeaderIndex);
        NativeBattleActionHeaderSlot header = process.ActionPackets.ReadHeader(reference);
        Assert.Equal(NativeBattleActionGraphics.ResolvePlayerSequence(action.Command, 0, action.ActionId, source.ReadDefinition().NativeCommandSequences), header.Sequence);
        var contacts = process.ActionPackets.ReadHeaderContacts(reference);
        Assert.Equal(action.HitCount, contacts.Length);
        foreach (NativeBattleContactPacket contact in contacts)
        {
            Assert.Equal(process.FindOriginalActor(NativeBattleModelSlot.Decode(3)), contact.Primary.Recipient);
            Assert.Equal(0, contact.Primary.Amount6);
            Assert.Equal(source.NativePresentationStatusMemory.Rows[3].Saved, contact.Primary.SavedStatuses4);
            Assert.Equal(source.NativePresentationStatusMemory.Rows[3].Battle, contact.Primary.BattleStatuses8);
            Assert.Null(contact.Secondary);
        }
        NativeBattleGraphicsState graphics = process.Graphics;
        NativeBattleStageState stage = process.NativeStage!;
        long? callback = source.NativeHostCallbacks;
        int events = source.Events.Count;
        var combat = (caster.CurrentHp, caster.NativeStatus0, caster.NativeStatus1, caster.NativePresentationStatus);
        BattleSession fork = source.ForkForContinuation();
        bool rebound = false;
        for (int frame = 0; frame < 1200 && !rebound; frame++)
        {
            fork.AdvanceNativeGraphicsPresentation(BattlePresentationTiming.ElapsedForCallbacks(1));
            NativeBattleGraphicsModel current = fork.Presentation!.Graphics.ActorModels.Get(admission.Actor);
            rebound = current.ActiveAnimationDirectory == 0x18821A0 && current.ActiveStagedResourceRow100 is not null;
        }
        Assert.True(rebound, "The original limit must bind its actual embedded actor DAT through the cinematic actor resource instruction.");
        long reboundCallback = fork.Presentation!.Graphics.Callback;
        fork.AdvanceNativeGraphicsPresentation(BattlePresentationTiming.ElapsedForCallbacks(1));
        NativeBattleGraphicsModel sampled = fork.Presentation.Graphics.ActorModels.Get(admission.Actor);
        Assert.True(fork.Presentation.Graphics.Callback > reboundCallback);
        Assert.Equal(0x18821A0u, sampled.ActiveAnimationDirectory);
        Assert.NotNull(sampled.CurrentBodyBoneMatrices);
        Assert.NotNull(sampled.SequenceState!.Pose);
        Assert.Same(graphics, process.Graphics);
        Assert.Same(stage, process.NativeStage);
        Assert.Equal(callback, source.NativeHostCallbacks);
        Assert.Equal(events, source.Events.Count);
        Assert.Equal(header, process.ActionPackets.ReadHeader(reference));
        Assert.Equal(combat, (caster.CurrentHp, caster.NativeStatus0, caster.NativeStatus1, caster.NativePresentationStatus));
    }

    [Fact]
    public void Original_limit_rejects_a_different_caster_before_source_initialization()
    {
        Assert.Throws<InvalidDataException>(() => NativeBattleComponentSourceTests.PrepareOriginalEffect228Component(null!, new(0, 0, 228)));
    }
}
