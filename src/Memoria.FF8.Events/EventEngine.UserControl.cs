namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private void ExecuteNativeUserControlOff()
    {
        if (!userControlEnabled)
        {
            return;
        }
        if (Scene.PartyBindingMemory is { Version: 1 } binding && Scene.NativeActorLayoutVersion == 1)
        {
            // Native51DE90 resets full idle/phase0 for the original saved party
            // bodies before disabling their ordinary locomotion callbacks.
            List<ActorState> party = [];
            for (int slot = 0; slot < 3; slot++)
            {
                byte nativeId = binding.At(slot);
                if (nativeId == byte.MaxValue)
                {
                    continue;
                }
                if (program is null || nativeId >= Scene.NativeActorObjectIds.Count)
                {
                    throw new NotSupportedException("UCOFF requires its original saved party body mapping.");
                }
                ActorState member = actors[Scene.NativeActorObjectIds[nativeId]];
                if (!HasNativeEmptyAnimationModel(member) && AnimationFrameCount?.Invoke(member.ModelId, member.BaseAnimationId) is not > 0)
                {
                    throw new NotSupportedException("UCOFF requires the saved party body's actual full idle animation.");
                }
                party.Add(member);
            }
            foreach (ActorState member in party)
            {
                SelectFullNativeAnimation(member, member.BaseAnimationId, true);
            }
        }
        IsUserControlEnabled = false;
    }
}
