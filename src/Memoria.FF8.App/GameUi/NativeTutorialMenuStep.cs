namespace Memoria.FF8.GameUi;

internal enum NativeTutorialPress
{
    Select,
    Confirm,
    Cancel,
    Inspect
}

/// <summary>A resolved original tutorial input. Screen and entry identities never depend on the host menu's ordering or geometry.</summary>
internal sealed record NativeTutorialMenuStep(string ScreenId, string? EntryId = null, NativeTutorialPress Press = NativeTutorialPress.Select, string? CharacterId = null, MenuAction? Context = null, string? Destination = null);
