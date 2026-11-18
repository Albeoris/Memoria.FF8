using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect92ComponentSourceTests
{
    [Fact]
    public void Installed_component_retains_Search_Rinoa_recipient_through_target_override_and_next_pose_callback()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(game is not null, "Select installed original resources with MEMORIA_FF8_GAME_DIR.");
        GameSession session = GameWorld.CreateColdProcess(game!);
        session.StartNewGame();
        using GameWorld world = new(session, game!);
        string originalRecords = JsonSerializer.Serialize(session.State.CharacterVitals!.Records);
        BattleSession source = NativeBattleComponentSourceTests.PrepareComponentSource(world, NativeBattleComponentSourceTests.CreateComponentRequest(92));
        Assert.Equal(originalRecords, JsonSerializer.Serialize(session.State.CharacterVitals!.Records));
        BattleDiagnosticComponentAdmission admission = GameWorld.ValidateEffectComponentSource(source, 92);
        Assert.Equal((byte)0, admission.OriginalActorIndex);
        Assert.Equal((byte)240, admission.Command);
        Assert.Equal((ushort)14, admission.Action);
        BattlePresentationProcess process = source.Presentation!;
        BattleCombatant target = Assert.Single(source.Combatants, actor => actor.IsPlayer && actor.IsActive);
        Assert.Equal(4, target.ModelId);
        Assert.Equal(0, target.NativeActorIndex);
        BattleActionHeaderReference reference = new(admission.HeaderIndex);
        NativeBattleActionHeaderSlot header = process.ActionPackets.ReadHeader(reference);
        Assert.Equal((byte)0, header.Sequence);
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
        var combat = (target.CurrentHp, target.NativeStatus0, target.NativeStatus1, target.NativePresentationStatus);
        BattleSession fork = source.ForkForContinuation();
        bool rebound = false;
        for (int frame = 0; frame < 1200 && !rebound; frame++)
        {
            fork.AdvanceNativeGraphicsPresentation(BattlePresentationTiming.ElapsedForCallbacks(1));
            rebound = fork.Presentation!.Graphics.ActorModels.Get(admission.Actor).ActiveStagedResourceRow100 == 0x2598EF8;
        }
        NativeBattleGraphicsState current = fork.Presentation!.Graphics;
        Assert.True(rebound, $"The actual Search model callback must bind its original recipient override. Callback={current.Callback}, flags={current.Flags:X}, effect={current.OriginalEffect?.RequestedEffect}, row={current.ActorModels.Get(admission.Actor).ActiveStagedResourceRow100:X}.");
        long reboundCallback = current.Callback;
        fork.AdvanceNativeGraphicsPresentation(BattlePresentationTiming.ElapsedForCallbacks(1));
        NativeBattleGraphicsModel sampled = fork.Presentation.Graphics.ActorModels.Get(admission.Actor);
        Assert.True(fork.Presentation.Graphics.Callback > reboundCallback);
        Assert.Equal(0x2598EF8u, sampled.ActiveStagedResourceRow100);
        Assert.NotNull(sampled.CurrentBodyBoneMatrices);
        Assert.NotNull(sampled.SequenceState!.Pose);
        Assert.Same(graphics, process.Graphics);
        Assert.Same(stage, process.NativeStage);
        Assert.Equal(callback, source.NativeHostCallbacks);
        Assert.Equal(events, source.Events.Count);
        Assert.Equal(header, process.ActionPackets.ReadHeader(reference));
        Assert.Equal(combat, (target.CurrentHp, target.NativeStatus0, target.NativeStatus1, target.NativePresentationStatus));
    }
}
