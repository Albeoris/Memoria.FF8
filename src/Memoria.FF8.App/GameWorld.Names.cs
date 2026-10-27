using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    /// <summary>Applies a research name to the current standalone snapshot without advancing the game clock.</summary>
    public void Rename(int subject, string text)
    {
        if (!GameNames.IsValidSubject(subject))
        {
            throw new ArgumentOutOfRangeException(nameof(subject));
        }
        string name = GameNames.Normalize(text);
        if (battle is not null || session.HasActiveBattle)
        {
            throw new NotSupportedException("Character naming is unavailable while a game-owned battle is active.");
        }
        if (session.State.Scene is not (SceneKind.Field or SceneKind.WorldMap))
        {
            throw new NotSupportedException("Character naming requires an active field or world session.");
        }
        EnsureField();
        if (Events.FieldId != session.State.FieldId && session.State.Scene != SceneKind.WorldMap)
        {
            throw new NotSupportedException("Character naming requires the current field allocation to finish.");
        }
        Events.Scene.Names[subject] = name;
        session.SetEventState(Events.Capture());
    }
}
