using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRevivalDiagnosticResolutionTests
{
    [Theory]
    [InlineData(5, 68)]
    [InlineData(6, 544)]
    public void Native_revival_observes_one_causal_hp_write_without_changing_execution_and_restores_queued_continuation(byte attackType, int restoredHp)
    {
        BattleSession battle = Create(attackType);
        battle.QueueAction("caster", "fallen", BattleAbility.Magic, 24);
        BattleSnapshot queued = battle.Capture();
        BattleSession unobserved = BattleSession.Restore(queued);
        BattleSession cold = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(queued))!);
        List<BattleDiagnosticResolution> resolutions = [];
        List<BattleDiagnosticResolution> coldResolutions = [];
        battle.DiagnosticResolution += resolutions.Add;
        cold.DiagnosticResolution += coldResolutions.Add;

        for (int frame = 0; frame <= BattleSession.AttackWindupFrames; frame++)
        {
            battle.Tick();
            unobserved.Tick();
            cold.Tick();
        }

        BattleDiagnosticResolution resolution = Assert.Single(resolutions);
        Assert.Equal("healing", resolution.Kind);
        Assert.Equal("caster", resolution.ActorId);
        Assert.Equal("fallen", resolution.OriginalTargetId);
        Assert.Equal("fallen", resolution.ActualTargetId);
        Assert.Equal("character", resolution.HpOwner);
        Assert.Equal(0, resolution.BeforeHp);
        Assert.Equal(restoredHp, resolution.AfterHp);
        Assert.Equal(restoredHp, resolution.ResolvedAmount);
        Assert.Equal(restoredHp, resolution.ActualAmount);
        Assert.Equal(JsonSerializer.Serialize(resolution), JsonSerializer.Serialize(Assert.Single(coldResolutions)));
        BattleCombatant recipient = battle.Combatants.Single(actor => actor.Id == "fallen");
        Assert.Equal(restoredHp, recipient.CurrentHp);
        Assert.Equal(0, recipient.NativeStatus0 & 1);
        BattleEvent healing = Assert.Single(battle.Events, item => item.Kind == "Healing");
        Assert.Equal(restoredHp, healing.Amount);
        Assert.Equal("fallen", healing.TargetId);
        NativeBattleActionHeaderSlot header = battle.NativeActionPackets.Headers[0];
        NativeBattleContactPacket contact = battle.NativeActionPackets.ReadContact(header.FirstContactIndex!.Value);
        Assert.Equal((byte)1, contact.Primary.ActorIndex);
        Assert.Equal((ushort)restoredHp, contact.Primary.Amount6);
        Assert.Equal(1, contact.Primary.HitFlags3 & 1);
        Assert.Equal(JsonSerializer.Serialize(unobserved.Capture()), JsonSerializer.Serialize(battle.Capture()));
        Assert.Equal(JsonSerializer.Serialize(unobserved.Capture()), JsonSerializer.Serialize(cold.Capture()));

        BattleSession continued = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
        battle.Tick();
        continued.Tick();
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(continued.Capture()));
        Assert.Single(resolutions);
    }

    private static BattleSession Create(byte attackType)
    {
        BattleNativeAction revival = new(2, 24, 1, 24)
        {
            MagicId = 5, AttackType = attackType, Power = 0, HitCount = 1, TargetFlags = 0x10, AttackFlags = 3, DamagePresentation = 0, Element = 0, StatusAttack = 255, Statuses0 = 1, Statuses1 = 0, ElementPercent = null, Hit = null, Critical = null, PowerModifier = null, LevelModifier = null, RawRecord = [], SupplementalRecord = []
        };
        return new(new BattleDefinition
        {
            NativeFieldBattleRules = 0,
            NativeCommandSequences = NativeBattleCommandSequenceFixture.Values,
            ActionDefinitions = [new(BattleAbility.Magic, 24, "Native revival component", BattleActionEffect.Native, 0, true) { NativeAction = revival }],
            Combatants =
            [
                new() { Id = "caster", Name = "Caster", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum, MagicStocks = [new(24, 2)], Abilities = [BattleAbility.Attack, BattleAbility.Magic], NativeDeathCount = 0 },
                new() { Id = "fallen", Name = "Fallen", ModelId = 1, IsPlayer = true, MaximumHp = 544, CurrentHp = 0, NativeStatus0 = 1, NativeDeathCount = 1 },
                new() { Id = "enemy", Name = "Enemy", Speed = 1 }
            ]
        }, BattleAtbMode.TurnBased);
    }
}
