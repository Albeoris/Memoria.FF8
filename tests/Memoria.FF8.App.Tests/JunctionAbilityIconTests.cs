using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class JunctionAbilityIconTests
{
    [Theory]
    [InlineData("junction-command")]
    [InlineData("junction-command-choice")]
    [InlineData("junction-ability")]
    [InlineData("junction-ability-choice")]
    public void AssignedSlotsAndCandidatesKeepNativeCategoryIconsInEverySelectionState(string screen)
    {
        MenuData data = new();
        MenuCharacter character = new() { Id = "character-0", Name = "Squall", InParty = true, AbilitySlots = 4 };
        character.Commands.AddRange(["Attack", "Magic", "GF", "--"]);
        character.Abilities.AddRange(["HP+20%", "Mug", "Alert", "--"]);
        character.AvailableCommands.Add(new MenuAbilityChoice("21", "Magic", true) { Icon = NativeAbilityMenuMetadata.Icon(21) });
        character.AvailableCommands.Add(new MenuAbilityChoice("22", "GF", true) { Icon = NativeAbilityMenuMetadata.Icon(22) });
        character.AvailableAbilities.Add(new MenuAbilityChoice("39", "HP+20%", true) { Icon = NativeAbilityMenuMetadata.Icon(39) });
        character.AvailableAbilities.Add(new MenuAbilityChoice("58", "Mug", true) { Icon = NativeAbilityMenuMetadata.Icon(58) });
        character.AvailableAbilities.Add(new MenuAbilityChoice("78", "Alert", true) { Icon = NativeAbilityMenuMetadata.Icon(78) });
        data.Characters.Add(character);
        data.SupportedActions.UnionWith(["junction-command", "junction-ability"]);
        MenuController menus = new(() => data);
        menus.Open(screen);
        UiElement root = menus.BuildView().Root;
        UiElement commandPanel = Assert.Single(Descendants(root), element => element.Id == "junction-command-slots");
        UiElement passivePanel = Assert.Single(Descendants(root), element => element.Id == "junction-passive-slots");
        UiElement candidatePanel = Assert.Single(Descendants(root), element => element.Id == "junction-ability-choices");
        UiList commands = Assert.Single(Descendants(commandPanel).OfType<UiList>());
        UiList passives = Assert.Single(Descendants(passivePanel).OfType<UiList>());
        UiList candidates = Assert.Single(Descendants(candidatePanel).OfType<UiList>());
        UiImageSource commandIcon = new(UiImageKind.Icon, 217);
        UiImageSource characterIcon = new(UiImageKind.Icon, 218);
        UiImageSource partyIcon = new(UiImageKind.Icon, 219);
        UiImageSource guardianForceIcon = new(UiImageKind.Icon, 220);
        Assert.Equal(new UiImageSource?[] { commandIcon, commandIcon, null }, commands.Images);
        Assert.Equal(new UiImageSource?[] { characterIcon, partyIcon, guardianForceIcon, null }, passives.Images);
        Assert.Single(Descendants(commandPanel).OfType<UiLabel>(), label => label.Text == "Attack");
        Assert.DoesNotContain(commands.Items, item => item.Contains("Attack", StringComparison.Ordinal));
        Assert.DoesNotContain(Descendants(commandPanel).OfType<UiImage>(), image => image.Source?.Kind == UiImageKind.Icon);
        UiImageSource?[] expectedCandidates = screen is "junction-command" or "junction-command-choice"
            ? [null, commandIcon, commandIcon]
            : [null, characterIcon, partyIcon, guardianForceIcon];
        string[] expectedNames = screen is "junction-command" or "junction-command-choice" ? ["Empty", "Magic", "GF"] : ["Empty", "HP+20%", "Mug", "Alert"];
        Assert.Equal(expectedNames, candidates.Items);
        Assert.Equal(expectedCandidates, candidates.Images);
    }

    [Theory]
    [InlineData("junction-command")]
    [InlineData("junction-command-choice")]
    [InlineData("junction-ability")]
    [InlineData("junction-ability-choice")]
    public void DemoCandidatesAndAssignedSlotsRetainIconsInEverySelectionState(string screen)
    {
        MenuController menus = new();
        menus.Open(screen, demo: true);
        UiElement root = menus.BuildView().Root;
        UiElement commandPanel = Assert.Single(Descendants(root), element => element.Id == "junction-command-slots");
        UiElement passivePanel = Assert.Single(Descendants(root), element => element.Id == "junction-passive-slots");
        UiElement candidatePanel = Assert.Single(Descendants(root), element => element.Id == "junction-ability-choices");
        UiList commands = Assert.Single(Descendants(commandPanel).OfType<UiList>());
        UiList passives = Assert.Single(Descendants(passivePanel).OfType<UiList>());
        UiList candidates = Assert.Single(Descendants(candidatePanel).OfType<UiList>());
        Assert.Equal(commands.Items.Count, commands.Images.Count);
        Assert.All(commands.Images, image => Assert.Equal(new UiImageSource(UiImageKind.Icon, 217), image));
        Assert.Equal(new UiImageSource?[] { new(UiImageKind.Icon, 218), new(UiImageKind.Icon, 218), new(UiImageKind.Icon, 219) }, passives.Images);
        Assert.NotEmpty(candidates.Items);
        Assert.Equal(candidates.Items.Count, candidates.Images.Count);
        for (int index = 0; index < candidates.Items.Count; index++)
        {
            if (candidates.Items[index] == "Empty")
            {
                Assert.Null(candidates.Images[index]);
            }
            else
            {
                Assert.NotNull(candidates.Images[index]);
                Assert.Equal(UiImageKind.Icon, candidates.Images[index]!.Value.Kind);
            }
        }
        Assert.Single(Descendants(commandPanel).OfType<UiLabel>(), label => label.Text == "Attack");
        Assert.DoesNotContain(commands.Items, item => item.Contains("Attack", StringComparison.Ordinal));
        Assert.DoesNotContain(Descendants(commandPanel).OfType<UiImage>(), image => image.Source?.Kind == UiImageKind.Icon);
    }

    private static IEnumerable<UiElement> Descendants(UiElement root)
    {
        yield return root;
        foreach (UiElement child in root.Children)
        {
            foreach (UiElement descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }
}
