using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect209ComponentSourceTests
{
    [Fact]
    public void Installed_component_preserves_original_hidden_caster_and_rebuilds_the_real_stage_variant_destination()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(game is not null, "Select installed original resources with MEMORIA_FF8_GAME_DIR.");
        GameSession session = GameWorld.CreateColdProcess(game!);
        session.StartNewGame();
        using GameWorld world = new(session, game!);
        BattleSession source = NativeBattleComponentSourceTests.PrepareComponentSource(world, NativeBattleComponentSourceTests.CreateComponentRequest(209));
        Assert.Equal(813, source.ReadDefinition().EncounterId);
        Assert.Equal(137, source.ReadDefinition().StageId);
        BattleDiagnosticComponentAdmission admission = GameWorld.ValidateEffectComponentSource(source, 209);
        Assert.Equal((byte)3, admission.OriginalActorIndex);
        Assert.Equal((byte)8, admission.Command);
        Assert.Equal((ushort)319, admission.Action);
        BattlePresentationProcess process = source.Presentation!;
        BattleCombatant actor = Assert.Single(source.Combatants, candidate => candidate.NativeActorIndex == 3);
        Assert.Equal(115, actor.ModelId);
        Assert.True(actor.Hidden);
        Assert.False(actor.Suspended);
        NativeBattlePoseSource pose = world.BattleResources.ReadNativeGraphicsResource(source.GetCombatantDefinition(actor.Id)).PoseSource!;
        Assert.Equal(33, NativeBattleBodyBoneMatrices.Require(process.Graphics.ActorModels.Get(admission.Actor), pose).Length);
        BattleActionHeaderReference reference = new(admission.HeaderIndex);
        NativeBattleActionHeaderSlot header = process.ActionPackets.ReadHeader(reference);
        Assert.Equal((byte)13, header.Sequence);
        NativeBattleContactPacket contact = Assert.Single(process.ActionPackets.ReadHeaderContacts(reference));
        Assert.Equal(process.FindOriginalActor(NativeBattleModelSlot.Decode(0)), contact.Primary.Recipient);
        Assert.Equal(0, contact.Primary.Amount6);
        Assert.Equal(source.NativePresentationStatusMemory.Rows[0].Saved, contact.Primary.SavedStatuses4);
        Assert.Equal(source.NativePresentationStatusMemory.Rows[0].Battle, contact.Primary.BattleStatuses8);
        Assert.Null(contact.Secondary);
        NativeBattleGraphicsState graphics = process.Graphics;
        NativeBattleStageState stage = process.NativeStage!;
        NativeBattleStageVariant137State variants = stage.Extended!.Stage137.Variants!;
        Assert.True(variants.GeometryAddress > 0);
        Assert.Equal(variants.GeometryAddress, stage.SourceAddress1D9A820);
        int initialIndex = variants.ReadIndex();
        long? callback = source.NativeHostCallbacks;
        int events = source.Events.Count;
        var combat = source.Combatants.Select(candidate => (candidate.Id, candidate.CurrentHp, candidate.NativeStatus0, candidate.NativeStatus1, candidate.IsActive, candidate.Hidden, candidate.Untargetable, candidate.Suspended)).ToArray();
        BattleSession fork = source.ForkForContinuation();
        bool rebuilt = false;
        for (int frame = 0; frame < 1200 && !rebuilt; frame++)
        {
            fork.AdvanceNativeGraphicsPresentation(BattlePresentationTiming.ElapsedForCallbacks(1));
            rebuilt = fork.Presentation!.NativeStage!.Extended!.Stage137.Variants!.ReadIndex() != initialIndex;
        }
        Assert.True(rebuilt, "The actual effect callback must rebuild its loaded stage variant into the initialized physical destination.");
        NativeBattleStageState rebuiltStage = fork.Presentation!.NativeStage!;
        Assert.Equal((initialIndex + 1) % 10, rebuiltStage.Extended!.Stage137.Variants!.ReadIndex());
        Assert.Equal(variants.GeometryAddress, rebuiltStage.Extended.Stage137.Variants.GeometryAddress);
        Assert.Equal(variants.GeometryAddress, rebuiltStage.SourceAddress1D9A820);
        Assert.NotSame(stage, rebuiltStage);
        Assert.Same(graphics, process.Graphics);
        Assert.Same(stage, process.NativeStage);
        Assert.Equal(initialIndex, stage.Extended.Stage137.Variants!.ReadIndex());
        Assert.Equal(callback, source.NativeHostCallbacks);
        Assert.Equal(events, source.Events.Count);
        Assert.Equal(header, process.ActionPackets.ReadHeader(reference));
        Assert.Equal(combat, source.Combatants.Select(candidate => (candidate.Id, candidate.CurrentHp, candidate.NativeStatus0, candidate.NativeStatus1, candidate.IsActive, candidate.Hidden, candidate.Untargetable, candidate.Suspended)).ToArray());
    }
}
