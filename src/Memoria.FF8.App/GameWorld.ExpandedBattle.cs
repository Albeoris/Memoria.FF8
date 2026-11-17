using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    private readonly int defaultExpandedRandomAllyCount;
    private readonly int defaultAdditionalEnemyCount;
    internal int ConfiguredBattleAllyCount => session.State.GameplayPreferences.BattleAllyCount == 0 ? defaultExpandedRandomAllyCount : session.State.GameplayPreferences.BattleAllyCount;
    internal int EffectiveBattleAllyCount => BattlePartySelectionPolicy.ResolveCapacity(session.State, ConfiguredBattleAllyCount == 0 ? 3 : ConfiguredBattleAllyCount);
}
