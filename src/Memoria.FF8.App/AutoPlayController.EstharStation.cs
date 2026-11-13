using Memoria.FF8.Events;

namespace Memoria.FF8;

internal sealed partial class AutoPlayController
{
    private bool TryDriveEstharStationReunion(GameWorld world)
    {
        EventEngine events = world.Events;
        if (events.FieldId != 547 || session.State.WorldEntryMemory?.StoryWord != 1600 || !world.HasCurrentFieldAllocation || world.ReadAutoPlayFieldProgramHash(547) != "565CE828052F7DA8CA812033D0741FA6450B6FEE04DE5A29C335C788E7627EC3" || events.EventMemory.Read(672, 2).RequireExact() != 4 || events.EventMemory.Read(1027, 1).RequireExact() != 0)
        {
            return false;
        }

        // NG-063: The station controller can wait for a held button while movement is enabled.
        if (!events.IsAtScriptedInputLoop(9, 124, 1448, 1460))
        {
            return false;
        }

        var mapping = session.State.InputConfiguration ?? throw new NotSupportedException("The station reunion requires its saved input mapping.");
        Apply(new(PlayerActionKind.FieldButtons, mapping.RawForMappedPulse(64)));
        try
        {
            AdvanceInputFrame(world);
        }
        finally
        {
            Apply(new(PlayerActionKind.FieldButtons, 0));
        }
        return true;
    }

    private bool TryWaitForEstharCountdown(GameWorld world)
    {
        EventEngine events = world.Events;
        if (events.FieldId != 434 || session.State.WorldEntryMemory?.StoryWord != 2300 || session.State.ExamTimer is not { TimerMemoryActivated: true, DisplayEnabled: true } timer || !world.HasCurrentFieldAllocation || world.ReadAutoPlayFieldProgramHash(434) != "E3D356234021435181E88E9EA5C5F4CAA360F2EE0CD2549298650FDB07C1B598" || events.EventMemory.Read(672, 2).RequireExact() != 19 || !events.IsAtScriptedInputLoop(11, 53, 429, 458))
        {
            return false;
        }

        // NG-070: The original city script continues when its own clock expires.
        int remaining = timer.RequireRemaining();
        for (int callback = 0; callback < 300 && session.State.FieldId == 434 && events.NextTrigger.Kind == PlayerTriggerKind.Playable && session.State.ExamTimer is { DisplayEnabled: true }; callback++)
        {
            AdvanceInputFrame(world);
            if (events.LastFault is { } fault)
            {
                throw new EventScriptException(fault.Message);
            }
        }
        if (session.State.ExamTimer is { } current && current.RequireRemaining() < remaining)
        {
            ResetIdleDeadline();
        }
        return true;
    }
}
