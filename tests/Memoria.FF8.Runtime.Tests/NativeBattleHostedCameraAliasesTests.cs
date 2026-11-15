using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedCameraAliasesTests
{
    [Fact]
    public void HostedAliasesKeepTheHighestSelectedRowAndRoundTripWithTheirOwner()
    {
        BattleModelRoster roster = Roster();
        BattleModelTargets targets = default(BattleModelTargets).Add(24).Add(OriginalOwner(0)).Add(7);
        NativeBattleCameraAliases observed = NativeBattleCameraAliases.FromColdProcess().Observe50D300(7, targets, roster);
        NativeBattleCameraAliases restored = JsonSerializer.Deserialize<NativeBattleCameraAliases>(JsonSerializer.Serialize(observed))!;
        Assert.Equal((byte)7, restored.Resolve50D790(0x9F));
        Assert.Equal((byte)24, restored.Resolve50D790(0xAF));
        Assert.Equal(roster.Rows.ToArray(), restored.PresentationRoster!.Rows.ToArray());
        NativeBattleCameraAliases next = restored.Observe50D300(0, default, roster);
        Assert.Equal((byte)0, next.Resolve50D790(0x9F));
        Assert.Equal((byte)24, next.Resolve50D790(0xAF));
    }

    [Fact]
    public void UnknownAndUnboundAliasesRejectOnlyWhenResolved()
    {
        BattleModelRoster roster = Roster();
        NativeBattleCameraAliases aliases = new(null, 6) { PresentationRoster = roster };
        aliases.Validate();
        Assert.Throws<NotSupportedException>(() => aliases.Resolve50D790(0x90));
        Assert.Throws<InvalidDataException>(() => aliases.Resolve50D790(0xA0));
        Assert.Throws<InvalidDataException>(() => aliases.Observe50D300(6, default, roster));
        Assert.Throws<InvalidDataException>(() => aliases.Observe50D300(7, default(BattleModelTargets).Add(OriginalOwner(6)), roster));
        Assert.Throws<InvalidDataException>(() => (aliases with { Target1D99AAA = 25 }).Validate());
    }

    [Fact]
    public void NativeAliasesKeepSevenRowValidationAndOriginalJson()
    {
        NativeBattleCameraAliases original = NativeBattleCameraAliases.FromColdProcess().Observe50D300(6, 0x41);
        Assert.Equal((byte)6, original.Resolve50D790(0x90));
        Assert.Equal((byte)6, original.Resolve50D790(0xA0));
        Assert.DoesNotContain("\"PresentationRoster\"", JsonSerializer.Serialize(original));
        Assert.Throws<InvalidDataException>(() => original.Observe50D300(7, 1));
        Assert.Throws<InvalidDataException>(() => new NativeBattleCameraAliases(0, 7).Validate());
    }

    private static BattleModelRoster Roster()
    {
        BattleModelOwner?[] rows = new BattleModelOwner?[25];
        rows[0] = new BattlePartyActorModel(new("original"));
        rows[7] = new BattlePartyActorModel(new("source"));
        rows[24] = new BattleEnemyActorModel(new("target"));
        return new([.. rows]);
    }
}
