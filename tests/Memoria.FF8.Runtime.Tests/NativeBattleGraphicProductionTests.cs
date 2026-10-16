using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGraphicProductionTests
{
    [Fact]
    public void Graphic252PublishesItsOriginalHeaderAndZeroContactWithPhysicalElementAcrossRestore()
    {
        NativeBattleRetainedMemory retained = NativeBattleRetainedMemory.FromColdProcess() with { ElementAttack = [0, 0, 0, 0x20, 0, 0, 0] };
        BattleSession battle = new(Definition([4, 0, 30, 7, 0], retained), BattleAtbMode.Normal);
        battle.Tick();

        NativeBattleActionPacketsState packets = battle.NativeActionPackets;
        Assert.Equal((byte)1, packets.ActionCount);
        Assert.Equal((byte)1, packets.ContactCount);
        NativeBattleActionHeaderSlot header = packets.Headers[0];
        Assert.Equal((byte)252, header.ReadByte(1));
        Assert.Equal((ushort)1, NativeBattleActionGraphics.Word(header, 4));
        Assert.Equal((ushort)7, NativeBattleActionGraphics.Word(header, 6));
        Assert.Null(header.Data);
        NativeBattleContactHalf contact = packets.ReadContact(0).Primary;
        Assert.Equal((byte)0, contact.Sequence);
        Assert.Equal((byte)1, contact.Flags2);
        Assert.Equal((ushort)0, contact.Amount6);
        NativeBattleLastAction memory = Assert.IsType<NativeBattleLastAction>(battle.Capture().Combatants[0].NativeParticipant!.LastAction);
        Assert.Equal((byte)252, memory.Command);
        Assert.Equal((byte)1, memory.ActionId);
        Assert.Equal((byte)0x20, memory.ElementMask);
        Assert.Equal((byte)3, memory.AttackType);
        Assert.DoesNotContain(battle.Events, item => item.Kind == "Damage");

        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
        for (int frame = 0; frame < 30; frame++)
        {
            battle.Tick();
            restored.Tick();
            Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
        }
        Assert.Equal(100, battle.Combatants[0].CurrentHp);
        Assert.DoesNotContain(battle.Events, item => item.Kind == "Damage");
        NativeBattleRetainedMemory copied = NativeBattleRetainedMemory.Capture(battle.Capture(), retained.ColdExecutableSha256);
        BattleSession next = new(Definition([0], copied), BattleAtbMode.Normal);
        Assert.Equal((byte)0x20, next.NativeElementAttack[3]);
    }

    [Fact]
    public void UnknownRetainedElementFailsOnlyWhenGraphic252ActuallyHasARecipient()
    {
        BattleSession empty = new(Definition([30, 7, 0], null), BattleAtbMode.Normal);
        empty.Tick();
        Assert.Equal((byte)1, empty.NativeActionPackets.ActionCount);
        Assert.Equal((byte)0, empty.NativeActionPackets.ContactCount);
        Assert.Null(empty.NativeElementAttack[3]);

        BattleSession reached = new(Definition([4, 0, 30, 7, 0], null), BattleAtbMode.Normal);
        Assert.Contains("participant BYTE BD5", Assert.Throws<NotSupportedException>(() => reached.Tick()).Message);
    }

    [Fact]
    public void EnemyLaneZeroWritesAttackClassWithoutInventingTheOtherLastActionBytes()
    {
        BattleSession battle = new(Definition([4, 200, 30, 7, 2, 10, 0, 0, 3, 0, 3, 0, 15, 96, 1, 0], NativeBattleRetainedMemory.FromColdProcess()), BattleAtbMode.Normal);
        battle.Tick();
        NativeBattleParticipantSnapshot enemy = battle.Capture().Combatants[1].NativeParticipant!;
        NativeBattleLastAction memory = Assert.IsType<NativeBattleLastAction>(enemy.LastAction);
        Assert.Equal((byte)3, memory.AttackType);
        Assert.Null(memory.Command);
        Assert.Null(memory.ActionId);
        Assert.Null(memory.ElementMask);
        Assert.Null(memory.AttackerModelId);
        Assert.Equal(1u, battle.Capture().NativeAiVariables![0]);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(BattleSession.Restore(battle.Capture()).Capture()));
    }

    [Fact]
    public void AConditionCannotReadAnUnwrittenCommandAfterTheIndependentAttackClassWrite()
    {
        BattleSession battle = new(Definition([4, 200, 30, 7, 2, 10, 3, 0, 252, 0, 3, 0, 15, 96, 1, 0], NativeBattleRetainedMemory.FromColdProcess()), BattleAtbMode.Normal);
        Assert.Contains("retained B9B byte", Assert.Throws<NotSupportedException>(() => battle.Tick()).Message);
    }

    private static BattleDefinition Definition(byte[] script, NativeBattleRetainedMemory? retained)
    {
        BattleNativeAction row = new(8, 1, 3, 1) { MagicId = 0, AttackType = 0, Power = 0, HitCount = 1, TargetFlags = null, AttackFlags = 0, DamagePresentation = 0, Element = 0, ElementPercent = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = null, Critical = null, PowerModifier = null, LevelModifier = null, RawRecord = [], SupplementalRecord = [] };
        NativeBattleAiDefinition ai = new([script, [0], [0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray(), 0, []);
        return new() { NativeRetainedMemory = retained, NativeActions = [row], Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true, MaximumHp = 100, CurrentHp = 100 }, new() { Id = "enemy", Name = "Enemy", NativeAi = ai }] };
    }
}
