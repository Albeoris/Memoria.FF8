using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect112ComponentSourceTests
{
    [Theory]
    [InlineData(112, 7, 0x2521950u)]
    [InlineData(212, 6, NativeBattleDuel212Controller.Targets)]
    public void Installed_component_retains_original_Zell_finisher_through_Duel_override_and_next_pose_callback(ushort effect, ushort action, uint targetDescriptor)
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
        Assert.Equal((byte)239, admission.Command);
        Assert.Equal(action, admission.Action);
        BattlePresentationProcess process = source.Presentation!;
        BattleCombatant caster = Assert.Single(source.Combatants, actor => actor.NativeActorIndex == 0);
        Assert.Equal(1, caster.ModelId);
        Assert.Null(process.Graphics.ActorModels.Get(admission.Actor).LinkedTransform);
        BattleActionHeaderReference reference = new(admission.HeaderIndex);
        NativeBattleActionHeaderSlot header = process.ActionPackets.ReadHeader(reference);
        Assert.Equal((byte)28, header.Sequence);
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
        bool rebound = false;
        uint? expectedRow = null;
        for (int frame = 0; frame < 1200 && !rebound; frame++)
        {
            fork.AdvanceNativeGraphicsPresentation(BattlePresentationTiming.ElapsedForCallbacks(1));
            NativeBattleGraphicsState current = fork.Presentation!.Graphics;
            NativeBattleEffectTaskPool? targets = current.OriginalEffect?.Pools.FirstOrDefault(pool => pool.DescriptorAddress == targetDescriptor);
            if (targets is not null)
            {
                for (int slot = 0; slot < targets.Slots.Length; slot++)
                {
                    if ((targets.Slots[slot].Flags & 1) == 0)
                    {
                        continue;
                    }
                    expectedRow = checked(targets.BankAddress + (uint)(slot * targets.Stride) + 16);
                    rebound = current.ActorModels.Get(admission.Actor).ActiveStagedResourceRow100 == expectedRow;
                    break;
                }
            }
        }
        NativeBattleGraphicsState final = fork.Presentation!.Graphics;
        Assert.True(rebound, $"The real Duel target callback must bind its task-owned caster resource. Callback={final.Callback}, flags={final.Flags:X}, effect={final.OriginalEffect?.RequestedEffect}, row={final.ActorModels.Get(admission.Actor).ActiveStagedResourceRow100:X}, expected={expectedRow:X}.");
        long reboundCallback = final.Callback;
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
}
