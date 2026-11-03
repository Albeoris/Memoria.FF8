using System.Reflection;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleExperienceLifecycleTests
{
    [Fact]
    public void FreshResetAndObservedAccrualSurviveIndependentJsonAndDistributeExactlyOnce()
    {
        BattleSession battle = Session();
        Invoke(battle, "InitializeNativeExperience48D020");
        Assert.All(battle.NativeExperienceBanks!.Words, word => Assert.Equal((ushort)0, word));
        Assert.False(battle.NativeExperienceDistributed);
        NativeBattleExperienceBanks accrued = battle.NativeExperienceBanks.Accrue494AF0(new(0, 1, null, 10, 10, 10, 10, 5, 100, 0, 3));
        BattleSnapshot saved = battle.Capture() with { NativeExperience = new(accrued, false) };
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(saved))!);
        Assert.Equal((ushort)40, restored.NativeExperienceBanks!.ReadWord(6));
        Assert.False(restored.NativeExperienceDistributed);

        Invoke(restored, "FinalizeNativeExperience494D40");
        Assert.True(restored.NativeExperienceDistributed);
        Assert.Equal((ushort)20, restored.NativeExperienceBanks.ReadWord(0));
        Assert.Equal((ushort)20, restored.NativeExperienceBanks.ReadWord(12));
        Assert.Equal((ushort)3, restored.NativeExperienceBanks.ReadWord(76));
        string final = JsonSerializer.Serialize(restored.Capture());
        Invoke(restored, "FinalizeNativeExperience494D40");
        Assert.Equal(final, JsonSerializer.Serialize(restored.Capture()));
        BattleSession finalRestored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(final)!);
        Invoke(finalRestored, "FinalizeNativeExperience494D40");
        Assert.Equal(final, JsonSerializer.Serialize(finalRestored.Capture()));
    }

    [Fact]
    public void MissingHistoricalBankStaysUnknownWithoutReplayingDefeatsOrTheReset()
    {
        BattleSession battle = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(Session().Capture()))!);
        Invoke(battle, "AccrueNativeExperience494AF0", battle.Combatants[1]);
        Invoke(battle, "FinalizeNativeExperience494D40");
        Assert.Null(battle.NativeExperienceBanks);
        Assert.False(battle.NativeExperienceDistributed);
        Assert.Throws<NotSupportedException>(() => battle.CaptureNativeExperienceResult());
        Assert.Null(battle.Capture().NativeExperience);
    }

    [Fact]
    public void MissingFreshContactProvenanceDoesNotInventSourceCreditOrChangeTheBank()
    {
        BattleSession battle = Session();
        Invoke(battle, "InitializeNativeExperience48D020");
        string before = JsonSerializer.Serialize(battle.NativeExperienceBanks);

        TargetInvocationException error = Assert.Throws<TargetInvocationException>(() => Invoke(battle, "AccrueNativeExperience494AF0", battle.Combatants[1]));

        Assert.IsType<NotSupportedException>(error.InnerException);
        Assert.Equal(before, JsonSerializer.Serialize(battle.NativeExperienceBanks));
    }

    [Fact]
    public void ActualPoisonPostActionProducerCreditsSharedAndApFromItsCommandZeroContactOnce()
    {
        BattleSession battle = Session(poison: true);
        Invoke(battle, "InitializeNativeExperience48D020");
        Invoke(battle, "BeginNativeReactionBatch");
        SetField(battle, "nativeActionScratch", battle.Capture().NativeActionScratch! with { ActorIndex = 3, EventActorB0F = 3, EventLane = 0, Command = 2 });
        NativeBattleGraphicsState graphics = new([], [], 0, 0);

        NativeBattleGraphicsState post = (NativeBattleGraphicsState)Invoke(battle, "RunNativeDescriptorActionCallback", graphics, 0x47DE70u)!;

        Assert.Equal(0, battle.Combatants[1].CurrentHp);
        BattleEnemyDefeat defeat = Assert.Single(battle.EnemyDefeats);
        Assert.Equal((byte)0, defeat.NativeCommand);
        Assert.Equal((ushort)20, battle.NativeExperienceBanks!.ReadWord(0));
        Assert.Equal((ushort)0, battle.NativeExperienceBanks.ReadWord(12));
        Assert.Equal((ushort)3, battle.NativeExperienceBanks.ReadWord(76));
        Assert.Equal((byte)0, battle.NativeActionPackets.Headers[0].ReadByte(1));
        Assert.Equal((byte)3, battle.NativeActionPackets.Headers[0].ReadByte(0));
        Assert.Equal((ushort)2, System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(battle.NativeActionPackets.Headers[0].Payload.AsSpan()[4..]));
        Assert.Contains(post.Descriptors.Queue, slot => post.Descriptors.Packets[slot].Command == 10 && post.Descriptors.Packets[slot].Argument == 0x47E120);
        string accrued = JsonSerializer.Serialize(battle.NativeExperienceBanks);
        Invoke(battle, "RunNativeDescriptorActionCallback", post, 0x47E120u);
        Assert.Equal(accrued, JsonSerializer.Serialize(battle.NativeExperienceBanks));
        Assert.Single(battle.EnemyDefeats);
    }

    private static BattleSession Session(bool poison = false)
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Create() with { CombatParty = [0, 255, 255] };
        BattleDefinition definition = new()
        {
            MagicVitals = vitals,
            NativeClockRules = new(10),
            NativeBattleSpeed = 2,
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            NativeCommandSequences = NativeBattleCommandSequenceFixture.Values,
            Combatants =
            [
                new() { Id = "party-0", Name = "Party", IsPlayer = true, MagicRecordId = 0, Level = 10, MaximumHp = 100, CurrentHp = 100 },
                new() { Id = "enemy", Name = "Enemy", Level = 10, MaximumHp = 100, CurrentHp = poison ? 1 : 100, NativeStatus0 = poison ? (ushort)2 : (ushort)0, RewardSlot = 0, AbilityPoints = 3, Rewards = new(10, 5, 20, 30, 0, false, 0, [new(1, 1), new(1, 1), new(1, 1), new(1, 1), new(1, 1), new(1, 1), new(1, 1), new(1, 1), new(1, 1), new(1, 1), new(1, 1), new(1, 1)]) }
            ]
        };
        return new(definition, BattleAtbMode.Normal);
    }

    private static object? Invoke(BattleSession battle, string name, params object[] arguments)
    {
        return typeof(BattleSession).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(battle, arguments);
    }

    private static void SetField(BattleSession battle, string name, object value)
    {
        typeof(BattleSession).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(battle, value);
    }
}
