namespace Memoria.FF7.Runtime;

public sealed record Ff7GuestFieldVitalsView(int StoryCharacter, string Name, int Level, ushort Hp, ushort MaximumHp);

public interface IFf7GuestFieldVitals
{
    Ff7GuestFieldVitalsView Read();
    void Recover(bool clearStatuses);
    void AddHitPoints(short amount);
}

public sealed partial class Ff7Session
{
    public IFf7GuestFieldVitals? GuestFieldVitals { get; set; }

    private Ff7Progress PreserveGuestFieldCharacter(Ff7Progress before, Ff7Progress after)
    {
        if (GuestFieldVitals is not { } guest)
        {
            return after;
        }
        int id = guest.Read().StoryCharacter;
        Ff7CharacterState dormant = before.Characters.Single(character => character.Id == id);
        return after with { Characters = after.Characters.Select(character => character.Id == id ? dormant : character).ToArray() };
    }
}
