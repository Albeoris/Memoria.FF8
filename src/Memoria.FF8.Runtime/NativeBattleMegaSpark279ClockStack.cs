namespace Memoria.FF8.Runtime;

internal enum NativeBattleMegaSpark279ClockPath
{
    Unknown,
    AtbWithoutHelper,
    StatusWithoutTimer,
    SupportCooldown,
    AtbMenu,
    AtbDeferredScript
}

internal static class NativeBattleMegaSpark279ClockStack
{
    internal static bool AtbContextHasNoHelper(int guardianCasts, bool escapeHeld, bool escapePresented)
    {
        return guardianCasts == 0 && !escapeHeld && !escapePresented;
    }

    internal static NativeBattleMegaSpark279ClockPath AtbParticipantPath(NativeBattleAtbState gauge, byte speed, byte multiplier, uint actorFlags, ushort savedStatuses, uint battleStatuses)
    {
        NativeBattleAtbState advanced = gauge.Advance(speed, multiplier, actorFlags, savedStatuses, battleStatuses);
        return NativeBattleGaugeRegistrations.Evaluate4842B0(advanced, actorFlags, savedStatuses, battleStatuses).Kind switch
        {
            NativeBattleGaugeRegistrationKind.None => NativeBattleMegaSpark279ClockPath.AtbWithoutHelper,
            NativeBattleGaugeRegistrationKind.Menu => NativeBattleMegaSpark279ClockPath.AtbMenu,
            NativeBattleGaugeRegistrationKind.DeferredScript when (actorFlags & 0x10) != 0 => NativeBattleMegaSpark279ClockPath.AtbDeferredScript,
            _ => NativeBattleMegaSpark279ClockPath.Unknown
        };
    }

    internal static NativeBattleMegaSpark279ClockPath CombineAtbPaths(NativeBattleMegaSpark279ClockPath current, NativeBattleMegaSpark279ClockPath next)
    {
        if (current is not (NativeBattleMegaSpark279ClockPath.AtbWithoutHelper or NativeBattleMegaSpark279ClockPath.AtbMenu or NativeBattleMegaSpark279ClockPath.AtbDeferredScript) ||
            next is not (NativeBattleMegaSpark279ClockPath.AtbWithoutHelper or NativeBattleMegaSpark279ClockPath.AtbMenu or NativeBattleMegaSpark279ClockPath.AtbDeferredScript))
        {
            return NativeBattleMegaSpark279ClockPath.Unknown;
        }
        if (current == NativeBattleMegaSpark279ClockPath.AtbMenu || next == NativeBattleMegaSpark279ClockPath.AtbMenu)
        {
            return NativeBattleMegaSpark279ClockPath.AtbMenu;
        }
        return current == NativeBattleMegaSpark279ClockPath.AtbDeferredScript || next == NativeBattleMegaSpark279ClockPath.AtbDeferredScript
            ? NativeBattleMegaSpark279ClockPath.AtbDeferredScript
            : NativeBattleMegaSpark279ClockPath.AtbWithoutHelper;
    }

    internal static bool StatusHasNoTimer(ushort savedStatuses, IReadOnlyList<short>? timers)
    {
        return (savedStatuses & 5) != 0 || timers is { Count: 14 } && timers.All(timer => timer == -1111);
    }

    internal static bool SupportHasCooldown(NativeBattleSupportState? state)
    {
        return state is { EntryInitialized: true, Cooldown: > 0 };
    }

    internal static NativeBattleStackBytes Observe(NativeBattleStackBytes stack, uint callback, string phase, int? outerPhase, NativeBattleMegaSpark279ClockPath path)
    {
        stack.Validate();
        if (stack.FirstOffset != -0xAC || stack.Bytes.Length != 8)
        {
            throw new InvalidDataException("Mega Spark clock observation requires its actual common-frame interval.");
        }
        int? firstWrite = (callback, path) switch
        {
            (0x4842B0, NativeBattleMegaSpark279ClockPath.AtbWithoutHelper) => -0x40,
            (0x483470, NativeBattleMegaSpark279ClockPath.StatusWithoutTimer) => -0x50,
            (0x482F80, NativeBattleMegaSpark279ClockPath.SupportCooldown) => -0x34,
            (0x4842B0, NativeBattleMegaSpark279ClockPath.AtbMenu) => -0x90,
            (0x4842B0, NativeBattleMegaSpark279ClockPath.AtbDeferredScript) => -0x48,
            _ => null
        };
        return outerPhase == 4 && firstWrite is int first
            ? ObserveWriteRange(stack, first, 0, callback, phase)
            : stack.Invalidate(-0xAC, 8, callback, phase);
    }

    internal static NativeBattleStackBytes ObserveWriteRange(NativeBattleStackBytes stack, int first, int end, uint callback, string phase)
    {
        stack.Validate();
        if (first >= end)
        {
            throw new InvalidDataException("A native clock write footprint requires an ordered byte interval.");
        }
        int overlapFirst = Math.Max(stack.FirstOffset, first);
        int overlapEnd = Math.Min(checked(stack.FirstOffset + stack.Bytes.Length), end);
        return overlapFirst < overlapEnd ? stack.Invalidate(overlapFirst, overlapEnd - overlapFirst, callback, phase) : stack;
    }
}
