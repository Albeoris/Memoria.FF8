namespace Memoria.FF8.Events;

/// <summary>The current field's compact actor bindings, independent of save party identities and BYTE194..196.</summary>
public static class NativeFieldPartyBinding
{
    public static bool TryGetObjectId(SceneEventState scene, int slot, out int objectId)
    {
        ArgumentNullException.ThrowIfNull(scene);
        objectId = -1;
        if ((uint)slot >= 3 || scene.NativeActorLayoutVersion != 1 || scene.NativeActorObjectIds.Count is < 1 or > 255 || scene.FieldPartyNativeActorIds.Count != 3)
        {
            return false;
        }
        HashSet<int> objects = [];
        foreach (int id in scene.NativeActorObjectIds)
        {
            if (id < 0 || !objects.Add(id))
            {
                return false;
            }
        }
        HashSet<int> bindings = [];
        foreach (int nativeActor in scene.FieldPartyNativeActorIds)
        {
            if (nativeActor != 255 && ((uint)nativeActor >= scene.NativeActorObjectIds.Count || !bindings.Add(nativeActor)))
            {
                return false;
            }
        }
        int selected = scene.FieldPartyNativeActorIds[slot];
        if (selected == 255)
        {
            return false;
        }
        objectId = scene.NativeActorObjectIds[selected];
        return true;
    }

    public static ActorState? ResolveActor(SceneEventState scene, IReadOnlyList<ActorState> actors, int slot)
    {
        ArgumentNullException.ThrowIfNull(actors);
        if (!TryGetObjectId(scene, slot, out int objectId))
        {
            return null;
        }
        ActorState? result = null;
        foreach (ActorState actor in actors)
        {
            if (actor.ObjectId != objectId)
            {
                continue;
            }
            if (result is not null)
            {
                return null;
            }
            result = actor;
        }
        return result;
    }
}
