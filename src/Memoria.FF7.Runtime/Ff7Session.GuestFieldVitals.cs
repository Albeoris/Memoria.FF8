namespace Memoria.FF7.Runtime;

public sealed record Ff7GuestFieldVitalsView(int StoryCharacter, string Name, int Level, ushort Hp, ushort MaximumHp);

public interface IFf7GuestFieldVitals
{
    Ff7GuestFieldVitalsView Read();
    void Recover(bool clearStatuses);
    void AddHitPoints(short amount);

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
}

public sealed partial class Ff7Session
{
    public IFf7GuestFieldVitals? GuestFieldVitals { get; set; }

    private Ff7Progress PreserveGuestFieldCharacters(Ff7Progress before, Ff7Progress after)
    {
        if (GuestFieldVitals is not { } guest)
        {
            return after;
        }
        IReadOnlyList<int> storyCharacters = guest.StoryCharacters;
        return after with { Characters = after.Characters.Select(character => storyCharacters.Contains(character.Id) ? before.Characters.Single(dormant => dormant.Id == character.Id) : character).ToArray() };
    }
}
