using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private GameplaySaveFieldLocation? gameplaySavePartyLocation;
    private int[]? gameplaySavePartyIdentities;

    /// <summary>Completes the existing synchronous normal-load initialization, installing current follower poses before input or default scripts.</summary>
    public void InitializeGameplaySaveFieldParty(GameplaySaveFieldLocation? location)
    {
        if (services is not GameSession session || session.State.Scene != SceneKind.Field || session.State.FieldLoadContext is not { Origin: NativeFieldLoadOrigin.GameplaySave } context || context.FieldId != FieldId || program is null || NativeRuntimeProfile is not { IsVerified: true } || defaultsStarted || Frame != 0 || pendingArrival is null || gameplaySavePartyIdentities is not null)
        {
            throw new InvalidOperationException("Semantic party placement requires a newly allocated gameplay normal-load field before its initialization transaction.");
        }
        location?.Validate();
        gameplaySavePartyLocation = location?.FieldId == FieldId ? location with { PartyPositions = location.PartyPositions.ToArray() } : null;
        gameplaySavePartyIdentities = Scene.Party.ToArray();
        try
        {
            RunSynchronousFieldInitialization();
        }
        finally
        {
            gameplaySavePartyLocation = null;
            gameplaySavePartyIdentities = null;
        }
    }

    private NativeFieldPartyLoad? PrepareGameplaySavePartyLoad(NativeFieldPartyLoad? load)
    {
        if (gameplaySavePartyLocation is not { } location)
        {
            return load;
        }
        if (load is null || !Scene.Party.SequenceEqual(gameplaySavePartyIdentities!) || load.Bindings.Length != 3)
        {
            throw new NotSupportedException("Semantic party poses require unchanged current party identities and the installed native normal-load bindings.");
        }
        NativeFieldActorPlacement[] placements = load.Placements.ToArray();
        HashSet<int> bound = [];
        for (int slot = 0; slot < 3; slot++)
        {
            int binding = load.Bindings[slot];
            NativeFieldLocationPose? pose = location.PartyPositions[slot];
            if (slot == 0 || pose is null)
            {
                // The current resume descriptor, not a cached menu leader pose, owns the arrival.
                continue;
            }
            if (Scene.Party[slot] == 255 || binding == 255 || (uint)binding >= placements.Length || !bound.Add(binding))
            {
                throw new NotSupportedException("Semantic follower pose has no unique current installed party actor binding.");
            }
            NativeFieldActorPlacement original = placements[binding];
            ActorState actor = actors[original.ObjectId];
            if (!actor.Active || actor.CharacterId != Scene.Party[slot])
            {
                throw new InvalidDataException("Semantic follower binding disagrees with its initialized actor identity.");
            }
            int height = ResolveNativeTriangleHeight?.Invoke(pose.Triangle, pose.X, pose.Y) ?? throw new NotSupportedException("Semantic follower pose requires its installed original triangle plane.");
            if (ResolvePartyTraceTriangle?.Invoke(pose.X, pose.Y, height) is null)
            {
                throw new NotSupportedException("Semantic follower pose is outside the installed field walkmesh.");
            }
            placements[binding] = original with { X = pose.X, Y = pose.Y, Z = height, Triangle = pose.Triangle, Direction = pose.Direction };
        }
        return load with { Placements = placements };
    }

    /// <summary>Samples current bound actor positions without relying on the historical menu-location cache or inventing a native writer.</summary>
    public GameplaySaveFieldLocation CaptureGameplaySaveFieldParty()
    {
        if (program is null || !defaultsStarted || NativeRuntimeProfile is not { IsVerified: true } || Scene.NativeFieldInitialization is not { Stage: NativeFieldInitializationStage.Completed } || Scene.NativeActorLayoutVersion != 1 || Scene.FieldPartyNativeActorIds.Count != 3 || Scene.Party.Count != 3)
        {
            throw new NotSupportedException("Semantic party capture requires completed native field initialization and current compact actor bindings.");
        }
        NativeFieldLocationPose?[] poses = new NativeFieldLocationPose?[3];
        HashSet<int> bound = [];
        for (int slot = 0; slot < 3; slot++)
        {
            int binding = Scene.FieldPartyNativeActorIds[slot];
            if (Scene.Party[slot] == 255)
            {
                if (binding != 255)
                {
                    throw new InvalidDataException("An empty semantic party slot retains an occupied native binding.");
                }
                continue;
            }
            if ((uint)binding >= Scene.NativeActorObjectIds.Count || !bound.Add(binding))
            {
                throw new NotSupportedException("Semantic party capture requires a unique current actor for every occupied field slot.");
            }
            ActorState actor = actors[Scene.NativeActorObjectIds[binding]];
            FieldFixedPosition? position = ReadNativeScriptPosition(actor);
            if (!actor.Active || actor.CharacterId != Scene.Party[slot] || position is null || actor.WalkmeshTriangle is < 0 or > ushort.MaxValue || !float.IsFinite(actor.Direction) || actor.Direction != MathF.Truncate(actor.Direction) || actor.Direction is < 0 or > byte.MaxValue)
            {
                throw new NotSupportedException("Semantic party capture requires current exact physical positions, triangle and direction.");
            }
            poses[slot] = NativeFieldLocationPose.FromPhysicalWords(position.X, position.Y, (ushort)actor.WalkmeshTriangle, (byte)actor.Direction);
        }
        return new(checked((ushort)FieldId), Scene.PreviousFieldWord84, poses);
    }
}
