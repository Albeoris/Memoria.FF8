using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private readonly SemaphoreSlim battleLoadoutPreparation = new(1, 1);

    private Task<BattleLoadoutModelPreview> PrepareBattleLoadoutPreviewAsync(BattleLoadoutDraft draft, int character, CancellationToken token)
    {
        BattleAssets resources = world?.BattleResources ?? PresentationBattleResources;
        int costume = BattleLoadoutCostume(character);
        int weapon = draft.ResolveWeapon(character);
        return RunBattleLoadoutPreparationAsync(() => resources.WithPlayerModelResources(() => BattleLoadoutModelPreview.Prepare(resources, character, costume, weapon)), token);
    }

    private Task PrepareBattleLoadoutBatchAsync(BattleLoadoutDraft draft, IReadOnlyList<int> characters, IProgress<(int Completed, int Total)> progress, CancellationToken token)
    {
        BattleAssets resources = world?.BattleResources ?? PresentationBattleResources;
        GameState proposed = session.State with { BattleLoadouts = draft.Overrides };
        var selections = characters.Select(character => (Character: character, Costume: BattleLoadoutCostume(character), Weapon: draft.ResolveWeapon(character))).ToArray();
        return RunBattleLoadoutPreparationAsync(() =>
        {
            progress.Report((0, selections.Length));
            for (int index = 0; index < selections.Length; index++)
            {
                token.ThrowIfCancellationRequested();
                var (character, costume, weapon) = selections[index];
                BattleResolvedLoadout loadout = resources.ResolveBattleLoadout(proposed, character, weapon);
                resources.PreparePlayerLoadout(costume, loadout, proposed.BattleLoadouts?.Get(character)?.LimitFamilies is not null);
                progress.Report((index + 1, selections.Length));
            }
            return true;
        }, token);
    }

    private Task<T> RunBattleLoadoutPreparationAsync<T>(Func<T> prepare, CancellationToken token)
    {
        return Task.Run(async () =>
        {
            await battleLoadoutPreparation.WaitAsync(token).ConfigureAwait(false);
            try
            {
                token.ThrowIfCancellationRequested();
                return prepare();
            }
            finally
            {
                battleLoadoutPreparation.Release();
            }
        }, token);
    }
}
