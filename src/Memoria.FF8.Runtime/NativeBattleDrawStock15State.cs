using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDrawStock15State([property: JsonRequired] NativeBattleCuragaState Nodes)
{
    public void Validate()
    {
        if (Nodes is null || Nodes.SourceArenaOffset < 0 || Nodes.SourceArenaOffset > NativeBattleEffectArena.Length - 2 * NativeBattleDrawStock15Profile.DrawStep || Nodes.AuxiliaryCursor < 0 || Nodes.AuxiliaryCursor > NativeBattleEffectArena.Length || Nodes.PrimaryDrawCursor is < 0 or > NativeBattleEffectArena.Length || Nodes.SecondaryDrawCursor is < 0 or > NativeBattleEffectArena.Length)
        {
            throw new InvalidDataException("Draw Stock requires its original current arena cursors and generic node owner.");
        }
        Nodes.CopiedCamera?.Validate();
    }
}
