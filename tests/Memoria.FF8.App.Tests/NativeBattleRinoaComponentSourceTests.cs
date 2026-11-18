using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleRinoaComponentSourceTests
{
    [Theory]
    [InlineData(86, 0, 3, 0x25BC1F8u)]
    [InlineData(87, 1, 3, 0x25B4B28u)]
    [InlineData(88, 2, 0, 0x25ACD10u)]
    public void Installed_component_retains_original_Rinoa_limit_target_through_override_and_next_pose_callback(ushort effect, ushort action, byte targetIndex, uint overrideRow)
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(game is not null, "Select installed original resources with MEMORIA_FF8_GAME_DIR.");
        GameSession session = GameWorld.CreateColdProcess(game!);
        session.StartNewGame();
        using GameWorld world = new(session, game!);
        string originalRecords = JsonSerializer.Serialize(session.State.CharacterVitals!.Records);
        BattleSession source = NativeBattleComponentSourceTests.PrepareComponentSource(world, NativeBattleComponentSourceTests.CreateComponentRequest(effect));
        Assert.Equal(originalRecords, JsonSerializer.Serialize(session.State.CharacterVitals!.Records));
        BattleDiagnosticComponentAdmission admission = GameWorld.ValidateEffectComponentSource(source, effect);
        Assert.Equal((byte)0, admission.OriginalActorIndex);
        Assert.Equal((byte)19, admission.Command);
        Assert.Equal(action, admission.Action);
        BattlePresentationProcess process = source.Presentation!;
        BattleCombatant caster = Assert.Single(source.Combatants, actor => actor.NativeActorIndex == 0);
        Assert.Equal(4, caster.ModelId);
        BattleModelOwner recipient = process.FindOriginalActor(NativeBattleModelSlot.Decode(targetIndex))!;
        BattleActionHeaderReference reference = new(admission.HeaderIndex);
        NativeBattleActionHeaderSlot header = process.ActionPackets.ReadHeader(reference);
        Assert.Equal((byte)21, header.Sequence);
        Assert.Equal(NativeBattleActionGraphics.ResolvePlayerSequence(19, 0, action, source.ReadDefinition().NativeCommandSequences), header.Sequence);
        NativeBattleContactPacket contact = Assert.Single(process.ActionPackets.ReadHeaderContacts(reference));
        Assert.Equal(recipient, contact.Primary.Recipient);
        Assert.Equal(0, contact.Primary.Amount6);
        Assert.Equal(source.NativePresentationStatusMemory.Rows[targetIndex].Saved, contact.Primary.SavedStatuses4);
        Assert.Equal(source.NativePresentationStatusMemory.Rows[targetIndex].Battle, contact.Primary.BattleStatuses8);
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
            rebound = fork.Presentation!.Graphics.ActorModels.Get(admission.Actor).ActiveStagedResourceRow100 == overrideRow;
        }
        NativeBattleGraphicsState current = fork.Presentation!.Graphics;
        Assert.True(rebound, $"The actual body callback must bind its original caster resource. Effect={effect}, callback={current.Callback}, flags={current.Flags:X}, selected={current.OriginalEffect?.RequestedEffect}, row={current.ActorModels.Get(admission.Actor).ActiveStagedResourceRow100:X}.");
        long reboundCallback = current.Callback;
        fork.AdvanceNativeGraphicsPresentation(BattlePresentationTiming.ElapsedForCallbacks(1));
        NativeBattleGraphicsModel sampled = fork.Presentation.Graphics.ActorModels.Get(admission.Actor);
        Assert.True(fork.Presentation.Graphics.Callback > reboundCallback);
        Assert.Equal(overrideRow, sampled.ActiveStagedResourceRow100);
        Assert.NotNull(sampled.CurrentBodyBoneMatrices);
        Assert.NotNull(sampled.SequenceState!.Pose);
        Assert.Same(graphics, process.Graphics);
        Assert.Same(stage, process.NativeStage);
        Assert.Equal(callback, source.NativeHostCallbacks);
        Assert.Equal(events, source.Events.Count);
        Assert.Equal(header, process.ActionPackets.ReadHeader(reference));
        Assert.Equal(combat, (caster.CurrentHp, caster.NativeStatus0, caster.NativeStatus1, caster.NativePresentationStatus));
    }
}
