using Albeoris.Games.FF8.Jsm;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private string? UnsupportedNativeActorVisibility(Fiber fiber, JsmOperation operation)
    {
        if (operation.Opcode is not (Jsm.Opcode.HIDE or Jsm.Opcode.SHOW))
        {
            return null;
        }
        if (actors[fiber.ObjectId].NativeSavedCollisionDisabled16 is null || !program!.NativeObjects.Take(program.NativeActorCount).Contains(fiber.ObjectId))
        {
            return "Native HIDE/SHOW requires its allocated actor DWORD352 mask16 and current collision byte588; original PC and contact state preserved";
        }
        return null;
    }

    private void SetNativeActorVisibility(ActorState actor, bool visible)
    {
        bool savedCollisionDisabled = actor.NativeSavedCollisionDisabled16 ?? throw new NotSupportedException("Native HIDE/SHOW lost its allocated actor DWORD352 mask16.");
        if (visible)
        {
            // Native51EAD0 restores588 from mask16, independently of current588.
            actor.CollisionEnabled = !savedCollisionDisabled;
        }
        else
        {
            // Native51EB40 saves current588 on every call, including repeated HIDE.
            actor.NativeSavedCollisionDisabled16 = !actor.CollisionEnabled;
            actor.CollisionEnabled = false;
        }
        actor.PushEnabled = visible;
        actor.TalkEnabled = visible;
        actor.Visible = visible;
    }
}
