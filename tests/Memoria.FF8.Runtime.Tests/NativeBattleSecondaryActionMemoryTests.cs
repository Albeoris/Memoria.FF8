using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSecondaryActionMemoryTests
{
    [Theory]
    [InlineData((byte)2)]
    [InlineData(null)]
    public void LethalZombieDrainRetainsTheSourcesAttackClassWhenDeathPublishesTheOtherMemoryBytes(byte? previousClass)
    {
        BattleNativeAction drain = new(8, 1, 3, 1) { MagicId = 0, AttackType = 27, Power = 2, HitCount = 1, TargetFlags = 0, AttackFlags = 1, DamagePresentation = 0, Element = 0, ElementPercent = 0, StatusAttack = 100, Statuses0 = 0, Statuses1 = 0x8000, Hit = 0, Critical = 0, PowerModifier = 0, LevelModifier = 0, RawRecord = [0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0], SupplementalRecord = [] };
        NativeBattleAiDefinition ai = new([[4, 0, 5, 1, 7, 1, 6, 0], [0], [0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray(), 0, []);
        BattleSession battle = new(new()
        {
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            NativeActions = [drain],
            Combatants =
            [
                new() { Id = "party", Name = "Zombie", IsPlayer = true, MaximumHp = 1000, CurrentHp = 1000, NativeStatus0 = 0x40, NativeDeathCount = 0, NativeStatusResistances = new byte[40] },
                new() { Id = "enemy", Name = "Drainer", MaximumHp = 1000, CurrentHp = 1, NativeAi = ai, NativeCombatantFlags = 0x30, NativeStatusResistances = new byte[40] }
            ]
        }, BattleAtbMode.Normal);
        BattleSnapshot boundary = battle.Capture();
        BattleCombatantSnapshot[] participants = boundary.Combatants.ToArray();
        NativeBattleParticipantSnapshot source = participants[1].NativeParticipant!;
        participants[1] = participants[1] with { NativeParticipant = source with { LastAction = new(previousClass, 7, 2, 99, 4, 0), RemainingHits = 0 } };
        battle = BattleSession.Restore(boundary with { Combatants = participants, NativeActionMemory = boundary.NativeActionMemory!.WithAction(source.ActorIndex, participants[1].NativeParticipant!.LastAction) });
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
        battle.Tick();
        restored.Tick();
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
        Assert.Equal(0, battle.Combatants[1].CurrentHp);
        NativeBattleLastAction memory = Assert.IsType<NativeBattleLastAction>(battle.Capture().Combatants[1].NativeParticipant!.LastAction);
        Assert.Equal(previousClass, memory.AttackType);
        Assert.Equal((byte)8, memory.Command);
        Assert.Equal((byte)1, memory.ActionId);
        Assert.Equal((byte)3, memory.AttackerIndex);
        NativeBattleContactPacket contact = battle.NativeActionPackets.ReadContact(0);
        Assert.Equal((byte)3, contact.Secondary.ActorIndex);
        Assert.Equal((ushort)200, contact.Secondary.Amount6);
        Assert.Equal(800, battle.Combatants[0].CurrentHp);
    }
}
