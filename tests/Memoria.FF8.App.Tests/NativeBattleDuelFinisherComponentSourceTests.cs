using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleDuelFinisherComponentSourceTests
{
    [Theory]
    [InlineData(215, 9)]
    [InlineData(216, 8)]
    public void Installed_finisher_retains_original_Zell_header_and_survives_actual_resource_rebinding(ushort effect, ushort action)
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(game is not null, "Select installed original resources with MEMORIA_FF8_GAME_DIR.");
        GameSession session = GameWorld.CreateColdProcess(game!);
        session.StartNewGame();
        using GameWorld world = new(session, game!);
        string records = JsonSerializer.Serialize(session.State.CharacterVitals!.Records);
        BattleSession source = NativeBattleComponentSourceTests.PrepareComponentSource(world, NativeBattleComponentSourceTests.CreateComponentRequest(effect));
        Assert.Equal(records, JsonSerializer.Serialize(session.State.CharacterVitals!.Records));
        BattleDiagnosticComponentAdmission admission = GameWorld.ValidateEffectComponentSource(source, effect);
        Assert.Equal((byte)0, admission.OriginalActorIndex);
        Assert.Equal((byte)239, admission.Command);
        Assert.Equal(action, admission.Action);
        BattlePresentationProcess process = source.Presentation!;
        BattleCombatant caster = Assert.Single(source.Combatants, actor => actor.NativeActorIndex == 0);
        Assert.Equal(1, caster.ModelId);
        Assert.Null(process.Graphics.ActorModels.Get(admission.Actor).LinkedTransform);
        BattleActionHeaderReference reference = new(admission.HeaderIndex);
        NativeBattleActionHeaderSlot header = process.ActionPackets.ReadHeader(reference);
        Assert.Equal(NativeBattleActionGraphics.ResolvePlayerSequence(239, 0, action, source.ReadDefinition().NativeCommandSequences), header.Sequence);
        NativeBattleContactPacket contact = Assert.Single(process.ActionPackets.ReadHeaderContacts(reference));
        Assert.Equal(process.FindOriginalActor(NativeBattleModelSlot.Decode(3)), contact.Primary.Recipient);
        Assert.Equal(0, contact.Primary.Amount6);
        Assert.Equal(source.NativePresentationStatusMemory.Rows[3].Saved, contact.Primary.SavedStatuses4);
        Assert.Equal(source.NativePresentationStatusMemory.Rows[3].Battle, contact.Primary.BattleStatuses8);
        Assert.Null(contact.Secondary);
        NativeBattleGraphicsState graphics = process.Graphics;
        NativeBattleStageState stage = process.NativeStage!;
        long? callback = source.NativeHostCallbacks;
        int events = source.Events.Count;
        var combat = (caster.CurrentHp, caster.NativeStatus0, caster.NativeStatus1, caster.NativePresentationStatus);
        BattleSession fork = source.ForkForContinuation();
        uint descriptor = effect == 215 ? NativeBattleDuel215Controller.Scene : NativeBattleDuel216Controller.Main;
        uint? expectedRow = null;
        bool rebound = false;
        for (int frame = 0; frame < 1200 && !rebound; frame++)
        {
            fork.AdvanceNativeGraphicsPresentation(BattlePresentationTiming.ElapsedForCallbacks(1));
            NativeBattleGraphicsState current = fork.Presentation!.Graphics;
            NativeBattleEffectTaskPool? pool = current.OriginalEffect?.Pools.FirstOrDefault(candidate => candidate.DescriptorAddress == descriptor);
            if (pool is not null && (pool.Slots[0].Flags & 1) != 0)
            {
                expectedRow = checked(pool.BankAddress + 16);
                rebound = current.ActorModels.Get(admission.Actor).ActiveStagedResourceRow100 == expectedRow;
            }
        }
        Assert.True(rebound, "The original finisher must reach its actual task-owned caster resource binding.");
        long reboundCallback = fork.Presentation!.Graphics.Callback;
        fork.AdvanceNativeGraphicsPresentation(BattlePresentationTiming.ElapsedForCallbacks(1));
        NativeBattleGraphicsModel sampled = fork.Presentation.Graphics.ActorModels.Get(admission.Actor);
        Assert.True(fork.Presentation.Graphics.Callback > reboundCallback);
        Assert.Equal(expectedRow, sampled.ActiveStagedResourceRow100);
        Assert.NotNull(sampled.CurrentBodyBoneMatrices);
        Assert.NotNull(sampled.SequenceState!.Pose);
        Assert.Same(graphics, process.Graphics);
        Assert.Same(stage, process.NativeStage);
        Assert.Equal(callback, source.NativeHostCallbacks);
        Assert.Equal(events, source.Events.Count);
        Assert.Equal(header, process.ActionPackets.ReadHeader(reference));
        Assert.Equal(combat, (caster.CurrentHp, caster.NativeStatus0, caster.NativeStatus1, caster.NativePresentationStatus));
    }

    [Theory]
    [InlineData(215)]
    [InlineData(216)]
    public void Original_finisher_rejects_a_different_caster_before_source_initialization(ushort effect)
    {
        Assert.Throws<InvalidDataException>(() => NativeBattleComponentSourceTests.PrepareOriginalDuelFinisherComponent(null!, new(0, 0, effect)));
    }
}
