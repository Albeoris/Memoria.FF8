using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public sealed record Ff7BattleGuestVitalsBinding(Guid EncounterId, string SourcePayloadSha256, int HostSlot, int StoryCharacter, int OriginCharacter, int OriginRow, ushort InitialHp, ushort MaximumHp, byte Level, byte Luck, ushort Defense, ushort MagicDefense)
{
    public ushort OriginStatuses { get; init; }
    public string OriginName { get; init; } = "Squall";

    internal void Validate()
    {
        if (EncounterId == Guid.Empty || SourcePayloadSha256 is not { Length: 64 } || !SourcePayloadSha256.All(Uri.IsHexDigit) || HostSlot is < 0 or > 2 || StoryCharacter is < 0 or > 8 || OriginCharacter != 0 || OriginRow is < 0 or > 7 || InitialHp > MaximumHp || MaximumHp == 0 || MaximumHp > 9999 || Level > 99 || (OriginStatuses & ~Ff7GuestBattleStatuses.SupportedOrigin) != 0 || string.IsNullOrWhiteSpace(OriginName))
        {
            throw new InvalidDataException("Guest vitals lack their original save and host slot binding.");
        }
    }
}

public sealed partial class Ff7BattleSession
{
    public Ff7BattleGuestVitalsBinding? GuestVitals => state.GuestVitals;

    public void BindGuestVitals(Ff7BattleGuestVitalsBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        binding.Validate();
        if (state.GuestVitals is not null)
        {
            if (state.GuestVitals != binding)
            {
                throw new InvalidDataException("Guest vitals disagree with the saved encounter binding.");
            }
            return;
        }
        if (Tick != 0 || !state.Actors.Any(actor => actor.Actor.Slot == binding.HostSlot && actor.Identity == binding.StoryCharacter) || guestAttack is { } guest && (guest.EncounterId != binding.EncounterId || guest.ActorSlot != binding.HostSlot))
        {
            throw new NotSupportedException("The first guest vitals profile requires the original opening MP formation before its first tick.");
        }
        Ff7BattleActorState actor = Actor(binding.HostSlot);
        uint statuses = Ff7GuestBattleStatuses.FromOrigin(binding.OriginStatuses) | (binding.InitialHp == 0 ? 1u : 0u);
        Ff7BattleAiActor projected = actor.Actor with { HitPoints = binding.InitialHp, MaxHitPoints = binding.MaximumHp, MagicPoints = 0, MaxMagicPoints = 0, Statuses = statuses, Defense = binding.Defense, MagicDefense = binding.MagicDefense };
        Replace(actor with { Actor = projected, LimitGauge = 0, AllUses = null, Timers = Ff7NativeBattleStatusClock.Apply(default, 0, statuses) });
        state = state with { GuestVitals = binding };
    }

    private bool IsGuestVitalsSlot(int slot) => state.GuestVitals?.HostSlot == slot;

    private Ff7BattleDamageActor ProjectGuestDefense(Ff7BattleActorState actor, Ff7BattleDamageActor stats)
    {
        return state.GuestVitals is { } binding && actor.Actor.Slot == binding.HostSlot
            ? stats with { Level = binding.Level, Luck = binding.Luck, Evade = 0, MagicEvade = 0, DefenseModifier = 0, EvadeModifier = 0, MagicDefenseModifier = 0 }
            : stats;
    }

    private bool GuestRecoveryCommand(Ff7BattleCommand command, ushort argument) => command == Ff7BattleCommand.Item && ReadBattleItem(argument) is not null;

    private void ValidateGuestIncoming(Ff7BattleQueuedCommand command, int targetSlot, Ff7BattleDamageAction action, Ff7BattleAttack? record)
    {
        if (!IsGuestVitalsSlot(targetSlot))
        {
            return;
        }
        bool recovery = GuestRecoveryCommand((Ff7BattleCommand)command.CommandId, command.Argument) && command.ActorSlot < 3;
        bool enemy = command.ActorSlot >= 4 && (command.CommandId is 0x20 or 2) || command.CommandId == 0x23;
        if (!(enemy || recovery) || record is { AdditionalEffect: not byte.MaxValue } || record is { StatusChange: < 0xC0 } && (record.StatusMask & ~Ff7GuestBattleStatuses.SupportedHost) != 0)
        {
            throw new NotSupportedException("This guest vitals profile supports original MP physical attacks and ordinary Potion recovery.");
        }
    }

    private void ValidateGuestVitalsSnapshot(Ff7BattleSnapshot snapshot)
    {
        if (snapshot.GuestVitals is not { } binding)
        {
            return;
        }
        binding.Validate();
        Ff7BattleActorState actor = snapshot.Actors.SingleOrDefault(candidate => candidate.Actor.Slot == binding.HostSlot && candidate.Identity == binding.StoryCharacter)
            ?? throw new InvalidDataException("Saved guest vitals do not identify the occupied story slot.");
        Ff7BattleAiActor native = actor.Actor;
        if (native.HitPoints > binding.MaximumHp || native.MaxHitPoints != binding.MaximumHp || native.Defense != binding.Defense || native.MagicDefense != binding.MagicDefense || native.MagicPoints != 0 || native.MaxMagicPoints != 0 || (native.Statuses & ~Ff7GuestBattleStatuses.SupportedHost) != 0 || (native.HitPoints == 0) != ((native.Statuses & 1) != 0) || actor.LimitGauge != 0 || actor.AllUses is not null)
        {
            throw new InvalidDataException("Saved guest vitals contain an unsupported host projection.");
        }
    }
}
