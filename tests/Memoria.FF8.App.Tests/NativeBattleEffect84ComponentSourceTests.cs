using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect84ComponentSourceTests
{
    [Fact]
    public void Installed_component_retains_Rinoa_self_target_through_original_resource_override_and_pose_sampling()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(game is not null, "Select installed original resources with MEMORIA_FF8_GAME_DIR.");
        GameSession session = GameWorld.CreateColdProcess(game!);
        session.StartNewGame();
        using GameWorld world = new(session, game!);
        NativeCharacterVitals originalVitals = session.State.CharacterVitals!;
        int row = Assert.Single(originalVitals.Records.Select((record, index) => (record, index)), candidate => candidate.record.CharacterId == 4).index;
        Assert.NotEqual(0, row);
        string originalRecords = JsonSerializer.Serialize(originalVitals.Records);
        BattleSession source = NativeBattleComponentSourceTests.PrepareComponentSource(world, NativeBattleComponentSourceTests.CreateComponentRequest(84));
        Assert.Equal(new byte[] { checked((byte)row), 255, 255 }, session.State.CharacterVitals!.CombatParty);
        Assert.Equal(originalRecords, JsonSerializer.Serialize(session.State.CharacterVitals.Records));
        BattleDiagnosticComponentAdmission admission = GameWorld.ValidateEffectComponentSource(source, 84);
        Assert.Equal((byte)0, admission.OriginalActorIndex);
        Assert.Equal((byte)19, admission.Command);
        Assert.Equal((ushort)4, admission.Action);
        BattlePresentationProcess process = source.Presentation!;
        BattleCombatant caster = Assert.Single(source.Combatants, actor => actor.NativeActorIndex == 0);
        Assert.Equal(4, caster.ModelId);
        BattleActionHeaderReference reference = new(admission.HeaderIndex);
        NativeBattleActionHeaderSlot header = process.ActionPackets.ReadHeader(reference);
        Assert.Equal(NativeBattleActionGraphics.ResolvePlayerSequence(19, 0, 4, source.ReadDefinition().NativeCommandSequences), header.Sequence);
        Assert.Equal((byte)21, header.Sequence);
        NativeBattleContactPacket contact = Assert.Single(process.ActionPackets.ReadHeaderContacts(reference));
        Assert.Equal(admission.Actor, contact.Primary.Recipient);
        Assert.Equal(0, contact.Primary.Amount6);
        Assert.Equal(source.NativePresentationStatusMemory.Rows[0].Saved, contact.Primary.SavedStatuses4);
        Assert.Equal(source.NativePresentationStatusMemory.Rows[0].Battle, contact.Primary.BattleStatuses8);
        Assert.Null(contact.Secondary);
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
            rebound = fork.Presentation!.Graphics.ActorModels.Get(admission.Actor).ActiveStagedResourceRow100 == 0x25C9F10;
        }
        NativeBattleGraphicsState beforeSample = fork.Presentation!.Graphics;
        Assert.True(rebound, $"The actual body callback must bind its original caster override. Callback={beforeSample.Callback}, flags={beforeSample.Flags:X}, effect={beforeSample.OriginalEffect?.RequestedEffect}, row={beforeSample.ActorModels.Get(admission.Actor).ActiveStagedResourceRow100:X}.");
        long reboundCallback = fork.Presentation!.Graphics.Callback;
        fork.AdvanceNativeGraphicsPresentation(BattlePresentationTiming.ElapsedForCallbacks(1));
        NativeBattleGraphicsModel sampled = fork.Presentation!.Graphics.ActorModels.Get(admission.Actor);
        Assert.True(fork.Presentation.Graphics.Callback > reboundCallback, $"The pose check requires a subsequent actual native callback. Before={reboundCallback}, after={fork.Presentation.Graphics.Callback}, effect={fork.Presentation.Graphics.OriginalEffect?.RequestedEffect}, row={sampled.ActiveStagedResourceRow100:X}, sequence={sampled.Sequence}, pose={sampled.SequenceState?.Pose}.");
        Assert.Equal(0x25C9F10u, sampled.ActiveStagedResourceRow100);
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
    public void Installed_default_preview_retains_Squall_saved_row_and_character_records()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(game is not null, "Select installed original resources with MEMORIA_FF8_GAME_DIR.");
        GameSession session = GameWorld.CreateColdProcess(game!);
        session.StartNewGame();
        using GameWorld world = new(session, game!);
        NativeCharacterVitals originalVitals = session.State.CharacterVitals!;
        int row = Assert.Single(originalVitals.Records.Select((record, index) => (record, index)), candidate => candidate.record.CharacterId == 0).index;
        Assert.Equal(0, row);
        string originalRecords = JsonSerializer.Serialize(originalVitals.Records);
        BattleSession source = world.StartEffectPreview(new(0, 0, 1));
        Assert.Equal(0, Assert.Single(source.Combatants, actor => actor.IsPlayer && actor.NativeActorIndex == 0).ModelId);
        Assert.Equal(new byte[] { 0, 255, 255 }, session.State.CharacterVitals!.CombatParty);
        Assert.Equal(originalRecords, JsonSerializer.Serialize(session.State.CharacterVitals.Records));
    }
}
