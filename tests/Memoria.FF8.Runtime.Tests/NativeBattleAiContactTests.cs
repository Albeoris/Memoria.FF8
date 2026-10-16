using System.Reflection;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleAiContactTests
{
    [Fact]
    public void CallbackFourOpcodeNineChangesTheActualContactPacketAndRestoresBeforeTheHit()
    {
        NativeBattleAiAbility[] abilities = Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray();
        NativeBattleAiDefinition ai = new([[0], [0], [0], [0], [9, 18, 0]], abilities, 0, []);
        BattleNativeAction native = new(8, 1, 3, 1) { MagicId = 0, AttackType = 27, Power = 1, HitCount = 1, TargetFlags = 0, AttackFlags = 0, DamagePresentation = 0, Element = 0, ElementPercent = 0, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = 0, Critical = 0, PowerModifier = 0, LevelModifier = 0, RawRecord = [], SupplementalRecord = [] };
        BattleSession battle = new(new() { NativeActions = [native], Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy", MaximumHp = 1000, CurrentHp = 1000, NativeCombatantFlags = 0x10, NativeAi = ai }] }, BattleAtbMode.TurnBased);
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
        NativeBattleContactPacket packet = Hit(battle);
        NativeBattleContactPacket independent = Hit(restored);
        Assert.Equal((byte)18, packet.Primary.Sequence);
        Assert.Equal(packet, independent);
        Assert.Single(battle.Events, entry => entry.Kind == "NativeDamagePresentation");
        Assert.True(battle.Combatants[1].CurrentHp < 1000);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));

        NativeBattleContactPacket Hit(BattleSession session)
        {
            //Exercise the real48FE20/494410/callback4/48EF80 producer without
            //introducing an unrelated graphics-resource or presentation clock.
            MethodInfo producer = typeof(BattleSession).GetMethod("ResolveInstalledNativeHitWithContact", BindingFlags.Instance | BindingFlags.NonPublic)!;
            BattleActionDefinition action = new(BattleAbility.EnemyAbility, 1, "Hit", BattleActionEffect.Native, 1, false) { NativeAction = native };
            BattleActionPresentation command = new("party", "enemy", BattleActionPhase.Windup, 0, BattleAbility.EnemyAbility, 1) { NativeAction = native, NativeQueuedCommand = 8 };
            return (NativeBattleContactPacket)producer.Invoke(session, [session.Combatants[0], session.Combatants[1], command, action, native, (byte)255])!;
        }
    }
}
