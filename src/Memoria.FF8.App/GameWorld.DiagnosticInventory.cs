using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed record DiagnosticInventoryPreparationResult(NativeEconomy Economy, ImmutableArray<DiagnosticPreparationChange> Changes);

internal sealed partial class GameWorld
{
    // This creates initial fixture stock through the existing inventory owner before encounter allocation.
    // It never edits a live battle inventory or repairs stock between focal executions.
    internal static DiagnosticInventoryPreparationResult PrepareDiagnosticInventory(NativeEconomy source, DiagnosticBattlePreparation recipe, NativeItemOrderAliases? aliases = null)
    {
        recipe.Validate();
        source.Validate();
        NativeEconomy next = source.DeepCopy();
        foreach (DiagnosticItemPreparation item in recipe.Items)
        {
            NativeItemStock[] matches = next.Items.Where(stock => stock.ItemId == item.Id).ToArray();
            if (matches.Length > 1)
            {
                throw new InvalidOperationException("Exact diagnostic stock requires a single native inventory row for the declared item.");
            }
            int before = matches.FirstOrDefault()?.Count ?? 0;
            next = next.AddItem(item.Id, item.Count - before, out _);
            next = next.UpdateItemOrder(item.Id, aliases);
            if (next.Items.Where(stock => stock.ItemId == item.Id).Sum(stock => stock.Count) != item.Count)
            {
                throw new InvalidOperationException("Diagnostic inventory acquisition could not satisfy the exact declared quantity.");
            }
        }
        next.Validate();
        JsonElement beforeJson = JsonSerializer.SerializeToElement(source);
        JsonElement afterJson = JsonSerializer.SerializeToElement(next);
        ImmutableArray<DiagnosticPreparationChange> changes = JsonElement.DeepEquals(beforeJson, afterJson) ? [] : [new("economy", 0, beforeJson, afterJson)];
        return new(next, changes);
    }
}
