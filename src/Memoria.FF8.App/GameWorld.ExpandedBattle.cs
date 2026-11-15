using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    private readonly int defaultExpandedRandomAllyCount;
    private readonly int defaultAdditionalEnemyCount;
    internal bool UseAttachedBattlePresentation { get; init; }
    internal int ConfiguredBattleAllyCount => session.State.GameplayPreferences.BattleAllyCount == 0 ? defaultExpandedRandomAllyCount : session.State.GameplayPreferences.BattleAllyCount;
    internal int EffectiveBattleAllyCount => BattlePartySelectionPolicy.ResolveCapacity(session.State, ConfiguredBattleAllyCount == 0 ? 3 : ConfiguredBattleAllyCount);

    internal static void AttachExpandedEffects(BattleSession active, GameAssets game, BattleAssets assets)
    {
        ExpandedBattlePresentationResources resources = new(game, assets);
        active.AttachExpandedPresentationProcess(resources.CreateProcess(active));
    }
}
