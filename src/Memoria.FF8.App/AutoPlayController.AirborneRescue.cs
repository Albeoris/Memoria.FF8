using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class AutoPlayController
{
    private bool TryDriveAirborneRescueInput(GameWorld world)
    {
        if (session.State.FieldId == 152 && session.State.WorldEntryMemory?.StoryWord == 892 && !world.Events.IsUserControlEnabled && world.Events.EventMemory.Read(1027, 1).RequireExact() == 0)
        {
            HoldAirbornePunch();
            PlayerActionResult result = Apply(new(PlayerActionKind.Wait, Frames: 120));
            if (!result.Accepted || result.FramesAdvanced == 0 && session.State.Scene == SceneKind.Field && session.State.FieldId == 152)
            {
                throw new NotSupportedException($"The airborne rescue did not advance its field callbacks: {result.Detail}");
            }
            ResetIdleDeadline();
            return true;
        }

        // The emergency-exit scene waits for Confirm in the active request
        // while ordinary player control remains enabled.
        if (session.State.FieldId != 144 || session.State.WorldEntryMemory?.StoryWord != 892 || !world.Events.IsUserControlEnabled || world.Events.EventMemory.Read(1025, 1).RequireExact() != 1 || world.Events.EventMemory.Read(1028, 1).RequireExact() != 0)
        {
            return false;
        }
        ushort confirm = (session.State.InputConfiguration ?? throw new NotSupportedException("The emergency exit requires its saved input mapping.")).RawForMappedPulse(64);
        Apply(new(PlayerActionKind.FieldButtons, confirm));
        try
        {
            Apply(new(PlayerActionKind.Wait));
        }
        finally
        {
            if (session.State.FieldButtonInput is { } input)
            {
                session.SetFieldButtonInput(input with { PhysicalHeld = 0 });
            }
        }
        return true;
    }

    private bool TryDriveAirborneRescueMovie()
    {
        // NG-050: The airborne fight executes its field input scripts during both movies.
        if ((session.State.FieldId, session.State.MovieSource?.MovieIndex) is not ((144, 32) or (152, 33)) || session.State.MovieSource?.DiscIndex != 1)
        {
            return false;
        }

        if (getWorld().CurrentDialogue is { CanConfirm: true } dialogue)
        {
            HandleDialogue(dialogue);
            return true;
        }

        int field = session.State.FieldId;
        HoldAirbornePunch();
        PlayerActionResult result = Apply(new(PlayerActionKind.Wait, Frames: 120));
        if (!result.Accepted || result.FramesAdvanced == 0 && session.State.Scene == SceneKind.Movie)
        {
            throw new NotSupportedException($"The airborne rescue movie did not advance its input callbacks: {result.Detail}");
        }
        if ((session.State.Scene != SceneKind.Movie || session.State.FieldId != field) && session.State.FieldButtonInput is { } input)
        {
            session.SetFieldButtonInput(input with { PhysicalHeld = 0 });
        }
        ResetIdleDeadline();
        return true;
    }

    private void HoldAirbornePunch()
    {
        ushort punch = (session.State.InputConfiguration ?? throw new NotSupportedException("The airborne rescue requires its saved input mapping.")).RawForMappedPulse(16);
        if (session.State.FieldButtonInput?.PhysicalHeld != punch)
        {
            Apply(new(PlayerActionKind.FieldButtons, punch));
        }
    }
}
