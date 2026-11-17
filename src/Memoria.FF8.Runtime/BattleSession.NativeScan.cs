using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    public void SetUnrestrictedScanRotation(bool enabled)
    {
        definition = definition with { Tuning = definition.Tuning with { UnrestrictedScanRotation = enabled } };
    }
}
