using System.Globalization;

namespace Memoria.FF8.Runtime;

/// <summary>The levels at command completion, retained independently of later changes to the target.</summary>
public sealed record BattleLevelChange(int PreviousLevel, int NewLevel)
{
    public string Format(string label = "Level")
    {
        int delta = NewLevel - PreviousLevel;
        string color = delta > 0 ? "Green" : delta < 0 ? "Red" : "White";
        string signedDelta = delta.ToString("+0;-0;0", CultureInfo.InvariantCulture);
        return $"{label}: {PreviousLevel} -> {{Color {color}}}{NewLevel} ({signedDelta}){{Color White}}";
    }
}
