namespace Memoria.FF8.Events;

public sealed record NativeActorFacingQuery(int FixedX, int FixedY, int TargetX, int TargetY);

public sealed partial class EventEngine
{
    public Func<NativeActorFacingQuery, byte?>? ResolveNativeActorFacing { get; set; }

    private NativeActorFacingQuery? PreparePartyActorFacing(Fiber fiber)
    {
        if (fiber.Stack.Count == 0 || (uint)fiber.Stack.Peek() >= 3 || Scene.NativeActorLayoutVersion != 1 || Scene.PartyBindingMemory is not { Version: 1 } memory || NativeRuntimeProfile is not { IsVerified: true })
        {
            return null;
        }
        int slot = fiber.Stack.Peek();
        int address = 194 + slot;
        byte nativeActor = memory.At(slot);
        if (nativeActor == 0xFF || nativeActor >= Scene.NativeActorObjectIds.Count || variables.TryGetValue(address, out int alias) && alias != nativeActor || services.NativeVariableValues?.Any(pair => pair.Key <= address && pair.Key + pair.Value.Width > address) == true || !Scene.NativeActorObjectIds.Contains(fiber.ObjectId))
        {
            return null;
        }
        ActorState actor = actors[fiber.ObjectId];
        ActorState target = actors[Scene.NativeActorObjectIds[nativeActor]];
        FieldFixedPosition? from = ReadNativePlayerContactPosition(actor);
        FieldFixedPosition? to = ReadNativePlayerContactPosition(target);
        return from is not null && to is not null ? new NativeActorFacingQuery(from.X, from.Y, to.X, to.Y) : null;
    }

    private string? UnsupportedPartyActorDirection(Fiber fiber)
    {
        NativeActorFacingQuery? query = PreparePartyActorFacing(fiber);
        return query is not null && ResolveNativeActorFacing?.Invoke(query) is not null ? null : "PDIRA requires its original fullDWORD slot, known save BYTE194..196 retained binding and certified raw actor direction; absent/out-of-range native bodies are unsupported";
    }

    private int[] ExecutePartyActorDirection(Fiber fiber)
    {
        NativeActorFacingQuery query = PreparePartyActorFacing(fiber)!;
        byte direction = ResolveNativeActorFacing!(query)!.Value;
        int[] args = Pop(fiber, 1);
        ActorState actor = actors[fiber.ObjectId];
        actor.Direction = direction;
        if (actor.NativeScriptMovement is { } movement && (!movement.Active || movement.HoldsFacing))
        {
            actor.NativeScriptMovement = movement with { FacingDirection = direction };
        }
        // 526F70 writes visible577 only, preserving independent movement575/hold576; return2.
        return args;
    }
}
