using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private bool HasNativeInitializationTraceOverwrite()
    {
        return !defaultsStarted && pendingArrival is { DiscJump: null } && NativeRuntimeProfile is { IsVerified: true } && Scene.NativeFieldInitialization is { Stage: NativeFieldInitializationStage.Initializing } proof && proof.IsFrom(NativeRuntimeProfile, FieldId, programHash) && Scene.NativeActorLayoutVersion == 0 && Scene.NativeActorObjectIds.Count == 0 && Scene.FieldPartyNativeActorIds.Count == 0 && Scene.NativeFieldLoadVersion == 0 && Scene.NativePartyTraceVersion == 0 && Scene.NativePlayerPosition is null && ResolveNativeTriangleHeight is not null && ResolvePartyTraceTriangle is not null;
    }

    private bool HasOpaqueInitializationTrace()
    {
        return Scene.NativeActorLayoutVersion == 0 && Scene.NativePartyTraceVersion == 0 && Scene.PartyTrace is { Cursor: 0, FirstOffset: 15, SecondOffset: 30 } trace && trace.FirstTrail.All(sample => sample is null) && trace.SecondTrail.All(sample => sample is null);
    }

    private void InitializeNativePartyTrace(Fiber fiber)
    {
        if (HasNativeInitializationTraceOverwrite())
        {
            // 526190 -> 47A900 runs inside 52BC00's synchronous init, before
            // compaction. Its controlled WORD1CD8FD0 still belongs to the prior
            // field. For normal nonzero-kind entry, later 477C90 -> 47A900
            // replaces records63..1 and record0XY before any trace consumer.
            // Keep those temporary records opaque. The surviving record0 tail
            // remains in FieldLoad*RecordZero for the actual loader to preserve.
            Scene.PartyTrace = new PartyTraceState(0, 15, 30, new PartyTraceSample?[64], new PartyTraceSample?[64]);
            return;
        }
        ActorState player = PlayerActor is { Active: true } boundPlayer ? boundPlayer : throw Fault(fiber, "INITTRACE requires the current native runtime leader binding or proved normal initialization overwrite.");
        PartyTraceSample sample = new((short)(int)player.X, (short)(int)player.Y, (short)(int)player.Z, player.WalkmeshTriangle, (byte)player.Direction);
        Scene.PartyTrace = new PartyTraceState(0, 15, 30, Enumerable.Repeat<PartyTraceSample?>(sample, 64).ToArray(), Enumerable.Repeat<PartyTraceSample?>(sample, 64).ToArray());
    }
}
