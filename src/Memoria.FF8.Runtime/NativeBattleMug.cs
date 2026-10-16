using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleMugDefinition(byte Rate, byte MediumLevel, byte HighLevel, ImmutableArray<BattleStock> Items)
{
    public void Validate()
    {
        if (Items.IsDefault || Items.Length != 12 || Items.Any(stock => stock is null || stock.Id is < 0 or > 255 || stock.Count is < 0 or > 255))
        {
            throw new InvalidDataException("Native Mug requires the original three four-entry item tables.");
        }
    }

    public BattleStock Item(int level, int selection)
    {
        Validate();
        int band = level < MediumLevel ? 0 : level < HighLevel ? 1 : 2;
        return Items[4 * band + selection];
    }
}
