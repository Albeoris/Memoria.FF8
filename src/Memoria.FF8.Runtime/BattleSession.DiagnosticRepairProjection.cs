using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BattleDiagnosticRepairProjection([property: JsonRequired] Guid LifecycleId, [property: JsonRequired] long Frame, [property: JsonRequired] long Callback, [property: JsonRequired] BattleDiagnosticCombatRepair Request, [property: JsonRequired] byte RecipientIndex);

public sealed partial class BattleSession
{
    private BattleDiagnosticRepairProjection? diagnosticRepairProjection;
}
