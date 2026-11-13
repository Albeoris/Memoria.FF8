using Memoria.FF8.Events;

namespace Memoria.FF8;

internal sealed partial class AutoPlayController
{
    private bool TryWaitForLunarCorridor(GameWorld world)
    {
        if (world.Events.FieldId != 873 || session.State.WorldEntryMemory?.StoryWord is not (2522 or 2524 or 2525) || !world.HasCurrentFieldAllocation)
        {
            return false;
        }

        // NG-072: Let Rinoa finish the original corridor walk before following her north.
        Apply(new(PlayerActionKind.Wait, Frames: 300, WaitWhilePlayable: true));
        return true;
    }
}
