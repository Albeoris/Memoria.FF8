using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

internal readonly record struct BattleResistanceBadge(UiImageSource? Icon, string Label, string Value, BattleResistanceTone Tone);

internal enum BattleResistanceTone
{
    Normal,
    Favorable,
    Immune,
    Warning
}

internal static class BattleResistanceColors
{
    internal static BattleResistanceTone Tone(int percent, bool status)
    {
        return BattleResistanceHintProfile.Classify(percent, status) switch
        {
            BattleResistanceHintRating.Favorable => BattleResistanceTone.Favorable,
            BattleResistanceHintRating.Ineffective when !status && percent > 100 => BattleResistanceTone.Warning,
            BattleResistanceHintRating.Ineffective => BattleResistanceTone.Immune,
            _ => BattleResistanceTone.Normal
        };
    }

    internal static UiColor Color(this BattleResistanceTone tone, UiTheme theme)
    {
        return tone switch
        {
            BattleResistanceTone.Favorable => new(125, 221, 245),
            BattleResistanceTone.Immune => theme.Muted,
            BattleResistanceTone.Warning => new(255, 154, 142),
            _ => theme.Foreground
        };
    }
}
