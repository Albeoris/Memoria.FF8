namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCommandRowSelection([property: System.Text.Json.Serialization.JsonRequired] sbyte ActorIndex, [property: System.Text.Json.Serialization.JsonRequired] byte RowIndex, [property: System.Text.Json.Serialization.JsonRequired] byte RowFlags, [property: System.Text.Json.Serialization.JsonRequired] bool Editing)
{
    public void Validate()
    {
        if (ActorIndex is < 0 or > 2 || RowIndex > 3)
        {
            throw new InvalidDataException("Native command selection requires a physical party actor and one of four rows.");
        }
    }
}

public sealed partial class BattleSession
{
    public void SetNativeCommandRowSelection(string actorId, byte row, bool editing, bool hasLimit)
    {
        if (NativeGraphics?.UiServices is not { } ui)
        {
            return;
        }
        NativeBattleLimitCommandRow current = ui.CommandRow ?? throw new NotSupportedException("Native command row requires its retained widget BYTE26; migrate the named snapshot explicitly.");
        sbyte actor = checked((sbyte)GetActor(actorId).NativeIndex);
        NativeBattleCommandRowSelection selection = new(actor, row, hasLimit ? (byte)4 : (byte)0, editing);
        selection.Validate();
        if (ui.CommandRowSelection is { } previous && previous.ActorIndex != actor)
        {
            current = NativeBattleLimitCommandRow.Reset4BB840();
        }
        //TODO(TD-009): The current command-menu transport supplies state7 admission until native entry/opening phases are owned.
        NativeGraphics = NativeGraphics with { UiServices = ui with { CommandRow = current, CommandRowSelection = selection } };
    }

    public NativeBattleLimitCommandRow? NativeCommandRow => NativeGraphics?.UiServices?.CommandRow;

    private void AdvanceNativeCommandRow4BB9E0()
    {
        NativeBattleGraphicsState graphics = RequireNativeGraphics();
        NativeBattleUiServices ui = graphics.UiServices!;
        if (Outcome == BattleOutcome.Active)
        {
            NativeGraphics = graphics with { UiServices = ui.AdvanceCommandRow4BB9E0() };
        }
    }

    private static NativeBattleUiServices ConfirmNativeCommandRow4BC770(NativeBattleUiServices ui)
    {
        if (ui.CommandRowSelection is not { } selection)
        {
            return ui;
        }
        NativeBattleLimitCommandRow row = ui.CommandRow ?? throw new NotSupportedException("Native4BC770 requires retained widget BYTE26.");
        return ui with { CommandRow = row.Confirm4BC770(selection.RowFlags), CommandRowSelection = selection with { Editing = false } };
    }
}
