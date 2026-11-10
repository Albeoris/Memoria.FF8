using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    internal IEnumerable<MenuEntry> GuardianForcePassiveEntries()
    {
        return Data.GuardianForces.SelectMany(gf => gf.LearningChoices.Where(choice => choice.IsPassive && choice.Learned).Select(choice => new MenuEntry($"{gf.Id}-passive-{choice.Id}", choice.Name, $"{gf.Name}: {choice.Description} {GuardianForcePassiveState(choice)}", Action: new MenuAction("gf-passive-toggle", choice.Id, gf.Id))));
    }

    private MenuAbilityChoice? GuardianForcePassiveChoice(MenuEntry entry)
    {
        return entry.Action is { Kind: "gf-passive-toggle" } action
            ? Data.GuardianForces.FirstOrDefault(gf => gf.Id == action.TargetId)?.LearningChoices.FirstOrDefault(choice => choice.Id == action.SubjectId && choice.IsPassive && choice.Learned)
            : null;
    }

    private string GuardianForcePassiveState(MenuAbilityChoice choice)
    {
        return FormatSystemText("guardian.passive.state", "{0} / {1}", choice.PassiveEnabled ? SystemText("guardian.passive.on", "On") : SystemText("guardian.passive.off", "Off"), choice.PassiveAvailable ? SystemText("guardian.passive.available", "Available") : SystemText("guardian.passive.ko", "GF KO"));
    }

    private static UiColor GuardianForcePassiveColor(MenuAbilityChoice choice)
    {
        return choice.PassiveEnabled ? new UiColor(112, 232, 144) : new UiColor(232, 184, 96);
    }
}
