using Memoria.FF8.Runtime;

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
        NativeCharacterVitals? reserve = PrepareNativePartyReserveRefresh();
        if (reserve is not null)
        {
            services.SetCharacterVitals(reserve);
        }
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
        }
        RecordPartyBindingMemory();
    }

    private NativeCharacterVitals PrepareNativeReserveParty(NativeCharacterVitals current) => PrepareNativeReserveParty(current, Scene.Party.Select(row => (byte)row).ToArray());

    private static NativeCharacterVitals PrepareNativeReserveParty(NativeCharacterVitals current, IReadOnlyList<byte> fieldParty)
    {
        current.Validate();
        byte[] reserve = Enumerable.Range(0, 6).Where(identity => !fieldParty.Contains((byte)identity)).Select(identity => (byte)identity).ToArray();
        if (reserve.Length > 3)
        {
            throw new NotSupportedException("Native reserve construction would overwrite primary party bytes194 onward outside its six-byte backup owner.");
        }
        byte[] backup = current.JunctionPartyBackup.ToArray();
        for (int slot = 0; slot < reserve.Length; slot++)
        {
            backup[slot] = reserve[slot];
            backup[slot + 3] = reserve[slot];
        }
        return current.DeepCopy() with { JunctionPartyBackup = backup };
    }

    private NativeCharacterVitals? PrepareNativePartyReserveRefresh()
    {
        if (NativeRuntimeProfile is not { IsVerified: true })
        {
            return null;
        }
        NativeWorldControlState controls = services.WorldControls ?? throw new NotSupportedException("Native52D050 requires its actual eventDWORD104 owner.");
        if ((controls.EventFlags104 & 0x800) == 0)
        {
            return null;
        }
        if (!HasCanonicalPartySlots(Scene) || services.CharacterVitals is not { } current || !services.CanStoreNativeEventOwners)
        {
            throw new NotSupportedException("Native52D050 auxiliary reserve refresh requires its three field identities, saved character owner and publication service.");
        }
        if (UnsupportedJunctionBackupAliases() is string aliases)
        {
            throw new NotSupportedException(aliases);
        }
        //52D050 rebuilds both188..190 and191..193 in identity0..5 order.
        //It does not rebuild combat stats, replace combat slots or change the field party.
        return PrepareNativeReserveParty(current);
    }

    private string? UnsupportedNativePartyReserveRefresh()
    {
        try
        {
            _ = PrepareNativePartyReserveRefresh();
            return null;
        }
        catch (Exception exception) when (exception is NotSupportedException or InvalidDataException)
        {
            return exception.Message;
        }
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
