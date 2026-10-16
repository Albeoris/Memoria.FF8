namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    internal string TutorialExchangeId => _exchangeId;
    internal void ClearTutorialActionReceipt()
    {
        Message = "";
        _view = null;
    }
    public void SelectCharacter(string characterId)
    {
        if (!IsOpen || !IsSelectingCharacter)
        {
            throw new InvalidOperationException("A character selection must be open before targeting a character.");
        }
        int index = CharacterChoices.ToList().FindIndex(character => character.Id == characterId);
        if (index < 0)
        {
            throw new InvalidOperationException($"Character '{characterId}' is absent from the current selection.");
        }
        _characterSelectionIndex = index;
        _view = null;
    }

    internal MenuNavigationState CaptureNavigation() => new(ScreenId, _history.Reverse().ToArray(), new(_selections, StringComparer.Ordinal), _characterId, _gfId, _magicId, _exchangeId, _junctionSlot, _commandSlot, _abilitySlot, _characterSelectionDestination, _characterSelectionIndex);

    internal void RestoreNavigation(MenuNavigationState saved)
    {
        if (saved.ScreenId is null || saved.History is null || saved.Selections is null || saved.CharacterId is null || saved.GuardianForceId is null || saved.MagicId is null || saved.ExchangeId is null || saved.JunctionSlot is null || saved.CharacterSelectionDestination is null || !_screens.ContainsKey(saved.ScreenId) || saved.History.Any(screen => screen is null || !_screens.ContainsKey(screen)) || saved.Selections.Any(selection => !_screens.ContainsKey(selection.Key) || selection.Value < 0) || saved.CharacterSelectionDestination.Length > 0 && !_screens.ContainsKey(saved.CharacterSelectionDestination) || saved.CharacterSelectionIndex < 0 || saved.CommandSlot is < 1 or > 3 || saved.AbilitySlot is < 0 or > 3)
        {
            throw new InvalidDataException("Saved tutorial menu navigation is invalid.");
        }
        Open(saved.ScreenId);
        _history.Clear();
        foreach (string screen in saved.History)
        {
            _history.Push(screen);
        }
        _selections.Clear();
        foreach ((string screen, int selection) in saved.Selections)
        {
            _selections.Add(screen, selection);
        }
        _characterId = saved.CharacterId;
        _gfId = saved.GuardianForceId;
        _magicId = saved.MagicId;
        _exchangeId = saved.ExchangeId;
        _junctionSlot = saved.JunctionSlot;
        _commandSlot = saved.CommandSlot;
        _abilitySlot = saved.AbilitySlot;
        _characterSelectionDestination = saved.CharacterSelectionDestination;
        _characterSelectionIndex = saved.CharacterSelectionIndex;
        _view = null;
    }

    /// <summary>Selects the visible command by its identity. Confirmation remains a separate input so the pointer is drawn before activation.</summary>
    public void SelectEntry(string screenId, string entryId, bool allowDisabled = false)
    {
        if (!IsOpen || ScreenId != screenId || IsSelectingCharacter)
        {
            throw new InvalidOperationException($"Expected menu '{screenId}', found '{ScreenId}'.");
        }
        IReadOnlyList<MenuEntry> entries = Entries;
        int index = -1;
        for (int candidate = 0; candidate < entries.Count; candidate++)
        {
            if (entries[candidate].Id == entryId)
            {
                if (index >= 0)
                {
                    throw new InvalidOperationException($"Menu '{screenId}' repeats command '{entryId}'.");
                }
                index = candidate;
            }
        }
        if (index < 0 || !allowDisabled && !entries[index].Enabled)
        {
            throw new InvalidOperationException($"Menu '{screenId}' has no enabled command '{entryId}'.");
        }
        Select(index, entries.Count);
    }
}

internal sealed record MenuNavigationState(string ScreenId, string[] History, Dictionary<string, int> Selections, string CharacterId, string GuardianForceId, string MagicId, string ExchangeId, string JunctionSlot, int CommandSlot, int AbilitySlot, string CharacterSelectionDestination, int CharacterSelectionIndex);
