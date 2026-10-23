using System.Diagnostics;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private Dictionary<string, string>? battleLearnedAbilityIds;
    private readonly HashSet<string> battleLearningVisited = new(StringComparer.Ordinal);

    public bool IsBattleLearningReview => battleLearnedAbilityIds is not null;

    public void OpenBattleLearning(IReadOnlyDictionary<string, string> learnedAbilityIds)
    {
        ArgumentNullException.ThrowIfNull(learnedAbilityIds);
        Open("gf-learn");
        battleLearnedAbilityIds = new Dictionary<string, string>(learnedAbilityIds, StringComparer.Ordinal);
        if (Data.GuardianForces.Count == 0)
        {
            Close();
            return;
        }
        _gfId = Data.GuardianForces[0].Id;
        FocusBattleLearnedAbility();
    }

    private void ClearBattleLearningReview()
    {
        battleLearnedAbilityIds = null;
        battleLearningVisited.Clear();
    }

    private void FocusBattleLearnedAbility()
    {
        if (battleLearnedAbilityIds is null || GuardianForce is not { } force)
        {
            return;
        }
        battleLearningVisited.Add(force.Id);
        int index = battleLearnedAbilityIds.TryGetValue(force.Id, out string? ability) ? Entries.ToList().FindIndex(entry => entry.Id == ability) : -1;
        _selections[ScreenId] = Math.Max(0, index);
        _view = null;
    }

    private bool BackFromBattleLearning()
    {
        if (!IsBattleLearningReview || ScreenId != "gf-learn")
        {
            return false;
        }
        SoundRequested?.Invoke(MenuSound.Cancel);
        MenuGuardianForce? next = Data.GuardianForces.FirstOrDefault(force => !battleLearningVisited.Contains(force.Id));
        if (next is null)
        {
            Close();
        }
        else
        {
            _gfId = next.Id;
            FocusBattleLearnedAbility();
        }
        return true;
    }

    private UiColor? BattleLearnedAbilityColor(string guardianForceId, string abilityId)
    {
        if (battleLearnedAbilityIds is null || !battleLearnedAbilityIds.TryGetValue(guardianForceId, out string? learned) || learned != abilityId)
        {
            return null;
        }
        double phase = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency * Math.PI;
        return new UiColor((byte)(208 + 47 * Math.Sin(phase)), (byte)(208 + 47 * Math.Sin(phase + 2 * Math.PI / 3)), (byte)(208 + 47 * Math.Sin(phase + 4 * Math.PI / 3)));
    }
}
