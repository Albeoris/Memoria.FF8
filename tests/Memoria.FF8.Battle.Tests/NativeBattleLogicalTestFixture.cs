using System.Runtime.CompilerServices;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Battle.Tests;

internal static class NativeBattleLogicalTestFixture
{
    [UnsafeAccessor(UnsafeAccessorKind.Method)]
    internal static extern void RunNativeAi(BattleSession battle, BattleCombatant actor, int callback);

    [UnsafeAccessor(UnsafeAccessorKind.Method)]
    internal static extern void DispatchGuardianForceCasts(BattleSession battle);

    [UnsafeAccessor(UnsafeAccessorKind.Method)]
    internal static extern void DispatchExpandedGuardianForceCharges(BattleSession battle);
}
