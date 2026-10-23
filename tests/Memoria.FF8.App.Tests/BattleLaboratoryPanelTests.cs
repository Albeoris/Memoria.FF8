using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BattleLaboratoryPanelTests
{
    [Fact]
    public void StartRequiresConfirmationRatherThanAnAdjustmentGesture()
    {
        int starts = 0;
        BattleDebugController editor = new(new BattleDefinition { Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, BattleDebugDefaults.Dummy] }, new BattleDebugCatalog(), BattleAtbMode.Normal, (_, _) => starts++, () => { });
        editor.Navigate(MenuInput.Right);
        editor.Navigate(MenuInput.Left);
        Assert.Equal(0, starts);
        editor.Navigate(MenuInput.Confirm);
        Assert.Equal(1, starts);
    }

    [Fact]
    public void LocalPanelsKeepActorDetailsSeparateAndBackRestoresParticipantFocus()
    {
        BattleDebugController editor = Create();
        Assert.DoesNotContain(editor.Rows, row => row.Label == "Party 1");
        Adjust(editor, "Editor panel");
        Assert.Contains(editor.Rows, row => row.Label == "Party 1");
        Assert.DoesNotContain(editor.Rows, row => row.Label.Contains("Maximum HP", StringComparison.Ordinal));
        Adjust(editor, "Party 1");
        Assert.Contains(editor.Rows, row => row.Label == "Party 1 · Character / model");
        Assert.DoesNotContain(editor.Rows, row => row.Label == "Party 1 · Maximum HP");
        Adjust(editor, "Actor details");
        Assert.Contains(editor.Rows, row => row.Label == "Party 1 · Maximum HP");
        Assert.DoesNotContain(editor.Rows, row => row.Label == "Party 1 · Weapon");
        Adjust(editor, "Actor details");
        Assert.Contains(editor.Rows, row => row.Label == "Party 1 · Weapon");
        Adjust(editor, "Actor details");
        Assert.Contains(editor.Rows, row => row.Label == "Party 1 · Poison");
        Assert.DoesNotContain(editor.Rows, row => row.Label == "Party 1 · Poison duration");
        Adjust(editor, "Party 1 · Poison");
        Assert.Contains(editor.Rows, row => row.Label == "Party 1 · Poison duration");
        Assert.Contains(editor.Rows, row => row.Label == "Party 1 · Poison interval");
        editor.Navigate(MenuInput.Cancel);
        Assert.Equal("Party 1", editor.Rows[editor.SelectedIndex].Label);
        Assert.Contains(editor.Rows, row => row.Label == "Start scenario");
    }

    [Fact]
    public void OpeningSnapshotResetRestoresActorsAndStageButKeepsModeAndPresetSlot()
    {
        BattleDebugController editor = Create();
        Adjust(editor, "Scenario ATB mode");
        Adjust(editor, "Battle stage");
        Adjust(editor, "Party members");
        Adjust(editor, "Editor panel");
        Adjust(editor, "Editor panel");
        Adjust(editor, "Preset slot");
        Adjust(editor, "Editor panel");
        Adjust(editor, "Current party + last enemy");
        Assert.Equal(2, editor.Definition.StageId);
        Assert.Single(editor.Definition.Combatants, actor => actor.IsPlayer);
        Assert.Equal(BattleAtbMode.TurnBased, editor.Mode);
        Adjust(editor, "Editor panel");
        Adjust(editor, "Editor panel");
        Assert.StartsWith("2 ·", editor.Rows.Single(row => row.Label == "Preset slot").Value, StringComparison.Ordinal);
    }

    private static BattleDebugController Create()
    {
        BattleDefinition definition = new() { StageId = 2, Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, BattleDebugDefaults.Dummy] };
        return new BattleDebugController(definition, new BattleDebugCatalog { StageIds = [2, 4] }, BattleAtbMode.Normal, (_, _) => { }, () => { });
    }

    private static void Adjust(BattleDebugController editor, string label)
    {
        int index = editor.Rows.Select((row, index) => (row, index)).Single(pair => pair.row.Label == label).index;
        editor.Select(index, true);
    }
}
