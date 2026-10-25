using System.Text.Json;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class LiveMenuTextTests
{
    [Theory]
    [InlineData("Angelo1")]
    [InlineData("Angelo2")]
    public void LimitNamesExpandBothAngeloAliasesUsingSavedNames(string alias)
    {
        GameState state = new()
        {
            EventState = JsonSerializer.SerializeToElement(new { Scene = new { Names = new Dictionary<int, string> { [2] = "Comet" } } })
        };
        string original = JsonSerializer.Serialize(state);

        Assert.Equal("Comet Rush", LiveMenuCatalog.FormatText($"{{Char {alias}}} Rush", LiveMenuData.TextNames(state, null)));
        Assert.Equal(original, JsonSerializer.Serialize(state));
    }

    [Fact]
    public void MenuNamesUseCurrentOverridesAndRetainLiteralCustomText()
    {
        GameState state = new()
        {
            DepartingFieldState = JsonSerializer.SerializeToElement(new { Scene = new { Names = new Dictionary<int, string> { [2] = "Saved" } } })
        };
        IReadOnlyDictionary<int, string> savedNames = LiveMenuData.TextNames(state, null);
        Dictionary<int, string> currentNames = new() { [2] = "Comet{Page}" };

        Assert.Equal("Saved Recover", LiveMenuCatalog.FormatText("{Char Angelo2} Recover", savedNames));
        Assert.Equal("Comet{Page} Recover", LiveMenuCatalog.FormatText("{Char Angelo2} Recover", LiveMenuData.TextNames(state, currentNames)));
        Assert.Equal("Angelo Recover", LiveMenuCatalog.FormatText("{Char Angelo2} Recover", LiveMenuData.TextNames(new GameState(), null)));
        Assert.Equal("Comet{Page}", currentNames[2]);
        Assert.Null(LiveMenuCatalog.FormatText(null));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData((byte)0, true)]
    [InlineData((byte)1, true)]
    public void AngeloLearningRequiresTheSavedPetPalsOwner(byte? magazineFlags, bool supported)
    {
        NativeLimitProgress progress = new(new string('A', 64), 0, 0, 0, 0, [0, 0, 0, 0, 0, 0, 0, 0]) { PetPalsFlags = magazineFlags };
        MenuData data = LiveMenuData.Build(new GameState { LimitProgress = progress });

        Assert.Equal(supported, data.SupportedActions.Contains("angelo-learn"));
    }
}
