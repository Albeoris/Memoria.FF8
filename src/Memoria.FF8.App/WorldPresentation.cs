using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;
using System.Runtime.ExceptionServices;

namespace Memoria.FF8;

/// <summary>Transient presentation history of committed world callbacks. Never serialized or advanced by drawing.</summary>
internal sealed class WorldPresentation
{
    private WorldState? current;
    private WorldRenderFrame? previousFrame;
    private WorldRenderFrame? currentFrame;
    private bool active;
    private ExceptionDispatchInfo? preparationError;

    public void Commit(WorldState previousCallback, WorldState committed, Func<WorldState, WorldRenderFrame> prepare)
    {
        bool continuous = ReferenceEquals(current, previousCallback) && committed.Frame == previousCallback.Frame + 1 && SameOwner(previousCallback, committed);
        previousFrame = continuous ? currentFrame : null;
        current = committed;
        // Capture while this callback still owns the process globals. Historical
        // native owners must never be revalidated against a later callback.
        currentFrame = null;
        preparationError = null;
        if (active)
        {
            try
            {
                currentFrame = prepare(committed);
            }
            catch (Exception exception)
            {
                // Presentation failures belong to Draw, not to the committed simulation transaction.
                previousFrame = null;
                preparationError = ExceptionDispatchInfo.Capture(exception);
            }
        }
    }

    public WorldRenderFrame Sample(WorldState state, float fraction, Func<WorldState, WorldRenderFrame> prepare)
    {
        ArgumentNullException.ThrowIfNull(prepare);
        if (!float.IsFinite(fraction) || fraction is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(fraction));
        }
        if (!ReferenceEquals(state, current))
        {
            // A restored, teleported or externally replaced state starts a new visual history.
            previousFrame = null;
            current = state;
            currentFrame = null;
            preparationError = null;
        }
        active = true;
        preparationError?.Throw();
        currentFrame ??= prepare(state);
        if (fraction == 1 || previousFrame is null)
        {
            return currentFrame;
        }
        return WorldRenderInterpolation.Interpolate(previousFrame, currentFrame, fraction);
    }

    private static bool SameOwner(WorldState first, WorldState second)
    {
        return first.NativeFootRuntime?.EntityType == second.NativeFootRuntime?.EntityType && first.NativeCarRuntime?.EntityType == second.NativeCarRuntime?.EntityType && (first.NativeGardenRuntime is null) == (second.NativeGardenRuntime is null) && (first.NativeRagnarokRuntime is null) == (second.NativeRagnarokRuntime is null) && first.NativeTrainRuntime?.EntityType == second.NativeTrainRuntime?.EntityType && first.NativeScriptedRuntime?.Path.Packet == second.NativeScriptedRuntime?.Path.Packet;
    }
}
