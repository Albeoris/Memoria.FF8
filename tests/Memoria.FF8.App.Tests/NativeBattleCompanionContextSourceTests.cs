using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleCompanionContextSourceTests
{
    [Theory]
    [InlineData((ushort)314, 216, 56, 103, 104, (ushort)191, (byte)16, false)]
    [InlineData((ushort)315, 104, 19, 75, 34, (ushort)151, (byte)13, true)]
    public void Installed_neutral_component_preserves_original_companion_owners_and_private_source_state(ushort effect, int formation, int stage, int model, int companionModel, ushort action, byte sequence, bool hidden)
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(game is not null, "Select installed original resources with MEMORIA_FF8_GAME_DIR.");
        GameSession session = GameWorld.CreateColdProcess(game!);
        session.StartNewGame();
        using GameWorld world = new(session, game!);
        BattleSession source = NativeBattleComponentSourceTests.PrepareComponentSource(world, new(model, 0, effect));
        Assert.Equal(formation, source.ReadDefinition().EncounterId);
        Assert.Equal(stage, source.ReadDefinition().StageId);
        BattleCombatant caster = Assert.Single(source.Combatants, actor => !actor.IsPlayer && actor.NativeActorIndex == 3);
        BattleCombatant companion = Assert.Single(source.Combatants, actor => !actor.IsPlayer && actor.NativeActorIndex == 4);
        Assert.Equal(model, caster.ModelId);
        Assert.Equal(companionModel, companion.ModelId);
        Assert.Equal(BattleRosterSlot.Enemy(0), caster.Slot);
        Assert.Equal(BattleRosterSlot.Enemy(1), companion.Slot);
        Assert.Equal(hidden, companion.Hidden);
        Assert.True(companion.Suspended);
        Assert.True(companion.Untargetable);
        BattlePresentationProcess process = source.Presentation!;
        BattleEnemyActorModel casterOwner = Assert.IsType<BattleEnemyActorModel>(process.FindOriginalActor(NativeBattleModelSlot.Decode(3)));
        BattleEnemyActorModel companionOwner = Assert.IsType<BattleEnemyActorModel>(process.FindOriginalActor(NativeBattleModelSlot.Decode(4)));
        Assert.Equal(caster.Id, casterOwner.Actor.ActorId);
        Assert.Equal(companion.Id, companionOwner.Actor.ActorId);
        BattleDiagnosticComponentAdmission admission = GameWorld.ValidateEffectComponentSource(source, effect);
        Assert.Equal(casterOwner, admission.Actor);
        Assert.Equal((byte)8, admission.Command);
        Assert.Equal(action, admission.Action);
        BattleActionHeaderReference reference = new(admission.HeaderIndex);
        NativeBattleActionHeaderSlot header = process.ActionPackets.ReadHeader(reference);
        Assert.Equal(effect, header.Effect);
        Assert.Equal(sequence, header.Sequence);
        NativeBattleContactPacket contact = Assert.Single(process.ActionPackets.ReadHeaderContacts(reference));
        Assert.Equal(0, contact.Primary.Amount6);
        Assert.Null(contact.Secondary);
        Assert.Equal(effect == 314 ? process.FindOriginalActor(NativeBattleModelSlot.Decode(0)) : casterOwner, contact.Primary.Recipient);
        NativeBattleGraphicsState originalGraphics = process.Graphics;
        NativeBattleEffectMemory originalMemory = process.NativeEffectMemory!;
        long originalFrame = source.Frame;
        long? originalCallback = source.NativeHostCallbacks;
        int originalTask = originalMemory.ReadReturnedTask5044B0();
        BattleSession fork = source.ForkForContinuation();
        fork.AdvanceNativeGraphicsPresentation(PresentationTiming.SimulationStep);
        Assert.Equal(originalFrame, source.Frame);
        Assert.Equal(originalCallback, source.NativeHostCallbacks);
        Assert.Same(originalGraphics, process.Graphics);
        Assert.Same(originalMemory, process.NativeEffectMemory);
        Assert.Equal(originalTask, originalMemory.ReadReturnedTask5044B0());
        Assert.Equal(header, process.ActionPackets.ReadHeader(reference));
        Assert.Equal(hidden, companion.Hidden);
        Assert.True(companion.Suspended);
        Assert.True(companion.Untargetable);
    }
}
