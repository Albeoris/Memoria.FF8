using System.Globalization;
using Memoria.FF8.Events;

namespace Memoria.FF8.GameUi;

public static class NativeJunctionMenuProjection
{
    public static bool IsAction(MenuAction action) => action.Kind is "junction-gf" or "junction-command" or "junction-ability" or "junction-magic" or "gf-learn" or "junction-off" or "junction-auto";

    public static PlayerAction PlayerAction(MenuAction action)
    {
        static int Id(string value, string prefix)
        {
            if (!value.StartsWith(prefix, StringComparison.Ordinal) || !int.TryParse(value.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out int id))
            {
                throw new ArgumentException("Menu input lost its explicit native identity.");
            }
            return id;
        }
        PlayerAction result = action.Kind switch
        {
            "junction-gf" => new PlayerAction(PlayerActionKind.JunctionGuardianForce, Id(action.TargetId, "character-"), Id(action.SubjectId, "gf-").ToString(CultureInfo.InvariantCulture)),
            "junction-off" => new PlayerAction(PlayerActionKind.RemoveJunctions, Id(action.TargetId, "character-"), action.Value switch { "Magic" => "0", "All" => "1", _ => throw new ArgumentException("Unknown junction removal category.", nameof(action)) }),
            "junction-auto" => new PlayerAction(PlayerActionKind.AutoJunction, Id(action.TargetId, "character-"), action.Value switch { "Attack" => "0", "Magic" => "1", "Defense" => "2", _ => throw new ArgumentException("Unknown Auto junction priority.", nameof(action)) }),
            "junction-command" => new PlayerAction(PlayerActionKind.SetJunctionCommand, Id(action.TargetId, "character-"), action.SubjectId, Quantity: int.Parse(action.Value, CultureInfo.InvariantCulture)),
            "junction-ability" => new PlayerAction(PlayerActionKind.SetJunctionAbility, Id(action.TargetId, "character-"), action.SubjectId, Quantity: int.Parse(action.Value, CultureInfo.InvariantCulture)),
            "gf-learn" => new PlayerAction(PlayerActionKind.SelectGuardianForceLearning, Id(action.TargetId, "gf-"), action.SubjectId),
            "junction-magic" => new PlayerAction(PlayerActionKind.SetJunctionMagic, Id(action.TargetId, "character-"), MagicSpell(action), Quantity: Enumerable.Range(0, 19).Single(slot => LiveMenuData.JunctionSlotName(slot) == action.Value)),
            _ => throw new ArgumentException("Unsupported junction menu input.", nameof(action))
        };
        NativeJunctionMenuInput.Validate(result);
        return result;

        static string MagicSpell(MenuAction action)
        {
            if (action.SubjectId == "0")
            {
                return "0";
            }
            string prefix = $"magic-{Id(action.TargetId, "character-")}-";
            return Id(action.SubjectId, prefix).ToString(CultureInfo.InvariantCulture);
        }
    }
}
