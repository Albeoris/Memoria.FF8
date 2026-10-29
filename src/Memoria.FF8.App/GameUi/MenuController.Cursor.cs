namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private void InitializeScreenCursor(string screenId)
    {
        if (IsNamingScreen)
        {
            return;
        }
        _selections[screenId] = 0;
        if (screenId is not ("item-target" or "magic-target"))
        {
            return;
        }
        _liveData = null;
        Dictionary<string, int>? scores = screenId == "item-target" ? Item?.RecoveryTargetScores : Magic?.RecoveryTargetScores;
        if (scores is null || scores.Count == 0)
        {
            return;
        }
        IReadOnlyList<MenuEntry> entries = Entries;
        int bestScore = 0;
        for (int index = 0; index < entries.Count; index++)
        {
            MenuEntry entry = entries[index];
            int score = scores.GetValueOrDefault(entry.Action?.TargetId ?? entry.Id);
            if (entry.Enabled && score > bestScore)
            {
                bestScore = score;
                _selections[screenId] = index;
            }
        }
    }
}
