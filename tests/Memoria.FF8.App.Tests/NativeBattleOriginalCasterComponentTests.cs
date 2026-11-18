using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleOriginalCasterComponentTests
{
    [Theory]
    [InlineData((ushort)17, 91, (byte)252, (ushort)1, (byte)15, true)]
    [InlineData((ushort)167, 70, (byte)8, (ushort)274, (byte)12, false)]
    [InlineData((ushort)318, 38, (byte)8, (ushort)278, (byte)13, false)]
    public void Installed_component_uses_its_original_caster_header_and_recipient(ushort effect, int enemy, byte command, ushort action, byte sequence, bool self)
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(game is not null, "Select installed original resources with MEMORIA_FF8_GAME_DIR.");
        GameSession session = GameWorld.CreateColdProcess(game!);
        session.StartNewGame();
        using GameWorld world = new(session, game!);
        EffectPreviewRequest request = new(enemy, 0, effect);
        Assert.Equal(enemy, NativeBattleComponentSourceTests.OriginalComponentEnemy(effect));
        BattleSession source = NativeBattleComponentSourceTests.PrepareComponentSource(world, request);
        BattleDiagnosticComponentAdmission admission = GameWorld.ValidateEffectComponentSource(source, effect);
        Assert.Equal(effect, admission.Effect);
        Assert.Equal(command, admission.Command);
        Assert.Equal(action, admission.Action);
        Assert.Equal((byte)(effect == 17 ? 5 : 3), admission.OriginalActorIndex);
        BattleEnemyActorModel actor = Assert.IsType<BattleEnemyActorModel>(admission.Actor);
        BattleCombatant caster = Assert.Single(source.Combatants, combatant => combatant.Id == actor.Actor.ActorId);
        Assert.False(caster.IsPlayer);
        Assert.Equal(enemy, caster.ModelId);
        Assert.Equal(BattleRosterSlot.Enemy(effect == 17 ? 2 : 0), caster.Slot);
        BattlePresentationProcess process = source.Presentation!;
        NativeBattleActionHeaderSlot header = process.ActionPackets.ReadHeader(new BattleActionHeaderReference(admission.HeaderIndex));
        Assert.Equal(admission.Actor, header.Source);
        Assert.Equal(command, header.Command);
        Assert.Equal(action, header.Action);
        Assert.Equal(effect, header.Effect);
        Assert.Equal(sequence, header.Sequence);
        NativeBattleContactPacket contact = Assert.Single(process.ActionPackets.ReadHeaderContacts(new BattleActionHeaderReference(admission.HeaderIndex)));
        Assert.Equal(0, contact.Primary.Amount6);
        Assert.Null(contact.Secondary);
        if (self)
        {
            Assert.Equal(admission.Actor, contact.Primary.Recipient);
            Assert.Equal(3, source.Combatants.Count(combatant => combatant.IsPlayer && combatant.IsActive));
            for (int index = 0; index < 3; index++)
            {
                BattleCombatant party = Assert.Single(source.Combatants, combatant => combatant.IsPlayer && combatant.Slot == BattleRosterSlot.Ally(index));
                Assert.Equal(index, party.NativeActorIndex);
                Assert.NotNull(process.FindOriginalActor(NativeBattleModelSlot.Decode(checked((byte)index))));
            }
            Assert.Equal(admission.Actor, process.FindOriginalActor(NativeBattleModelSlot.Decode(5)));
        }
        else
        {
            BattlePartyActorModel target = Assert.IsType<BattlePartyActorModel>(contact.Primary.Recipient);
            BattleCombatant recipient = Assert.Single(source.Combatants, combatant => combatant.Id == target.Actor.ActorId);
            Assert.Equal(BattleRosterSlot.Ally(0), recipient.Slot);
            Assert.True(recipient.IsPlayer);
        }
        Assert.Equal(BattleOutcome.Active, source.Outcome);
        if (!admission.ModuleLoaded)
        {
            Assert.Single(process.Graphics.Descriptors.Queue);
            Assert.Equal((ushort)104, process.Graphics.Descriptors.Packets[process.Graphics.Descriptors.Queue[0]].Command);
        }
    }
}
