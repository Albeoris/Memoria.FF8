using System.Runtime.ExceptionServices;

namespace Memoria.FF8;

/// <summary>Retains endpoints prepared by completed simulation callbacks for subsequent draws.</summary>
internal sealed class CommittedPresentationHistory<TState, TFrame>(
    Func<TState, TState, bool> sameState,
    Func<TState, TState, bool> continuous,
    Func<TFrame, TFrame, bool>? continuousFrames = null)
    where TFrame : class
{
    private TState state = default!;
    private bool hasState;
    private bool active;
    private ExceptionDispatchInfo? preparationError;

    public TFrame? Previous { get; private set; }
    public TFrame? Current { get; private set; }

    public void Reset()
    {
        state = default!;
        hasState = false;
        Previous = null;
        Current = null;
        preparationError = null;
    }

    public void Commit(TState committed, Func<TFrame> prepare)
    {
        Previous = hasState && continuous(state, committed) ? Current : null;
        state = committed;
        hasState = true;
        Current = null;
        preparationError = null;
        if (!active)
        {
            return;
        }
        try
        {
            Current = prepare();
            if (Previous is not null && continuousFrames?.Invoke(Previous, Current) == false)
            {
                Previous = null;
            }
        }
        catch (Exception exception)
        {
            Previous = null;
            Current = null;
            preparationError = ExceptionDispatchInfo.Capture(exception);
        }
    }

    public (TFrame? Previous, TFrame Current) Sample(TState latest, float fraction, Func<TFrame> prepare, Func<TFrame, bool>? matchesLatest = null, bool reportPreviousCallbackError = false)
    {
        if (!float.IsFinite(fraction) || fraction is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(fraction));
        }
        active = true;
        if (reportPreviousCallbackError)
        {
            preparationError?.Throw();
        }
        if (!hasState || !sameState(state, latest))
        {
            Reset();
            state = latest;
            hasState = true;
        }
        preparationError?.Throw();
        if (Current is null || matchesLatest?.Invoke(Current) == false)
        {
            Previous = null;
            Current = prepare();
        }
        return (fraction < 1 ? Previous : null, Current);
    }
}
