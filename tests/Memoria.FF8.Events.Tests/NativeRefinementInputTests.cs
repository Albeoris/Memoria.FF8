using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeRefinementInputContract()
    {
        foreach (int recipe in new[] { 143, 255 })
        {
            PlayerAction action = new(PlayerActionKind.RefineItems, 97, Text: $"{recipe}:0", Quantity: 1);
            Require(NativeRefinementInput.Validate(action) == (recipe, 0), "Native refinement input must retain the complete byte recipe selector domain.");
        }
        try
        {
            NativeRefinementInput.Validate(new PlayerAction(PlayerActionKind.RefineItems, 97, Text: "256:0", Quantity: 1));
            Require(false, "Native refinement input must reject recipe indices outside the byte domain.");
        }
        catch (ArgumentException)
        {
        }
    }
}
