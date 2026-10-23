using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeFieldItemBattleTests
{
    private static readonly NativeFieldItem Lamp = new(14, 1, 43, 3);

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void BattleItemSpendsOneAndAcquiresGuardianBeforeEntryWithoutClamping(int count)
    {
        NativeCharacterVitals vitals = Vitals();
        NativeEconomy economy = Economy(count);
        var result = Lamp.BeginBattle(vitals, economy, 168, false);
        Assert.True(Lamp.SupportsBattleUse);
        Assert.Equal((ushort)811, result.Encounter);
        Assert.Equal(0x032B0004, result.MenuResult);
        Assert.Equal(count - 1, result.Economy.Items.Where(stock => stock.ItemId == 168).Sum(stock => stock.Count));
        Assert.Equal(count == 1 ? new NativeItemStock(0, 0) : new NativeItemStock(168, 1), result.Economy.Items[7]);
        Assert.Equal(vitals.GuardianForces[5] with { Flags = 0x81 }, result.Vitals.GuardianForces[5]);
        Assert.Equal(vitals.Records, result.Vitals.Records);
        Assert.Equal(vitals.GuardianForces.Where((_, id) => id != 5), result.Vitals.GuardianForces.Where((_, id) => id != 5));
        Assert.Equal((byte)0x80, vitals.GuardianForces[5].Flags);
        Assert.Equal(count, economy.Items[7].Count);
        result.Vitals.Validate();
        result.Economy.Validate();
    }

    [Fact]
    public void AlreadyAcquiredGuardianStillSpendsBattleItemAndKeepsSavedState()
    {
        NativeCharacterVitals vitals = Vitals().AcquireGuardianForce(5);
        var result = Lamp.BeginBattle(vitals, Economy(1), 168, false);
        Assert.Equal(vitals.GuardianForces, result.Vitals.GuardianForces);
        Assert.DoesNotContain(result.Economy.Items, stock => stock.ItemId == 168);
    }

    [Fact]
    public void BattleBootstrapRefreshesAcquiredGuardiansWithoutChangingCharacterOrUnacquiredOwners()
    {
        NativeCharacterVitals vitals = Vitals();
        var used = Lamp.BeginBattle(vitals, Economy(1), 168, false);
        Assert.Equal((ushort)60000, used.Vitals.GuardianForces[5].HitPoints);
        NativeCharacterVitals refreshed = used.Vitals.RefreshGuardianForces();
        ushort maximum = vitals.Definitions.GuardianForceMaximum(5, used.Vitals.GuardianForces[5]);
        Assert.Equal(used.Vitals.GuardianForces[5] with { HitPoints = maximum, MaximumHitPoints = maximum }, refreshed.GuardianForces[5]);
        Assert.Equal(used.Vitals.Records, refreshed.Records);
        Assert.Equal(used.Vitals.CombatParty, refreshed.CombatParty);
        Assert.Equal(used.Vitals.GuardianForces[4], refreshed.GuardianForces[4]);
        Assert.Equal((ushort)60000, used.Vitals.GuardianForces[5].HitPoints);
        refreshed.Validate();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void RejectedBattleItemLeavesOwnersUnchanged(int failure)
    {
        NativeCharacterVitals vitals = Vitals() with { DreamEnabled = failure == 0 };
        NativeEconomy economy = Economy(failure == 2 ? 0 : 1);
        if (failure == 7)
        {
            economy = economy with { InitOutSha256 = new string('1', 64) };
        }
        NativeFieldItem item = failure switch
        {
            3 => Lamp with { Type = 13 },
            4 => Lamp with { TargetFlags = 3 },
            _ => Lamp
        };
        string originalVitals = JsonSerializer.Serialize(vitals);
        string originalEconomy = JsonSerializer.Serialize(economy);
        int itemId = failure == 5 ? 0 : failure == 6 ? 256 : 168;
        if (failure <= 2)
        {
            Assert.Throws<InvalidOperationException>(() => item.BeginBattle(vitals, economy, itemId, failure == 1));
        }
        else
        {
            Assert.Throws<NotSupportedException>(() => item.BeginBattle(vitals, economy, itemId, false));
        }
        Assert.Equal(originalVitals, JsonSerializer.Serialize(vitals));
        Assert.Equal(originalEconomy, JsonSerializer.Serialize(economy));
    }

    [Fact]
    public void FormationUsesBothOriginalBytesAndMenuResultKeepsUnsignedWordBits()
    {
        var result = (Lamp with { Power = 0xAB, StatusMask = 0xCD }).BeginBattle(Vitals(), Economy(1), 168, false);
        Assert.Equal((ushort)0xCDAB, result.Encounter);
        Assert.Equal(unchecked((int)0xCDAB0004), result.MenuResult);
    }

    private static NativeCharacterVitals Vitals()
    {
        NativeStatCurve curve = new(1, 0, 1, 1, 1);
        NativeStatModifier modifier = new(0, 0, 0);
        NativeCharacterVitals vitals = new()
        {
            Version = 1,
            Rule = NativeCharacterVitals.InitializationRule,
            InitOutSha256 = new string('0', 64),
            KernelSha256 = new string('0', 64),
            Records = Enumerable.Range(0, 8).Select(id => new NativeCharacterVital((byte)id, new NativeHpWord(60000, 60000), 0, new NativeCharacterJunction(0, 0, 0, new byte[20]), 0, 0, 0, new byte[6], Enumerable.Repeat(new NativeMagicStock(0, 0), 32).ToArray(), 1, 0)).ToArray(),
            CombatParty = [0, 255, 255],
            DreamEnabled = false,
            GuardianForces = Enumerable.Range(0, 16).Select(_ => new NativeGuardianForceVital(123, 0x80, 60000, new NativeGuardianForceLearnedBank(1, 2), 1, ImmutableArray.CreateRange(new byte[21]), 3, 4)).ToArray(),
            Definitions = new NativeStatDefinitions(Enumerable.Repeat(curve, 11).ToImmutableArray(), ImmutableArray.CreateRange(new byte[57]), Enumerable.Repeat(modifier, 19).ToImmutableArray(), Enumerable.Repeat(curve, 16).ToImmutableArray(), Enumerable.Repeat(modifier, 9).ToImmutableArray()),
            MenuDefinitions = new NativeGuardianForceMenuDefinitions(Enumerable.Range(0, 16).Select(_ => Enumerable.Repeat(new NativeGuardianForceLearningRule(0, 255, 0), 21).ToImmutableArray()).ToImmutableArray(), ImmutableArray.CreateRange(new byte[116]), ImmutableArray.CreateRange(new byte[19]), ImmutableArray.CreateRange(new byte[5])),
            DreamWeapons = new byte[3],
            JunctionPartyBackup = new byte[6]
        };
        vitals.Validate();
        return vitals;
    }

    private static NativeEconomy Economy(int count)
    {
        NativeItemStock[] items = Enumerable.Repeat(new NativeItemStock(0, 0), NativeEconomy.ItemSlotCount).ToArray();
        items[7] = new NativeItemStock(168, (byte)count);
        return new NativeEconomy
        {
            Version = 1,
            Rule = NativeEconomy.InitializationRule,
            InitOutSha256 = new string('0', 64),
            Items = items,
            Gil = 0,
            PastGil = 0,
            EventBankGil72 = 0,
            EventBankPastGil76 = 0
        };
    }
}
