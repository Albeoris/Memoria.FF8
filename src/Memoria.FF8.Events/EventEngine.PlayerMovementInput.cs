using System.Numerics;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    /// <summary>Runs a decoded movie callback with the currently held movement axes.</summary>
    public void StepMovieFrame(float horizontal, float vertical)
    {
        if (!float.IsFinite(horizontal) || !float.IsFinite(vertical) || Math.Abs(horizontal) > 1 || Math.Abs(vertical) > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(horizontal), "Movement axes must be finite and between -1 and 1.");
        }
        if (horizontal == 0 && vertical == 0)
        {
            Step();
            return;
        }
        Vector2 axes = new(horizontal, vertical);
        if (axes.LengthSquared() > 1)
        {
            axes = Vector2.Normalize(axes);
        }
        StepPlayerMovement(null, axes, Step, sampleAfterScripts: true);
    }

    private void StepPlayerMovement(ActorState? player, Vector2 axes, Action advance, bool sampleAfterScripts = false)
    {
        if (!sampleAfterScripts)
        {
            ValidateNativePlayerInput(player!);
        }
        int[] pushContacts = [];
        IReadOnlyList<NativeLineContactChange> lineChanges = [];
        pendingNativePlayerInput = () =>
        {
            ActorState currentPlayer = sampleAfterScripts ? PlayerActor ?? throw new NotSupportedException("Player movement requires the current native leader.") : player!;
            if (sampleAfterScripts)
            {
                ValidateNativePlayerInput(currentPlayer);
            }
            if (ResolveWalkmesh is null)
            {
                throw new InvalidOperationException("Player movement requires the field walkmesh resolver.");
            }
            var native = ResolveNativePartyPlayerStep(currentPlayer, axes);
            pushContacts = native.PushContacts;
            lineChanges = MovePlayerCore(native.Position.Position.X, native.Position.Position.Y, native.Position.Position.Z, native.Position.Triangle, native.FixedPosition, native.Direction, native.NativeMovement, native.TriggerPosition);
            PublishNativePlayerMovementResult(native.MovementSucceeded);
            if (native.NativeMovement)
            {
                Scene.NativePlayerMovementVersion = 1;
                // 47A2F6 clears 1CDBFED after the native field-line query.
                Scene.NativePlayerEarlyRetryState = 0;
            }
        };
        try
        {
            advance();
        }
        finally
        {
            pendingNativePlayerInput = null;
        }
        // Native4767B0 runs scripts before player movement.
        // New byte584 writes belong to the next update,
        // including a gateway update with no destination tick.
        foreach (int objectId in pushContacts)
        {
            actors[objectId].NativePushContactPending = true;
        }
        CommitNativeLineMovement(lineChanges);
    }
}
