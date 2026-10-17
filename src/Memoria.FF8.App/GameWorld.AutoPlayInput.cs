using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    /// <summary>Runs the same callback as the graphical host while a scripted screen samples physical input.</summary>
    public void AdvanceAutoPlayInputFrame()
    {
        EnsureField();
        if (session.State.Scene != SceneKind.Field || Events.Scene.NativeTutorial is not null || Events.Scene.NativeMenuTutorial is not null || Events.PartyMenu is not null || Events.PendingHostedBattle is not null)
        {
            throw new InvalidOperationException("A scripted input callback requires its current field owner.");
        }
        Events.Step();
        PublishFieldAction(new PlayerActionResult(true, NextTrigger, 1));
    }
}
