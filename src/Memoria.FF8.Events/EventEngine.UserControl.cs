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
                byte nativeId = slot == 0 || Scene.FieldPartyNativeActorIds.Count == 3 && Scene.FieldPartyNativeActorIds[slot] != byte.MaxValue ? binding.At(slot) : byte.MaxValue;
                if (nativeId == byte.MaxValue)
                {
                    continue;
                }
                if (program is null || nativeId >= Scene.NativeActorObjectIds.Count)
                {
                    throw new NotSupportedException("UCOFF requires its original saved party body mapping.");
                }
                ActorState member = actors[Scene.NativeActorObjectIds[nativeId]];
                if (!HasNativeEmptyAnimationModel(member) && AnimationFrameCount?.Invoke(member.RenderModelId, member.BaseAnimationId) is not > 0)
                {
                    throw new NotSupportedException("UCOFF requires the saved party body's actual full idle animation.");
                }
                if (member.NativePartyFlags is null)
                {
                    throw new NotSupportedException("UCOFF requires its current physical DWORD352 masks4/64.");
                }
                party.Add(member);
            }
            foreach (ActorState member in party)
            {
                SelectFullNativeAnimation(member, member.BaseAnimationId, true);
                member.NativePartyFlags = member.NativePartyFlags! with { AutoAnimation4 = false };
            }
            if (Scene.FieldPartyNativeActorIds.Count == 3)
            {
                Scene.FieldPartyNativeActorIds[1] = byte.MaxValue;
                Scene.FieldPartyNativeActorIds[2] = byte.MaxValue;
            }
        }
        IsUserControlEnabled = false;
    }

    private void ExecuteNativeUserControlOn()
    {
        if (userControlEnabled)
        {
            return;
        }
        if (Scene.PartyBindingMemory is { Version: 1 } binding && Scene.NativeActorLayoutVersion == 1)
        {
            List<(int Slot, byte NativeId, ActorState Actor)> prepared = [];
            for (int slot = 0; slot < 3; slot++)
            {
                byte nativeId = binding.At(slot);
                if (nativeId == byte.MaxValue)
                {
                    continue;
                }
                if (nativeId >= Scene.NativeActorObjectIds.Count || actors[Scene.NativeActorObjectIds[nativeId]].NativePartyFlags is not NativeActorPartyFlags flags)
                {
                    throw new NotSupportedException("UCON requires its actual saved body binding and physical masks4/64.");
                }
                if (!flags.DetachedFollow64)
                {
                    ActorState member = actors[Scene.NativeActorObjectIds[nativeId]];
                    prepared.Add((slot, nativeId, member));
                }
            }
            foreach ((int slot, byte nativeId, ActorState member) in prepared)
            {
                member.NativePartyFlags = member.NativePartyFlags! with { AutoAnimation4 = true };
                Scene.FieldPartyNativeActorIds[slot] = nativeId;
            }
        }
        IsUserControlEnabled = true;
    }
}
