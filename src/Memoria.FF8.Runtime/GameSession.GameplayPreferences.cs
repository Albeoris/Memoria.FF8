namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    public void SetGameplayPreferences(GameplayPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        preferences.Validate();
        State = State with { GameplayPreferences = preferences };
    }
}
