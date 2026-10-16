using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleAiContactTests
{
    [Fact]
    public void CallbackFourOpcodeNineChangesTheActualContactPacketAndRestoresBeforeTheHit()
    {
        NativeBattleAiAbility[] abilities = Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray();
        NativeBattleAiDefinition ai = new([[0], [0], [0], [0], [9, 18, 0]], abilities, 0, []);
        BattleNativeAction native = new(8, 1, 3, 1) { MagicId = 0, AttackType = 27, Power = 1, HitCount = 1, TargetFlags = 0, AttackFlags = 0, DamagePresentation = 0, Element = 0, ElementPercent = 0, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = 0, Critical = 0, PowerModifier = 0, LevelModifier = 0, RawRecord = new byte[20].ToImmutableArray(), SupplementalRecord = [] };
        BattleSession battle = new(new() { NativeCommandSequences = NativeBattleCommandSequenceFixture.Values, NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(), NativeActions = [native], Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum, NativeCommands = [new(8, "Hit", 0x50, 1)] }, new() { Id = "enemy", Name = "Enemy", MaximumHp = 1000, CurrentHp = 1000, NativeCombatantFlags = 0x10, NativeAi = ai }] }, BattleAtbMode.TurnBased);
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
            session.QueueNativePlayerCommand("party", "enemy", 8, 1);
            for (int admission = 0; admission < 7 && session.NativeActionPackets.ActionCount == 0; admission++)
            {
                session.Tick();
            }
            Assert.Equal((byte)1, session.NativeActionPackets.ActionCount);
            NativeBattleActionHeaderSlot header = session.NativeActionPackets.Headers[0];
            Assert.Equal((byte)1, header.ReadByte(16));
            return session.NativeActionPackets.ReadContact(header.FirstContactIndex!.Value);
        }
    }
}
