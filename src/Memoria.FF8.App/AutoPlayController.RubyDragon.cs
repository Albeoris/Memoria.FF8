using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class AutoPlayController
{
    private const int RubyDragonField = 948;
    private const string RubyDragonProgram = "6BD0F1097817C0C985B097C8927964C3B029CFCE267887406B6A40D13BFF37BA";

    private bool TryDriveRubyDragonDuel(GameWorld world)
    {
        EventEngine events = world.Events;
        if (events.FieldId != RubyDragonField || session.State.WorldEntryMemory?.StoryWord != 1210 || events.IsUserControlEnabled || !world.HasCurrentFieldAllocation || world.ReadAutoPlayFieldProgramHash(RubyDragonField) != RubyDragonProgram)
        {
            return false;
        }

        // NG-057: Use held Cards through the dragon's hit check and fresh Menu edges between windups.
        var mapping = session.State.InputConfiguration ?? throw new NotSupportedException("The Ruby Dragon duel requires its saved input mapping.");
        ushort attack = mapping.RawForMappedPulse(16);
        ushort guard = mapping.RawForMappedPulse(128);
        int playerHealth = events.EventMemory.Read(1031, 1).RequireExact();
        int dragonHealth = events.EventMemory.Read(1032, 1).RequireExact();
        for (int callback = 0; callback < 48 && session.State is { Scene: SceneKind.Field, FieldId: RubyDragonField } && world.CurrentDialogue?.CanConfirm != true; callback++)
        {
            ActorState dragon = events.Actors[11];
            bool windingUp = dragon is { AnimationId: 2, AnimationLast: 47 };
            ushort held = session.State.FieldButtonInput?.PhysicalHeld ?? 0;
            ushort desired = 0;
            if (IsRubyDragonDuelActive(events))
            {
                desired = windingUp ? guard : held == attack ? (ushort)0 : attack;
            }
            if (desired != held)
            {
                Apply(new(PlayerActionKind.FieldButtons, desired));
            }
            AdvanceInputFrame(world);
            if (events.LastFault is { } fault)
            {
                throw new EventScriptException(fault.Message);
            }
        }
        if (session.State.FieldButtonInput is { PhysicalHeld: not 0 } && (session.State is not { Scene: SceneKind.Field, FieldId: RubyDragonField } || !IsRubyDragonDuelActive(events)))
        {
            Apply(new(PlayerActionKind.FieldButtons, 0));
        }
        if (session.State is { Scene: SceneKind.Field, FieldId: RubyDragonField } && events.EventMemory.Read(1029, 1).RequireExact() == 2 && (events.EventMemory.Read(1031, 1).RequireExact() != playerHealth || events.EventMemory.Read(1032, 1).RequireExact() != dragonHealth))
        {
            ResetIdleDeadline();
        }
        if (++run.ScriptedInputSteps > 500)
        {
            throw new TimeoutException("The Ruby Dragon duel exceeded its bounded native input callbacks.");
        }
        return true;
    }

    private static bool IsRubyDragonDuelActive(EventEngine events)
    {
        return events.EventMemory.Read(1029, 1).RequireExact() == 2 && events.EventMemory.Read(1031, 1).RequireExact() > 0 && events.EventMemory.Read(1032, 1).RequireExact() > 0;
    }
}
