using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private string JunctionGfFocusId => Data.GuardianForces.FirstOrDefault() is { } force ? "gf-portrait-" + force.Id : "slot-HP";

    private MenuCharacter? JunctionAdjacentCharacter(int direction)
    {
        List<MenuCharacter> choices = Data.Characters.ToList();
        if (Character is { } selected && choices.All(character => character.Id != selected.Id))
        {
            choices.Add(selected);
        }
        int current = choices.FindIndex(character => character.Id == Character?.Id);
        int next = current + direction;
        return current >= 0 && next >= 0 && next < choices.Count ? choices[next] : null;
    }

    private string JunctionControlLabel(UiButton button)
    {
        if (button.Id?.StartsWith("gf-portrait-", StringComparison.Ordinal) == true)
        {
            return Data.GuardianForces.FirstOrDefault(force => "gf-portrait-" + force.Id == button.Id)?.Name ?? "";
        }
        if (button.Id is "previous-character" or "next-character")
        {
            return JunctionAdjacentCharacter(button.Id == "previous-character" ? -1 : 1)?.Name ?? "";
        }
        if (button.Id?.StartsWith("gf-capability-", StringComparison.Ordinal) == true || button.Id?.StartsWith("candidate-", StringComparison.Ordinal) == true)
        {
            return button.Children.OfType<UiLabel>().FirstOrDefault()?.Text ?? "";
        }
        return button.Text;
    }

    private sealed class JunctionGuardianPortraitRow : UiElement
    {
        public override void Arrange(UiRect bounds)
        {
            base.Arrange(bounds);
            int count = Children.Count;
            if (count == 0)
            {
                return;
            }
            const float gap = 3;
            float ownerHeight = Math.Min(20, bounds.Height);
            float size = Math.Max(0, Math.Min(bounds.Height - ownerHeight, (bounds.Width - gap * (count - 1)) / count));
            for (int index = 0; index < count; index++)
            {
                Children[index].Arrange(new UiRect(bounds.X + index * (size + gap), bounds.Y, size, ownerHeight + size));
            }
        }
    }
}
