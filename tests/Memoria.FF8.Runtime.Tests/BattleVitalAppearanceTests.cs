using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class BattleVitalAppearanceTests
{
    [Theory]
    [InlineData(249, 1000, true)]
    [InlineData(250, 1000, false)]
    [InlineData(251, 1000, false)]
    [InlineData(249, 1003, true)]
    [InlineData(250, 1003, false)]
    public void CriticalUsesStrictQuarterOfCachedMaximum(int cachedHp, int maximum, bool critical)
    {
        BattleSession session = Create(new((ushort)cachedHp, (ushort)maximum, null, null) { DisplayedHitPointsQ12 = cachedHp << 12 });

        Assert.Equal(new(false, false, critical), session.ReadVitalAppearance("party"));
    }

    [Theory]
    [InlineData(0, true, true)]
    [InlineData(1, false, true)]
    [InlineData(4095, false, true)]
    [InlineData(4096, false, false)]
    public void KnockoutNameWaitsForExactZeroWhileDigitsUseWholeDisplayedHp(int displayedQ12, bool nameKnockedOut, bool digitsKnockedOut)
    {
        BattleSession session = Create(new(0, 1000, null, null) { DisplayedHitPointsQ12 = displayedQ12 }, knockedOut: true);

        Assert.Equal(new(nameKnockedOut, digitsKnockedOut, true), session.ReadVitalAppearance("party"));
    }

    [Fact]
    public void EmptyDisplayedHpDoesNotSupplyTheSavedKnockoutFlag()
    {
        BattleSession session = Create(new(500, 1000, null, null) { DisplayedHitPointsQ12 = 0 });

        Assert.Equal(new(false, false, false), session.ReadVitalAppearance("party"));
    }

    [Theory]
    [InlineData(249, 900, true)]
    [InlineData(500, 1, false)]
    public void CriticalUsesCachedHpInsteadOfLogicalOrCountingDigits(int cachedHp, int displayedHp, bool critical)
    {
        BattleSession session = Create(new((ushort)cachedHp, 1000, null, null) { DisplayedHitPointsQ12 = displayedHp << 12 });

        Assert.Equal(900, session.Combatants.Single(actor => actor.Id == "party").CurrentHp);
        Assert.Equal(displayedHp, session.ReadDisplayedVital("party").HitPoints);
        Assert.Equal(critical, session.ReadVitalAppearance("party").Critical);
    }

    [Fact]
    public void GuardianAppearanceUsesItsOwnCachedMaximumAndDisplayedAccumulator()
    {
        BattleSession session = Create(new(500, 1000, 49, 200)
        {
            DisplayedHitPointsQ12 = 500 << 12,
            DisplayedGuardianForceHitPointsQ12 = 100 << 12
        });

        Assert.Equal(new(false, false, false), session.ReadVitalAppearance("party"));
        Assert.Equal(new(false, false, true), session.ReadVitalAppearance("party", 0));
        Assert.Equal(100, session.ReadDisplayedVital("party", 0).HitPoints);
    }

    private static BattleSession Create(NativeBattlePartyPresentedVital row, bool knockedOut = false)
    {
        BattleSession session = new(new()
        {
            GuardianForces = [new(0, 180, 200)],
            Combatants =
            [
                new() { Id = "party", Name = "Party", IsPlayer = true, CurrentHp = knockedOut ? 0 : 900, MaximumHp = 1000, NativeStatus0 = knockedOut ? (ushort)1 : (ushort)0 },
                new() { Id = "survivor", Name = "Survivor", IsPlayer = true },
                new() { Id = "enemy", Name = "Enemy" }
            ]
        }, BattleAtbMode.TurnBased);
        NativeBattlePartyPresentation presentation = new([row, null, null]);
        presentation.Validate();
        typeof(BattleSession).GetField("nativePartyPresentation", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(session, presentation);
        return session;
    }
}
