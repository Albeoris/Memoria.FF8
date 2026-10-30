using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private NativeEventMemory? PrepareNativeBattleCounterMirrors(NativeFieldRuntimeProfile? profile, bool restoringCheckpoint)
    {
        if (restoringCheckpoint || profile is not { IsVerified: true } || services is not GameSession session)
        {
            return null;
        }
        if (session.State.BattleStatistics is not { } statistics)
        {
            return null;
        }
        //52BC00 copies these totals once into ordinary mutable field memory before init scripts run.
        return EventMemory.Write(20, 4, NativeScalarValue.Exact(unchecked((int)statistics.Victories)))
            .Write(24, 2, NativeScalarValue.Exact(statistics.Defeats))
            .Write(26, 2, NativeScalarValue.Exact(statistics.Escapes));
    }
}
