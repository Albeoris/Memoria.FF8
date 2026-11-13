using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

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

    private bool TryDriveLunarRescue(GameWorld world)
    {
        const int rescueField = 878;
        EventEngine events = world.Events;
        if (events.FieldId != rescueField || !world.HasCurrentFieldAllocation || world.ReadAutoPlayFieldProgramHash(rescueField) != "FB940E926AE4E9037524E43C4D49DCDECCDCB474C6D7D897E335935C34DB804C")
        {
            return false;
        }

        // NG-074: Steer toward the original catch window with ordinary mapped input.
        var mapping = session.State.InputConfiguration ?? throw new NotSupportedException("The Lunar rescue requires its saved input mapping.");
        long firstFrame = events.Frame;
        for (int callback = 0; callback < 120 && session.State is { Scene: SceneKind.Field, FieldId: rescueField } && world.CurrentDialogue?.CanConfirm != true; callback++)
        {
            ushort buttons = 0;
            if (events.IsAtScriptedInputLoop(5, 46, 1129, 1322) && events.EventMemory.Read(1037, 1).RequireExact() == 0)
            {
                int x = events.EventMemory.Read(1040, 4, signed: true).RequireExact();
                int y = events.EventMemory.Read(1044, 4, signed: true).RequireExact();
                int horizontal = x < -100 ? 32768 : x > 100 ? 8192 : 0;
                int vertical = y < -100 ? 16384 : y > 100 ? 4096 : 0;
                if ((horizontal | vertical) != 0)
                {
                    int boost = events.EventMemory.Read(1052, 4, signed: true).RequireExact() >= 8 ? 16 : 0;
                    buttons = mapping.RawForMappedPulse((ushort)(horizontal | vertical | boost));
                }
            }
            if ((session.State.FieldButtonInput?.PhysicalHeld ?? 0) != buttons)
            {
                Apply(new(PlayerActionKind.FieldButtons, buttons));
            }
            AdvanceInputFrame(world);
            if (events.LastFault is { } fault)
            {
                throw new EventScriptException(fault.Message);
            }
        }
        if (session.State is not { Scene: SceneKind.Field, FieldId: rescueField } && session.State.FieldButtonInput is { PhysicalHeld: not 0 } input)
        {
            session.SetFieldButtonInput(input with { PhysicalHeld = 0 });
        }
        if (++run.ScriptedInputSteps > 200)
        {
            throw new TimeoutException("The Lunar rescue exceeded its bounded native input callbacks.");
        }
        if (events.Frame > firstFrame)
        {
            ResetIdleDeadline();
        }
        return true;
    }
}
