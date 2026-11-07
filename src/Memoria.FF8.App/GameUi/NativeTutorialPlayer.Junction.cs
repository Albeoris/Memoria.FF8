namespace Memoria.FF8.GameUi;

internal sealed partial class NativeTutorialPlayer
{
    private void PrepareJunctionInput(NativeTutorialMenuStep step)
    {
        if (step.Press == NativeTutorialPress.Cancel)
        {
            return;
        }
        string[] preparation = step.DashboardPreparation ?? [];
        if (preparation.Length == 0)
        {
            CloseJunctionPicker();
        }
        foreach (string control in preparation)
        {
            JunctionDashboardNavigationState navigation = Menu.CaptureJunctionDashboard();
            if (control == "junction-actions")
            {
                if (navigation.Editor is not ("actions" or "optimize"))
                {
                    CloseJunctionPicker();
                    Menu.Navigate(MenuInput.Actions);
                }
            }
            else if (control == "optimize")
            {
                if (navigation.Editor != "optimize")
                {
                    ActivateJunctionControl(control);
                }
            }
            else if (control.StartsWith("priority-", StringComparison.Ordinal))
            {
                if (!navigation.Review)
                {
                    ActivateJunctionControl(control);
                }
            }
            else if (navigation.Editor is not ("magic" or "command" or "passive") || navigation.Origin != control)
            {
                CloseJunctionPicker();
                ActivateJunctionControl(control);
            }
        }
        if (step.EntryId is string entry)
        {
            Menu.SelectEntry(step.ScreenId, entry);
        }
    }

    private void ActivateJunctionControl(string control)
    {
        Menu.SelectEntry("junction-dashboard", control);
        Menu.Navigate(MenuInput.Confirm);
    }

    private void CloseJunctionPicker()
    {
        JunctionDashboardNavigationState navigation = Menu.CaptureJunctionDashboard();
        if (navigation.Review || navigation.Editor is "magic" or "command" or "passive" or "actions" or "optimize")
        {
            Menu.Navigate(MenuInput.Cancel);
        }
    }

    private void RestoreJunctionPicker()
    {
        if (!Menu.IsJunctionDashboard || Menu.CaptureJunctionDashboard() is not { Editor: "magic" } navigation)
        {
            return;
        }
        bool omitted = steps.Values.Any(step => step.Context is { Kind: "select-magic" } stock && navigation.Focus == "candidate-" + stock.SubjectId && step.DashboardPreparation?.Contains(navigation.Origin, StringComparer.Ordinal) == true);
        if (omitted)
        {
            Menu.Navigate(MenuInput.Cancel);
            ActivateJunctionControl(navigation.Origin);
        }
    }
}
