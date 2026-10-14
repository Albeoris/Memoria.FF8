namespace Memoria.FF8.Events;

/// <summary>Proved global save BYTE194..196 writes, independent of the currently allocated field's actor mapping.</summary>
public sealed record NativePartyBindingMemory(int Version, byte First, byte Second, byte Third)
{
    public byte At(int slot)
    {
        return slot switch
        {
            0 => First,
            1 => Second,
            2 => Third,
            _ => throw new ArgumentOutOfRangeException(nameof(slot))
        };
    }
}

public sealed partial class EventEngine
{
    private string? UnsupportedPartyQuery(Fiber fiber)
    {
        if (fiber.Stack.Count == 0)
        {
            return "GETPARTY requires its original party-slot operand";
        }
        if ((uint)fiber.Stack.Peek() >= 3)
        {
            return "GETPARTY only supports the original three party bytes; an out-of-range DWORD would read unrelated native save data";
        }
        if (!HasCanonicalPartySlots(Scene))
        {
            return "GETPARTY requires the exact three native character bytes, including absent FF slots";
        }
        return null;
    }

    private static string? UnsupportedCanonicalPartyWrite(Fiber fiber)
    {
        if (fiber.Stack.Count < 3 || fiber.Stack.Capture().Take(3).Any(value => !value.IsExact))
        {
            return "SETPARTY requires its original three character operands";
        }
        int[] party = fiber.Stack.Take(3).Select(character => (int)unchecked((byte)character)).Reverse().ToArray();
        HashSet<int> present = [];
        foreach (int character in party)
        {
            if (character != 255 && !present.Add(character))
            {
                return "SETPARTY low-BYTE slots require distinct active characters; duplicate body-slot ownership is unsupported";
            }
        }
        return null;
    }

    private static bool HasCanonicalPartySlots(SceneEventState scene)
    {
        return scene.Party is { Count: 3 } && scene.Party.All(character => character is >= 0 and <= 255) && scene.ActiveParty.Distinct().Count() == scene.ActiveParty.Count();
    }

    private void CompactNativeActors()
    {
        // Native 52C4E3..52C52D preserves raw pointer-table slots, but rebuilds
        // the separate actor/model array after original initialization.
        Scene.NativeActorObjectIds.Clear();
        foreach (int objectId in program!.NativeObjects.Take(program.NativeActorCount))
        {
            if (actors[objectId].Active)
            {
                Scene.NativeActorObjectIds.Add(objectId);
            }
        }
        Scene.NativeActorLayoutVersion = 1;
    }

    private void RefreshParty()
    {
        foreach (int objectId in Scene.NativeActorObjectIds)
        {
            ActorState retained = actors[objectId];
            retained.NativePartyFlags = new NativeActorPartyFlags(false, false);
        }
        Scene.FieldPartyNativeActorIds.Clear();
        for (int slot = 0; slot < 3; slot++)
        {
            byte character = (byte)Scene.Party[slot];
            int nativeActor = 0xFF;
            for (int index = 0; index < Scene.NativeActorObjectIds.Count; index++)
            {
                ActorState actor = actors[Scene.NativeActorObjectIds[index]];
                if ((byte)actor.CharacterId == character)
                {
                    nativeActor = index;
                    break;
                }
            }
            Scene.FieldPartyNativeActorIds.Add(nativeActor);
            variables[194 + slot] = nativeActor;
        }
        RecordPartyBindingMemory();
    }

    private void RecordPartyBindingMemory()
    {
        Scene.PartyBindingMemory = new NativePartyBindingMemory(1, checked((byte)Scene.FieldPartyNativeActorIds[0]), checked((byte)Scene.FieldPartyNativeActorIds[1]), checked((byte)Scene.FieldPartyNativeActorIds[2]));
    }

    public ActorState? PlayerActor => ResolvePartyActor(0);

    private ActorState? ResolvePartyActor(int slot)
    {
        return NativeFieldPartyBinding.ResolveActor(Scene, actors, slot);
    }
}
