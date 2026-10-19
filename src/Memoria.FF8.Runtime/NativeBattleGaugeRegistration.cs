namespace Memoria.FF8.Runtime;

public enum NativeBattleGaugeRegistrationKind
{
    None,
    Menu,
    DeferredScript,
    ForcedCommand
}

/// <summary>Transient4842B0/483EB0 result; the participant flags remain the sole retained registration owner.</summary>
public sealed record NativeBattleGaugeRegistration(NativeBattleGaugeRegistrationKind Kind, uint ActorFlags);

public static class NativeBattleGaugeRegistrations
{
    public static NativeBattleGaugeRegistration Evaluate4842B0(NativeBattleAtbState gauge, uint actorFlags, ushort savedStatuses, uint battleStatuses)
    {
        gauge.Validate();
        if (!NativeBattleAtbState.CanAdvance(actorFlags, savedStatuses, battleStatuses) || gauge.Current < gauge.Maximum || (actorFlags & 0xC) != 0)
        {
            return new(NativeBattleGaugeRegistrationKind.None, actorFlags);
        }
        bool forced = (savedStatuses & 0x20) != 0 || (battleStatuses & 0x2004000) != 0;
        if (forced)
        {
            return new(NativeBattleGaugeRegistrationKind.ForcedCommand, actorFlags | 4u);
        }
        uint flags = actorFlags | 8u;
        if ((actorFlags & 0x10) != 0)
        {
            return new(NativeBattleGaugeRegistrationKind.DeferredScript, flags | 0x200);
        }
        return new(NativeBattleGaugeRegistrationKind.Menu, flags);
    }
}
