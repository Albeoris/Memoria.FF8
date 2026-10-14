namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    public void InitializeNewGameSalaryTable(NativeSalaryTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        table.Validate();
        if (State.SeeD is not NativeSeeDState seed || seed.SalaryTable is not null || State.Scene != SceneKind.Field || State.FieldId != 74 || State.EventState is not null || State.ReplayJournal is not { EntryCount: 0, IsComplete: true } || State.SavedWorld?.Origin.Kind != NativeSavedWorldOriginKind.NativeNewGame)
        {
            throw new NotSupportedException("Salary table initialization requires untouched authentic New Game before its first engine transaction.");
        }
        SetSeeD(seed with { SalaryTable = table });
    }
}
