using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    private readonly int defaultExpandedRandomAllyCount;
    private readonly int? legacyExpandedEnemyCount;
    private readonly int defaultAdditionalEnemyCount;
    internal int ConfiguredBattleAllyCount => session.State.GameplayPreferences.BattleAllyCount == 0 ? defaultExpandedRandomAllyCount : session.State.GameplayPreferences.BattleAllyCount;
    internal int EffectiveBattleAllyCount => BattlePartySelectionPolicy.ResolveCapacity(session.State, ConfiguredBattleAllyCount == 0 ? 3 : ConfiguredBattleAllyCount);

    private static BattleEnemyPlacement[]? ExpandRandomEnemyRoster(BattleEncounter encounter, int enemyCount)
    {
        BattleEnemyPlacement[] source = encounter.Slots.Where(slot => slot.Enabled).ToArray();
        if (source.Length < 1 || source.Length > enemyCount || source.Any(slot => !slot.IsEnemyModel || slot.NativeModelId == 255 || slot.Hidden || slot.Untargetable || slot.InitiallySuspended))
        {
            return null;
        }
        return Enumerable.Range(0, enemyCount).Select(index => source[index % source.Length] with { Slot = index }).ToArray();
    }

    internal static void AttachExpandedEffects(BattleSession active, GameAssets game, BattleAssets assets)
    {
        active.AttachExpandedPresentationHost(new ExpandedBattlePresentationHost(game, assets));
    }
}
