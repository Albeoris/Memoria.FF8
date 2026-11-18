using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect79ComponentSourceTests
{
    [Fact]
    public void Installed_component_retains_original_caster_and_hidden_companion_through_casting()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(game is not null, "Select installed original resources with MEMORIA_FF8_GAME_DIR.");
        GameSession session = GameWorld.CreateColdProcess(game!);
        session.StartNewGame();
        using GameWorld world = new(session, game!);
        BattleSession source = NativeBattleComponentSourceTests.PrepareComponentSource(world, NativeBattleComponentSourceTests.CreateComponentRequest(79));
        Assert.Equal(509, source.ReadDefinition().EncounterId);
        Assert.Equal(142, source.ReadDefinition().StageId);
        BattleDiagnosticComponentAdmission admission = GameWorld.ValidateEffectComponentSource(source, 79);
        Assert.Equal((byte)4, admission.OriginalActorIndex);
        Assert.Equal((byte)8, admission.Command);
        Assert.Equal((ushort)351, admission.Action);
        BattlePresentationProcess process = source.Presentation!;
        BattleCombatant caster = Assert.Single(source.Combatants, actor => actor.NativeActorIndex == 4);
        BattleCombatant target = Assert.Single(source.Combatants, actor => actor.NativeActorIndex == 3);
        Assert.Equal(122, caster.ModelId);
        Assert.Equal(123, target.ModelId);
        Assert.True(target.IsActive);
        Assert.True(target.Hidden);
        Assert.False(target.Suspended);
        Assert.True(target.Untargetable);
        BattleModelOwner recipient = process.FindOriginalActor(NativeBattleModelSlot.Decode(3))!;
        NativeBattlePoseSource pose = world.BattleResources.ReadNativeGraphicsResource(source.GetCombatantDefinition(caster.Id)).PoseSource!;
        Assert.Equal(70, pose.BoneCount);
        Assert.Equal(70, NativeBattleBodyBoneMatrices.Require(process.Graphics.ActorModels.Get(admission.Actor), pose).Length);
        BattleActionHeaderReference reference = new(admission.HeaderIndex);
        NativeBattleActionHeaderSlot header = process.ActionPackets.ReadHeader(reference);
        Assert.Equal((byte)15, header.Sequence);
        NativeBattleContactPacket contact = Assert.Single(process.ActionPackets.ReadHeaderContacts(reference));
        Assert.Equal(recipient, contact.Primary.Recipient);
        Assert.Equal(0, contact.Primary.Amount6);
        Assert.Equal(source.NativePresentationStatusMemory.Rows[3].Saved, contact.Primary.SavedStatuses4);
        Assert.Equal(source.NativePresentationStatusMemory.Rows[3].Battle, contact.Primary.BattleStatuses8);
        Assert.Null(contact.Secondary);
        NativeBattleGraphicsState graphics = process.Graphics;
        NativeBattleStageState stage = process.NativeStage!;
        long? callback = source.NativeHostCallbacks;
        var targetState = (target.IsActive, target.Hidden, target.Suspended, target.Untargetable, target.CurrentHp, target.NativeStatus0, target.NativeStatus1);
        BattleSession fork = source.ForkForContinuation();
        bool prepared = false;
        for (int frame = 0; frame < 120 && !prepared; frame++)
        {
            fork.AdvanceNativeGraphicsPresentation(PresentationTiming.SimulationStep);
            NativeBattleGraphicsState current = fork.Presentation!.Graphics;
            prepared = current.ActionContext is not null && current.ActionGraphics?.Header == reference && current.Descriptors.Tasks.Any(task => (task.Flags & 1) != 0 && task.Kind == NativeBattleDescriptorTaskKind.Action104 && task.Phase >= 3 && current.Descriptors.Packets[task.PacketSlot].Header == reference);
        }
        Assert.True(prepared);
        NativeBattleGraphicsState advanced = fork.Presentation!.Graphics;
        NativeBattleActionGeometry geometry = Assert.IsType<NativeBattleActionGeometry>(advanced.ActionContext);
        Assert.Equal(recipient, geometry.ResolveTarget(advanced.ActorModels, advanced.NativeBindings));
        Assert.Same(graphics, process.Graphics);
        Assert.Same(stage, process.NativeStage);
        Assert.Equal(callback, source.NativeHostCallbacks);
        Assert.Equal(header, process.ActionPackets.ReadHeader(reference));
        Assert.Equal(targetState, (target.IsActive, target.Hidden, target.Suspended, target.Untargetable, target.CurrentHp, target.NativeStatus0, target.NativeStatus1));
    }
}
