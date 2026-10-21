using System.Collections.Immutable;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class BattleHudController
{
    private int magicCastCount = 1;
    private readonly List<string> selectedMagicTargets = [];

    public int MagicCastCount => magicCastCount;

    internal string ChoiceValue(BattleActionDefinition action)
    {
        if (SelectedCommand == BattleAbility.Magic)
        {
            MagicCastQuote quote = session.QuoteMagic(SelectedActor!.Id, action.Id);
            return session.InfiniteMagic ? $"{quote.Available}/{quote.Capacity}" : (SelectedActor.MagicStocks.FirstOrDefault(stock => stock.Id == action.Id)?.Count ?? 0).ToString();
        }
        int? itemId = action.NativeAction is { Command: 14 } shot ? shot.ActionId + 101 : SelectedCommand == BattleAbility.Item ? action.Id : null;
        return itemId is int id ? (session.Items.FirstOrDefault(item => item.Id == id)?.Count ?? 0).ToString() : CanOpenGuardianFormActions(action) ? ">" : "";
    }

    internal UiImageSource? MagicOwnerIcon(BattleActionDefinition action)
    {
        int? owner = session.GetMagicJunctionOwner(SelectedActor!.Id, action.Id);
        return owner is int row && session.MagicVitals is { MagicSystem.SharedMagicStock: true } vitals ? new UiImageSource(UiImageKind.CharacterPortrait, vitals.Records[row].CharacterId) : null;
    }

    private void ChangeMagicCastCount(int direction)
    {
        if (SelectedActor is not { } actor || SelectedAction is not { } action)
        {
            return;
        }
        try
        {
            IReadOnlyList<int> choices = session.GetNativeMagicCastCounts(actor.Id, action.Id);
            if (choices.Count != 0)
            {
                int index = choices.ToList().IndexOf(magicCastCount);
                magicCastCount = choices[(Math.Max(0, index) + direction + choices.Count) % choices.Count];
                selectedMagicTargets.Clear();
                Message = $"Magic ×{magicCastCount}. Page Left/Right: number of casts.";
                SoundRequested?.Invoke(MenuSound.Move);
            }
        }
        catch (NotSupportedException error)
        {
            Message = error.Message;
            Console.Error.WriteLine(error);
        }
    }

    private void BeginMagicTargetSelection(BattleCombatant actor)
    {
        selectedMagicTargets.Clear();
        if (SelectedCommand == BattleAbility.Magic && SelectedAction is { } action && magicCastCount > 1 && !session.GetNativeMagicCastCounts(actor.Id, action.Id).Contains(magicCastCount))
        {
            magicCastCount = 1;
        }
    }

    private bool CollectMagicTarget(BattleCombatant target)
    {
        if (SelectedCommand != BattleAbility.Magic || magicCastCount == 1)
        {
            return true;
        }
        selectedMagicTargets.Add(target.Id);
        if (selectedMagicTargets.Count < magicCastCount)
        {
            Message = $"Magic ×{magicCastCount}: select target {selectedMagicTargets.Count + 1}/{magicCastCount}.";
            SoundRequested?.Invoke(MenuSound.Confirm);
            return false;
        }
        return true;
    }

    private BattleInput WithMagicTargets(BattleInput input)
    {
        return selectedMagicTargets.Count == 0 ? input : input with { TargetId = selectedMagicTargets[0], MagicTargets = selectedMagicTargets.ToImmutableArray() };
    }
}
