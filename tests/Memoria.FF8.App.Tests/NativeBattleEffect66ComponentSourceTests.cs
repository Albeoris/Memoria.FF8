using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect66ComponentSourceTests
{
    [Fact]
    public void Installed_component_retains_original_skeleton_stage_and_neutral_party_contact()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(game is not null, "Select installed original resources with MEMORIA_FF8_GAME_DIR.");
        GameSession session = GameWorld.CreateColdProcess(game!);
        session.StartNewGame();
        using GameWorld world = new(session, game!);
        EffectPreviewRequest request = NativeBattleComponentSourceTests.CreateComponentRequest(66);
        Assert.Equal(124, request.EnemyId);
        Assert.Equal(142, request.StageId);
        Assert.Throws<InvalidDataException>(() => NativeBattleComponentSourceTests.PrepareComponentSource(world, request with { StageId = 0 }));
        BattleSession source = NativeBattleComponentSourceTests.PrepareComponentSource(world, request);
        Assert.Equal(142, source.ReadDefinition().StageId);
        BattlePresentationProcess process = source.Presentation!;
        Assert.NotNull(process.NativeStage!.FinalStage142);
        Assert.Equal(4, process.NativeStage.Groups.Length);
        BattleDiagnosticComponentAdmission admission = GameWorld.ValidateEffectComponentSource(source, 66);
        Assert.Equal((byte)3, admission.OriginalActorIndex);
        Assert.Equal((byte)8, admission.Command);
        Assert.Equal((ushort)354, admission.Action);
        BattleEnemyActorModel owner = Assert.IsType<BattleEnemyActorModel>(admission.Actor);
        BattleCombatant caster = Assert.Single(source.Combatants, actor => actor.Id == owner.Actor.ActorId);
        Assert.Equal(124, caster.ModelId);
        NativeBattlePoseSource pose = world.BattleResources.ReadNativeGraphicsResource(source.GetCombatantDefinition(caster.Id)).PoseSource!;
        Assert.Equal(71, pose.BoneCount);
        Assert.Equal(71, NativeBattleBodyBoneMatrices.Require(process.Graphics.ActorModels.Get(owner), pose).Length);
        BattleActionHeaderReference reference = new(admission.HeaderIndex);
        NativeBattleActionHeaderSlot header = process.ActionPackets.ReadHeader(reference);
        Assert.Equal((byte)17, header.Sequence);
        NativeBattleContactPacket contact = Assert.Single(process.ActionPackets.ReadHeaderContacts(reference));
        Assert.Equal(process.FindOriginalActor(NativeBattleModelSlot.Decode(0)), contact.Primary.Recipient);
        Assert.Equal(0, contact.Primary.Amount6);
        Assert.Null(contact.Secondary);
        NativeBattleGraphicsState graphics = process.Graphics;
        NativeBattleStageState stage = process.NativeStage!;
        long? callback = source.NativeHostCallbacks;
        BattleSession fork = source.ForkForContinuation();
        fork.AdvanceNativeGraphicsPresentation(PresentationTiming.SimulationStep);
        Assert.Same(graphics, process.Graphics);
        Assert.Same(stage, process.NativeStage);
        Assert.Equal(callback, source.NativeHostCallbacks);
        Assert.Equal(header, process.ActionPackets.ReadHeader(reference));
    }
}
