using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect308ComponentSourceTests
{
    [Fact]
    public void Installed_trio_executes_its_original_prerequisite_before_publishing_the_component()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(game is not null, "Select installed original resources with MEMORIA_FF8_GAME_DIR.");
        GameSession session = GameWorld.CreateColdProcess(game!);
        session.StartNewGame();
        using GameWorld world = new(session, game!);
        BattleSession source = NativeBattleComponentSourceTests.PrepareComponentSource(world, new(105, 0, 308));
        Assert.Equal(794, source.ReadDefinition().EncounterId);
        Assert.Equal(65, source.ReadDefinition().StageId);
        BattlePresentationProcess process = source.Presentation!;
        for (int index = 0; index < 3; index++)
        {
            BattleCombatant actor = Assert.Single(source.Combatants, candidate => !candidate.IsPlayer && candidate.Slot == BattleRosterSlot.Enemy(index));
            Assert.Equal(105 + index, actor.ModelId);
            Assert.Equal(3 + index, actor.NativeActorIndex);
            Assert.True(actor.IsActive);
            Assert.False(actor.Hidden);
            Assert.False(actor.Suspended);
            Assert.False(actor.Untargetable);
            BattleEnemyActorModel owner = Assert.IsType<BattleEnemyActorModel>(process.FindOriginalActor(NativeBattleModelSlot.Decode(checked((byte)(index + 3)))));
            Assert.Equal(actor.Id, owner.Actor.ActorId);
            Assert.Null(process.Graphics.ActorModels.Get(owner).ReadLinkedModel());
        }
        BattleDiagnosticComponentAdmission admission = GameWorld.ValidateEffectComponentSource(source, 308);
        Assert.Equal((byte)3, admission.OriginalActorIndex);
        Assert.Equal((byte)8, admission.Command);
        Assert.Equal((ushort)286, admission.Action);
        BattleActionHeaderReference reference = new(admission.HeaderIndex);
        NativeBattleActionHeaderSlot header = process.ActionPackets.ReadHeader(reference);
        Assert.Equal((byte)14, header.Sequence);
        NativeBattleContactPacket contact = Assert.Single(process.ActionPackets.ReadHeaderContacts(reference));
        Assert.Equal(header.Source, contact.Primary.Recipient);
        Assert.Equal(0, contact.Primary.Amount6);
        Assert.Null(contact.Secondary);
        BattleSession fork = source.ForkForContinuation();
        long? callback = source.NativeHostCallbacks;
        NativeBattleGraphicsState graphics = process.Graphics;
        fork.AdvanceNativeGraphicsPresentation(PresentationTiming.SimulationStep);
        Assert.Same(graphics, process.Graphics);
        Assert.Equal(callback, source.NativeHostCallbacks);
        Assert.Equal(header, process.ActionPackets.ReadHeader(reference));
    }
}
