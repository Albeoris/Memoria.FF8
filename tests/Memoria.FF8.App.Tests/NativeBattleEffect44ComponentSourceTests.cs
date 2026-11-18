using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect44ComponentSourceTests
{
    [Fact]
    public void Installed_component_retains_original_caster_bones_header_and_party_recipient()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(game is not null, "Select installed original resources with MEMORIA_FF8_GAME_DIR.");
        GameSession session = GameWorld.CreateColdProcess(game!);
        session.StartNewGame();
        using GameWorld world = new(session, game!);
        BattleSession source = NativeBattleComponentSourceTests.PrepareComponentSource(world, new(83, 0, 44));
        BattleDiagnosticComponentAdmission admission = GameWorld.ValidateEffectComponentSource(source, 44);
        Assert.Equal((byte)3, admission.OriginalActorIndex);
        Assert.Equal((byte)8, admission.Command);
        Assert.Equal((ushort)226, admission.Action);
        BattleEnemyActorModel owner = Assert.IsType<BattleEnemyActorModel>(admission.Actor);
        BattleCombatant caster = Assert.Single(source.Combatants, actor => actor.Id == owner.Actor.ActorId);
        Assert.Equal(83, caster.ModelId);
        Assert.Equal(BattleRosterSlot.Enemy(0), caster.Slot);
        BattlePresentationProcess process = source.Presentation!;
        NativeBattlePoseSource pose = world.BattleResources.ReadNativeGraphicsResource(source.GetCombatantDefinition(caster.Id)).PoseSource!;
        Assert.True(pose.BoneCount > 23);
        Assert.Equal(pose.BoneCount, NativeBattleBodyBoneMatrices.Require(process.Graphics.ActorModels.Get(owner), pose).Length);
        BattleActionHeaderReference reference = new(admission.HeaderIndex);
        NativeBattleActionHeaderSlot header = process.ActionPackets.ReadHeader(reference);
        Assert.Equal((byte)15, header.Sequence);
        NativeBattleContactPacket contact = Assert.Single(process.ActionPackets.ReadHeaderContacts(reference));
        Assert.Equal(process.FindOriginalActor(NativeBattleModelSlot.Decode(0)), contact.Primary.Recipient);
        Assert.Equal(0, contact.Primary.Amount6);
        Assert.Null(contact.Secondary);
        NativeBattleGraphicsState graphics = process.Graphics;
        long? callback = source.NativeHostCallbacks;
        BattleSession fork = source.ForkForContinuation();
        fork.AdvanceNativeGraphicsPresentation(PresentationTiming.SimulationStep);
        Assert.Same(graphics, process.Graphics);
        Assert.Equal(callback, source.NativeHostCallbacks);
        Assert.Equal(header, process.ActionPackets.ReadHeader(reference));
    }
}
