using Memoria.FF7.Events;

﻿namespace Memoria.FF7.Runtime;

public sealed record Ff7GuestFieldVitalsView(int StoryCharacter, string Name, int Level, ushort Hp, ushort MaximumHp);

public interface IFf7GuestFieldVitals
{
    Ff7GuestFieldVitalsView Read();
    void Recover(bool clearStatuses);
    void AddHitPoints(short amount);
    void SubtractHitPoints(short amount)
    {
        throw new NotSupportedException("The guest field-vitals adapter has no native hit-point subtraction contract.");
    }

    IReadOnlyList<Ff7GuestFieldVitalsView> ReadAll() => [Read()];
    IReadOnlyList<int> StoryCharacters => [Read().StoryCharacter];

    void RecoverCharacters(IReadOnlyCollection<int> storyCharacters, bool clearStatuses)
    {
        if (storyCharacters.Contains(Read().StoryCharacter))
        {
            Recover(clearStatuses);
        }
    }

    void AddHitPoints(int storyCharacter, short amount)
    {
        if (storyCharacter == Read().StoryCharacter)
        {
            AddHitPoints(amount);
        }
    }

    void SubtractHitPoints(int storyCharacter, short amount)
    {
        if (storyCharacter == Read().StoryCharacter)
        {
            SubtractHitPoints(amount);
        }
    }
}

public sealed partial class Ff7Session
{
    public IFf7GuestFieldVitals? GuestFieldVitals { get; set; }

    private Ff7Progress PreserveGuestFieldCharacters(Ff7Progress before, Ff7Progress after)
    {
        return PreserveGuestFieldCharacters(before, after, GuestFieldVitals);
    }

    private static Ff7Progress PreserveGuestFieldCharacters(Ff7Progress before, Ff7Progress after, IFf7GuestFieldVitals? guest)
    {
        if (guest is null)
        {
            return after;
        }
        IReadOnlyList<int> storyCharacters = guest.StoryCharacters;
        return after with { Characters = after.Characters.Select(character => storyCharacters.Contains(character.Id) ? before.Characters.Single(dormant => dormant.Id == character.Id) : character).ToArray() };
    }

    internal static Ff7PartyRecoveryResult ApplyFieldHitPointAdjustment(Ff7Progress progress, FieldHitPointCommand command, IReadOnlyList<ReadOnlyMemory<byte>> materia, IReadOnlyList<short> modifiers, IFf7GuestFieldVitals? guest, Action<Action> publish)
    {
        Ff7PartyRecoveryResult adjusted = command.Subtract ? Ff7NativePartyRecovery.SubtractHitPoints(progress, command.Character, command.Amount, materia, modifiers) : Ff7NativePartyRecovery.AddHitPoints(progress, command.Character, command.Amount, materia, modifiers);
        Ff7Progress preserved = PreserveGuestFieldCharacters(progress, adjusted.Progress, guest);
        if (guest is not null && command.Character != 255 && progress.Party.Take(3).Contains(command.Character))
        {
            publish(() =>
            {
                if (command.Subtract)
                {
                    guest.SubtractHitPoints(command.Character, command.Amount);
                }
                else
                {
                    guest.AddHitPoints(command.Character, command.Amount);
                }
            });
        }
        return adjusted with { Progress = preserved };
    }
}
