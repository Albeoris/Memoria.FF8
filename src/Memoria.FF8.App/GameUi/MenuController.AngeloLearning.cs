using System.Diagnostics;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private string? learnedAngeloAbilityId;

    internal bool PresentAngeloLearning(NativeLimitProgress? before, NativeLimitProgress? after)
    {
        if (before is null || after is null)
        {
            return false;
        }
        int learned = after.AngeloFlags & ~before.AngeloFlags;
        if (learned == 0)
        {
            return false;
        }
        int ability = System.Numerics.BitOperations.TrailingZeroCount((uint)learned);
        Open("status-angelo");
        _characterId = "character-4";
        learnedAngeloAbilityId = $"limit-{ability}";
        SelectEntry("status-angelo", learnedAngeloAbilityId);
        return true;
    }

    private UiColor? AngeloLearnedAbilityColor(string abilityId)
    {
        return Character?.Id == "character-4" && learnedAngeloAbilityId == abilityId
            ? LearnedAbilityHighlight.Color(Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency)
            : null;
    }
}
