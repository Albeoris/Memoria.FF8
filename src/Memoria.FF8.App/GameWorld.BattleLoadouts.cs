using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    internal void PrepareSavedBattleLoadouts()
    {
        GameState state = session.State;
        if (state.BattleLoadouts is not { Characters.Count: > 0 } || state.CharacterVitals is not { } vitals || state.CharacterAppearance is null)
        {
            return;
        }
        for (int row = 0; row < vitals.Records.Length; row++)
        {
            int character = vitals.Records[row].CharacterId;
            if ((!vitals.IsRosterMember(row) && !vitals.CombatParty.Contains((byte)row)) || !state.BattleLoadouts.Characters.ContainsKey(character))
            {
                continue;
            }
            try
            {
                PrepareBattleLoadout(state, row);
            }
            catch (Exception exception) when (exception is NotSupportedException or InvalidDataException or IOException or ArgumentException)
            {
                // A resource failure still belongs to battle validation; the exploration save remains usable.
                Console.Error.WriteLine($"Could not prepare the saved battle loadout for character {character}: {exception.Message}");
            }
        }
    }

    internal void PrepareBattleLoadout(GameState state, int row)
    {
        NativeCharacterVitals vitals = state.CharacterVitals!;
        int character = vitals.Records[row].CharacterId;
        int costume = state.CharacterAppearance is { } appearance && row < appearance.Records.Length ? appearance.Records[row].Dress : 0;
        BattleResolvedLoadout loadout = BattleResources.ResolveBattleLoadout(state, character, vitals.GetBattleWeapon(row));
        BattleResources.PreparePlayerLoadout(costume, loadout, state.BattleLoadouts?.Get(character)?.LimitFamilies is not null);
    }
}
