using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect64ComponentSourceTests
{
    [Fact]
    public void Installed_component_retains_original_skeleton_stage_control_and_self_geometry_through_casting()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(game is not null, "Select installed original resources with MEMORIA_FF8_GAME_DIR.");
        GameSession session = GameWorld.CreateColdProcess(game!);
        session.StartNewGame();
        using GameWorld world = new(session, game!);
        EffectPreviewRequest request = NativeBattleComponentSourceTests.CreateComponentRequest(64);
        Assert.Equal(124, request.EnemyId);
        Assert.Equal(142, request.StageId);
        Assert.Throws<InvalidDataException>(() => NativeBattleComponentSourceTests.PrepareComponentSource(world, request with { StageId = 0 }));
        BattleSession source = NativeBattleComponentSourceTests.PrepareComponentSource(world, request);
        Assert.Equal(142, source.ReadDefinition().StageId);
        BattlePresentationProcess process = source.Presentation!;
        Assert.NotNull(process.NativeStage!.FinalStage142);
        BattleDiagnosticComponentAdmission admission = GameWorld.ValidateEffectComponentSource(source, 64);
        Assert.Equal((byte)3, admission.OriginalActorIndex);
        Assert.Equal((byte)8, admission.Command);
        Assert.Equal((ushort)353, admission.Action);
        BattleEnemyActorModel owner = Assert.IsType<BattleEnemyActorModel>(admission.Actor);
        BattleCombatant caster = Assert.Single(source.Combatants, actor => actor.Id == owner.Actor.ActorId);
        Assert.Equal(124, caster.ModelId);
        NativeBattlePoseSource pose = world.BattleResources.ReadNativeGraphicsResource(source.GetCombatantDefinition(caster.Id)).PoseSource!;
        Assert.Equal(71, pose.BoneCount);
        Assert.Equal(71, NativeBattleBodyBoneMatrices.Require(process.Graphics.ActorModels.Get(owner), pose).Length);
        BattleActionHeaderReference reference = new(admission.HeaderIndex);
        NativeBattleActionHeaderSlot header = process.ActionPackets.ReadHeader(reference);
        Assert.Equal((byte)16, header.Sequence);
        Assert.Equal((byte)1, header.ReadContactCount());
        NativeBattleContactPacket contact = Assert.Single(process.ActionPackets.ReadHeaderContacts(reference));
        Assert.Equal(owner, contact.Primary.Recipient);
        Assert.Equal(0, contact.Primary.Amount6);
        NativeBattlePresentationStatusMemory statusMemory = source.NativePresentationStatusMemory;
        NativeBattleStatusMirror cached = statusMemory.Rows[caster.RequireNativeActorIndex()];
        Assert.Equal(cached.Saved, contact.Primary.SavedStatuses4);
        Assert.Equal(cached.Battle, contact.Primary.BattleStatuses8);
        Assert.Null(contact.Secondary);
        NativeBattleActionGraphicsState normalized = NativeBattleActionGraphics.Normalize(process.ActionPackets, reference);
        Assert.True(normalized.ResolveTargets(process.Graphics).IsSingle(owner));
        NativeBattleGraphicsState graphics = process.Graphics;
        NativeBattleStageState stage = process.NativeStage!;
        long? callback = source.NativeHostCallbacks;
        var combat = (caster.CurrentHp, caster.NativeStatus0, caster.NativeStatus1, caster.NativePresentationStatus);
        BattleSession fork = source.ForkForContinuation();
        bool castingPrepared = false;
        for (int frame = 0; frame < 120 && !castingPrepared; frame++)
        {
            fork.AdvanceNativeGraphicsPresentation(PresentationTiming.SimulationStep);
            NativeBattleGraphicsState current = fork.Presentation!.Graphics;
            castingPrepared = current.ActionContext is not null && current.ActionGraphics?.Header == reference && current.Descriptors.Tasks.Any(task => (task.Flags & 1) != 0 && task.Kind == NativeBattleDescriptorTaskKind.Action104 && task.Phase >= 3 && current.Descriptors.Packets[task.PacketSlot].Header == reference);
        }
        Assert.True(castingPrepared);
        NativeBattleGraphicsState advanced = fork.Presentation!.Graphics;
        NativeBattleActionGeometry geometry = Assert.IsType<NativeBattleActionGeometry>(advanced.ActionContext);
        Assert.True(geometry.IsSelfTarget(advanced.ActorModels, advanced.NativeBindings));
        Assert.Equal(reference, advanced.ActionGraphics!.Header);
        Assert.DoesNotContain(fork.Events, item => item.Kind is "Damage" or "Healing" or "EffectContact");
        Assert.Same(graphics, process.Graphics);
        Assert.Same(stage, process.NativeStage);
        Assert.Equal(callback, source.NativeHostCallbacks);
        Assert.Equal(header, process.ActionPackets.ReadHeader(reference));
        Assert.Same(statusMemory, source.NativePresentationStatusMemory);
        Assert.Equal(combat, (caster.CurrentHp, caster.NativeStatus0, caster.NativeStatus1, caster.NativePresentationStatus));
    }
}
