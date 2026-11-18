using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect20ComponentSourceTests
{
    [Fact]
    public void Installed_source_retains_the_original_deferred_actor_and_neutral_self_header_across_a_private_fork()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(game is not null, "Select installed original resources with MEMORIA_FF8_GAME_DIR.");
        GameSession session = GameWorld.CreateColdProcess(game!);
        session.StartNewGame();
        using GameWorld world = new(session, game!);
        EffectPreviewRequest request = new(68, 0, 20);
        BattleSession source = NativeBattleComponentSourceTests.PrepareComponentSource(world, request);
        Assert.Equal(63, source.ReadDefinition().EncounterId);
        Assert.Equal(13, source.ReadDefinition().StageId);
        BattlePresentationProcess process = source.Presentation!;
        int[] models = [68, 81, 80, 77];
        Assert.Equal(4, source.Combatants.Count(actor => !actor.IsPlayer && actor.NativeActorIndex.HasValue));
        Assert.All(source.Combatants.Where(actor => !actor.IsPlayer && !actor.NativeActorIndex.HasValue), actor => Assert.False(actor.IsActive));
        for (int index = 0; index < models.Length; index++)
        {
            BattleCombatant actor = Assert.Single(source.Combatants, candidate => !candidate.IsPlayer && candidate.Slot == BattleRosterSlot.Enemy(index));
            Assert.Equal(models[index], actor.ModelId);
            Assert.Equal(index + 3, actor.NativeActorIndex);
            BattleEnemyActorModel owner = Assert.IsType<BattleEnemyActorModel>(process.FindOriginalActor(NativeBattleModelSlot.Decode(checked((byte)(index + 3)))));
            Assert.Equal(actor.Id, owner.Actor.ActorId);
        }
        BattleCombatant deferred = Assert.Single(source.Combatants, actor => !actor.IsPlayer && actor.Slot == BattleRosterSlot.Enemy(3));
        Assert.True(deferred.Hidden);
        Assert.True(deferred.Suspended);
        Assert.True(deferred.Untargetable);
        BattleDiagnosticComponentAdmission admission = GameWorld.ValidateEffectComponentSource(source, 20);
        Assert.Equal((byte)3, admission.OriginalActorIndex);
        Assert.Equal((byte)8, admission.Command);
        Assert.Equal((ushort)287, admission.Action);
        BattleActionHeaderReference reference = new(admission.HeaderIndex);
        NativeBattleActionHeaderSlot header = process.ActionPackets.ReadHeader(reference);
        Assert.Equal((byte)12, header.Sequence);
        Assert.Equal((ushort)20, header.Effect);
        Assert.Equal(admission.Actor, header.Source);
        NativeBattleContactPacket contact = Assert.Single(process.ActionPackets.ReadHeaderContacts(reference));
        Assert.Equal(header.Source, contact.Primary.Recipient);
        Assert.Equal(0, contact.Primary.Amount6);
        Assert.Null(contact.Secondary);
        NativeBattleGraphicsState originalGraphics = process.Graphics;
        NativeBattleEffectMemory originalMemory = process.NativeEffectMemory!;
        long frame = source.Frame;
        long? callback = source.NativeHostCallbacks;
        int taskPointer = originalMemory.ReadReturnedTask5044B0();
        BattleSession fork = source.ForkForContinuation();
        fork.AdvanceNativeGraphicsPresentation(PresentationTiming.SimulationStep);
        Assert.Equal(frame, source.Frame);
        Assert.Equal(callback, source.NativeHostCallbacks);
        Assert.Same(originalGraphics, process.Graphics);
        Assert.Same(originalMemory, process.NativeEffectMemory);
        Assert.Equal(taskPointer, originalMemory.ReadReturnedTask5044B0());
        Assert.Equal(header, process.ActionPackets.ReadHeader(reference));
        Assert.True(deferred.Hidden);
        Assert.True(deferred.Suspended);
        Assert.True(deferred.Untargetable);
    }
}
