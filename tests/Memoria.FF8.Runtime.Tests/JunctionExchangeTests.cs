using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class JunctionExchangeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExchangesWholePackagesWithoutMovingCharacterIdentityOrRefillingMagic(bool shared)
    {
        NativeCharacterVitals source = MagicSystemFixture.Stock(MagicSystemFixture.Stock(MagicSystemFixture.Create(), 0, 1, 100), 4, 2, 70);
        source = MagicSystemFixture.Modes(source, shared: shared, infinite: true);
        source = source.SetJunctionMagic(0, 0, 1).SetJunctionMagic(4, 1, 2);
        source.Records[0] = source.Records[0] with { Junction = source.Records[0].Junction with { Commands = 0x11223344, Abilities = 0x55667788 }, NativeByte90 = 19 };
        source.Records[0].Junction.MagicJunctions[19] = 56;
        source.Records[4] = source.Records[4] with { Junction = source.Records[4].Junction with { Commands = 0x44332211, Abilities = 0x88776655 }, NativeByte90 = 29 };
        source.MagicSystem.PersonalFieldMasks[0][1] = 3;
        source.MagicSystem.PersonalFieldMasks[4][2] = 7;
        source.MagicSystem.PersonalBattleMasks[0][1] = 1;
        source.MagicSystem.PersonalBattleMasks[4][2] = 15;
        source.MagicSystem.SharedFieldMasks[1] = 3;
        source.MagicSystem.SharedBattleMasks[2] = 7;
        source.MagicSystem.AttackModes[0] = MagicAttackMode.SmartMagic;
        string original = JsonSerializer.Serialize(source);

        NativeCharacterVitals result = source.ExchangeJunctions(0, 4);

        Assert.Equal(source.Records[4].Junction, result.Records[0].Junction);
        Assert.Equal(source.Records[0].Junction, result.Records[4].Junction);
        foreach (int row in new[] { 0, 4 })
        {
            int donor = row == 0 ? 4 : 0;
            NativeCharacterVital expected = source.Records[row] with { Junction = source.Records[donor].Junction, MagicInventory = source.Records[shared ? row : donor].MagicInventory, Status = row == 4 ? (ushort)0x300 : (ushort)0 };
            Assert.Equal(expected, result.Records[row]);
            Assert.Equal(source.MagicSystem.PersonalFieldMasks[shared ? row : donor], result.MagicSystem.PersonalFieldMasks[row]);
            Assert.Equal(source.MagicSystem.PersonalBattleMasks[shared ? row : donor], result.MagicSystem.PersonalBattleMasks[row]);
        }
        Assert.Equal(source.MagicSystem.SharedFieldMasks, result.MagicSystem.SharedFieldMasks);
        Assert.Equal(source.MagicSystem.SharedBattleMasks, result.MagicSystem.SharedBattleMasks);
        Assert.Equal(source.MagicSystem.AttackModes, result.MagicSystem.AttackModes);
        Assert.Equal(source.CombatParty, result.CombatParty);
        Assert.Equal(original, JsonSerializer.Serialize(result.ExchangeJunctions(4, 0)));
        NativeCharacterVitals restored = JsonSerializer.Deserialize<NativeCharacterVitals>(JsonSerializer.Serialize(result))!;
        restored.Validate();
        restored.ValidateMagicSystemConsistency();
        Assert.Equal(result.Records, restored.Records);
        result.Records[4].Junction.MagicJunctions[19] = 0;
        result.MagicSystem.PersonalFieldMasks[0][1] = 0;
        Assert.Equal(original, JsonSerializer.Serialize(source));
    }

    [Fact]
    public void RebuildClampsHpWithoutHealingAndRefreshesReserveStatus()
    {
        NativeCharacterVitals source = MagicSystemFixture.Stock(MagicSystemFixture.Create(), 4, 1, 100).SetJunctionMagic(4, 0, 1);
        source.Records[4] = source.Records[4] with { HitPoints = new NativeHpWord(90, 90), Status = 0x204 };
        source.Records[5] = source.Records[5] with { HitPoints = new NativeHpWord(0, 0), Status = 0x204 };
        NativeCharacterVitals result = source.ExchangeJunctions(4, 5);
        Assert.Equal(new NativeHpWord(10, 10), result.Records[4].HitPoints);
        Assert.Equal((ushort)4, result.Records[4].Status);
        Assert.Equal(new NativeHpWord(0, 0), result.Records[5].HitPoints);
        Assert.Equal((ushort)1, result.Records[5].Status);
        Assert.Equal(source.CombatParty, result.CombatParty);
        Assert.Equal(new NativeHpWord(90, 90), source.Records[4].HitPoints);
    }

    [Fact]
    public void InvalidRowsAndPendingCastsFailWithoutMutation()
    {
        NativeCharacterVitals source = MagicSystemFixture.Create();
        string original = JsonSerializer.Serialize(source);
        Assert.Throws<ArgumentOutOfRangeException>(() => source.ExchangeJunctions(-1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => source.ExchangeJunctions(0, 8));
        Assert.Throws<ArgumentException>(() => source.ExchangeJunctions(0, 0));
        NativeCharacterVitals pending = source with { MagicSystem = source.MagicSystem with { Reservations = [new MagicCastReservation("cast", 0, 0, 1, MagicCastContext.Field, 1, 0)] } };
        Assert.Throws<InvalidOperationException>(() => pending.ExchangeJunctions(0, 1));
        NativeCharacterVitals extended = source with { MagicSystem = source.MagicSystem with { RecordZeroExtension = [new NativeMagicStock(56, 1)] } };
        Assert.Throws<NotSupportedException>(() => extended.ExchangeJunctions(0, 1));
        Assert.Equal(original, JsonSerializer.Serialize(source));
    }
}
