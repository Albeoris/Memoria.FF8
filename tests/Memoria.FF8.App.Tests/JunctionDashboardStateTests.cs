using Memoria.FF8.GameUi;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.App.Tests;

public sealed class JunctionDashboardStateTests
{
    [Fact]
    public void IndependentDirectionsFollowOnlyNewRelevantDestinations()
    {
        JunctionDashboardState state = new();
        Assert.Equal(JunctionEffectDirection.Defense, state.ElementalDirection);
        Assert.Equal(JunctionEffectDirection.Defense, state.StatusDirection);
        Assert.True(state.SetDestination("Elem-Atk"));
        state.SetDestination("ST-Atk");
        state.SetDirection(JunctionEffectFamily.Elemental, JunctionEffectDirection.Defense);
        state.SetDirection(JunctionEffectFamily.Status, JunctionEffectDirection.Defense);
        state.SetDestination("Str");
        state.SetDestination("Elem-Atk");
        state.SetDestination("ST-Atk");
        Assert.Equal(JunctionEffectDirection.Defense, state.ElementalDirection);
        Assert.Equal(JunctionEffectDirection.Defense, state.StatusDirection);
        state.SetDestination("Elem-Def 1");
        state.SetDestination("Elem-Atk");
        Assert.Equal(JunctionEffectDirection.Attack, state.ElementalDirection);
        Assert.Equal(JunctionEffectDirection.Defense, state.StatusDirection);
    }

    [Fact]
    public void PinsBelongToStableRecordsAndCanProtectEmptySlots()
    {
        JunctionDashboardState state = new();
        state.SetPinned("character-0", "HP", true);
        Assert.True(state.IsPinned("character-0", "HP"));
        Assert.False(state.IsPinned("character-1", "HP"));
        HashSet<string> returnedPins = Assert.IsType<HashSet<string>>(state.GetPinnedSlots("character-0"));
        returnedPins.Clear();
        Assert.True(state.IsPinned("character-0", "HP"));
        Assert.Throws<ArgumentException>(() => state.SetPinned("character-0", "Elem-Atk", true));
    }

    [Fact]
    public void JunctionStringsAreExportedWithStableSemanticKeys()
    {
        Assert.Equal("Optimize stats", SystemTextCatalog.Defaults["junction.optimize"]);
        Assert.Equal("None — No added element", SystemTextCatalog.Defaults["junction.elemental.none"]);
        Assert.Contains(SystemTextCatalog.SourceEntries(), entry => entry.Key == SystemTextCatalog.Key("junction.command.leave"));
    }

    [Fact]
    public void SessionChangeClearsPinsAndInspectionButOrdinaryRefreshDoesNot()
    {
        JunctionDashboardState state = new();
        state.SetSession("session-one");
        state.SetPinned("character-0", "HP", true);
        state.SetDestination("ST-Atk");
        state.SetSession("session-one");
        Assert.True(state.IsPinned("character-0", "HP"));
        Assert.False(state.SetDestination("ST-Atk"));
        state.SetSession("session-two");
        Assert.False(state.IsPinned("character-0", "HP"));
        Assert.Equal(JunctionEffectDirection.Defense, state.StatusDirection);
        Assert.True(state.SetDestination("ST-Atk"));
    }
}
