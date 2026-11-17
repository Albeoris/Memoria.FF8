using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

internal static class BattleHudTestPresentation
{
    internal static BattlePresentationUiState Visible()
    {
        return new(commandMenuHidden: false, vitalsVisible: true, guardianBoostOwnsInput: false, paused: false);
    }
}
