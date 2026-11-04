using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Saved item order, quantities and both current/past gil owners without initialization resources.</summary>
public sealed record GameplaySaveEconomy([property: JsonRequired] NativeItemStock[] Items, [property: JsonRequired] byte[]? ItemOrder, [property: JsonRequired] uint Gil, [property: JsonRequired] uint PastGil, [property: JsonRequired] uint EventBankGil, [property: JsonRequired] uint EventBankPastGil)
{
    public static GameplaySaveEconomy Capture(NativeEconomy source)
    {
        ArgumentNullException.ThrowIfNull(source);
        source.Validate();
        return new(source.Items.ToArray(), source.ItemOrder?.ToArray(), source.Gil, source.PastGil, source.EventBankGil72, source.EventBankPastGil76);
    }

    public NativeEconomy Restore(NativeEconomy installed)
    {
        ArgumentNullException.ThrowIfNull(installed);
        installed.Validate();
        Validate();
        NativeEconomy result = installed with { Items = Items.ToArray(), ItemOrder = ItemOrder?.ToArray(), Gil = Gil, PastGil = PastGil, EventBankGil72 = EventBankGil, EventBankPastGil76 = EventBankPastGil };
        result.Validate();
        return result;
    }

    public void Validate()
    {
        if (Items is not { Length: NativeEconomy.ItemSlotCount } || Items.Any(item => item is null) || ItemOrder is { } order && (order.Length != 32 || order.Any(rank => rank >= 32) || order.Distinct().Count() != 32))
        {
            throw new InvalidDataException("Gameplay economy requires complete inventory and a valid optional item-order permutation.");
        }
    }
}
