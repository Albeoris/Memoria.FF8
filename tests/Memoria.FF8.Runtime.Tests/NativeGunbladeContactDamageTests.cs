using System.Reflection;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeGunbladeContactDamageTests
{
    [Theory]
    [InlineData((byte)251, (short)1, false, (byte)4)]
    [InlineData((byte)251, (short)25, false, (byte)4)]
    [InlineData((byte)251, (short)1, true, (byte)4)]
    [InlineData((byte)251, (short)25, true, (byte)4)]
    [InlineData((byte)251, (short)25, true, (byte)255)]
    [InlineData((byte)253, (short)1, false, (byte)4)]
    [InlineData((byte)253, (short)25, false, (byte)4)]
    [InlineData((byte)243, (short)25, true, (byte)4)]
    [InlineData((byte)248, (short)25, true, (byte)4)]
    public void TriggerContactRetainsFormulaRoundingBerserkAndPresentation(byte command, short timing, bool berserk, byte sequence)
    {
        BattleSession session = Create(berserk, false);
        NativeBattleRandomState expectedRandom = session.Capture().Random!.Draw(out byte roll);
        int expected = NativeGunbladeAttack.Damage(20, 5, 20, roll, timing);
        if (berserk)
        {
            expected = expected * 3 >> 1;
        }
        if (command == 243)
        {
            expected *= 3;
        }
        NativeBattleContactPacket packet = Resolve(session, command, timing, sequence);
        Assert.Equal(expected, packet.Primary.Amount6);
        Assert.Equal(expected, Assert.Single(session.Events, item => item.Kind == "Damage").Amount);
        Assert.Equal(timing >= 20 ? (byte)2 : (byte)0, packet.Primary.HitFlags3);
        Assert.Equal(sequence == 255 ? (byte)0 : timing >= 20 ? (byte)6 : sequence, packet.Primary.Sequence);
        Assert.Equal(JsonSerializer.Serialize(expectedRandom), JsonSerializer.Serialize(session.Capture().Random));
    }

    [Theory]
    [InlineData((byte)251)]
    [InlineData((byte)253)]
    public void SuccessfulTriggerFlagPrecedesPhysicalImmunityWithoutDrawingDamageRandom(byte command)
    {
        BattleSession session = Create(false, true);
        string random = JsonSerializer.Serialize(session.Capture().Random);
        NativeBattleContactPacket packet = Resolve(session, command, 25, 4);
        Assert.Equal((ushort)0, packet.Primary.Amount6);
        Assert.Equal((byte)2, packet.Primary.HitFlags3);
        Assert.Equal((byte)6, packet.Primary.Sequence);
        Assert.Equal(random, JsonSerializer.Serialize(session.Capture().Random));
    }

    private static BattleSession Create(bool berserk, bool immune)
    {
        return new(new BattleDefinition
        {
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            Combatants =
            [
                new() { Id = "party", Name = "Party", IsPlayer = true, ModelId = 0, WeaponId = 0, Attack = 20, NativeStatus0 = berserk ? (ushort)0x20 : (ushort)0 },
                new() { Id = "enemy", Name = "Enemy", MaximumHp = 1000, CurrentHp = 1000, Defense = 5, NativeStatus0 = immune ? (ushort)4 : (ushort)0 }
            ]
        }, BattleAtbMode.Normal);
    }

    private static NativeBattleContactPacket Resolve(BattleSession session, byte command, short timing, byte sequence)
    {
        BattleCombatant actor = session.Combatants.Single(item => item.Id == "party");
        BattleCombatant target = session.Combatants.Single(item => item.Id == "enemy");
        BattleNativeAction native = new(command, 0, -1, -1) { MagicId = 0, AttackType = 10, Power = 20, HitCount = 1, AttackFlags = 0, TargetFlags = 0, PowerModifier = 0, LevelModifier = 0, DamagePresentation = sequence, Element = 0, ElementPercent = 0, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = 255, Critical = 0, RawRecord = [], SupplementalRecord = [] };
        BattleActionPresentation presentation = new(actor.Id, target.Id, BattleActionPhase.Windup, 0, BattleAbility.Attack, 0) { NativeAction = native, NativeQueuedCommand = command, GunbladeTriggerTiming = timing };
        BattleActionDefinition action = new(BattleAbility.Attack, 0, "Trigger contact", BattleActionEffect.Native, 20, false) { NativeAction = native };
        return (NativeBattleContactPacket)typeof(BattleSession).GetMethod("ResolveInstalledNativeHitWithContact", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(session, [actor, target, presentation, action, native, (byte)255, false])!;
    }
}
