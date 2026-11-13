using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

/// <summary>Transient presentation history of committed world callbacks. Never serialized or advanced by drawing.</summary>
internal sealed class WorldPresentation
{
    private readonly CommittedPresentationHistory<(WorldState Current, WorldState? PreviousCallback), WorldRenderFrame> history = new(
        (first, second) => ReferenceEquals(first.Current, second.Current),
        (first, second) => ReferenceEquals(first.Current, second.PreviousCallback) && second.Current.Frame == first.Current.Frame + 1 && SameOwner(first.Current, second.Current));

    internal WorldRenderFrame? Previous => history.Previous;
    internal WorldRenderFrame? Current => history.Current;

    public void Commit(WorldState previousCallback, WorldState committed, Func<WorldState, WorldRenderFrame> prepare)
    {
        // Capture while this callback still owns the process globals. Historical
        // native owners must never be revalidated against a later callback.
        history.Commit((committed, previousCallback), () => prepare(committed));
    }

    public WorldRenderFrame Sample(WorldState state, float fraction, Func<WorldState, WorldRenderFrame> prepare)
    {
        ArgumentNullException.ThrowIfNull(prepare);
        var (previous, current) = history.Sample((state, null), fraction, () => prepare(state));
        return previous is null ? current : WorldRenderInterpolation.Interpolate(previous, current, fraction);
    }

    private static bool SameOwner(WorldState first, WorldState second)
    {
        return first.NativeFootRuntime?.EntityType == second.NativeFootRuntime?.EntityType && first.NativeCarRuntime?.EntityType == second.NativeCarRuntime?.EntityType && (first.NativeGardenRuntime is null) == (second.NativeGardenRuntime is null) && (first.NativeRagnarokRuntime is null) == (second.NativeRagnarokRuntime is null) && first.NativeTrainRuntime?.EntityType == second.NativeTrainRuntime?.EntityType && first.NativeScriptedRuntime?.Path.Packet == second.NativeScriptedRuntime?.Path.Packet;
    }
}
