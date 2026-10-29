using Memoria.FF8.GameUi;

namespace Memoria.FF8.App.Tests;

public sealed class NativeMenuUseFeedbackTests
{
    [Theory]
    [InlineData(3, MenuSound.Recovery)]
    [InlineData(5, MenuSound.StatusRecovery)]
    [InlineData(6, MenuSound.StatusRecovery)]
    public void MagicUsesItsEffectCueOnlyWhenTheTargetChanges(int attackType, MenuSound expected)
    {
        Assert.Equal(expected, NativeMenuUseFeedback.Magic(attackType, true));
        Assert.Equal(MenuSound.Error, NativeMenuUseFeedback.Magic(attackType, false));
    }

    [Theory]
    [InlineData(0, MenuSound.Recovery)]
    [InlineData(3, MenuSound.Recovery)]
    [InlineData(6, MenuSound.Recovery)]
    [InlineData(1, MenuSound.StatusRecovery)]
    [InlineData(2, MenuSound.StatusRecovery)]
    [InlineData(4, MenuSound.StatusRecovery)]
    [InlineData(5, MenuSound.StatusRecovery)]
    [InlineData(16, MenuSound.StatusRecovery)]
    [InlineData(17, MenuSound.StatusRecovery)]
    [InlineData(20, MenuSound.StatusRecovery)]
    [InlineData(21, MenuSound.StatusRecovery)]
    [InlineData(19, MenuSound.ItemUsed)]
    public void ItemUsesOriginalTypeClassificationAndRejectsNoEffect(int type, MenuSound expected)
    {
        Assert.Equal(expected, NativeMenuUseFeedback.Item(type, true));
        Assert.Equal(MenuSound.Error, NativeMenuUseFeedback.Item(type, false));
    }

    [Theory]
    [InlineData(MenuSound.Recovery)]
    [InlineData(MenuSound.StatusRecovery)]
    [InlineData(MenuSound.ItemUsed)]
    [InlineData(MenuSound.Error)]
    public void LiveActionFeedbackReplacesConfirmAndDoesNotLeakToTheNextSelection(MenuSound expected)
    {
        MenuData data = new();
        data.Items.Add(new MenuItem { Id = "item-1", Name = "Potion", Quantity = 2, NativeFieldUseSupported = true });
        data.Characters.Add(new MenuCharacter { Id = "character-0", Name = "Squall", CurrentHp = 10, MaximumHp = 100, NativeFieldItemTargetAllowed = true });
        data.SupportedActions.Add("item-use");
        MenuController? controller = null;
        controller = new MenuController(() => data, _ => controller!.SetActionSound(expected));
        List<MenuSound> sounds = [];
        controller.SoundRequested += sounds.Add;
        controller.Open("item-use");
        controller.Navigate(MenuInput.Confirm);
        sounds.Clear();
        controller.Navigate(MenuInput.Confirm);
        Assert.Equal([expected], sounds);
        controller.Navigate(MenuInput.Cancel);
        sounds.Clear();
        controller.Navigate(MenuInput.Confirm);
        Assert.Equal([MenuSound.Confirm], sounds);
    }
}
