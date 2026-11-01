using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class GuardianSummonCommandNameTests
{
    [Theory]
    [InlineData(false, 2, false)]
    [InlineData(true, 2, true)]
    [InlineData(true, 0, false)]
    [InlineData(true, 1, false)]
    public void SummonNameRequiresEnabledSupportedGuardian(bool enabled, int guardian, bool expected)
    {
        bool supported = NativeAbilityMenuMetadata.SupportsSummon(guardian, enabled);
        Assert.Equal(expected, supported);
        Assert.Equal(expected ? "GF / Summon" : "GF", NativeAbilityMenuMetadata.CommandName(21, "GF", supported));
        Assert.Equal("Magic", NativeAbilityMenuMetadata.CommandName(20, "Magic", supported));
    }

    [Fact]
    public void SummonNamePreservesLocalizedCommandAndUsesLocalizedAction()
    {
        Assert.Equal("G.F. / Beschwören", NativeAbilityMenuMetadata.CommandName(21, "G.F.", true, (key, fallback) => key == "battle.ui.choicelabels.summon" ? "Beschwören" : fallback));
    }

    [Theory]
    [InlineData(false, 2, true, false)]
    [InlineData(true, 2, true, true)]
    [InlineData(true, 0, true, false)]
    [InlineData(true, 2, false, false)]
    public void LiveProjectionNamesOnlyTheLearnedJunctionedCapability(bool enabled, int guardian, bool learned, bool actorSummon)
    {
        NativeCharacterVitals vitals = GuardianForceAbilityEditorTests.Source(22);
        if (learned)
        {
            vitals = vitals.TeachGuardianForceAbility(guardian, 21);
        }
        vitals = vitals.ToggleGuardianForceJunction(0, guardian);
        vitals.Records[0] = vitals.Records[0] with { Junction = vitals.Records[0].Junction with { Commands = learned ? 21u : 0u } };
        GameState state = new() { Scene = SceneKind.Field, CharacterVitals = vitals, GameplayPreferences = new() { GuardianFormsEnabled = enabled } };
        MenuData data = LiveMenuData.Build(state);
        MenuCharacter actor = data.Characters.Single(character => character.Id == "character-0");
        if (learned)
        {
            MenuAbilityChoice command = Assert.Single(actor.AvailableCommands, choice => choice.Id == "21");
            Assert.Equal(actorSummon ? "Ability 21 / Summon" : "Ability 21", command.Name);
            Assert.Equal(actorSummon ? "Command ability 21 / Summon" : "Command ability 21", actor.Commands[1]);
            Assert.Equal(NativeAbilityMenuMetadata.Icon(21), command.Icon);
        }
        else
        {
            Assert.DoesNotContain(actor.AvailableCommands, choice => choice.Id == "21");
        }
        foreach (MenuGuardianForce gf in data.GuardianForces)
        {
            MenuAbilityChoice command = Assert.Single(gf.LearningChoices, choice => choice.Id == "21");
            Assert.Equal(enabled && gf.NativeId == 2 ? "Ability 21 / Summon" : "Ability 21", command.Name);
        }
    }

    [Fact]
    public void SupportedGuardianUsesTheCommandLearnedFromAnotherAssignedGuardian()
    {
        NativeCharacterVitals vitals = GuardianForceAbilityEditorTests.Source(22).TeachGuardianForceAbility(0, 21).ToggleGuardianForceJunction(0, 0).ToggleGuardianForceJunction(0, 2);
        vitals.Records[0] = vitals.Records[0] with { Junction = vitals.Records[0].Junction with { Commands = 21 } };
        GameState state = new() { Scene = SceneKind.Field, CharacterVitals = vitals, GameplayPreferences = new() { GuardianFormsEnabled = true } };
        MenuCharacter actor = LiveMenuData.Build(state).Characters.Single(character => character.Id == "character-0");
        Assert.Equal("Ability 21 / Summon", Assert.Single(actor.AvailableCommands, choice => choice.Id == "21").Name);
        Assert.Equal("Command ability 21 / Summon", actor.Commands[1]);
        Assert.False(vitals.GuardianForceAbilities(2).Single(ability => ability.Ability == 21).Learned);
    }
}
